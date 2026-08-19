namespace Anonymyzer.Generators.Simple;

using System.Security.Cryptography;
using System.Text;
using Anonymyzer.Base.Generation;

internal sealed class EncryptedExternalTextShuffleBuilder : IDisposable
{
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly long _maximumChunkBytes;
    private readonly byte[] _encryptionKey = RandomNumberGenerator.GetBytes(32);
    private readonly Random _random;
    private readonly List<string> _temporaryFiles = new();
    private readonly string _inputPath;
    private BinaryWriter? _inputWriter;
    private long _ordinal;
    private bool _ownershipTransferred;

    public EncryptedExternalTextShuffleBuilder(int seed, long maximumChunkBytes)
    {
        _maximumChunkBytes = maximumChunkBytes;
        _random = new Random(seed);
        string directory = Path.Combine(Path.GetTempPath(), "Anonymyzer");
        Directory.CreateDirectory(directory);
        _inputPath = CreateTemporaryPath(directory);
        _inputWriter = CreateWriter(_inputPath);
    }

    public void Add(object? value)
    {
        if (_inputWriter is null)
        {
            throw new InvalidOperationException("The encrypted shuffle builder is already complete.");
        }

        byte[] plaintext = EncodeValue(value);
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[TagSize];
        try
        {
            using var aes = new AesGcm(_encryptionKey, TagSize);
            aes.Encrypt(nonce, plaintext, ciphertext, tag);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }

        WriteRecord(
            _inputWriter,
            new EncryptedShuffleRecord(_random.NextInt64(), _ordinal++, nonce, tag, ciphertext));
    }

    public IGeneratorSession Complete(string columnName, bool preserveNulls)
    {
        if (_inputWriter is null)
        {
            throw new InvalidOperationException("The encrypted shuffle builder is already complete.");
        }

        _inputWriter.Dispose();
        _inputWriter = null;
        string outputPath = CreateTemporaryPath(Path.GetDirectoryName(_inputPath)!);
        try
        {
            IReadOnlyList<string> chunks = CreateSortedChunks();
            MergeChunks(chunks, outputPath);
            DeleteFile(_inputPath);
            foreach (string chunk in chunks)
            {
                DeleteFile(chunk);
            }

            _ownershipTransferred = true;
            return new EncryptedExternalTextShuffleSession(
                columnName,
                preserveNulls,
                outputPath,
                _encryptionKey);
        }
        catch
        {
            DeleteFile(outputPath);
            throw;
        }
    }

    public void Dispose()
    {
        _inputWriter?.Dispose();
        _inputWriter = null;
        if (!_ownershipTransferred)
        {
            foreach (string file in _temporaryFiles)
            {
                DeleteFile(file);
            }

            CryptographicOperations.ZeroMemory(_encryptionKey);
        }
    }

    internal static long EstimateInMemoryBytes(object? value) => value switch
    {
        null => 32,
        string text => 32L + text.Length * sizeof(char),
        _ => throw new InvalidOperationException(
            $"TextShuffler expected a text value but received {value.GetType().Name}.")
    };

    private IReadOnlyList<string> CreateSortedChunks()
    {
        var chunks = new List<string>();
        using BinaryReader reader = CreateReader(_inputPath);
        while (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            var records = new List<EncryptedShuffleRecord>();
            long estimatedBytes = 0;
            do
            {
                EncryptedShuffleRecord record = ReadRecord(reader);
                records.Add(record);
                estimatedBytes += record.EstimatedInMemoryBytes;
            }
            while (reader.BaseStream.Position < reader.BaseStream.Length
                && estimatedBytes < _maximumChunkBytes);

            records.Sort(EncryptedShuffleRecordComparer.Instance);
            string chunkPath = CreateTemporaryPath(Path.GetDirectoryName(_inputPath)!);
            chunks.Add(chunkPath);
            using BinaryWriter writer = CreateWriter(chunkPath);
            foreach (EncryptedShuffleRecord record in records)
            {
                WriteRecord(writer, record);
            }
        }

        return chunks;
    }

    private static void MergeChunks(IReadOnlyList<string> chunks, string outputPath)
    {
        var states = new List<ChunkState>(chunks.Count);
        var queue = new PriorityQueue<ChunkState, ShufflePriority>();
        try
        {
            foreach (string chunk in chunks)
            {
                var state = new ChunkState(chunk);
                states.Add(state);
                if (state.MoveNext())
                {
                    queue.Enqueue(state, state.Current.Priority);
                }
            }

            using BinaryWriter writer = CreateWriter(outputPath);
            while (queue.TryDequeue(out ChunkState? state, out _))
            {
                WriteRecord(writer, state.Current);
                if (state.MoveNext())
                {
                    queue.Enqueue(state, state.Current.Priority);
                }
            }
        }
        finally
        {
            foreach (ChunkState state in states)
            {
                state.Dispose();
            }
        }
    }

    private string CreateTemporaryPath(string directory)
    {
        string path = Path.Combine(directory, $"shuffle-{Guid.NewGuid():N}.bin");
        _temporaryFiles.Add(path);
        return path;
    }

