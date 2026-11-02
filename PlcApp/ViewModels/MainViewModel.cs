using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;
using PlcApp.Models;
using PlcApp.Services;

namespace PlcApp.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private static readonly Color ColorIdle = Color.FromArgb("#64748B");
    private static readonly Color ColorBusy = Color.FromArgb("#F59E0B");
    private static readonly Color ColorOk = Color.FromArgb("#22C55E");
    private static readonly Color ColorError = Color.FromArgb("#F43F5E");

    /// <summary>
    /// Adresa folosita doar la prima pornire, cat timp nu exista inca o valoare
    /// salvata in Preferences. Fisierul ajunge in repository, deci aici se pune
    /// mereu o adresa generica — adresa reala a PLC-ului se introduce din
    /// interfata (Setari -> ADRESA PLC) si se retine local, pe dispozitiv.
    /// Se accepta si forma "gazda:port" sau "IP:port".
    /// </summary>
    private const string DefaultHost = "192.168.0.100";

    /// <summary>Portul OPC UA implicit, cand adresa nu contine unul explicit.</summary>
    private const int PlcPort = 4840;

    /// <summary>La cate secunde se reincearca conectarea cand PLC-ul nu raspunde.</summary>
    private const double ReconnectSeconds = 5;

    private readonly OpcUaPlcDataSource _opcUa;

    private CancellationTokenSource? _pollingCts;

    /// <summary>Supravegherea conexiunii automate (pornita o singura data, din MainPage).</summary>
    private CancellationTokenSource? _autoCts;

    private IPlcDataSource? _activeSource;
    private int _rawNextRecirculation;

    public MainViewModel(OpcUaPlcDataSource opcUa)
    {
        _opcUa = opcUa;

        Equipment = new ObservableCollection<MetricItem>
        {
            new("Recirculare",     "pompa de recirculare",  0, 4,  Color.FromArgb("#14B8A6")),
            new("Centrala gaz",    "arzator / centrala",    1, 5,  Color.FromArgb("#F59E0B")),
            new("Pompa puffer",    "circuit puffer",        2, 6,  Color.FromArgb("#60A5FA")),
            new("Pompa de caldura","unitate exterioara",    3, 7,  Color.FromArgb("#8B5CF6"))
        };

        Heating = new ObservableCollection<MetricItem>
        {
            new("Subsol", "zona de incalzire", 8,  12, Color.FromArgb("#38BDF8")),
            new("Parter", "zona de incalzire", 9,  13, Color.FromArgb("#34D399")),
            new("Etaj",   "zona de incalzire", 10, 14, Color.FromArgb("#FB7185")),
            new("Pod",    "zona de incalzire", 11, 15, Color.FromArgb("#A78BFA"))
        };

        LoadSettings();
    }

    // ---------------------------------------------------------------- date afisate

    public ObservableCollection<MetricItem> Equipment { get; }
    public ObservableCollection<MetricItem> Heating { get; }

    /// <summary>
    /// Sursa pe care s-a facut conectarea. Tab-urile "Stari" si "Comenzi" o folosesc
    /// ca sa lucreze pe aceeasi sesiune OPC UA, fara sa deschida una noua.
    /// </summary>
    public IPlcDataSource? ActiveSource => _activeSource;

    /// <summary>Accepta si "gazda:port"; fara port, se foloseste PlcPort (4840).</summary>
    private (string Host, int Port) ParseEndpoint()
    {
        var raw = (Host ?? string.Empty).Trim();
        var index = raw.LastIndexOf(':');

        if (index > 0 && int.TryParse(raw[(index + 1)..], out var port) && port is > 0 and <= 65535)
            return (raw[..index], port);

        return (raw, PlcPort);
    }


    public IReadOnlyList<string> UnitOptions { get; } = TimeFormat.Options;

    [ObservableProperty] private string host = DefaultHost;
    [ObservableProperty] private string nodeId = "ns=4;i=71";
    [ObservableProperty] private double intervalSeconds = 1;
    [ObservableProperty] private int selectedUnitIndex = 1;
    [ObservableProperty] private bool useSimulation;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(DisconnectCommand))]
    private bool isConnected;

    /// <summary>
    /// Adresa IP ramane editabila si cat timp citirea este activa: conexiunea fiind
    /// automata, nu mai exista buton de Stop prin care sa deblochezi campul.
    /// Schimbarea IP-ului se aplica cu ReconnectCommand (Enter in camp).
    /// </summary>
    public bool CanEditSettings => true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private bool isBusy;

    [ObservableProperty] private string statusText = "Deconectat";
    [ObservableProperty] private Brush statusBrush = new SolidColorBrush(ColorIdle);
    [ObservableProperty] private string sourceText = "—";
    [ObservableProperty] private string lastUpdateText = "—";
    [ObservableProperty] private int sampleCount;
    [ObservableProperty] private string nextRecirculationText = "—";
    [ObservableProperty] private double nextRecirculationProgress;
    [ObservableProperty] private string? errorText;
    [ObservableProperty] private bool hasError;

    /// <summary>Panoul de setari porneste pliat; se deschide din butonul "Setari".</summary>
    [ObservableProperty] private bool areSettingsVisible;

    /// <summary>Adevarat pe telefon / ferestre inguste (setat din code-behind).</summary>
    [ObservableProperty] private bool isCompact;

    public string SettingsToggleText => AreSettingsVisible ? "Ascunde setarile" : "Setari";

    partial void OnAreSettingsVisibleChanged(bool value) => OnPropertyChanged(nameof(SettingsToggleText));

    [RelayCommand]
    private void ToggleSettings() => AreSettingsVisible = !AreSettingsVisible;

    public TimeUnitKind Unit => (TimeUnitKind)SelectedUnitIndex;

    public string IntervalText => $"Interval citire: {IntervalSeconds:0.0} s";


    partial void OnSelectedUnitIndexChanged(int value)
    {
        foreach (var item in Equipment.Concat(Heating))
            item.Refresh(Unit);

        UpdateNextRecirculation();
        Preferences.Default.Set("unit", value);
    }

    // ---------------------------------------------------------------- conexiune automata

    /// <summary>
    /// Porneste supravegherea conexiunii. Se apeleaza o singura data, din
    /// MainPage.OnAppearing. Prima incercare se face imediat, iar daca PLC-ul nu
    /// raspunde se reincearca la fiecare <see cref="ReconnectSeconds"/> secunde.
    /// </summary>
    public void PornesteConexiunea()
    {
        if (_autoCts is not null)
            return;

        _autoCts = new CancellationTokenSource();
        _ = SupervizeazaConexiuneaAsync(_autoCts.Token);
    }

    /// <summary>Opreste reconectarea automata (la inchiderea ferestrei).</summary>
    public void OpresteConexiunea()
    {
        _autoCts?.Cancel();
        _autoCts = null;
    }

    private async Task SupervizeazaConexiuneaAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            // Acopera toate cazurile: prima pornire, conectare esuata, si caderea
            // buclei de citire dupa 5 erori consecutive (care lasa IsConnected = false).
            if (!IsConnected && !IsBusy && !string.IsNullOrWhiteSpace(Host))
                await ConnectAsync();

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(ReconnectSeconds), token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>Aplica adresa IP noua: inchide sesiunea curenta si reconecteaza.</summary>
    [RelayCommand]
    private async Task ReconnectAsync()
    {
        Host = Host?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(Host))
            return;

        Preferences.Default.Set("host", Host);

        if (IsConnected)
            await DisconnectAsync();

        if (!IsBusy)
            await ConnectAsync();
    }

    // ---------------------------------------------------------------- comenzi

    private bool CanConnect() => !IsConnected && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        IsBusy = true;
        SetError(null);
        SetStatus("Se conecteaza…", ColorBusy);

        var source = (IPlcDataSource)_opcUa;

        try
        {
            await source.ConnectAsync(Host, PlcPort);

            _activeSource = source;
            OnPropertyChanged(nameof(ActiveSource));

            // IsConnected se seteaza ultimul: celelalte tab-uri asculta aceasta
            // schimbare si pornesc citirea, deci sursa trebuie sa fie deja pregatita.
            IsConnected = true;
            SourceText = source.Name;
            SetStatus(UseSimulation ? "Simulare activa" : "Conectat", ColorOk);
            SaveSettings();

            _pollingCts = new CancellationTokenSource();
            _ = PollAsync(source, _pollingCts.Token);
        }
        catch (Exception ex)
        {
            SetError(ex.Message);
            SetStatus("Eroare de conectare", ColorError);
            try { await source.DisconnectAsync(); } catch { /* ignorat */ }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand(CanExecute = nameof(IsConnected))]
    private async Task DisconnectAsync()
    {
        _pollingCts?.Cancel();
        _pollingCts = null;

        var source = _activeSource;
        _activeSource = null;
        OnPropertyChanged(nameof(ActiveSource));

        if (source is not null)
        {
            try { await source.DisconnectAsync(); } catch { /* ignorat */ }
        }

        IsConnected = false;
        SetStatus("Deconectat", ColorIdle);
        SourceText = "—";
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        var app = Application.Current;
        if (app is null)
            return;

        app.UserAppTheme = app.RequestedTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
        Preferences.Default.Set("theme", app.UserAppTheme.ToString());
    }

    [RelayCommand]
    private void ResetCounters()
    {
        foreach (var item in Equipment.Concat(Heating))
            item.Reset();

        SampleCount = 0;
        LastUpdateText = "—";
        NextRecirculationText = "—";
        NextRecirculationProgress = 0;
    }

    // ---------------------------------------------------------------- bucla de citire

    private async Task PollAsync(IPlcDataSource source, CancellationToken token)
    {
        var consecutiveErrors = 0;

        while (!token.IsCancellationRequested)
        {
            try
            {
                var values = await source.ReadArrayAsync(NodeId, token);
                consecutiveErrors = 0;

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    SetError(null);
                    ApplyValues(values);
                    SetStatus("Conectat", ColorOk);
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
                    SetStatus($"Eroare la citire ({consecutiveErrors}/5)", ColorError);
                });

                if (consecutiveErrors >= 5)
                {
                    // Dupa deconectare, bucla de supraveghere reia conectarea singura.
                    MainThread.BeginInvokeOnMainThread(() => _ = DisconnectAsync());
                    break;
                }
            }

            try
            {
                var delay = Math.Clamp(IntervalSeconds, 0.25, 60);
                await Task.Delay(TimeSpan.FromSeconds(delay), token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void ApplyValues(int[] values)
    {
        if (values.Length < 17)
        {
            SetError($"Nodul  {NodeId}  a returnat {values.Length} valori; sunt asteptate cel putin 17. Verifica Node ID-ul.");
            return;
        }

        foreach (var item in Equipment.Concat(Heating))
            item.Apply(values, Unit);

        _rawNextRecirculation = values[16];
        UpdateNextRecirculation();

        SampleCount++;
        LastUpdateText = DateTime.Now.ToString("HH:mm:ss");
    }

    private void UpdateNextRecirculation()
    {
        NextRecirculationText = TimeFormat.Format(_rawNextRecirculation, Unit);

        if (Unit == TimeUnitKind.Brut)
        {
            NextRecirculationProgress = 0;
            return;
        }

        // Progresul se raporteaza la cea mai mare valoare vazuta in sesiunea curenta.
        var minutes = TimeFormat.ToTimeSpan(_rawNextRecirculation, Unit).TotalMinutes;
        NextRecirculationProgress = Math.Clamp(minutes / 60d, 0, 1);
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

    private void LoadSettings()
    {
        var prefs = Preferences.Default;
        Host = prefs.Get("host", DefaultHost);
        SelectedUnitIndex = prefs.Get("unit", 1);

        // Nodul contoarelor nu se editeaza din interfata, deci nu-l mai citim din
        // Preferences: o valoare salvata de o versiune veche a aplicatiei invingea
        // valoarea din cod (asa a ramas activ ns=4;i=68).
        NodeId = "ns=4;i=71";
        prefs.Remove("node");
    }

    private void SaveSettings()
    {
        Preferences.Default.Set("host", Host);
    }
}
