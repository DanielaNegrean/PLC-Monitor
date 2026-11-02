using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;


namespace PlcApp.Services;

/// <summary>
/// Client OPC UA bazat pe stiva oficiala OPC Foundation (.NET Standard).
/// Echivalentul lui `asyncua.Client` din aplicatia Python.
/// </summary>
public sealed class OpcUaPlcDataSource : IPlcDataSource, IAsyncDisposable
{
    private ApplicationConfiguration? _configuration;
    private ISession? _session;

    public string Name => "PLC (OPC UA)";

    public bool IsConnected => _session is { Connected: true };

    // ---------------------------------------------------------------- configurare

    private async Task<ApplicationConfiguration> GetConfigurationAsync()
    {
        if (_configuration is not null)
            return _configuration;

        var pki = Path.Combine(FileSystem.AppDataDirectory, "pki");

        var config = new ApplicationConfiguration
        {
            ApplicationName = "PlcApp",
            ApplicationUri = "urn:localhost:PlcApp",
            ApplicationType = ApplicationType.Client,

            SecurityConfiguration = new SecurityConfiguration
            {
                ApplicationCertificate = new CertificateIdentifier
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(pki, "own"),
                    SubjectName = "CN=PlcApp, O=Local, C=RO"
                },
                TrustedIssuerCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(pki, "issuer")
                },
                TrustedPeerCertificates = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(pki, "trusted")
                },
                RejectedCertificateStore = new CertificateTrustList
                {
                    StoreType = CertificateStoreType.Directory,
                    StorePath = Path.Combine(pki, "rejected")
                },
                // PLC-urile din retea locala au de obicei certificate self-signed.
                AutoAcceptUntrustedCertificates = true,
                AddAppCertToTrustedStore = true,
                RejectSHA1SignedCertificates = false,
                MinimumCertificateKeySize = 1024
            },

            TransportConfigurations = new TransportConfigurationCollection(),
            TransportQuotas = new TransportQuotas { OperationTimeout = 15000 },
            ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = 60000 },
            TraceConfiguration = new TraceConfiguration()
        };

        await config.Validate(ApplicationType.Client).ConfigureAwait(false);

        config.CertificateValidator.CertificateValidation += (_, e) => e.Accept = true;

        var application = new ApplicationInstance(config);
        await application.CheckApplicationInstanceCertificate(true, 2048).ConfigureAwait(false);

        _configuration = config;
        return config;
    }

    // ---------------------------------------------------------------- conectare

    public async Task ConnectAsync(string host, int port, CancellationToken ct = default)
    {
        
        await DisconnectAsync().ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(host))
            throw new ArgumentException("Adresa PLC-ului nu poate fi goala.", nameof(host));

        var config = await GetConfigurationAsync().ConfigureAwait(false);
        var endpointUrl = $"opc.tcp://{host.Trim()}:{port}";

        // Descoperim endpoint-ul serverului (fara securitate, ca in varianta Python).
        var description = await Task.Run(
            () => CoreClientUtils.SelectEndpoint(config, endpointUrl, false, 15000),
            ct).ConfigureAwait(false);

        var endpointConfiguration = EndpointConfiguration.Create(config);
        var endpoint = new ConfiguredEndpoint(null, description, endpointConfiguration);

        _session = await Session.Create(
            config,                                         // configuratia aplicatiei
            endpoint,                                       // endpoint-ul serverului
            false,                                          // updateBeforeConnect
            false,                                          // checkDomain
            "PlcApp",                                       // numele sesiunii
            60000u,                                         // timeout sesiune (ms)
            new UserIdentity(new AnonymousIdentityToken()), // conectare anonima
            (IList<string>?)null).ConfigureAwait(false);

        if (_session is null || !_session.Connected)
            throw new InvalidOperationException("Sesiunea OPC UA nu a putut fi deschisa.");

        _session.KeepAliveInterval = 5000;
    }

    // ---------------------------------------------------------------- citire

    /// <summary>Citeste un nod si returneaza DataValue-ul validat.</summary>
    private async Task<DataValue> ReadDataValueAsync(string nodeId, CancellationToken ct)
    {
        var session = _session ?? throw new InvalidOperationException("Nu exista o sesiune OPC UA activa.");

        if (string.IsNullOrWhiteSpace(nodeId))
            throw new ArgumentException("NodeId-ul nu poate fi gol.", nameof(nodeId));

        var dataValue = await Task.Run(() => session.ReadValue(new NodeId(nodeId.Trim())), ct).ConfigureAwait(false);

        if (dataValue is null || StatusCode.IsBad(dataValue.StatusCode))
            throw new InvalidOperationException($"Citire esuata: {dataValue?.StatusCode}");

        return dataValue;
    }

    public async Task<int[]> ReadArrayAsync(string nodeId, CancellationToken ct = default)
    {
        var dataValue = await ReadDataValueAsync(nodeId, ct).ConfigureAwait(false);
        return ToIntArray(dataValue.Value);
    }

    public async Task<bool[]> ReadBoolArrayAsync(string nodeId, CancellationToken ct = default)
    {
        var dataValue = await ReadDataValueAsync(nodeId, ct).ConfigureAwait(false);
        return ToBoolArray(dataValue.Value);
    }

    public async Task<object?> ReadRawValueAsync(string nodeId, CancellationToken ct = default)
    {
        var dataValue = await ReadDataValueAsync(nodeId, ct).ConfigureAwait(false);
        return dataValue.Value is Variant variant ? variant.Value : dataValue.Value;
    }

    public async Task WriteValueAsync(string nodeId, object value, CancellationToken ct = default)
    {
        var session = _session ?? throw new InvalidOperationException("Nu exista o sesiune OPC UA activa.");

        if (string.IsNullOrWhiteSpace(nodeId))
            throw new ArgumentException("NodeId-ul nu poate fi gol.", nameof(nodeId));

        await Task.Run(() =>
        {
            var values = new WriteValueCollection
            {
                new WriteValue
                {
                    NodeId = new NodeId(nodeId.Trim()),
                    AttributeId = Attributes.Value,
                    Value = new DataValue(new Variant(value))
                }
            };

            session.Write(null, values, out StatusCodeCollection results, out _);

            if (results.Count > 0 && StatusCode.IsBad(results[0]))
                throw new InvalidOperationException($"Scriere esuata: {results[0]}");
        }, ct).ConfigureAwait(false);
    }

    // ---------------------------------------------------------------- deconectare

    public async Task DisconnectAsync()
    {
        var session = _session;
        _session = null;

        if (session is null)
            return;

        try
        {
            await Task.Run(() =>
            {
                try { session.Close(); } catch { /* sesiunea poate fi deja inchisa */ }
                session.Dispose();
            }).ConfigureAwait(false);
        }
        catch
        {
            // ignoram erorile la inchidere
        }
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync().ConfigureAwait(false);

    // ---------------------------------------------------------------- helper

    /// <summary>
    /// PLC-ul poate returna Int16[], UInt16[], Int32[], UInt32[] etc.
    /// Normalizam totul la int[].
    /// </summary>
    private static int[] ToIntArray(object? value)
    {
        switch (value)
        {
            case null:
                return Array.Empty<int>();

            case int[] ints:
                return ints;

            case Matrix matrix:
                return ToIntArray(matrix.Elements);

            case Array array:
                {
                    var result = new int[array.Length];
                    for (var i = 0; i < array.Length; i++)
                    {
                        var element = array.GetValue(i);
                        if (element is Variant variant)
                            element = variant.Value;

                        result[i] = element is null ? 0 : Convert.ToInt32(element);
                    }
                    return result;
                }

            case Variant single:
                return ToIntArray(single.Value);

            default:
                return new[] { Convert.ToInt32(value) };
        }
    }

    /// <summary>
    /// Normalizeaza la bool[]. Serverul poate expune un Array[0..63] of Bool in trei feluri:
    ///   - direct ca Boolean[]            -> il folosim ca atare
    ///   - ca octeti / cuvinte            -> desfacem fiecare numar in bitii lui
    ///   - ca un singur Boolean           -> vector cu un element
    /// Bitii se desfac incepand cu cel mai putin semnificativ, deci Bit[0] = byte0.0,
    /// Bit[8] = byte1.0 etc., exact ca adresarea din TIA Portal.
    /// </summary>
    private static bool[] ToBoolArray(object? value)
    {
        switch (value)
        {
            case null:
                return Array.Empty<bool>();

            case bool[] bools:
                return bools;

            case bool single:
                return new[] { single };

            case Matrix matrix:
                return ToBoolArray(matrix.Elements);

            case Variant variant:
                return ToBoolArray(variant.Value);

            case Array array:
                {
                    var result = new List<bool>(array.Length * 8);

                    for (var i = 0; i < array.Length; i++)
                    {
                        var element = array.GetValue(i);
                        if (element is Variant inner)
                            element = inner.Value;

                        switch (element)
                        {
                            case null:
                                result.Add(false);
                                break;

                            case bool flag:
                                result.Add(flag);
                                break;

                            default:
                                var bits = ToBits(element);
                                if (bits is null)
                                    result.Add(Convert.ToBoolean(element));
                                else
                                    result.AddRange(bits);
                                break;
                        }
                    }

                    return result.ToArray();
                }

            default:
                return ToBits(value) ?? new[] { Convert.ToBoolean(value) };
        }
    }

    /// <summary>Desface un numar intreg in bitii lui, de la cel mai putin semnificativ.</summary>
    private static bool[]? ToBits(object value)
    {
        ulong raw;
        int width;

        switch (value)
        {
            case byte b: raw = b; width = 8; break;
            case sbyte sb: raw = unchecked((ulong)sb) & 0xFF; width = 8; break;
            case short s: raw = unchecked((ulong)s) & 0xFFFF; width = 16; break;
            case ushort us: raw = us; width = 16; break;
            case int i: raw = unchecked((ulong)i) & 0xFFFFFFFF; width = 32; break;
            case uint ui: raw = ui; width = 32; break;
            case long l: raw = unchecked((ulong)l); width = 64; break;
            case ulong ul: raw = ul; width = 64; break;
            default: return null;
        }

        var bits = new bool[width];
        for (var index = 0; index < width; index++)
            bits[index] = (raw & (1UL << index)) != 0;

        return bits;
    }
}
