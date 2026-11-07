using ApexPost.Courier.Presentation.ViewModels;

namespace ApexPost.Courier.Views;

/// <summary>New Scan tab; all logic is in <see cref="NewScanViewModel"/>.</summary>
public partial class NewScanPage : ContentPage
{
    /// <summary>Creates the page bound to its view model.</summary>
    /// <param name="viewModel">The new scan view model.</param>
    public NewScanPage(NewScanViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
