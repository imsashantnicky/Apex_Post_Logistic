using ApexPost.Courier.Presentation.ViewModels;

namespace ApexPost.Courier.Views;

/// <summary>Sync Status tab; all logic is in <see cref="SyncStatusViewModel"/>.</summary>
public partial class SyncStatusPage : ContentPage
{
    private readonly SyncStatusViewModel _viewModel;

    /// <summary>Creates the page bound to its view model.</summary>
    /// <param name="viewModel">The sync status view model.</param>
    public SyncStatusPage(SyncStatusViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    /// <inheritdoc />
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.RefreshCommand.Execute(null);
    }
}
