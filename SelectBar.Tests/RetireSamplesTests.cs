using System;
using System.IO;
using SelectBar.Extensions;
using Xunit;

namespace SelectBar.Tests;

/// <summary>RC-011 : les exemples de la 3.0 déjà renommés « .ancien » ne produisent plus d'erreur dans le journal à chaque démarrage.</summary>
public class RetireSamplesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "selecto-retire-" + Guid.NewGuid().ToString("N"));
    private readonly string _logFile;
    private readonly string _oldLogPath = Log.Path;

    public RetireSamplesTests()
    {
        Directory.CreateDirectory(_dir);
        _logFile = Path.Combine(_dir, "test.log");
        Log.Path = _logFile;
    }

    public void Dispose()
    {
        Log.Path = _oldLogPath;
        try { Directory.Delete(_dir, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static string Sample(string id) =>
        "// ==SelectoExtension==\n// @identifier " + id + "\n// @name Exemple\n// ==/SelectoExtension==\nselecto.copyText(selecto.text);\n";

    [Fact]
    public void Retired_samples_are_silent()
    {
        // état de la revue : uppercase.js est revenu à côté de uppercase.js.ancien
        File.WriteAllText(Path.Combine(_dir, "uppercase.js"), Sample("com.selectbar.sample.uppercase"));
        File.WriteAllText(Path.Combine(_dir, "uppercase.js.ancien"), Sample("com.selectbar.sample.uppercase"));

        var n = ExtensionLoader.RetireOldSamples(_dir);

        Assert.Equal(0, n);
        Assert.False(File.Exists(_logFile), File.Exists(_logFile) ? File.ReadAllText(_logFile) : "");
    }

    [Fact]
    public void A_fresh_old_sample_is_still_renamed()
    {
        File.WriteAllText(Path.Combine(_dir, "word-count.js"), Sample("com.selectbar.sample.word-count"));

        var n = ExtensionLoader.RetireOldSamples(_dir);

        Assert.Equal(1, n);
        Assert.True(File.Exists(Path.Combine(_dir, "word-count.js.ancien")));
        Assert.False(File.Exists(Path.Combine(_dir, "word-count.js")));
        Assert.False(File.Exists(_logFile));
    }
}
