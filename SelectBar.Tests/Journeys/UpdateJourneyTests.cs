using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SelectBar.Update;
using Xunit;

namespace SelectBar.Tests.Journeys;

/// <summary>Parcours « le site est en panne pendant la vérification de mise à jour » : échec silencieux, nouvel essai le lendemain.</summary>
[Collection("Localization")]
public class UpdateJourneyTests
{
    private sealed class SiteHandler : HttpMessageHandler
    {
        public Func<HttpResponseMessage> Reply { get; set; } = () => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(Reply());
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        public Exception Error { get; init; } = new HttpRequestException("Aucun réseau");
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => throw Error;
    }

    private static readonly Version Installed = new(3, 2, 1, 0);
    private static readonly DateTime Noon = new(2026, 10, 29, 12, 0, 0, DateTimeKind.Utc);

    private static UpdateService Service(HttpMessageHandler handler)
    {
        Log.Path = Path.Combine(Path.GetTempPath(), "selecto-journey-update-" + Guid.NewGuid().ToString("N") + ".log");
        return new UpdateService(new HttpClient(handler), Installed, allowed: true);
    }

    [Fact]
    public async Task Manifest_unavailable_is_silent_and_retries_next_day()
    {
        var site = new SiteHandler();
        var service = Service(site);
        var notifications = 0;
        service.Changed += () => { if (service.Available is not null || service.LastError is not null) notifications++; };

        var state = await service.CheckAsync(CancellationToken.None);

        Assert.Equal(1, site.Calls);
        Assert.Equal(UpdateState.Failed, state);
        Assert.Null(service.Available);                 // rien à proposer : aucune notification « mise à jour disponible »
        Assert.Null(service.LastError);                 // aucun message d'erreur affiché au client
        Assert.Equal(0, notifications);

        // La vérification suivante : l'application note l'heure AVANT d'interroger le site (AppController.RunUpdateCheckAsync),
        // donc une panne ne provoque pas de nouvelle tentative avant 24 h.
        Assert.False(UpdateLogic.ShouldCheck(Noon, Noon.AddHours(23).AddMinutes(59)));
        Assert.True(UpdateLogic.ShouldCheck(Noon, Noon.AddHours(24)));
    }

    [Fact]
    public async Task After_a_failure_the_service_can_check_again_and_recovers()
    {
        var site = new SiteHandler();
        var service = Service(site);
        Assert.Equal(UpdateState.Failed, await service.CheckAsync(CancellationToken.None));

        site.Reply = () => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"version\":\"3.2.1\",\"installers\":[]}", Encoding.UTF8, "application/json")
        };

        Assert.Equal(UpdateState.UpToDate, await service.CheckAsync(CancellationToken.None));
        Assert.Null(service.Available);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, "<html>Maintenance</html>")]
    [InlineData(HttpStatusCode.InternalServerError, "")]
    [InlineData(HttpStatusCode.NotFound, "Not found")]
    [InlineData(HttpStatusCode.OK, "<html>pas du JSON</html>")]
    [InlineData(HttpStatusCode.OK, "{\"version\":\"3.3.0\"}")]
    public async Task Bad_manifest_responses_fail_without_exception_or_toast(HttpStatusCode status, string body)
    {
        var site = new SiteHandler { Reply = () => new HttpResponseMessage(status) { Content = new StringContent(body) } };
        var service = Service(site);

        var state = await service.CheckAsync(CancellationToken.None);

        Assert.Equal(UpdateState.Failed, state);
        Assert.Null(service.Available);
        Assert.Null(service.LastError);
    }

    [Fact]
    public async Task Network_down_fails_without_exception_or_toast()
    {
        var service = Service(new ThrowingHandler());

        var state = await service.CheckAsync(CancellationToken.None);

        Assert.Equal(UpdateState.Failed, state);
        Assert.Null(service.Available);
        Assert.Null(service.LastError);
    }

    [Fact]
    public async Task Request_timeout_fails_without_exception()
    {
        var service = Service(new ThrowingHandler { Error = new TaskCanceledException("délai") });

        var state = await service.CheckAsync(CancellationToken.None);

        Assert.Equal(UpdateState.Failed, state);
    }
}
