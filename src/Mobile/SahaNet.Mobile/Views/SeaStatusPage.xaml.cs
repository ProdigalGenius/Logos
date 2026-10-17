using System.ComponentModel;
using SahaNet.Mobile.Core.ViewModels;

namespace SahaNet.Mobile.Views;

public partial class SeaStatusPage : ContentPage
{
    private readonly SeaStatusViewModel _viewModel;

    public SeaStatusPage(SeaStatusViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _viewModel.PropertyChanged += OnViewModelChanged;
        VerdictCard.BackgroundColor = Color.FromArgb(_viewModel.VerdictColor);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SeaStatusViewModel.VerdictColor))
        {
            VerdictCard.BackgroundColor = Color.FromArgb(_viewModel.VerdictColor);
        }
    }
}
