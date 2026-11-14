using RefundPilot.Application.Validation;

namespace RefundPilot.Tests.Application;

public sealed class TicketFileRulesTests
{
    private static TicketUpload File(string type = "image/jpeg", long length = 3, byte[]? data = null) =>
        new(type, length, () => new MemoryStream(data ?? [1, 2, 3]));

    [Fact]
    public void Validate_AcceptsUpToFourAllowedFiles() =>
        Assert.Null(TicketFileRules.Validate([File(), File("application/pdf"), File("IMAGE/PNG"), File("image/heic")]));

    [Theory]
    [InlineData(0, "Dodaj co najmniej jeden plik")]
    [InlineData(5, "maksymalnie 4 pliki")]
    public void Validate_RejectsWrongFileCount(int count, string message) =>
        Assert.Contains(message, TicketFileRules.Validate(Enumerable.Range(0, count).Select(_ => File()).ToList()));

    [Fact]
    public void Validate_RejectsEmptyOversizedAndWrongTypeFiles()
    {
        Assert.Contains("pusty", TicketFileRules.Validate([File(length: 0)]));
        Assert.Contains("5 MB", TicketFileRules.Validate([File(length: TicketFileRules.MaxFileBytes + 1)]));
        Assert.Contains("Dozwolone formaty", TicketFileRules.Validate([File("text/plain")]));
    }

    [Fact]
    public async Task ReadAsync_CopiesContentWithLowerCaseType()
    {
        var files = await TicketFileRules.ReadAsync([File("Image/PNG", 2, [7, 8])], CancellationToken.None);

        var file = Assert.Single(files);
        Assert.Equal("image/png", file.MimeType);
        Assert.Equal([7, 8], file.Data);
    }
}
