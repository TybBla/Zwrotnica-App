using RefundPilot.Api;

namespace RefundPilot.Tests.Application;

public sealed class DotEnvTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dotenv-" + Guid.NewGuid().ToString("N"));
    private readonly string _prefix = "DOTENVT_" + Guid.NewGuid().ToString("N")[..8] + "_";

    public DotEnvTests() => Directory.CreateDirectory(Path.Combine(_root, "backend", "src", "Api"));

    public void Dispose()
    {
        foreach (var name in new[] { "A", "B", "C", "D", "E" })
            Environment.SetEnvironmentVariable(_prefix + name, null);
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void Load_WalksUpParsesAndDoesNotOverrideExisting()
    {
        File.WriteAllText(Path.Combine(_root, ".env"), $"""
            # comment
            {_prefix}A=plain
            {_prefix}B = "quoted value"
            {_prefix}C='single'

            {_prefix}D=already-set-in-file
            not a pair
            ={_prefix}
            """);
        Environment.SetEnvironmentVariable(_prefix + "D", "from-environment");

        var count = DotEnv.Load(Path.Combine(_root, "backend", "src", "Api"));

        Assert.Equal("plain", Environment.GetEnvironmentVariable(_prefix + "A"));
        Assert.Equal("quoted value", Environment.GetEnvironmentVariable(_prefix + "B"));
        Assert.Equal("single", Environment.GetEnvironmentVariable(_prefix + "C"));
        Assert.Equal("from-environment", Environment.GetEnvironmentVariable(_prefix + "D"));
        Assert.Equal(3, count);
    }

    [Fact]
    public void Load_NoFile_ReturnsZero() =>
        Assert.Equal(0, DotEnv.Load(Path.Combine(_root, "backend")));
}
