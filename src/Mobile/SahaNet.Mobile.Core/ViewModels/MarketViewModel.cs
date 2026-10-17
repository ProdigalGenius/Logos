using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SahaNet.Mobile.Core.Api;
using SahaNet.Mobile.Core.Niche;

namespace SahaNet.Mobile.Core.ViewModels;

/// <summary>Pazar: nişe ait ilanları ara, sabit fiyatlı ilan ver, ilan sahibine yaz.</summary>
public sealed partial class MarketViewModel(ICommunityApi api, NicheProfile niche) : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _searchText;

    [ObservableProperty]
    private string? _statusText;

    [ObservableProperty]
    private CategoryDto? _selectedCategory;

    [ObservableProperty]
    private string? _title;

    [ObservableProperty]
    private string? _price;

    [ObservableProperty]
    private string? _quantity;

    [ObservableProperty]
    private string? _description;

    public ObservableCollection<ListingDto> Listings { get; } = [];

    public ObservableCollection<CategoryDto> Categories { get; } = [];

    /// <summary>Sohbet açıldığında sayfa yönlendirmesi için.</summary>
    public event EventHandler<Guid>? ConversationOpened;

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        try
        {
            if (Categories.Count == 0 && (await api.GetCategoriesAsync(niche.Key, cancellationToken)).Value is { } categories)
            {
                foreach (var category in categories.Where(c => c.AllowedTypes.Contains("FixedPrice", StringComparer.OrdinalIgnoreCase)))
                {
                    Categories.Add(category);
                }

                SelectedCategory ??= Categories.FirstOrDefault();
            }

            var page = await api.SearchListingsAsync(niche.Key, SearchText, 1, cancellationToken);
            Listings.Clear();
            foreach (var listing in page.Value?.Items ?? [])
            {
                Listings.Add(listing);
            }

            StatusText = page.ErrorMessage ?? (Listings.Count == 0 ? "İlan bulunamadı." : null);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task CreateAsync(CancellationToken cancellationToken)
    {
        if (SelectedCategory is null || string.IsNullOrWhiteSpace(Title)
            || !TryParseDecimal(Price, out var unitPrice) || !TryParseDecimal(Quantity, out var quantity))
        {
            StatusText = "Kategori, başlık, fiyat ve miktar gerekli.";
            return;
        }

        var result = await api.CreateListingAsync(
            new CreateListingRequest(SelectedCategory.Key, Title.Trim(), Description, "FixedPrice", unitPrice, quantity, null, null, null, null, null, null),
            cancellationToken);
        if (!result.IsSuccess)
        {
            StatusText = result.ErrorMessage;
            return;
        }

        Title = Price = Quantity = Description = null;
        await LoadAsync(cancellationToken);
        StatusText = "İlanınız yayında. 🛒";
    }

    [RelayCommand]
    private async Task ContactAsync(ListingDto listing, CancellationToken cancellationToken)
    {
        var result = await api.StartConversationAsync(listing.Id, cancellationToken);
        if (result.Value is { } conversation)
        {
            ConversationOpened?.Invoke(this, conversation.Id);
        }
        else
        {
            StatusText = result.ErrorMessage;
        }
    }

    /// <summary>Hem "12,5" hem "12.5" kabul edilir.</summary>
    internal static bool TryParseDecimal(string? text, out decimal value) =>
        decimal.TryParse(text?.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out value) && value > 0;
}
