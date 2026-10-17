using System.Text;
using Microsoft.Data.Sqlite;
using SahaNet.Mobile.Core.Api;
using SahaNet.Mobile.Core.Offline;
using SahaNet.Mobile.Core.ViewModels;

namespace SahaNet.UnitTests.Mobile;

public sealed class MobileAdditionsTests
{
    [Theory]
    [InlineData("192.168.1.20:5080", "http://192.168.1.20:5080/")]
    [InlineData("https://api.sahanet.app", "https://api.sahanet.app/")]
    [InlineData(" http://10.0.2.2:5080/ ", "http://10.0.2.2:5080/")]
    public void ApiEndpoint_ParsesUserInput(string input, string expected)
    {
        ApiEndpoint.TryParse(input, out var uri).Should().BeTrue();
        uri.ToString().Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ftp://host")]
    [InlineData("::::")]
    public void ApiEndpoint_RejectsInvalidInput(string input) =>
        ApiEndpoint.TryParse(input, out _).Should().BeFalse();

    [Fact]
    public void ApiEndpoint_CanBeChangedAtRuntime()
    {
        var endpoint = new ApiEndpoint(new Uri("http://a.test"));
        endpoint.BaseUrl = new Uri("http://b.test:5080");
        endpoint.BaseUrl.ToString().Should().Be("http://b.test:5080/");
    }

    [Fact]
    public void JwtSubject_ReadsSubClaim()
    {
        var id = Guid.NewGuid();
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes($$"""{"sub":"{{id}}"}""")).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        JwtSubject.Read($"header.{payload}.sig").Should().Be(id);
        JwtSubject.Read("not-a-jwt").Should().BeNull();
        JwtSubject.Read(null).Should().BeNull();
    }

    [Theory]
    [InlineData("12,5", 12.5)]
    [InlineData("12.5", 12.5)]
    [InlineData("3", 3)]
    public void TryParseDecimal_AcceptsCommaAndDot(string text, double expected)
    {
        MarketViewModel.TryParseDecimal(text, out var value).Should().BeTrue();
        value.Should().Be((decimal)expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-4")]
    public void TryParseDecimal_RejectsInvalidOrNonPositive(string? text) =>
        MarketViewModel.TryParseDecimal(text, out _).Should().BeFalse();

    [Fact]
    public async Task LocalStore_IsEncrypted_AndCannotBeOpenedWithWrongKey()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sahanet-{Guid.NewGuid():N}.db3");
        try
        {
            await using (var store = await LocalStore.OpenAsync(path, "dogru-anahtar"))
            {
                await store.PutAsync("k", "gizli-veri", DateTimeOffset.UtcNow);
            }

            SqliteConnection.ClearAllPools();
            Convert.ToHexString(await File.ReadAllBytesAsync(path)).Should().NotContain(Convert.ToHexString(Encoding.UTF8.GetBytes("gizli-veri")));

            var wrongKey = async () => await LocalStore.OpenAsync(path, "yanlis-anahtar");
            await wrongKey.Should().ThrowAsync<SqliteException>();

            await using var reopened = await LocalStore.OpenAsync(path, "dogru-anahtar");
            (await reopened.GetAsync<string>("k"))!.Value.Should().Be("gizli-veri");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            foreach (var file in Directory.GetFiles(Path.GetTempPath(), Path.GetFileName(path) + "*"))
            {
                File.Delete(file);
            }
        }
    }
}
