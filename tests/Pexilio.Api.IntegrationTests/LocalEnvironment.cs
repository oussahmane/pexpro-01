namespace Pexilio.Api.IntegrationTests;

internal static class LocalEnvironment
{
    public static string TestConnectionString()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("PEXILIO_TEST_CONNECTION");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            DatabaseGuard.EnsureTestDatabase(fromEnvironment);
            return fromEnvironment;
        }

        var envFile = FindRepoFile(Path.Combine("config", "dev", "local.env"));
        foreach (var line in File.ReadAllLines(envFile))
        {
            const string prefix = "PEXILIO_TEST_CONNECTION=";
            if (!line.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var value = Unquote(line[prefix.Length..]);
            DatabaseGuard.EnsureTestDatabase(value);
            return value;
        }

        throw new InvalidOperationException($"PEXILIO_TEST_CONNECTION is missing from {envFile}.");
    }

    private static string FindRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException(
            $"Could not find {relativePath} starting from {AppContext.BaseDirectory}.");
    }

    private static string Unquote(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '\'' && trimmed[^1] == '\'')
        {
            return trimmed[1..^1];
        }

        return trimmed;
    }
}
