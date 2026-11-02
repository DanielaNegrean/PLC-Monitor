using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Graphics;
using PlcApp.Drawables;

namespace PlcApp.Models;

/// <summary>
/// Un contor afisat pe un card: valoarea totala si valoarea din ultima zi.
/// Indicii corespund pozitiilor din vectorul citit de la PLC.
/// </summary>
public partial class MetricItem : ObservableObject
{
    private readonly List<double> _history = new();
    private bool _hasPreviousSample;
    private int _rawTotal;
    private int _rawLastDay;

    public MetricItem(string title, string subtitle, int totalIndex, int lastDayIndex, Color accent)
    {
        Title = title;
        Subtitle = subtitle;
        TotalIndex = totalIndex;
        LastDayIndex = lastDayIndex;
        Accent = accent;
        AccentBrush = new SolidColorBrush(accent);
        StateBrush = new SolidColorBrush(Color.FromArgb("#64748B"));
        Sparkline = new SparklineDrawable(Array.Empty<double>(), accent);
    }

    public string Title { get; }
    public string Subtitle { get; }
    public int TotalIndex { get; }
    public int LastDayIndex { get; }
    public Color Accent { get; }
    public Brush AccentBrush { get; }

    [ObservableProperty] private string totalText = "—";
    [ObservableProperty] private string lastDayText = "—";
    [ObservableProperty] private string stateText = "inactiv";
    [ObservableProperty] private double progress;
    [ObservableProperty] private string progressText = "—";
    [ObservableProperty] private bool isRunning;
    [ObservableProperty] private Brush stateBrush;
    [ObservableProperty] private IDrawable sparkline;

    private static readonly Color RunningColor = Color.FromArgb("#22C55E");
    private static readonly Color IdleColor = Color.FromArgb("#64748B");

    /// <summary>Aplica un esantion nou citit de la PLC.</summary>
    public void Apply(int[] values, TimeUnitKind unit)
    {
        var total = TotalIndex < values.Length ? values[TotalIndex] : 0;
        var lastDay = LastDayIndex < values.Length ? values[LastDayIndex] : 0;

        IsRunning = _hasPreviousSample && total > _rawTotal;
        _hasPreviousSample = true;

        _rawTotal = total;
        _rawLastDay = lastDay;

        _history.Add(lastDay);
        if (_history.Count > 90)
            _history.RemoveAt(0);

        Sparkline = new SparklineDrawable(_history, Accent);
        StateText = IsRunning ? "in functiune" : "inactiv";
        StateBrush = new SolidColorBrush(IsRunning ? RunningColor : IdleColor);

        Refresh(unit);
    }

    /// <summary>Reformateaza textele cand utilizatorul schimba unitatea de timp.</summary>
    public void Refresh(TimeUnitKind unit)
    {
        TotalText = TimeFormat.Format(_rawTotal, unit);
        LastDayText = TimeFormat.Format(_rawLastDay, unit);

        if (unit == TimeUnitKind.Brut)
        {
            Progress = 0;
            ProgressText = $"{_rawLastDay} (valoare bruta)";
            return;
        }

        var hours = TimeFormat.ToTimeSpan(_rawLastDay, unit).TotalHours;
        Progress = Math.Clamp(hours / 24d, 0, 1);
        ProgressText = $"{Progress * 100:0.#}% din 24h";
    }

    public void Reset()
    {
        _history.Clear();
        _hasPreviousSample = false;
        _rawTotal = 0;
        _rawLastDay = 0;
        TotalText = "—";
        LastDayText = "—";
        ProgressText = "—";
        Progress = 0;
        IsRunning = false;
        StateText = "inactiv";
        StateBrush = new SolidColorBrush(IdleColor);
        Sparkline = new SparklineDrawable(Array.Empty<double>(), Accent);
    }
}
