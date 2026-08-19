namespace Anonymyzer.Console.Cli;

internal static class CliHelp
{
    public const string Text = """
        Anonymyzer works only with a detached, disposable database clone.

        Commands:
          generate-config --engine <SqlServer|PostgreSql> --database <name>
                          --connection-env <variable> --marker-id <guid>
                          --output <path> [--force]

          run --config <path> --connection-env <variable> --marker-id <guid>
              (--dry-run | --execute) [--report <path>]
              [--checkpoint <path> --checkpoint-key-env <variable>]

        Connection strings are accepted only through the named environment variable.
        --execute modifies only a validated detached clone and currently supports one table,
        Row and Column generators, a single unchanged primary key, and validated read-only lookup scans.
        --report writes an atomic JSON execution report without connection strings or row values.
        --checkpoint enables safe resume for replayable deterministic Row plans and read-only
        Relational lookup plans. Its HMAC key and dependency secrets are never stored.
        """;
}
