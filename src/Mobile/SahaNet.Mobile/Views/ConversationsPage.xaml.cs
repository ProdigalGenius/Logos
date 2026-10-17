using SahaNet.Mobile.Core.ViewModels;

namespace SahaNet.Mobile.Views;

public partial class ConversationsPage : ContentPage
{
    private readonly ConversationsViewModel _viewModel;

    public ConversationsPage(ConversationsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadCommand.ExecuteAsync(null);
    }

    private async void OnSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is ConversationItem item && sender is CollectionView list)
        {
            list.SelectedItem = null;
            await Shell.Current.GoToAsync($"chat?id={item.Id}");
        }
    }
}
