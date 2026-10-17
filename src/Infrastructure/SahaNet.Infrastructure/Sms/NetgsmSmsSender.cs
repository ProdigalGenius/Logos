using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SahaNet.Application.Identity.Abstractions;
using SahaNet.Domain.Identity;
using SahaNet.Infrastructure.Options;

namespace SahaNet.Infrastructure.Sms;

/// <summary>Netgsm REST v2 SMS gönderimi. Numara loglarda her zaman maskelenir.</summary>
internal sealed partial class NetgsmSmsSender(HttpClient http, IOptions<SmsOptions> options, ILogger<NetgsmSmsSender> logger) : ISmsSender
{
    public async Task SendAsync(PhoneNumber to, string message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(to);
        var netgsm = options.Value.Netgsm;

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(netgsm.BaseUrl, "sms/rest/v2/send"));
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{netgsm.Username}:{netgsm.Password}")));
        request.Content = JsonContent.Create(new NetgsmRequest(
            netgsm.Header ?? string.Empty,
            [new NetgsmMessage(message, to.Value[3..])],
            "TR"));

        using var response = await http.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadFromJsonAsync<NetgsmResponse>(cancellationToken);
        if (!response.IsSuccessStatusCode || body?.Code != "00")
        {
            LogFailed(logger, to.Masked, body?.Code ?? ((int)response.StatusCode).ToString(System.Globalization.CultureInfo.InvariantCulture));
            throw new HttpRequestException($"Netgsm SMS gönderilemedi (kod {body?.Code}).");
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Netgsm SMS hatası: {MaskedPhone} kod {Code}")]
    private static partial void LogFailed(ILogger logger, string maskedPhone, string code);

    private sealed record NetgsmRequest(
        [property: JsonPropertyName("msgheader")] string MessageHeader,
        [property: JsonPropertyName("messages")] IReadOnlyList<NetgsmMessage> Messages,
        [property: JsonPropertyName("encoding")] string Encoding);

    private sealed record NetgsmMessage(
        [property: JsonPropertyName("msg")] string Text,
        [property: JsonPropertyName("no")] string Number);

    private sealed record NetgsmResponse([property: JsonPropertyName("code")] string? Code);
}
