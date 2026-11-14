using System.Web;
using ProjectK.Common.Options.InfrastructureModule;
using ProjectK.Infrastructure.Services.BlobStorageService;

namespace ProjectK.Infrastructure.Tests.Services.BlobStorageService;

/// <summary>
/// The container is private: a photo is readable only through a link signed for that one blob, for
/// a bounded time. Links are cut from fixed windows so the browser can cache the image.
/// </summary>
public class SignedBlobReadLinksTests
{
    // Azurite's published development account; a real key shape, so the SDK can sign with it.
    private const string Azurite =
        "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;" +
        "AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;" +
        "BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;";

    private const string Blob = "member-photos/2026/10/08/abc.jpg";

    private static readonly DateTimeOffset Noon = new(2026, 10, 8, 12, 20, 0, TimeSpan.Zero);

    private static SignedBlobReadLinks Links(DateTimeOffset now, string? publicBaseUrl = null) =>
        new(new BlobStorageOptions { ConnectionString = Azurite, ContainerName = "photos", PublicBaseUrl = publicBaseUrl },
            new StoppedClock(now));

    private static (Uri Uri, System.Collections.Specialized.NameValueCollection Query) Parse(string? link)
    {
        Assert.NotNull(link);
        var uri = new Uri(link);
        return (uri, HttpUtility.ParseQueryString(uri.Query));
    }

    [Fact]
    public void For_SignsReadOnlyAccessToThatOneBlob()
    {
        var (uri, query) = Parse(Links(Noon).For(Blob));

        Assert.Equal("/devstoreaccount1/photos/" + Blob, uri.AbsolutePath);
        Assert.Equal("r", query["sp"]);
        Assert.Equal("b", query["sr"]);
        Assert.False(string.IsNullOrEmpty(query["sig"]));
    }

    [Fact]
    public void For_ExpiresWithinTheLifetime_AndLeavesAtLeastHalfOfIt()
    {
        var (_, query) = Parse(Links(Noon).For(Blob));
        var expires = DateTimeOffset.Parse(query["se"]!);

        Assert.InRange(expires, Noon + TimeSpan.FromHours(1), Noon + TimeSpan.FromHours(2));
    }

    [Fact]
    public void For_GivesTheSameLinkWithinAWindow_SoTheBrowserCanCacheTheImage()
    {
        var early = Links(Noon).For(Blob);
        var later = Links(Noon.AddMinutes(30)).For(Blob);
        var nextWindow = Links(Noon.AddHours(1)).For(Blob);

        Assert.Equal(early, later);
        Assert.NotEqual(early, nextWindow);
    }

    [Fact]
    public void For_KeepsThePublicBaseUrlHost_AndTheSignature()
    {
        var direct = Parse(Links(Noon).For(Blob));
        var proxied = Parse(Links(Noon, "https://photos.example/devstoreaccount1/photos").For(Blob));

        Assert.Equal("photos.example", proxied.Uri.Host);
        Assert.Equal("/devstoreaccount1/photos/" + Blob, proxied.Uri.AbsolutePath);
        Assert.Equal(direct.Query["sig"], proxied.Query["sig"]);
    }

    [Fact]
    public void ForSharing_OutlivesAReadLink()
    {
        var (_, query) = Parse(Links(Noon).ForSharing(Blob));

        Assert.True(DateTimeOffset.Parse(query["se"]!) > Noon.AddDays(180));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void NoBlob_NoLink(string? blobName)
    {
        Assert.Null(Links(Noon).For(blobName));
        Assert.Null(Links(Noon).ForSharing(blobName));
    }

    [Fact]
    public void AConnectionWithoutAnAccountKey_FailsAtStartup_NotOnTheFirstPhoto()
    {
        var options = new BlobStorageOptions
        {
            ConnectionString = "BlobEndpoint=https://account.blob.core.windows.net/;SharedAccessSignature=sv=2024-08-04&sig=abc",
            ContainerName = "photos"
        };

        Assert.Throws<InvalidOperationException>(() => new SignedBlobReadLinks(options, new StoppedClock(Noon)));
    }

    private sealed class StoppedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
