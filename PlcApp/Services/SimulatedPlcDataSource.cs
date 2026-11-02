namespace PlcApp.Services;

/// <summary>
/// Genereaza valori plauzibile, cu aceeasi structura ca nodul real (17 pozitii).
/// Serveste la verificarea interfetei cand nu esti langa PLC.
/// </summary>
public sealed class SimulatedPlcDataSource : IPlcDataSource
{
    private readonly int[] _values = new int[17];
    private readonly bool[] _bits = new bool[64];
    private readonly Dictionary<string, object> _written = new(StringComparer.OrdinalIgnoreCase);
    private readonly Random _random = new();
    private bool _connected;

    public string Name => "Simulare";

    public bool IsConnected => _connected;

    public Task ConnectAsync(string host, int port, CancellationToken ct = default)
    {
        // Pornim de la niste contoare deja "rulate".
        _values[0] = 1450; _values[1] = 8210; _values[2] = 3120; _values[3] = 15640;
        _values[4] = 62;   _values[5] = 310;  _values[6] = 145;  _values[7] = 640;
        _values[8] = 4100; _values[9] = 9800; _values[10] = 7400; _values[11] = 2200;
        _values[12] = 180; _values[13] = 420; _values[14] = 330; _values[15] = 95;
        _values[16] = 42;

        // Cateva echipamente pornite de la inceput, ca sa se vada tab-ul de stari.
        Array.Clear(_bits);
        _bits[0] = true;   // pompa de recirculare
        _bits[1] = true;   // centrala pe gaz
        _bits[5] = true;   // incalzire parter
        _bits[6] = true;   // incalzire etaj

        _connected = true;
        return Task.CompletedTask;
    }

    public Task<int[]> ReadArrayAsync(string nodeId, CancellationToken ct = default)
    {
        if (!_connected)
            throw new InvalidOperationException("Simulatorul nu este pornit.");

        // Cateva echipamente "merg" in acest moment, altele stau.
        for (var i = 0; i < 4; i++)
        {
            if (_random.NextDouble() < 0.55)
            {
                _values[i] += 1;
                _values[i + 4] += 1;
            }
        }

        for (var i = 8; i < 12; i++)
        {
            if (_random.NextDouble() < 0.4)
            {
                _values[i] += 1;
                _values[i + 4] += 1;
            }
        }

        // Timp pana la urmatoarea recirculare: numara invers si se reseteaza.
        _values[16] = _values[16] > 0 ? _values[16] - 1 : 60;

        return Task.FromResult((int[])_values.Clone());
    }

    public Task<bool[]> ReadBoolArrayAsync(string nodeId, CancellationToken ct = default)
    {
        if (!_connected)
            throw new InvalidOperationException("Simulatorul nu este pornit.");

        // Din cand in cand comutam cate o stare, ca indicatoarele sa nu stea inghetate.
        for (var i = 0; i < 8; i++)
        {
            if (_random.NextDouble() < 0.08)
                _bits[i] = !_bits[i];
        }

        return Task.FromResult((bool[])_bits.Clone());
    }

    public Task<object?> ReadRawValueAsync(string nodeId, CancellationToken ct = default)
    {
        if (!_connected)
            throw new InvalidOperationException("Simulatorul nu este pornit.");

        // Returnam ce s-a "scris" ultima data in acel nod, ca sa se vada efectul comenzii.
        return Task.FromResult(_written.TryGetValue(nodeId ?? string.Empty, out var value) ? value : null);
    }

    public Task WriteValueAsync(string nodeId, object value, CancellationToken ct = default)
    {
        if (!_connected)
            throw new InvalidOperationException("Simulatorul nu este pornit.");

        _written[nodeId ?? string.Empty] = value;
        return Task.CompletedTask;
    }

    public Task DisconnectAsync()
    {
        _connected = false;
        return Task.CompletedTask;
    }
}
