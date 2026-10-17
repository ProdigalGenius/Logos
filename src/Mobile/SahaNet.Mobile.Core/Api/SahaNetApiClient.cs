using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace SahaNet.Mobile.Core.Api;

/// <summary>Uygulamanın kullandığı API işlemleri (ViewModel'ler bu arayüze bağlıdır, testte sahtelenir).</summary>
public interface ISahaNetApi
{
    Task<ApiResult<RequestOtpResult>> RequestOtpAsync(string phone, CancellationToken cancellationToken);

    Task<ApiResult<AuthResult>> VerifyOtpAsync(string phone, string code, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<ConsentDocumentDto>>> GetConsentDocumentsAsync(CancellationToken cancellationToken);

    Task<ApiResult<bool>> RecordConsentsAsync(IReadOnlyList<ConsentDecisionDto> decisions, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<RegionDto>>> GetRegionsAsync(CancellationToken cancellationToken);

    Task<ApiResult<ProfileDto>> CompleteOnboardingAsync(string nicheKey, string regionCode, string? displayName, CancellationToken cancellationToken);

    Task<ApiResult<ProfileDto>> GetProfileAsync(CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<PortDto>>> GetPortsAsync(double? latitude, double? longitude, CancellationToken cancellationToken);

    Task<ApiResult<SeaStatusDto>> GetSeaStatusAsync(string portCode, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<SpeciesDto>>> GetSpeciesAsync(CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<SpotDto>>> GetSpotsAsync(CancellationToken cancellationToken);

    Task<ApiResult<SpotDto>> SaveSpotAsync(Guid? existingId, SaveSpotRequest request, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<CatchDto>>> GetCatchesAsync(CancellationToken cancellationToken);

    Task<ApiResult<RecordCatchResult>> RecordCatchAsync(RecordCatchRequest request, CancellationToken cancellationToken);

    Task<ApiResult<IReadOnlyList<AlertDto>>> GetActiveAlertsAsync(CancellationToken cancellationToken);

    Task<ApiResult<bool>> RegisterDeviceAsync(string token, string platform, CancellationToken cancellationToken);
}

public sealed class SahaNetApiClient(HttpClient http, ITokenStore tokens) : ISahaNetApi
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task<ApiResult<RequestOtpResult>> RequestOtpAsync(string phone, CancellationToken cancellationToken) =>
        SendAsync<RequestOtpResult>(HttpMethod.Post, "api/auth/otp/request", new { phone }, cancellationToken);

    public async Task<ApiResult<AuthResult>> VerifyOtpAsync(string phone, string code, CancellationToken cancellationToken)
    {
        var result = await SendAsync<AuthResult>(HttpMethod.Post, "api/auth/otp/verify", new { phone, code }, cancellationToken);
        if (result is { IsSuccess: true, Value: { } auth })
        {
            await tokens.SetAsync(new StoredTokens(auth.AccessToken, auth.AccessTokenExpiresAt, auth.RefreshToken));
        }

        return result;
    }

    public Task<ApiResult<IReadOnlyList<ConsentDocumentDto>>> GetConsentDocumentsAsync(CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<ConsentDocumentDto>>(HttpMethod.Get, "api/consents/documents", null, cancellationToken);

    public async Task<ApiResult<bool>> RecordConsentsAsync(IReadOnlyList<ConsentDecisionDto> decisions, CancellationToken cancellationToken)
    {
        var result = await SendAsync<JsonElement>(HttpMethod.Post, "api/me/consents", new { decisions, source = "mobile" }, cancellationToken);
        return result.IsSuccess ? ApiResult<bool>.Ok(true) : ApiResult<bool>.Fail(result.Outcome, result.Problem);
    }

    public Task<ApiResult<IReadOnlyList<RegionDto>>> GetRegionsAsync(CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<RegionDto>>(HttpMethod.Get, "api/regions", null, cancellationToken);

    public Task<ApiResult<ProfileDto>> CompleteOnboardingAsync(string nicheKey, string regionCode, string? displayName, CancellationToken cancellationToken) =>
        SendAsync<ProfileDto>(HttpMethod.Post, "api/me/onboarding", new { nicheKey, regionCode, displayName }, cancellationToken);

    public Task<ApiResult<ProfileDto>> GetProfileAsync(CancellationToken cancellationToken) =>
        SendAsync<ProfileDto>(HttpMethod.Get, "api/me", null, cancellationToken);

    public Task<ApiResult<IReadOnlyList<PortDto>>> GetPortsAsync(double? latitude, double? longitude, CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<PortDto>>(
            HttpMethod.Get,
            latitude is { } lat && longitude is { } lon
                ? string.Create(CultureInfo.InvariantCulture, $"api/fishing/ports?lat={lat}&lon={lon}")
                : "api/fishing/ports",
            null,
            cancellationToken);

    public Task<ApiResult<SeaStatusDto>> GetSeaStatusAsync(string portCode, CancellationToken cancellationToken) =>
        SendAsync<SeaStatusDto>(HttpMethod.Get, $"api/fishing/ports/{Uri.EscapeDataString(portCode)}/sea-status", null, cancellationToken);

    public Task<ApiResult<IReadOnlyList<SpeciesDto>>> GetSpeciesAsync(CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<SpeciesDto>>(HttpMethod.Get, "api/fishing/species", null, cancellationToken);

    public Task<ApiResult<IReadOnlyList<SpotDto>>> GetSpotsAsync(CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<SpotDto>>(HttpMethod.Get, "api/fishing/spots", null, cancellationToken);

    public Task<ApiResult<SpotDto>> SaveSpotAsync(Guid? existingId, SaveSpotRequest request, CancellationToken cancellationToken) =>
        existingId is { } id
            ? SendAsync<SpotDto>(HttpMethod.Put, $"api/fishing/spots/{id}", request, cancellationToken)
            : SendAsync<SpotDto>(HttpMethod.Post, "api/fishing/spots", request, cancellationToken);

    public Task<ApiResult<IReadOnlyList<CatchDto>>> GetCatchesAsync(CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<CatchDto>>(HttpMethod.Get, "api/fishing/catches", null, cancellationToken);

    public Task<ApiResult<RecordCatchResult>> RecordCatchAsync(RecordCatchRequest request, CancellationToken cancellationToken) =>
        SendAsync<RecordCatchResult>(HttpMethod.Post, "api/fishing/catches", request, cancellationToken);

    public Task<ApiResult<IReadOnlyList<AlertDto>>> GetActiveAlertsAsync(CancellationToken cancellationToken) =>
        SendAsync<IReadOnlyList<AlertDto>>(HttpMethod.Get, "api/alerts/active", null, cancellationToken);

    public async Task<ApiResult<bool>> RegisterDeviceAsync(string token, string platform, CancellationToken cancellationToken)
    {
        var result = await SendAsync<JsonElement?>(HttpMethod.Post, "api/me/devices", new { token, platform }, cancellationToken);
        return result.IsSuccess ? ApiResult<bool>.Ok(true) : ApiResult<bool>.Fail(result.Outcome, result.Problem);
    }

    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
            if (body is not null)
            {
                request.Content = JsonContent.Create(body, options: Json);
            }

            using var response = await http.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.NoContent || response.Content.Headers.ContentLength == 0)
                {
                    return ApiResult<T>.Ok(default!);
                }

                var value = await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken);
                return ApiResult<T>.Ok(value!);
            }

            var problem = await TryReadProblemAsync(response, cancellationToken);
            return response.StatusCode switch
            {
                HttpStatusCode.Unauthorized => ApiResult<T>.Fail(ApiOutcome.Unauthorized, problem),
                HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests when problem?.Title is null => ApiResult<T>.Fail(ApiOutcome.Transient, problem),
                >= HttpStatusCode.InternalServerError => ApiResult<T>.Fail(ApiOutcome.Transient, problem),
                _ => ApiResult<T>.Fail(ApiOutcome.Rejected, problem),
            };
        }
        catch (HttpRequestException)
        {
            return ApiResult<T>.Fail(ApiOutcome.Transient);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Zaman aşımı (zayıf bağlantı).
            return ApiResult<T>.Fail(ApiOutcome.Transient);
        }
    }

    private static async Task<ProblemDto?> TryReadProblemAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ProblemDto>(Json, cancellationToken);
        }
        catch (JsonException)
        {
            return new ProblemDto(null, null, (int)response.StatusCode);
        }
    }
}