    private static BinaryWriter CreateWriter(string path) => new(
        new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024),
        Encoding.UTF8,
        leaveOpen: false);

    private static BinaryReader CreateReader(string path) => new(
        new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024),
        Encoding.UTF8,
        leaveOpen: false);

    private static byte[] EncodeValue(object? value)
    {
        if (value is null)
        {
            return [0];
        }

        if (value is not string text)
        {
            throw new InvalidOperationException(
                $"TextShuffler expected a text value but received {value.GetType().Name}.");
        }

        byte[] textBytes = Encoding.UTF8.GetBytes(text);
        var payload = new byte[textBytes.Length + 1];
        payload[0] = 1;
        textBytes.CopyTo(payload, 1);
        CryptographicOperations.ZeroMemory(textBytes);
        return payload;
    }

    private static object? DecodeValue(EncryptedShuffleRecord record, byte[] key)
    {
        var plaintext = new byte[record.Ciphertext.Length];
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(record.Nonce, record.Ciphertext, record.Tag, plaintext);
            if (plaintext.Length == 0)
            {
                throw new InvalidOperationException("Encrypted shuffle file contains an empty value.");
            }

            return plaintext[0] switch
            {
                0 => null,
                1 => Encoding.UTF8.GetString(plaintext, 1, plaintext.Length - 1),
                _ => throw new InvalidOperationException("Encrypted shuffle file contains an invalid value marker.")
            };
        }
        catch (CryptographicException exception)
        {
            throw new InvalidOperationException("Encrypted shuffle file failed authentication.", exception);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static void WriteRecord(BinaryWriter writer, EncryptedShuffleRecord record)
    {
        writer.Write(record.SortKey);
        writer.Write(record.Ordinal);
        writer.Write(record.Nonce);
        writer.Write(record.Tag);
        writer.Write(record.Ciphertext.Length);
        writer.Write(record.Ciphertext);
    }

    private static EncryptedShuffleRecord ReadRecord(BinaryReader reader)
    {
        long sortKey = reader.ReadInt64();
        long ordinal = reader.ReadInt64();
        byte[] nonce = ReadExactly(reader, NonceSize);
        byte[] tag = ReadExactly(reader, TagSize);
        int ciphertextLength = reader.ReadInt32();
        if (ciphertextLength is < 1 or > 1_073_741_824)
        {
            throw new InvalidOperationException("Encrypted shuffle file contains an invalid record length.");
        }

        return new EncryptedShuffleRecord(
            sortKey,
            ordinal,
            nonce,
            tag,
            ReadExactly(reader, ciphertextLength));
    }

    private static byte[] ReadExactly(BinaryReader reader, int count)
    {
        byte[] bytes = reader.ReadBytes(count);
        return bytes.Length == count
            ? bytes
            : throw new InvalidOperationException("Encrypted shuffle file ended unexpectedly.");
    }

    private static void DeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup; values remain encrypted with an ephemeral key.
        }
    }

    private sealed record EncryptedShuffleRecord(
        long SortKey,
        long Ordinal,
        byte[] Nonce,
        byte[] Tag,
        byte[] Ciphertext)
    {
        public long EstimatedInMemoryBytes => 96L + Nonce.Length + Tag.Length + Ciphertext.Length;

        public ShufflePriority Priority => new(SortKey, Ordinal);
    }

    private sealed class EncryptedShuffleRecordComparer : IComparer<EncryptedShuffleRecord>
    {
        public static readonly EncryptedShuffleRecordComparer Instance = new();

        public int Compare(EncryptedShuffleRecord? left, EncryptedShuffleRecord? right) =>
            left is null ? right is null ? 0 : -1
            : right is null ? 1
            : left.Priority.CompareTo(right.Priority);
    }

    private readonly record struct ShufflePriority(long SortKey, long Ordinal) : IComparable<ShufflePriority>
    {
        public int CompareTo(ShufflePriority other)
        {
            int keyComparison = SortKey.CompareTo(other.SortKey);
            return keyComparison != 0 ? keyComparison : Ordinal.CompareTo(other.Ordinal);
        }
    }

    private sealed class ChunkState : IDisposable
    {
        private readonly BinaryReader _reader;

        public ChunkState(string path)
        {
            _reader = CreateReader(path);
        }

        public EncryptedShuffleRecord Current { get; private set; } = null!;

        public bool MoveNext()
        {
            if (_reader.BaseStream.Position >= _reader.BaseStream.Length)
            {
                return false;
            }

            Current = ReadRecord(_reader);
            return true;
        }

        public void Dispose() => _reader.Dispose();
    }

    private sealed class EncryptedExternalTextShuffleSession(
        string columnName,
        bool preserveNulls,
        string path,
        byte[] encryptionKey) : IGeneratorSession
    {
        private readonly BinaryReader _reader = CreateReader(path);
        private bool _disposed;

        public ValueTask ApplyAsync(IGeneratorRow row, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            object? currentValue = row.GetValue(columnName);
            if (preserveNulls && currentValue is null)
            {
                return ValueTask.CompletedTask;
            }

            if (_reader.BaseStream.Position >= _reader.BaseStream.Length)
            {
                throw new InvalidOperationException(
                    "The shuffler received more target rows than values prepared from the source column.");
            }

            row.SetValue(columnName, DecodeValue(ReadRecord(_reader), encryptionKey));
            return ValueTask.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;
                _reader.Dispose();
                DeleteFile(path);
                CryptographicOperations.ZeroMemory(encryptionKey);
            }

            return ValueTask.CompletedTask;
        }
    }
}
