namespace Anonymyzer.Generators.Simple;

using System.Security.Cryptography;

internal interface IReferenceHashIndex : IAsyncDisposable
{
    bool Contains(ReadOnlySpan<byte> hash);
}

internal sealed class InMemoryReferenceHashIndex(HashSet<string> hashes) : IReferenceHashIndex
{
    public bool Contains(ReadOnlySpan<byte> hash) => hashes.Contains(Convert.ToHexString(hash));

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

internal sealed class EncryptedExternalHashIndexBuilder : IDisposable
{
    private const int HashSize = 32;
    private const int EstimatedHashBytes = 96;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    private readonly long _maximumChunkBytes;
    private readonly int _outputHashCharacters;
    private readonly byte[] _encryptionKey = RandomNumberGenerator.GetBytes(32);
    private readonly List<byte[]> _buffer = new();
    private readonly List<string> _temporaryFiles = new();
    private long _estimatedBufferBytes;
    private bool _ownershipTransferred;

    public EncryptedExternalHashIndexBuilder(long maximumChunkBytes, int outputHashCharacters)
    {
        _maximumChunkBytes = maximumChunkBytes;
        _outputHashCharacters = outputHashCharacters;
    }

    public void Add(ReadOnlySpan<byte> hash)
    {
        if (hash.Length != HashSize)
        {
            throw new ArgumentException($"Expected a {HashSize}-byte hash.", nameof(hash));
        }

        if (_estimatedBufferBytes > 0
            && EstimatedHashBytes > _maximumChunkBytes - _estimatedBufferBytes)
        {
            FlushChunk();
        }

        _buffer.Add(hash.ToArray());
        _estimatedBufferBytes += EstimatedHashBytes;
    }

    public IReferenceHashIndex Complete()
    {
        FlushChunk();
        string outputPath = CreateTemporaryPath();
        try
        {
            MergeChunks(outputPath);
            foreach (string chunk in _temporaryFiles.Where(path => path != outputPath).ToArray())
            {
                DeleteFile(chunk);
            }

            _ownershipTransferred = true;
            return new EncryptedExternalHashIndex(outputPath, _encryptionKey);
        }
        catch
        {
            DeleteFile(outputPath);
            throw;
        }
    }

    public void Dispose()
    {
        ClearBuffer();
        if (_ownershipTransferred)
        {
            return;
        }

        foreach (string file in _temporaryFiles)
        {
            DeleteFile(file);
        }

        CryptographicOperations.ZeroMemory(_encryptionKey);
    }

    private void FlushChunk()
    {
        if (_buffer.Count == 0)
        {
            return;
        }

        _buffer.Sort(ByteArrayComparer.Instance);
        string path = CreateTemporaryPath();
        using var writer = new BinaryWriter(new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            64 * 1024));
        foreach (byte[] hash in _buffer)
        {
            WriteEncryptedHash(writer, hash, _encryptionKey);
        }

        ClearBuffer();
    }

