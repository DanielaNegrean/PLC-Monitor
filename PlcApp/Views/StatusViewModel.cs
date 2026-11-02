using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;
using PlcApp.Models;
using PlcApp.Services;

namespace PlcApp.ViewModels;

/// <summary>
/// Tab-ul "Stari": citeste vectorul de Bool din PLC si il afiseaza ca indicatoare.
/// Foloseste aceeasi conexiune ca pagina principala — conectarea se face tot de acolo.
/// </summary>
public partial class StatusViewModel : ObservableObject
{
    private static readonly Color ColorIdle = Color.FromArgb("#64748B");
    private static readonly Color ColorOk = Color.FromArgb("#22C55E");
    private static readonly Color ColorError = Color.FromArgb("#F43F5E");

    /// <summary>
    /// Nodul FromPLC.Bool (Array[0..63] of Bool) din server interface.
    /// Nu se mai citeste din Preferences: o valoare salvata de o versiune veche
    /// ar invinge valoarea din cod.
    /// </summary>
    private const string BoolNode = "ns=4;i=6";

    /// <summary>
    /// Denumirile din TIA Portal, in ordinea bitilor. Daca mai adaugi comentarii
    /// in DB, adauga-le si aici — restul pana la 64 raman "bit nefolosit".
    /// </summary>
    private static readonly string[] BitNames =
    {
        "Pompa recirculare apa",
        "Centrala pe gaz",
        "Centrala pe lemne",
        "Pompa de caldura",
        "Incalzire Subsol",
        "Incalzire Parter",
        "Incalzire Etaj",
        "Incalzire Pod"
    };

    private const int BitCount = 64;

    private readonly MainViewModel _main;
    private CancellationTokenSource? _pollingCts;

    public StatusViewModel(MainViewModel main)
    {
        _main = main;
        _main.PropertyChanged += OnMainViewModelChanged;

        var signals = new List<BoolSignal>(BitCount);
        for (var index = 0; index < BitCount; index++)
        {
            var named = index < BitNames.Length;
            signals.Add(new BoolSignal(
                index,
                named ? BitNames[index] : $"Bit[{index}]",
                named ? "stare transmisa de PLC" : "bit nefolosit",
                named));
        }

        Signals = new ObservableCollection<BoolSignal>(signals);

        boolNodeId = BoolNode;

        // Curatam cheile vechi, ca sa nu reapara un Node ID depasit la o viitoare
        // citire din Preferences.
        Preferences.Default.Remove("boolNode");
        Preferences.Default.Remove("boolShowAll");

        ApplyVisibility();
    }

    public ObservableCollection<BoolSignal> Signals { get; }

    /// <summary>Nodul care contine Array[0..63] of Bool.</summary>
    [ObservableProperty] private string boolNodeId;

    /// <summary>
    /// Pus pe true afiseaza si bitii fara denumire. Nu mai are comutator in
    /// interfata; se schimba aici doar cand vrei sa verifici bitii nefolositi.
    /// </summary>
    [ObservableProperty] private bool showAllBits;

    [ObservableProperty] private string statusText = "Deconectat";
    [ObservableProperty] private Brush statusBrush = new SolidColorBrush(ColorIdle);
    [ObservableProperty] private string lastUpdateText = "—";
    [ObservableProperty] private string activeCountText = "—";
    [ObservableProperty] private string? errorText;
    [ObservableProperty] private bool hasError;

    public bool IsConnected => _main.IsConnected;

    partial void OnShowAllBitsChanged(bool value) => ApplyVisibility();

    private void ApplyVisibility()
    {
        foreach (var signal in Signals)
            signal.IsVisible = signal.IsNamed || ShowAllBits;
    }

    // ---------------------------------------------------------------- legatura cu pagina principala

    private void OnMainViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainViewModel.IsConnected))
            return;

        OnPropertyChanged(nameof(IsConnected));

        if (_main.IsConnected)
            Start();
        else
            Stop();
    }

    private void Start()
    {
        Stop();

        if (string.IsNullOrWhiteSpace(BoolNodeId))
        {
            SetStatus("Node ID lipsa", ColorIdle);
            return;
        }

        _pollingCts = new CancellationTokenSource();
        _ = PollAsync(_pollingCts.Token);
    }

    private void Stop()
    {
        _pollingCts?.Cancel();
        _pollingCts = null;

        SetStatus("Deconectat", ColorIdle);
        SetError(null);
        LastUpdateText = "—";
        ActiveCountText = "—";

        foreach (var signal in Signals)
            signal.Clear();
    }

    /// <summary>Reporneste citirea starilor fara reconectare.</summary>
    [RelayCommand]
    private void Reload()
    {
        if (_main.IsConnected)
            Start();
    }

    // ---------------------------------------------------------------- bucla de citire

    private async Task PollAsync(CancellationToken token)
    {
        var consecutiveErrors = 0;

        while (!token.IsCancellationRequested)
        {
            var source = _main.ActiveSource;

            if (source is null)
                break;

            try
            {
                var values = await source.ReadBoolArrayAsync(BoolNodeId, token);
                consecutiveErrors = 0;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    SetError(null);
                    ApplyValues(values);
                    SetStatus(_main.UseSimulation ? "Simulare activa" : "Citire activa", ColorOk);
                });
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                consecutiveErrors++;
                var message = ex.Message;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    SetError(message);
                    SetStatus($"Eroare la citirea starilor ({consecutiveErrors}/5)", ColorError);
                });

                // Spre deosebire de pagina principala, nu inchidem sesiunea: poate fi
                // doar un Node ID gresit aici, iar contoarele merg mai departe.
                if (consecutiveErrors >= 5)
                    break;
            }

            try
            {
                var delay = Math.Clamp(_main.IntervalSeconds, 0.25, 60);
                await Task.Delay(TimeSpan.FromSeconds(delay), token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void ApplyValues(bool[] values)
    {
        var active = 0;

        for (var index = 0; index < Signals.Count; index++)
        {
            if (index >= values.Length)
                continue;

            Signals[index].Apply(values[index]);

            if (values[index] && Signals[index].IsNamed)
                active++;
        }

        var named = Math.Min(BitNames.Length, values.Length);
        ActiveCountText = $"{active} din {named} active";
        LastUpdateText = DateTime.Now.ToString("HH:mm:ss");

        if (values.Length < BitNames.Length)
            SetError($"Nodul {BoolNodeId} a returnat doar {values.Length} valori; sunt asteptate cel putin {BitNames.Length}.");
    }

    // ---------------------------------------------------------------- ajutatoare

    private void SetStatus(string text, Color color)
    {
        StatusText = text;
        StatusBrush = new SolidColorBrush(color);
    }

    private void SetError(string? message)
    {
        ErrorText = message;
        HasError = !string.IsNullOrWhiteSpace(message);
    }
}
