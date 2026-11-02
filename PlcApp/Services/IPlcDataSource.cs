namespace PlcApp.Services;

/// <summary>
/// Sursa de date pentru PLC. Exista doua implementari:
///   - <see cref="OpcUaPlcDataSource"/> : PLC real, prin OPC UA
///   /// </summary>
public interface IPlcDataSource
{
    bool IsConnected { get; }

    /// <summary>Numele sursei, afisat in interfata.</summary>
    string Name { get; }

    Task ConnectAsync(string host, int port, CancellationToken ct = default);

    /// <summary>Citeste nodul si returneaza valorile ca vector de intregi.</summary>
    Task<int[]> ReadArrayAsync(string nodeId, CancellationToken ct = default);

    /// <summary>
    /// Citeste nodul si returneaza valorile ca vector de Bool (tab-ul "Stari").
    /// Daca serverul expune bitii impachetati in octeti / cuvinte, sunt desfacuti
    /// automat in biti, primul bit fiind cel mai putin semnificativ (ca la Siemens).
    /// </summary>
    Task<bool[]> ReadBoolArrayAsync(string nodeId, CancellationToken ct = default);

    /// <summary>
    /// Citeste un nod si returneaza valoarea asa cum vine de la server, fara conversie.
    /// Folosita in tab-ul "Comenzi" ca sa vezi valoarea curenta inainte de a scrie.
    /// </summary>
    Task<object?> ReadRawValueAsync(string nodeId, CancellationToken ct = default);

    /// <summary>Scrie o valoare intr-un nod (comenzi catre PLC).</summary>
    Task WriteValueAsync(string nodeId, object value, CancellationToken ct = default);

    Task DisconnectAsync();
}
