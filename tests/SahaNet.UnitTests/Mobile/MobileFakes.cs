using SahaNet.Mobile.Core.Api;
using SahaNet.Mobile.Core.Offline;

namespace SahaNet.UnitTests.Mobile;

internal sealed class FakeConnectivity : IConnectivityMonitor
{
    private bool _isOnline = true;

    public bool IsOnline
    {
        get => _isOnline;
        set
        {
            _isOnline = value;
            ConnectivityChanged?.Invoke(this, value);
        }
    }

    public event EventHandler<bool>? ConnectivityChanged;
}

/// <summary>Sahte API: çevrimdışı modda tüm çağrılar Transient döner.</summary>
internal sealed class FakeApi : ISahaNetApi
{
    public bool Offline { get; set; }

    public List<RecordCatchRequest> RecordedCatches { get; } = [];

    public List<SaveSpotRequest> SavedSpots { get; } = [];

    public HashSet<string> RejectSpecies { get; } = [];

    public SeaStatusDto? SeaStatus { get; set; }

    public bool RequiresOnboarding { get; set; } = true;

    public List<ConsentDecisionDto>? RecordedDecisions { get; private set; }

    public (string Niche, string Region)? Onboarded { get; private set; }

    private ApiResult<T> Respond<T>(Func<T> value) => Offline ? ApiResult<T>.Fail(ApiOutcome.Transient) : ApiResult<T>.Ok(value());

    public Task<ApiResult<RequestOtpResult>> RequestOtpAsync(string phone, CancellationToken cancellationToken) =>
        Task.FromResult(Respond(() => new RequestOtpResult(180, 60)));

    public Task<ApiResult<AuthResult>> VerifyOtpAsync(string phone, string code, CancellationToken cancellationToken) =>
        Task.FromResult(code == "000000"
            ? ApiResult<AuthResult>.Fail(ApiOutcome.Rejected, new ProblemDto("identity.otp_invalid", "Kod hatalı.", 401))
            : Respond(() => new AuthResult(Guid.NewGuid(), "a", DateTimeOffset.UtcNow.AddMinutes(15), "r", DateTimeOffset.UtcNow.AddDays(30), true, RequiresOnboarding)));

    public Task<ApiResult<IReadOnlyList<ConsentDocumentDto>>> GetConsentDocumentsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Respond<IReadOnlyList<ConsentDocumentDto>>(() =>
        [
            new(Guid.NewGuid(), "PrivacyNotice", "1.0", "Aydınlatma", "...", true),
            new(Guid.NewGuid(), "LocationSharing", "1.0", "Konum", "...", false),
        ]));

    public Task<ApiResult<bool>> RecordConsentsAsync(IReadOnlyList<ConsentDecisionDto> decisions, CancellationToken cancellationToken)
    {
        RecordedDecisions = decisions.ToList();
        return Task.FromResult(Respond(() => true));
    }

    public Task<ApiResult<IReadOnlyList<RegionDto>>> GetRegionsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Respond<IReadOnlyList<RegionDto>>(() => [new("34", "İstanbul"), new("57", "Sinop")]));

    public Task<ApiResult<ProfileDto>> CompleteOnboardingAsync(string nicheKey, string regionCode, string? displayName, CancellationToken cancellationToken)
    {
        Onboarded = (nicheKey, regionCode);
        return Task.FromResult(Respond(() => new ProfileDto(Guid.NewGuid(), "+905321234567", displayName, nicheKey, regionCode, null, true)));
    }

    public Task<ApiResult<ProfileDto>> GetProfileAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<ApiResult<IReadOnlyList<PortDto>>> GetPortsAsync(double? latitude, double? longitude, CancellationToken cancellationToken) =>
        Task.FromResult(Respond<IReadOnlyList<PortDto>>(() => [new(Guid.NewGuid(), "sariyer", "Sarıyer", "34", 41.17, 29.06, null)]));

    public Task<ApiResult<SeaStatusDto>> GetSeaStatusAsync(string portCode, CancellationToken cancellationToken) =>
        Task.FromResult(Respond(() => SeaStatus!));

    public Task<ApiResult<IReadOnlyList<SpeciesDto>>> GetSpeciesAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Respond<IReadOnlyList<SpeciesDto>>(() => [new("levrek", "Levrek", null, 25), new("lufer", "Lüfer", null, 20)]));

    public Task<ApiResult<IReadOnlyList<SpotDto>>> GetSpotsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Respond<IReadOnlyList<SpotDto>>(() => SavedSpots
            .Select(s => new SpotDto(s.ClientId ?? Guid.NewGuid(), s.Name, s.Latitude, s.Longitude, s.DepthM, s.Notes, s.Species ?? [], true, 0, DateTimeOffset.UtcNow))
            .ToList()));

    public Task<ApiResult<SpotDto>> SaveSpotAsync(Guid? existingId, SaveSpotRequest request, CancellationToken cancellationToken)
    {
        if (Offline)
        {
            return Task.FromResult(ApiResult<SpotDto>.Fail(ApiOutcome.Transient));
        }

        SavedSpots.Add(request);
        return Task.FromResult(ApiResult<SpotDto>.Ok(new SpotDto(request.ClientId ?? existingId!.Value, request.Name, request.Latitude, request.Longitude, null, null, [], true, 0, DateTimeOffset.UtcNow)));
    }

    public Task<ApiResult<IReadOnlyList<CatchDto>>> GetCatchesAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Respond<IReadOnlyList<CatchDto>>(() => RecordedCatches
            .DistinctBy(c => c.ClientId)
            .Select(c => new CatchDto(c.ClientId!.Value, c.CaughtAt, c.SpeciesCode, c.SpeciesCode, c.WeightKg, c.Count, c.LengthCm, null, null, c.Notes))
            .ToList()));

    public Task<ApiResult<RecordCatchResult>> RecordCatchAsync(RecordCatchRequest request, CancellationToken cancellationToken)
    {
        if (Offline)
        {
            return Task.FromResult(ApiResult<RecordCatchResult>.Fail(ApiOutcome.Transient));
        }

        if (RejectSpecies.Contains(request.SpeciesCode))
        {
            return Task.FromResult(ApiResult<RecordCatchResult>.Fail(ApiOutcome.Rejected, new ProblemDto("fishing.species_unknown", "Tür bulunamadı.", 400)));
        }

        RecordedCatches.Add(request);
        var dto = new CatchDto(request.ClientId!.Value, request.CaughtAt, request.SpeciesCode, request.SpeciesCode, request.WeightKg, null, request.LengthCm, null, null, null);
        IReadOnlyList<string> warnings = request.LengthCm < 25 ? ["Levrek için asgari av boyu 25 cm"] : [];
        return Task.FromResult(ApiResult<RecordCatchResult>.Ok(new RecordCatchResult(dto, warnings)));
    }

    public Task<ApiResult<IReadOnlyList<AlertDto>>> GetActiveAlertsAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Respond<IReadOnlyList<AlertDto>>(() => []));

    public Task<ApiResult<bool>> RegisterDeviceAsync(string token, string platform, CancellationToken cancellationToken) =>
        Task.FromResult(Respond(() => true));
}
