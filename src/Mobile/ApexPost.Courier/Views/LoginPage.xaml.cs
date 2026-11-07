using ApexPost.Courier.Presentation.ViewModels;

namespace ApexPost.Courier.Views;

/// <summary>Sign-in page; all logic is in <see cref="LoginViewModel"/>.</summary>
public partial class LoginPage : ContentPage
{
    /// <summary>Creates the page bound to its view model.</summary>
    /// <param name="viewModel">The sign-in view model.</param>
    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
