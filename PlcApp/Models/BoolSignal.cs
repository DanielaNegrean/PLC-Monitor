using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Graphics;

namespace PlcApp.Models;

/// <summary>
/// Un bit din vectorul de stari al PLC-ului (Array[0..63] of Bool).
/// Echivalentul lui <see cref="MetricItem"/>, dar pentru valori logice.
/// </summary>
public partial class BoolSignal : ObservableObject
{
    private static readonly Color ColorOn = Color.FromArgb("#22C55E");
    private static readonly Color ColorOff = Color.FromArgb("#64748B");

    public BoolSignal(int index, string title, string subtitle, bool isNamed)
    {
        Index = index;
        Title = title;
        Subtitle = subtitle;
        IsNamed = isNamed;
        isVisible = isNamed;
        stateBrush = new SolidColorBrush(ColorOff);
    }

    /// <summary>Pozitia in vector: Bit[0] … Bit[63].</summary>
    public int Index { get; }

    public string Title { get; }

    public string Subtitle { get; }

    /// <summary>Adevarat pentru bitii carora le-am dat o denumire din TIA Portal.</summary>
    public bool IsNamed { get; }

    /// <summary>Adresa din DB, asa cum apare in TIA Portal: 0.0, 0.1, … 1.0 …</summary>
    public string AddressText => $"Bit[{Index}] · {Index / 8}.{Index % 8}";

    /// <summary>Bitii fara denumire se ascund pana cand utilizatorul cere sa-i vada pe toti.</summary>
    [ObservableProperty] private bool isVisible;

    [ObservableProperty] private bool value;
    [ObservableProperty] private string valueText = "—";
    /// <summary>Pentru Ellipse.Fill, care cere un Brush.</summary>
    [ObservableProperty] private Brush stateBrush;

    /// <summary>Pentru Label.TextColor, care cere un Color (nu un Brush).</summary>
    [ObservableProperty] private Color stateColor = ColorOff;

    [ObservableProperty] private bool hasData;
    [ObservableProperty] private string lastChangeText = "—";

    public void Apply(bool newValue)
    {
        // Retinem momentul comutarii, nu si prima citire (acolo nu stim ce a fost inainte).
        if (HasData && newValue != Value)
            LastChangeText = DateTime.Now.ToString("HH:mm:ss");

        Value = newValue;
        HasData = true;
        ValueText = newValue ? "PORNIT" : "OPRIT";
        StateColor = newValue ? ColorOn : ColorOff;
        StateBrush = new SolidColorBrush(StateColor);
    }

    public void Clear()
    {
        HasData = false;
        Value = false;
        ValueText = "—";
        LastChangeText = "—";
        StateColor = ColorOff;
        StateBrush = new SolidColorBrush(ColorOff);
    }
}
