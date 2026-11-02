using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;
using PlcApp.Models;
using PlcApp.Services;

namespace PlcApp.ViewModels;

/// <summary>
/// Tab-ul "Comenzi": trimite valori catre PLC.
/// Lista de variabile se configureaza din interfata si se salveaza local,
/// deci nu trebuie recompilata aplicatia cand apare un nod nou.
/// </summary>
public partial class ControlViewModel : ObservableObject
{
    private const string StorageKey = "writeCommands";

    private static readonly Color ColorOk = Color.FromArgb("#22C55E");
    private static readonly Color ColorError = Color.FromArgb("#F43F5E");
    private static readonly Color ColorIdle = Color.FromArgb("#64748B");

    private readonly MainViewModel _main;

    public ControlViewModel(MainViewModel main)
    {
        _main = main;
        _main.PropertyChanged += OnMainViewModelChanged;

        Commands = new ObservableCollection<PlcCommand>(Load());

        if (Commands.Count == 0)
            Commands.Add(new PlcCommand { Name = "Setpoint temperatura" });
    }

    public ObservableCollection<PlcCommand> Commands { get; }

    public IReadOnlyList<string> KindOptions => PlcValueKinds.Names;

    public bool IsConnected => _main.IsConnected;

    /// <summary>Avertisment afisat cand esti pe simulare: scrierile nu ajung la PLC.</summary>
    public bool IsSimulation => _main.IsConnected && _main.UseSimulation;

    public string ConnectionHint => _main.IsConnected
        ? (_main.UseSimulation
            ? "Esti pe simulare — valorile nu ajung la PLC, doar se retin local."
            : "Conectat la PLC. Fiecare trimitere cere confirmare.")
        : "Conecteaza-te din pagina Contoare ca sa poti trimite valori.";

    /// <summary>Dialogul de confirmare, pus la dispozitie de pagina (code-behind).</summary>
    public Func<string, string, Task<bool>>? ConfirmAsync { get; set; }

