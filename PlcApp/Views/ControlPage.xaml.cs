using PlcApp.ViewModels;

namespace PlcApp.Views;

public partial class ControlPage : ContentPage
{
    public ControlPage(ControlViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;

        // Confirmarea inainte de scriere are nevoie de o pagina, deci o legam aici.
        viewModel.ConfirmAsync = (title, message) => DisplayAlert(title, message, "Trimite", "Renunta");
    }
}
