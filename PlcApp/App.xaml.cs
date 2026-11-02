using Microsoft.Extensions.DependencyInjection;
using PlcApp.ViewModels;
using PlcApp.Views;

namespace PlcApp;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App(IServiceProvider services)
    {
        InitializeComponent();
        _services = services;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Cele trei tab-uri: Contoare (pagina initiala), Stari, Comenzi.
        var page = _services.GetRequiredService<MainTabbedPage>();

        var window = new Window(page)
        {
            Title = "Monitorizare PLC"
        };

        // Oprim citirea cand se inchide fereastra, ca sa nu ramana sesiunea OPC UA
        // deschisa. Inainte asta se facea in MainPage.OnDisappearing, dar intr-un
        // TabbedPage acel eveniment apare si la simpla schimbare de tab.
        window.Destroying += async (_, _) =>
        {
            var viewModel = _services.GetRequiredService<MainViewModel>();

            // Intai oprim reconectarea automata: altfel supervizorul ar porni o
            // sesiune noua imediat dupa deconectare.
            viewModel.OpresteConexiunea();

            if (viewModel.IsConnected)
                await viewModel.DisconnectCommand.ExecuteAsync(null);
        };

#if WINDOWS
        window.Width = 1240;
        window.Height = 820;
        window.MinimumWidth = 820;
        window.MinimumHeight = 600;
#endif

        return window;
    }
}
