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
        Row and Column generators, a single unchanged primary key, and no cross-table input.
        --report writes an atomic JSON execution report without connection strings or row values.
        --checkpoint enables safe resume only for replayable deterministic Row-only plans.
        Its HMAC key is accepted only through the named environment variable and is never stored.
        """;
}
