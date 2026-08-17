namespace Anonymyzer.Console.Cli;

internal static class CliHelp
{
    public const string Text = """
        Anonymyzer works only with a detached, disposable database clone.

        Commands:
          generate-config --engine <SqlServer|PostgreSql> --database <name>
                          --connection-env <variable> --marker-id <guid>
                          --output <path> [--force]

          run --config <path> --connection-env <variable> --marker-id <guid> --dry-run

        Connection strings are accepted only through the named environment variable.
        The current run command validates safety and configuration but never modifies data.
        """;
}
