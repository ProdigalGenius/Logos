using SahaNet.Domain.Common;

namespace SahaNet.Domain.Geo;

/// <summary>WGS84 coğrafi nokta. Domain PostGIS'e bağlı değildir; dönüşüm Infrastructure'dadır.</summary>
public sealed class GeoPoint : ValueObject
{
    private const double EarthRadiusKm = 6371.0088;

    private GeoPoint(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
    }

    public double Latitude { get; }

    public double Longitude { get; }

    public static Result<GeoPoint> Create(double latitude, double longitude)
    {
        if (double.IsNaN(latitude) || latitude is < -90 or > 90 || double.IsNaN(longitude) || longitude is < -180 or > 180)
        {
            return Result.Failure<GeoPoint>(new Error("geo.point_invalid", "Konum geçersiz.", ErrorType.Validation));
        }

        return Result.Success(new GeoPoint(latitude, longitude));
    }

    /// <summary>Önceden doğrulanmış değerler için (seed, kalıcı depo).</summary>
    public static GeoPoint FromTrusted(double latitude, double longitude) => new(latitude, longitude);

    /// <summary>Haversine ile iki nokta arası mesafe (km).</summary>
    public double DistanceKmTo(GeoPoint other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var dLat = ToRadians(other.Latitude - Latitude);
        var dLon = ToRadians(other.Longitude - Longitude);
        var a = Math.Pow(Math.Sin(dLat / 2), 2) +
                Math.Cos(ToRadians(Latitude)) * Math.Cos(ToRadians(other.Latitude)) * Math.Pow(Math.Sin(dLon / 2), 2);
        return 2 * EarthRadiusKm * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Latitude;
        yield return Longitude;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180;
}
