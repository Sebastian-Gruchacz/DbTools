namespace ScriptCut;

using System.Text.RegularExpressions;

internal static class Program
{
    private const string DefaultDatabase = "_database_";
    private const string DefaultServer = @".\SQLEXPRESS";

    public static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] is "-h" or "--help")
        {
            PrintUsage();
            return 0;
        }

        if (args.Length is < 1 or > 3)
        {
            PrintUsage();
            return 2;
        }

        try
        {
            var sourcePath = Path.GetFullPath(args[0]);
            if (!File.Exists(sourcePath))
            {
                Console.Error.WriteLine($"Source file does not exist: {sourcePath}");
                return 2;
            }

            var database = GetArgument(args, 1, DefaultDatabase);
            var server = GetArgument(args, 2, DefaultServer);
            var result = new SqlScriptSplitter(database).Split(sourcePath);

            BatchFileWriter.Write(result.OutputDirectory, result.PartFiles, server);
            Console.WriteLine($"Created {result.PartFiles.Count} part(s) in: {result.OutputDirectory}");
            return 0;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Console.Error.WriteLine(exception.Message);
            return 1;
        }
    }

    private static string GetArgument(string[] args, int index, string defaultValue) =>
        args.Length > index && !string.IsNullOrWhiteSpace(args[index]) ? args[index] : defaultValue;

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: ScriptCut <source.sql> [database] [server]");
        Console.WriteLine($"Defaults: database={DefaultDatabase}, server={DefaultServer}");
    }
}

internal sealed partial class SqlScriptSplitter
{
    private readonly string _database;

    public SqlScriptSplitter(string database)
    {
        _database = database;
    }

    public SplitResult Split(string sourcePath)
    {
        var source = new FileInfo(sourcePath);
        var outputDirectory = Path.Combine(
            source.DirectoryName!,
            $"{Path.GetFileNameWithoutExtension(source.Name)}.parts");

        Directory.CreateDirectory(outputDirectory);

        var partFiles = new List<string>();
        PartWriter? currentPart = null;
        string? currentTable = null;

        try
        {
            foreach (var line in File.ReadLines(source.FullName))
            {
                var table = FindTable(line);
                if (table is not null && !table.Equals(currentTable, StringComparison.OrdinalIgnoreCase))
                {
                    currentPart?.Dispose();
                    currentTable = table;

                    var fileName = $"{partFiles.Count + 1:D3}.{SafeFileName(table)}.sql";
                    partFiles.Add(fileName);
                    currentPart = new PartWriter(Path.Combine(outputDirectory, fileName), _database, table);
                    Console.WriteLine($"Extracting table [{table}] to {fileName}");
                }

                currentPart?.WriteLine(line);
            }
        }
        finally
        {
            currentPart?.Dispose();
        }

        return new SplitResult(outputDirectory, partFiles);
    }

    private static string? FindTable(string line)
    {
        var match = TableStartRegex().Match(line);
        return match.Success ? match.Groups["table"].Value : null;
    }

    private static string SafeFileName(string tableName)
    {
        var invalidCharacters = Path.GetInvalidFileNameChars();
        return string.Concat(tableName.Select(character => invalidCharacters.Contains(character) ? '_' : character));
    }

    [GeneratedRegex(
        @"^\s*(?:SET\s+IDENTITY_INSERT\s+\[dbo\]\.\[(?<table>[^\]]+)\]\s+ON\b|INSERT\s+\[dbo\]\.\[(?<table>[^\]]+)\])",
        RegexOptions.IgnoreCase)]
    private static partial Regex TableStartRegex();
}

internal sealed class PartWriter : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly string _table;

    public PartWriter(string path, string database, string table)
    {
        _table = table;
        _writer = new StreamWriter(path, append: false);
        _writer.WriteLine($"USE [{EscapeIdentifier(database)}]");
        _writer.WriteLine("GO");
        _writer.WriteLine($"DISABLE TRIGGER ALL ON [dbo].[{EscapeIdentifier(table)}]");
        _writer.WriteLine("GO");
    }

    public void WriteLine(string line) => _writer.WriteLine(line);

    public void Dispose()
    {
        _writer.WriteLine($"ENABLE TRIGGER ALL ON [dbo].[{EscapeIdentifier(_table)}]");
        _writer.WriteLine("GO");
        _writer.Dispose();
    }

    private static string EscapeIdentifier(string identifier) => identifier.Replace("]", "]]", StringComparison.Ordinal);
}

internal static class BatchFileWriter
{
    public static void Write(string outputDirectory, IReadOnlyList<string> partFiles, string server)
    {
        using var writer = new StreamWriter(Path.Combine(outputDirectory, "insert_all.bat"), append: false);
        writer.WriteLine("@ECHO OFF");
        writer.WriteLine("IF NOT EXIST output MD output");

        foreach (var partFile in partFiles)
        {
            var outputFile = Path.Combine("output", $"{Path.GetFileNameWithoutExtension(partFile)}.txt");
            writer.WriteLine($"sqlcmd -S \"{server}\" -b -i \"{partFile}\" -o \"{outputFile}\"");
            writer.WriteLine("IF ERRORLEVEL 1 EXIT /B %ERRORLEVEL%");
        }
    }
}

internal sealed record SplitResult(string OutputDirectory, IReadOnlyList<string> PartFiles);
