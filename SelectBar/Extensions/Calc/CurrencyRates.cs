using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace SelectBar.Extensions;

/// <summary>
/// Taux de change de la BCE (référence EUR), téléchargés au plus une fois par jour et gardés en cache.
/// <see cref="Current"/> ne bloque jamais sur le réseau : il renvoie le cache et lance le rafraîchissement en arrière-plan.
/// </summary>
public sealed class CurrencyRates
{
    public const string EcbUrl = "https://www.ecb.europa.eu/stats/eurofxref/eurofxref-daily.xml";
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);

    private readonly string _cachePath;
    private readonly Func<CancellationToken, Task<string>> _download;
    private readonly Func<DateTime> _utcNow;
    private int _refreshing;

    public CurrencyRates(string cachePath, Func<CancellationToken, Task<string>> download, Func<DateTime>? utcNow = null)
    {
        _cachePath = cachePath;
        _download = download;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    /// <summary>Instance de l'application : %APPDATA%\SelectoAI\rates.xml et téléchargement HTTP.</summary>
    public static CurrencyRates Shared { get; } = new(DefaultCachePath, HttpDownloader(new HttpClient { Timeout = TimeSpan.FromSeconds(15) }));

    public static string DefaultCachePath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SelectoAI", "rates.xml");

    public static Func<CancellationToken, Task<string>> HttpDownloader(HttpClient client) =>
        ct => client.GetStringAsync(EcbUrl, ct);

    /// <summary>
    /// Taux connus (1 EUR = x CCY), ou null si les actions en ligne sont désactivées ou si aucun taux n'a jamais été reçu.
    /// Cache périmé (plus de 24 h) : rafraîchissement lancé en arrière-plan, les taux anciens restent utilisés en attendant.
    /// </summary>
    public IReadOnlyDictionary<string, decimal>? Current(bool onlineAllowed)
    {
        if (!onlineAllowed) return null;
        var cached = ReadCache();
        if (cached == null || _utcNow() - cached.Value.WrittenUtc > MaxAge) StartRefresh();
        return cached?.Rates;
    }

    /// <summary>Télécharge, valide et enregistre les taux. Renvoie false (sans lever d'exception) en cas d'échec.</summary>
    public async Task<bool> RefreshAsync(CancellationToken ct)
    {
        try
        {
            var xml = await _download(ct).ConfigureAwait(false);
            if (ParseEcb(xml) == null) return false;
            var directory = Path.GetDirectoryName(_cachePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            var temp = _cachePath + ".tmp";
            File.WriteAllText(temp, xml);
            File.Move(temp, _cachePath, overwrite: true);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Lit le flux XML de la BCE (éléments Cube currency/rate). EUR vaut 1. Null si le contenu n'est pas valide.</summary>
    public static IReadOnlyDictionary<string, decimal>? ParseEcb(string xml)
    {
        try
        {
            var document = XDocument.Parse(xml);
            var rates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase) { ["EUR"] = 1m };
            foreach (var cube in document.Descendants().Where(e => e.Name.LocalName == "Cube"))
            {
                var code = (string?)cube.Attribute("currency");
                var rate = (string?)cube.Attribute("rate");
                if (string.IsNullOrEmpty(code) || rate == null) continue;
                if (decimal.TryParse(rate, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value > 0)
                    rates[code] = value;
            }
            return rates.Count > 1 ? rates : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private (IReadOnlyDictionary<string, decimal> Rates, DateTime WrittenUtc)? ReadCache()
    {
        try
        {
            if (!File.Exists(_cachePath)) return null;
            var rates = ParseEcb(File.ReadAllText(_cachePath));
            if (rates == null) return null;
            return (rates, File.GetLastWriteTimeUtc(_cachePath));
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void StartRefresh()
    {
        if (Interlocked.Exchange(ref _refreshing, 1) == 1) return;
        _ = Task.Run(async () =>
        {
            try { await RefreshAsync(CancellationToken.None).ConfigureAwait(false); }
            finally { Interlocked.Exchange(ref _refreshing, 0); }
        });
    }
}