    private void MergeChunks(string outputPath)
    {
        string[] chunks = _temporaryFiles.Where(path => path != outputPath).ToArray();
        var states = new List<ChunkState>(chunks.Length);
        var queue = new PriorityQueue<ChunkState, byte[]>(ByteArrayComparer.Instance);
        try
        {
            foreach (string chunk in chunks)
            {
                var state = new ChunkState(chunk, _encryptionKey);
                states.Add(state);
                if (state.MoveNext())
                {
                    queue.Enqueue(state, state.Current);
                }
            }

            using var writer = new BinaryWriter(new FileStream(
                outputPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                64 * 1024));
            byte[]? previous = null;
            string? previousOutputHash = null;
            try
            {
                while (queue.TryDequeue(out ChunkState? state, out _))
                {
                    byte[] current = state.Current;
                    if (previous is null || !current.AsSpan().SequenceEqual(previous))
                    {
                        string outputHash = Convert.ToHexString(current)[.._outputHashCharacters];
                        if (previousOutputHash == outputHash)
                        {
                            throw new InvalidOperationException(
                                "ReferencePseudonym produced a collision. Increase HashLength and retry on a fresh clone.");
                        }

                        WriteEncryptedHash(writer, current, _encryptionKey);
                        if (previous is not null)
                        {
                            CryptographicOperations.ZeroMemory(previous);
                        }

                        previous = current.ToArray();
                        previousOutputHash = outputHash;
                    }

                    if (state.MoveNext())
                    {
                        queue.Enqueue(state, state.Current);
                    }
                }
            }
            finally
            {
                if (previous is not null)
                {
                    CryptographicOperations.ZeroMemory(previous);
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

    private void ClearBuffer()
    {
        foreach (byte[] hash in _buffer)
        {
            CryptographicOperations.ZeroMemory(hash);
        }

        _buffer.Clear();
        _estimatedBufferBytes = 0;
    }

    private string CreateTemporaryPath()
    {
        string directory = Path.Combine(Path.GetTempPath(), "Anonymyzer");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, $"reference-index-{Guid.NewGuid():N}.bin");
        _temporaryFiles.Add(path);
        return path;
    }

    private static void WriteEncryptedHash(BinaryWriter writer, byte[] hash, byte[] key)
    {
        byte[] nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var ciphertext = new byte[HashSize];
        var tag = new byte[TagSize];
        using var aes = new AesGcm(key, TagSize);
        aes.Encrypt(nonce, hash, ciphertext, tag);
        writer.Write(nonce);
        writer.Write(ciphertext);
        writer.Write(tag);
    }

    private static byte[] ReadEncryptedHash(BinaryReader reader, byte[] key)
    {
        byte[] nonce = ReadExactly(reader, NonceSize);
        byte[] ciphertext = ReadExactly(reader, HashSize);
        byte[] tag = ReadExactly(reader, TagSize);
        var plaintext = new byte[HashSize];
        try
        {
            using var aes = new AesGcm(key, TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext);
            return plaintext;
        }
        catch (CryptographicException exception)
        {
            CryptographicOperations.ZeroMemory(plaintext);
            throw new InvalidOperationException("Encrypted reference index failed authentication.", exception);
        }
    }

    private static byte[] ReadExactly(BinaryReader reader, int count)
    {
        byte[] value = reader.ReadBytes(count);
        return value.Length == count
            ? value
            : throw new InvalidOperationException("Encrypted reference index ended unexpectedly.");
    }

    private static void DeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup; records remain encrypted with an ephemeral key.
        }
    }

    private sealed class ChunkState(string path, byte[] key) : IDisposable
    {
        private readonly BinaryReader _reader = new(new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            64 * 1024));

        public byte[] Current { get; private set; } = Array.Empty<byte>();

        public bool MoveNext()
        {
            if (Current.Length > 0)
            {
                CryptographicOperations.ZeroMemory(Current);
            }

            if (_reader.BaseStream.Position >= _reader.BaseStream.Length)
            {
                Current = Array.Empty<byte>();
                return false;
            }

            Current = ReadEncryptedHash(_reader, key);
            return true;
        }

        public void Dispose()
        {
            if (Current.Length > 0)
            {
                CryptographicOperations.ZeroMemory(Current);
            }

            _reader.Dispose();
        }
    }

    private sealed class EncryptedExternalHashIndex(string path, byte[] key) : IReferenceHashIndex
    {
        private const int RecordSize = NonceSize + HashSize + TagSize;
        private readonly FileStream _stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            64 * 1024);
        private bool _disposed;

        public bool Contains(ReadOnlySpan<byte> hash)
        {
            if (_stream.Length % RecordSize != 0)
            {
                throw new InvalidOperationException("Encrypted reference index has an invalid length.");
            }

            long low = 0;
            long high = _stream.Length / RecordSize - 1;
            using var reader = new BinaryReader(_stream, System.Text.Encoding.UTF8, leaveOpen: true);
            while (low <= high)
            {
                long middle = low + (high - low) / 2;
                _stream.Position = middle * RecordSize;
                byte[] candidate = ReadEncryptedHash(reader, key);
                int comparison = ByteArrayComparer.Compare(candidate, hash);
                CryptographicOperations.ZeroMemory(candidate);
                if (comparison == 0)
                {
                    return true;
                }

                if (comparison < 0)
                {
                    low = middle + 1;
                }
                else
                {
                    high = middle - 1;
                }
            }

            return false;
        }

        public ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _disposed = true;
                _stream.Dispose();
                DeleteFile(path);
                CryptographicOperations.ZeroMemory(key);
            }

            return ValueTask.CompletedTask;
        }
    }

    private sealed class ByteArrayComparer : IComparer<byte[]>
    {
        public static readonly ByteArrayComparer Instance = new();

        public int Compare(byte[]? left, byte[]? right) =>
            left is null ? right is null ? 0 : -1
            : right is null ? 1
            : ByteArrayComparer.Compare(left.AsSpan(), right.AsSpan());

        public static int Compare(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right) =>
            left.SequenceCompareTo(right);
    }
}
