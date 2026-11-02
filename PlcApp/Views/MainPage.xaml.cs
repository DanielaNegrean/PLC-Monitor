using PlcApp.ViewModels;

namespace PlcApp.Views;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;
    private bool _pornit;   // singurul camp ramas: garda de pornire

    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();

        _viewModel = viewModel;
        BindingContext = viewModel;

        SizeChanged += OnPageSizeChanged;

        var saved = Preferences.Default.Get("theme", string.Empty);
        if (Application.Current is not null && Enum.TryParse<AppTheme>(saved, out var theme))
            Application.Current.UserAppTheme = theme;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_pornit) return;        // OnAppearing se declanseaza si la schimbarea de tab
        _pornit = true;
        _viewModel.PornesteConexiunea();
    }

    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        if (Width <= 0) return;
        _viewModel.IsCompact = Width < 760;
    }
}