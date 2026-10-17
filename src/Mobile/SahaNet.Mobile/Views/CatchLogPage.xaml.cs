using SahaNet.Mobile.Core.ViewModels;
using SahaNet.Mobile.Services;

namespace SahaNet.Mobile.Views;

public partial class CatchLogPage : ContentPage
{
    private readonly CatchLogViewModel _viewModel;
    private readonly IVoiceInput _voice;

    public CatchLogPage(CatchLogViewModel viewModel, IVoiceInput voice)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
        _voice = voice;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private async void OnWeightMic(object? sender, EventArgs e) =>
        _viewModel.WeightText = await _voice.ListenAsync(CancellationToken.None) ?? _viewModel.WeightText;

    private async void OnLengthMic(object? sender, EventArgs e) =>
        _viewModel.LengthText = await _voice.ListenAsync(CancellationToken.None) ?? _viewModel.LengthText;
}
