using ProjectK.Common.Rules.Shared;

namespace ProjectK.API.Tests.Security;

/// <summary>
/// Blob names are foldered (<c>{folder}/{yyyy}/{MM}/{guid}{ext}</c>). Two of the four builders this
/// replaced escaped the whole name, turning the separators into <c>%2F</c> and pointing at a blob
/// that does not exist — so member photos and report images resolved differently for the same file.
/// </summary>
public class BlobPublicUrlRulesTests
{
    [Fact]
    public void Build_ShouldKeepFolderSeparators()
    {
        var url = BlobPublicUrlRules.Build("https://cdn.example/photos", "member-photos/2026/08/abc123.jpg");

        Assert.Equal("https://cdn.example/photos/member-photos/2026/08/abc123.jpg", url);
        Assert.DoesNotContain("%2F", url);
    }

    [Fact]
    public void Build_ShouldEscapeWithinASegment()
    {
        var url = BlobPublicUrlRules.Build("https://cdn.example/photos", "member-photos/a b&c.jpg");

        Assert.Equal("https://cdn.example/photos/member-photos/a%20b%26c.jpg", url);
    }

    [Theory]
    [InlineData("https://cdn.example/photos/")]
    [InlineData("https://cdn.example/photos")]
    public void Build_ShouldNotDoubleTheSeparator(string baseUrl)
    {
        Assert.Equal("https://cdn.example/photos/a.jpg", BlobPublicUrlRules.Build(baseUrl, "a.jpg"));
    }

    [Fact]
    public void Build_ShouldFallBackWhenNoPublicBaseUrlIsConfigured()
    {
        Assert.Equal("raw/name.jpg", BlobPublicUrlRules.Build(null, "raw/name.jpg"));
        Assert.Equal("https://account.blob/x", BlobPublicUrlRules.Build("", "raw/name.jpg", "https://account.blob/x"));
    }

    [Fact]
    public void Build_ShouldReturnNullForAnEmptyBlobName()
    {
        Assert.Null(BlobPublicUrlRules.Build("https://cdn.example", null));
        Assert.Null(BlobPublicUrlRules.Build("https://cdn.example", "   "));
    }
}
