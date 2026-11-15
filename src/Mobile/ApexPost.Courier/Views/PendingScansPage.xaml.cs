using ApexPost.Courier.Presentation.ViewModels;

namespace ApexPost.Courier.Views;

/// <summary>Pending tab; all logic is in <see cref="PendingScansViewModel"/>.</summary>
public partial class PendingScansPage : ContentPage
{
    private readonly PendingScansViewModel _viewModel;

    /// <summary>Creates the page bound to its view model.</summary>
    /// <param name="viewModel">The pending scans view model.</param>
    public PendingScansPage(PendingScansViewModel viewModel)
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
