using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SelectBar.Update;
using Xunit;

namespace SelectBar.Tests;

/// <summary>Le téléchargement d'une mise à jour suit une redirection 301 vers un autre hôte (GitHub Releases), puis vérifie le SHA-256.</summary>
[Collection("Localization")]
public class UpdateRedirectTests
{
    private static int FreePort()
    {
        var l = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    [Fact]
    public async Task Download_follows_a_301_to_another_host_and_verifies_the_hash()
    {
        var content = Encoding.UTF8.GetBytes("contenu de l'installateur de test " + Guid.NewGuid());
        var sha = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        int portA = FreePort(), portB = FreePort();
        // deux hôtes différents : 127.0.0.1 redirige vers localhost
        using var a = new HttpListener();
        using var b = new HttpListener();
        a.Prefixes.Add($"http://127.0.0.1:{portA}/");
        b.Prefixes.Add($"http://localhost:{portB}/");
        a.Start(); b.Start();
        var serveA = Task.Run(async () =>
        {
            var ctx = await a.GetContextAsync();
            ctx.Response.StatusCode = 301;
            ctx.Response.RedirectLocation = $"http://localhost:{portB}/releases/SelectoAI-Setup-x64-4.0.1.exe";
            ctx.Response.Close();
        });
        var serveB = Task.Run(async () =>
        {
            var ctx = await b.GetContextAsync();
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentLength64 = content.Length;
            await ctx.Response.OutputStream.WriteAsync(content);
            ctx.Response.Close();
        });

        Log.Path = Path.Combine(Path.GetTempPath(), "selecto-redirect-" + Guid.NewGuid().ToString("N") + ".log");
        var service = new UpdateService(new HttpClient(), new Version(4, 0, 0, 0), allowed: true);
        var info = new UpdateInfo("4.0.1", $"http://127.0.0.1:{portA}/downloads/SelectoAI-Setup-x64-4.0.1.exe", sha, content.Length, null);
        string? path = null;
        try
        {
            UpdateLogic.CaptureOrigin = new Uri($"http://127.0.0.1:{portA}/latest.json");
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            path = await service.DownloadAndVerifyAsync(info, null, cts.Token);

            Assert.NotNull(path);
            Assert.Equal(content, await File.ReadAllBytesAsync(path!));
            await Task.WhenAll(serveA, serveB);
        }
        finally
        {
            UpdateLogic.CaptureOrigin = null;
            if (path != null && File.Exists(path)) File.Delete(path);
        }
    }
}