    private void OnMainViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(MainViewModel.IsConnected) or nameof(MainViewModel.UseSimulation)))
            return;

        OnPropertyChanged(nameof(IsConnected));
        OnPropertyChanged(nameof(IsSimulation));
        OnPropertyChanged(nameof(ConnectionHint));
    }

    // ---------------------------------------------------------------- lista de variabile

    [RelayCommand]
    private void Add()
    {
        Commands.Add(new PlcCommand());
        Save();
    }

    [RelayCommand]
    private void Remove(PlcCommand? item)
    {
        if (item is null)
            return;

        Commands.Remove(item);
        Save();
    }

    [RelayCommand]
    private void Save()
    {
        try
        {
            var list = Commands
                .Select(c => new CommandDto(c.Name, c.NodeId, c.KindIndex, c.Minimum, c.Maximum))
                .ToList();

            Preferences.Default.Set(StorageKey, JsonSerializer.Serialize(list));
        }
        catch
        {
            // Salvarea listei nu e critica — aplicatia merge mai departe.
        }
    }

    private static List<PlcCommand> Load()
    {
        try
        {
            var json = Preferences.Default.Get(StorageKey, string.Empty);

            if (string.IsNullOrWhiteSpace(json))
                return new List<PlcCommand>();

            var list = JsonSerializer.Deserialize<List<CommandDto>>(json);

            if (list is null)
                return new List<PlcCommand>();

            return list.Select(dto => new PlcCommand
            {
                Name = dto.Name ?? string.Empty,
                NodeId = dto.NodeId ?? string.Empty,
                KindIndex = dto.KindIndex,
                Minimum = dto.Minimum ?? string.Empty,
                Maximum = dto.Maximum ?? string.Empty
            }).ToList();
        }
        catch
        {
            return new List<PlcCommand>();
        }
    }

    // ---------------------------------------------------------------- citire / scriere

    [RelayCommand]
    private async Task ReadAsync(PlcCommand? item)
    {
        if (item is null)
            return;

        var source = _main.ActiveSource;

        if (source is null || !_main.IsConnected)
        {
            SetStatus(item, "Nu esti conectat.", true);
            return;
        }

        if (string.IsNullOrWhiteSpace(item.NodeId))
        {
            SetStatus(item, "Completeaza Node ID-ul.", true);
            return;
        }

        try
        {
            var value = await source.ReadRawValueAsync(item.NodeId);
            item.CurrentValueText = Describe(value);
            SetStatus(item, $"Citit la {DateTime.Now:HH:mm:ss}.", false);
        }
        catch (Exception ex)
        {
            item.CurrentValueText = "—";
            SetStatus(item, $"Eroare la citire: {ex.Message}", true);
        }
    }

    [RelayCommand]
    private async Task WriteAsync(PlcCommand? item)
    {
        if (item is null)
            return;

        var source = _main.ActiveSource;

        if (source is null || !_main.IsConnected)
        {
            SetStatus(item, "Nu esti conectat.", true);
            return;
        }

        if (string.IsNullOrWhiteSpace(item.NodeId))
        {
            SetStatus(item, "Completeaza Node ID-ul.", true);
            return;
        }

        object value;

        try
        {
            value = Parse(item);
        }
        catch (Exception ex)
        {
            SetStatus(item, ex.Message, true);
            return;
        }

        // Scrierea in PLC schimba procesul fizic, deci cerem confirmare de fiecare data.
        var confirm = ConfirmAsync;

        if (confirm is not null)
        {
            var message =
                $"{item.DisplayName}\n" +
                $"Nod: {item.NodeId.Trim()}\n" +
                $"Tip: {PlcValueKinds.Names[item.KindIndex]}\n\n" +
                $"Valoare trimisa: {value}";

            if (!await confirm("Confirmi trimiterea catre PLC?", message))
            {
                SetStatus(item, "Trimitere anulata.", false);
                return;
            }
        }

        try
        {
            await source.WriteValueAsync(item.NodeId.Trim(), value);
            SetStatus(item, $"Trimis la {DateTime.Now:HH:mm:ss}.", false);

            // Citim inapoi, ca sa vezi ce a retinut PLC-ul.
            try
            {
                var readBack = await source.ReadRawValueAsync(item.NodeId);
                item.CurrentValueText = Describe(readBack);
            }
            catch
            {
                // Daca nodul nu e si citibil, ramanem doar cu confirmarea scrierii.
            }
        }
        catch (Exception ex)
        {
            SetStatus(item, $"Eroare la scriere: {ex.Message}", true);
        }
    }

    // ---------------------------------------------------------------- conversii

    /// <summary>Transforma textul introdus in tipul CLR potrivit pentru OPC UA.</summary>
    private static object Parse(PlcCommand item)
    {
        var raw = (item.InputValue ?? string.Empty).Trim();

        if (raw.Length == 0)
            throw new InvalidOperationException("Introdu o valoare.");

        if (item.Kind == PlcValueKind.String)
            return raw;

        if (item.Kind == PlcValueKind.Bool)
        {
            var text = raw.ToLowerInvariant();

            if (text is "1" or "true" or "da" or "on" or "pornit" or "adevarat")
                return true;

            if (text is "0" or "false" or "nu" or "off" or "oprit" or "fals")
                return false;

            throw new InvalidOperationException("Pentru Bool scrie 1/0, true/false sau da/nu.");
        }

        var number = ParseNumber(raw);

        // Limitele puse de utilizator (de ex. 15–30 °C pentru un setpoint).
        if (TryParseNumber(item.Minimum, out var min) && number < min)
            throw new InvalidOperationException($"Valoarea este sub minimul setat ({item.Minimum}).");

        if (TryParseNumber(item.Maximum, out var max) && number > max)
            throw new InvalidOperationException($"Valoarea depaseste maximul setat ({item.Maximum}).");

        // Limitele tipului de data din PLC.
        var limits = PlcValueKinds.Limits(item.Kind);

        if (limits is not null && (number < limits.Value.Min || number > limits.Value.Max))
            throw new InvalidOperationException(
                $"Tipul {PlcValueKinds.Names[item.KindIndex]} accepta valori intre {limits.Value.Min:0.###} si {limits.Value.Max:0.###}.");

        return item.Kind switch
        {
            PlcValueKind.Byte => (byte)Math.Round(number),
            PlcValueKind.Int16 => (short)Math.Round(number),
            PlcValueKind.UInt16 => (ushort)Math.Round(number),
            PlcValueKind.Int32 => (int)Math.Round(number),
            PlcValueKind.Real => (float)number,
            PlcValueKind.Double => number,
            _ => number
        };
    }

    /// <summary>Acceptam si virgula ca separator zecimal, cum se scrie la noi.</summary>
    private static double ParseNumber(string raw)
    {
        if (!TryParseNumber(raw, out var value))
            throw new InvalidOperationException($"\"{raw}\" nu este un numar valid.");

        return value;
    }

    private static bool TryParseNumber(string? raw, out double value)
    {
        value = 0;

        if (string.IsNullOrWhiteSpace(raw))
            return false;

        return double.TryParse(raw.Trim().Replace(',', '.'),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static string Describe(object? value)
    {
        if (value is null)
            return "—";

        if (value is bool flag)
            return flag ? "TRUE" : "FALSE";

        if (value is float f)
            return f.ToString("0.###", CultureInfo.InvariantCulture);

        if (value is double d)
            return d.ToString("0.###", CultureInfo.InvariantCulture);

        if (value is Array array)
            return $"vector cu {array.Length} elemente";

        return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "—";
    }

    private static void SetStatus(PlcCommand item, string message, bool isError)
    {
        item.StatusText = message;
        item.HasStatus = !string.IsNullOrWhiteSpace(message);
        item.StatusColor = isError ? ColorError : (message.StartsWith("Trimis") ? ColorOk : ColorIdle);
    }

    /// <summary>Forma salvata pe disc a unei variabile (fara valorile de moment).</summary>
    private sealed record CommandDto(string Name, string NodeId, int KindIndex, string Minimum, string Maximum);
}
