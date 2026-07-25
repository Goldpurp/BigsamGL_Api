namespace Api.Configuration;

/// <summary>
/// Loads the repository's ignored .env.local file for Local/Development
/// processes. Hosting-platform environment variables remain authoritative and
/// production never reads a developer environment file.
/// </summary>
public static class LocalEnvironmentFile
{
    private const string FileName = ".env.local";

    public static string? LoadForCurrentProcess()
    {
        var environmentName =
            Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ??
            Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

        if (!string.Equals(environmentName, AppEnvironments.Local,
                StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(environmentName, Environments.Development,
                StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var path = FindFrom(Directory.GetCurrentDirectory());
        if (path is not null)
        {
            foreach (var (key, value) in Parse(File.ReadLines(path)))
            {
                if (Environment.GetEnvironmentVariable(key) is null)
                    Environment.SetEnvironmentVariable(key, value);
            }

            Console.WriteLine($"Loaded local developer configuration from {path}.");
        }

        // On current macOS releases the generic-host configuration file watcher
        // can spin before Kestrel starts. Source-code hot reload belongs to
        // dotnet-watch and remains enabled; this only disables host configuration
        // reload unless a developer explicitly selected a different value.
        if (OperatingSystem.IsMacOS() &&
            Environment.GetEnvironmentVariable(
                "DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE") is null)
        {
            Environment.SetEnvironmentVariable(
                "DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE", "false");
        }

        return path;
    }

    public static IReadOnlyDictionary<string, string> Parse(IEnumerable<string> lines)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var sourceLine in lines)
        {
            var line = sourceLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            if (line.StartsWith("export ", StringComparison.Ordinal))
                line = line[7..].TrimStart();

            var separator = line.IndexOf('=');
            if (separator <= 0) continue;

            var key = line[..separator].Trim();
            if (!IsEnvironmentVariableName(key)) continue;

            var value = line[(separator + 1)..].Trim();
            if (value.Length >= 2 &&
                (value[0] == '"' && value[^1] == '"' ||
                 value[0] == '\'' && value[^1] == '\''))
            {
                value = value[1..^1];
            }

            values[key] = value;
        }

        return values;
    }

    private static string? FindFrom(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, FileName);
            if (File.Exists(candidate)) return candidate;

            // Do not search beyond the repository boundary.
            if (File.Exists(Path.Combine(directory.FullName, "LogisticsSystem.sln")))
                return null;

            directory = directory.Parent;
        }

        return null;
    }

    private static bool IsEnvironmentVariableName(string value)
    {
        if (value.Length == 0 || value[0] != '_' && !char.IsLetter(value[0]))
            return false;

        return value.All(character =>
            character == '_' || char.IsLetterOrDigit(character));
    }
}
