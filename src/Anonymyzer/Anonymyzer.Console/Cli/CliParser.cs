namespace Anonymyzer.Console.Cli;

internal static class CliParser
{
    public static CliParseResult Parse(IReadOnlyList<string> args)
    {
        if (args.Count == 0 || IsHelp(args[0]))
        {
            return new CliParseResult(null, null, ShowHelp: true);
        }

        string command = args[0];
        ParseOptionsResult options = ParseOptions(args.Skip(1).ToArray());
        if (options.Error is not null)
        {
            return new CliParseResult(null, options.Error, ShowHelp: false);
        }

        if (options.Help)
        {
            return new CliParseResult(null, null, ShowHelp: true);
        }

        return command.ToLowerInvariant() switch
        {
            "generate-config" => ParseGenerateConfig(options),
            "run" => ParseRun(options),
            _ => new CliParseResult(null, $"Unknown command '{command}'.", ShowHelp: false)
        };
    }

    private static CliParseResult ParseGenerateConfig(ParseOptionsResult parsed)
    {
        string? error = RequireOnly(
            parsed,
            valueOptions: ["engine", "database", "connection-env", "marker-id", "output"],
            flags: ["force"]);
        if (error is not null)
        {
            return new CliParseResult(null, error, ShowHelp: false);
        }

        if (!TryGetRequired(parsed, "engine", out string engine, out error)
            || !TryGetRequired(parsed, "database", out string database, out error)
            || !TryGetRequired(parsed, "connection-env", out string connectionEnvironment, out error)
            || !TryGetRequiredGuid(parsed, "marker-id", out Guid markerId, out error)
            || !TryGetRequired(parsed, "output", out string output, out error))
        {
            return new CliParseResult(null, error, ShowHelp: false);
        }

        if (!engine.Equals("SqlServer", StringComparison.OrdinalIgnoreCase)
            && !engine.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase))
        {
            return new CliParseResult(
                null,
                "Option '--engine' must be SqlServer or PostgreSql.",
                ShowHelp: false);
        }

        return new CliParseResult(
            new GenerateConfigCliOptions(
                engine,
                database,
                connectionEnvironment,
                markerId,
                output,
                parsed.Flags.Contains("force")),
            null,
            ShowHelp: false);
    }

    private static CliParseResult ParseRun(ParseOptionsResult parsed)
    {
        string? error = RequireOnly(
            parsed,
            valueOptions: ["config", "connection-env", "marker-id", "report", "checkpoint", "checkpoint-key-env"],
            flags: ["dry-run", "execute"]);
        if (error is not null)
        {
            return new CliParseResult(null, error, ShowHelp: false);
        }

        if (!TryGetRequired(parsed, "config", out string config, out error)
            || !TryGetRequired(parsed, "connection-env", out string connectionEnvironment, out error)
            || !TryGetRequiredGuid(parsed, "marker-id", out Guid markerId, out error))
        {
            return new CliParseResult(null, error, ShowHelp: false);
        }

        bool dryRun = parsed.Flags.Contains("dry-run");
        bool execute = parsed.Flags.Contains("execute");
        if (dryRun == execute)
        {
            return new CliParseResult(
                null,
                "The run command requires exactly one of --dry-run or --execute.",
                ShowHelp: false);
        }

        parsed.Values.TryGetValue("report", out string? reportPath);
        parsed.Values.TryGetValue("checkpoint", out string? checkpointPath);
        parsed.Values.TryGetValue("checkpoint-key-env", out string? checkpointKeyEnvironment);
        if ((checkpointPath is null) != (checkpointKeyEnvironment is null))
        {
            return new CliParseResult(
                null,
                "Options '--checkpoint' and '--checkpoint-key-env' must be specified together.",
                ShowHelp: false);
        }

        if (dryRun && (reportPath is not null || checkpointPath is not null))
        {
            return new CliParseResult(
                null,
                "Options '--report' and '--checkpoint' are available only with --execute.",
                ShowHelp: false);
        }

        return new CliParseResult(
            new RunCliOptions(
                connectionEnvironment,
                markerId,
                config,
                dryRun,
                execute,
                reportPath,
                checkpointPath,
                checkpointKeyEnvironment),
            null,
            ShowHelp: false);
    }

    private static ParseOptionsResult ParseOptions(IReadOnlyList<string> args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < args.Count; index++)
        {
            string token = args[index];
            if (IsHelp(token))
            {
                return new ParseOptionsResult(values, flags, Help: true, Error: null);
            }

            if (!token.StartsWith("--", StringComparison.Ordinal) || token.Length == 2)
            {
                return new ParseOptionsResult(values, flags, Help: false, Error: $"Unexpected argument '{token}'.");
            }

            string name = token[2..];
            if (index + 1 < args.Count && !args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                if (!values.TryAdd(name, args[++index]))
                {
                    return new ParseOptionsResult(values, flags, Help: false, Error: $"Option '--{name}' was specified more than once.");
                }
            }
            else if (!flags.Add(name))
            {
                return new ParseOptionsResult(values, flags, Help: false, Error: $"Flag '--{name}' was specified more than once.");
            }
        }

        return new ParseOptionsResult(values, flags, Help: false, Error: null);
    }

    private static string? RequireOnly(
        ParseOptionsResult parsed,
        IReadOnlyCollection<string> valueOptions,
        IReadOnlyCollection<string> flags)
    {
        string? unknownValue = parsed.Values.Keys.FirstOrDefault(key => !valueOptions.Contains(key, StringComparer.OrdinalIgnoreCase));
        if (unknownValue is not null)
        {
            return $"Unknown option '--{unknownValue}'.";
        }

        string? unknownFlag = parsed.Flags.FirstOrDefault(key => !flags.Contains(key, StringComparer.OrdinalIgnoreCase));
        return unknownFlag is null ? null : $"Unknown flag '--{unknownFlag}'.";
    }

    private static bool TryGetRequired(
        ParseOptionsResult parsed,
        string name,
        out string value,
        out string? error)
    {
        if (parsed.Values.TryGetValue(name, out value!) && !string.IsNullOrWhiteSpace(value))
        {
            error = null;
            return true;
        }

        value = string.Empty;
        error = $"Required option '--{name}' is missing.";
        return false;
    }

    private static bool TryGetRequiredGuid(
        ParseOptionsResult parsed,
        string name,
        out Guid value,
        out string? error)
    {
        if (TryGetRequired(parsed, name, out string text, out error)
            && Guid.TryParse(text, out value)
            && value != Guid.Empty)
        {
            return true;
        }

        value = Guid.Empty;
        error = $"Option '--{name}' must be a non-empty GUID.";
        return false;
    }

    private static bool IsHelp(string value) => value is "-h" or "--help";

    private sealed record ParseOptionsResult(
        IReadOnlyDictionary<string, string> Values,
        IReadOnlySet<string> Flags,
        bool Help,
        string? Error);
}
