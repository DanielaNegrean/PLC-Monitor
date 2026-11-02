using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Graphics;

namespace PlcApp.Models;

/// <summary>Tipul de data al nodului din PLC. Trebuie sa corespunda cu cel din TIA Portal.</summary>
public enum PlcValueKind
{
    Bool,
    Byte,
    Int16,
    UInt16,
    Int32,
    Real,
    Double,
    String
}

/// <summary>Denumirile tipurilor, pentru Picker-ul din interfata.</summary>
public static class PlcValueKinds
{
    /// <summary>Ordinea trebuie sa fie identica cu cea din <see cref="PlcValueKind"/>.</summary>
    public static readonly string[] Names =
    {
        "Bool",
        "Byte (USInt)",
        "Int (Int16)",
        "UInt (UInt16)",
        "DInt (Int32)",
        "Real (Float)",
        "LReal (Double)",
        "String"
    };

    /// <summary>Limitele tipului, folosite la validare inainte de scriere.</summary>
    public static (double Min, double Max)? Limits(PlcValueKind kind) => kind switch
    {
        PlcValueKind.Byte => (byte.MinValue, byte.MaxValue),
        PlcValueKind.Int16 => (short.MinValue, short.MaxValue),
        PlcValueKind.UInt16 => (ushort.MinValue, ushort.MaxValue),
        PlcValueKind.Int32 => (int.MinValue, int.MaxValue),
        PlcValueKind.Real => (-3.4e38, 3.4e38),
        _ => null
    };
}

/// <summary>
/// O variabila pe care o poti scrie in PLC din tab-ul "Comenzi".
/// Lista se salveaza local, deci o configurezi o singura data.
/// </summary>
public partial class PlcCommand : ObservableObject
{
    [ObservableProperty] private string name = string.Empty;
    [ObservableProperty] private string nodeId = string.Empty;

    /// <summary>Index in <see cref="PlcValueKinds.Names"/>.</summary>
    [ObservableProperty] private int kindIndex;

    /// <summary>Valoarea introdusa de utilizator, inainte de conversie.</summary>
    [ObservableProperty] private string inputValue = string.Empty;

    /// <summary>Limite optionale, ca sa nu trimiti din greseala o valoare aiurea.</summary>
    [ObservableProperty] private string minimum = string.Empty;
    [ObservableProperty] private string maximum = string.Empty;

    [ObservableProperty] private string currentValueText = "—";
    [ObservableProperty] private string statusText = string.Empty;
    [ObservableProperty] private bool hasStatus;

    /// <summary>Verde pentru confirmari, rosu pentru erori.</summary>
    [ObservableProperty] private Color statusColor = Color.FromArgb("#64748B");

    public PlcValueKind Kind => (PlcValueKind)KindIndex;

    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "Variabila fara nume" : Name;

    partial void OnNameChanged(string value) => OnPropertyChanged(nameof(DisplayName));
}
