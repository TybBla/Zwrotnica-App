namespace RefundPilot.Api;

/// <summary>
/// Development convenience: ASP.NET does not read .env files, so this loads the nearest one (searching upwards)
/// into environment variables. Existing variables win, and values are never logged.
/// </summary>
public static class DotEnv
{
    /// <summary>Returns how many variables were set.</summary>
    public static int Load(string startDirectory)
    {
        for (var dir = new DirectoryInfo(startDirectory); dir is not null; dir = dir.Parent)
        {
            var file = Path.Combine(dir.FullName, ".env");
            if (File.Exists(file))
                return Apply(File.ReadAllLines(file));
        }
        return 0;
    }

    private static int Apply(IEnumerable<string> lines)
    {
        var set = 0;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            var eq = line.IndexOf('=');
            if (line.Length == 0 || line[0] == '#' || eq <= 0)
                continue;

            var key = line[..eq].Trim();
            var value = line[(eq + 1)..].Trim();
            if (value.Length >= 2 && (value[0] == '"' || value[0] == '\'') && value[^1] == value[0])
                value = value[1..^1];

            if (key.Length == 0 || Environment.GetEnvironmentVariable(key) is not null)
                continue;
            Environment.SetEnvironmentVariable(key, value);
            set++;
        }
        return set;
    }
}
