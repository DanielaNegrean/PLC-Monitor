using Microsoft.Extensions.Logging;
using PlcApp.Services;
using PlcApp.ViewModels;
using PlcApp.Views;

namespace PlcApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder.UseMauiApp<App>();

        // Surse de date: PLC real (OPC UA) si simulator (pentru testarea interfetei).
        builder.Services.AddSingleton<OpcUaPlcDataSource>();
      
        // MainViewModel ramane singleton: el detine conexiunea, iar celelalte
        // doua ViewModel-uri o folosesc prin el.
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<StatusViewModel>();
        builder.Services.AddSingleton<ControlViewModel>();

        builder.Services.AddSingleton<MainPage>();
        builder.Services.AddSingleton<StatusPage>();
        builder.Services.AddSingleton<ControlPage>();
        builder.Services.AddSingleton<MainTabbedPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
