using System.Globalization;
using SahaNet.Domain.Common;
using SahaNet.Domain.Market;

namespace SahaNet.Application.Market;

public enum AttributeType
{
    Text = 0,
    Number = 1,
    DateTime = 2,
    Choice = 3,
}

public sealed record AttributeDefinition(
    string Key,
    string Label,
    AttributeType Type,
    bool Required,
    IReadOnlyList<string>? Choices = null,
    int MaxLength = 200);

/// <summary>Bir modülün sunduğu ilan kategorisi ve kategoriye özel alan şeması.</summary>
public sealed record ListingCategory(
    string Key,
    string NicheKey,
    string Name,
    string DefaultUnit,
    IReadOnlyList<ListingType> AllowedTypes,
    IReadOnlyList<AttributeDefinition> Attributes);

/// <summary>Modüller ilan kategorilerini bu sözleşmeyle çekirdeğe kaydeder (kategori şeması modül bazında genişler).</summary>
public interface IListingCategoryProvider
{
    IEnumerable<ListingCategory> GetCategories();
}

public static class AttributeValidator
{
    /// <summary>Şemaya göre doğrular ve normalize eder (sayılar/tarihler değişmez kültürle saklanır).</summary>
    public static Result<IReadOnlyDictionary<string, string>> Validate(ListingCategory category, IReadOnlyDictionary<string, string>? input)
    {
        ArgumentNullException.ThrowIfNull(category);
        input ??= new Dictionary<string, string>();

        var unknown = input.Keys.Except(category.Attributes.Select(a => a.Key), StringComparer.Ordinal).FirstOrDefault();
        if (unknown is not null)
        {
            return Fail($"Bilinmeyen alan: {unknown}");
        }

        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var definition in category.Attributes)
        {
            if (!input.TryGetValue(definition.Key, out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                if (definition.Required)
                {
                    return Fail($"{definition.Label} zorunlu.");
                }

                continue;
            }

            var value = raw.Trim();
            switch (definition.Type)
            {
                case AttributeType.Text when value.Length > definition.MaxLength:
                    return Fail($"{definition.Label} en fazla {definition.MaxLength} karakter olabilir.");
                case AttributeType.Number:
                    if (!decimal.TryParse(value.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
                    {
                        return Fail($"{definition.Label} sayı olmalı.");
                    }

                    value = number.ToString(CultureInfo.InvariantCulture);
                    break;
                case AttributeType.DateTime:
                    if (!DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
                    {
                        return Fail($"{definition.Label} geçerli bir tarih olmalı.");
                    }

                    value = date.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
                    break;
                case AttributeType.Choice when definition.Choices is not null && !definition.Choices.Contains(value, StringComparer.Ordinal):
                    return Fail($"{definition.Label} için geçersiz seçim.");
            }

            result[definition.Key] = value;
        }

        return Result.Success<IReadOnlyDictionary<string, string>>(result);

        static Result<IReadOnlyDictionary<string, string>> Fail(string message) =>
            Result.Failure<IReadOnlyDictionary<string, string>>(MarketErrors.AttributeInvalid(message));
    }
}
