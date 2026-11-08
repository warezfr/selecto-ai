using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SelectBar.Online;
using Xunit;

namespace SelectBar.Tests;

[Collection("Localization")]
public class OllamaReleaseTests
{
    private static readonly string Json = Fx.Read("ollama-release.json");

    [Fact]
    public void Release_assets_are_parsed_with_their_digest()
    {
        var all = OllamaRelease.ParseAssets(Json);
        Assert.Equal(5, all.Count);
        var amd = all.Single(a => a.Name == "ollama-windows-amd64.zip");
        Assert.Equal(1471164488, amd.Size);
        Assert.Equal("e359637575302f288d76c0007c408a4be9c7376d59c4f28684d63b232a44b427", amd.Sha256);
        Assert.Empty(OllamaRelease.ParseAssets("pas du json"));
        Assert.Empty(OllamaRelease.ParseAssets("{\"assets\":3}"));
    }

    [Fact]
    public void Asset_is_chosen_by_architecture_and_never_mlx_rocm_or_setup()
    {
        var all = OllamaRelease.ParseAssets(Json);
        var x64 = OllamaRelease.Choose(all, arm64: false)!;
        var arm = OllamaRelease.Choose(all, arm64: true)!;
        Assert.Equal("ollama-windows-amd64.zip", x64.Name);
        Assert.Equal("ollama-windows-arm64.zip", arm.Name);
        Assert.Equal(210482819, arm.Size);
        foreach (var a in new[] { x64, arm })
        {
            Assert.DoesNotContain("mlx", a.Name);
            Assert.DoesNotContain("rocm", a.Name);
            Assert.DoesNotContain("Setup", a.Name);
        }
        Assert.Null(OllamaRelease.Choose(all.Where(a => a.Name.Contains("mlx") || a.Name.Contains("rocm") || a.Name.Contains("Setup")).ToList(), arm64: false));
    }

    [Fact]
    public void Digest_parser_accepts_only_sha256_hex()
    {
        var hex = new string('A', 64);
        Assert.Equal(new string('a', 64), OllamaRelease.ParseDigest("sha256:" + hex));
        Assert.Null(OllamaRelease.ParseDigest("md5:" + hex));
        Assert.Null(OllamaRelease.ParseDigest("sha256:abc"));
        Assert.Null(OllamaRelease.ParseDigest("sha256:" + new string('z', 64)));
        Assert.Null(OllamaRelease.ParseDigest(""));
        Assert.Null(OllamaRelease.ParseDigest(null));
    }

    [Fact]
    public void Sha256_is_checked_good_bad_and_absent()
    {
        var actual = Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes("abc"))).ToLowerInvariant();
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", actual);
        Assert.Equal(DigestCheck.Match, OllamaRelease.CheckDigest(actual, actual));
        Assert.Equal(DigestCheck.Match, OllamaRelease.CheckDigest(actual.ToUpperInvariant(), actual));
        Assert.Equal(DigestCheck.Mismatch, OllamaRelease.CheckDigest(new string('0', 64), actual));
        Assert.Equal(DigestCheck.Missing, OllamaRelease.CheckDigest("", actual));
        Assert.Equal(DigestCheck.Missing, OllamaRelease.CheckDigest(null, actual));
        Assert.Contains("corrompu", OllamaRelease.CorruptMessage);
    }

    [Fact]
    public void Plan_refuses_missing_digest_foreign_url_and_unknown_arch()
    {
        Assert.True(OllamaRelease.TryPlan(Json, false, out var asset, out var err));
        Assert.Null(err);
        Assert.Equal("ollama-windows-amd64.zip", asset!.Name);

        const string noDigest = "{\"assets\":[{\"name\":\"ollama-windows-amd64.zip\",\"browser_download_url\":\"https://github.com/ollama/ollama/releases/download/v1/ollama-windows-amd64.zip\",\"size\":10}]}";
        Assert.False(OllamaRelease.TryPlan(noDigest, false, out _, out var e1));
        Assert.Equal(OllamaRelease.NoDigestMessage, e1);

        var foreign = noDigest.Replace("https://github.com/ollama", "https://evil.example/ollama").Replace("\"size\"", "\"digest\":\"sha256:" + new string('a', 64) + "\",\"size\"");
        Assert.False(OllamaRelease.TryPlan(foreign, false, out _, out var e2));
        Assert.Contains("refusée", e2);

        Assert.False(OllamaRelease.TryPlan(noDigest, true, out _, out var e3));   // pas d'asset arm64
        Assert.Contains("Aucune version", e3);
    }

    [Theory]
    [InlineData("https://github.com/ollama/ollama/releases/download/v0.40.6/ollama-windows-amd64.zip", true)]
    [InlineData("http://github.com/ollama/ollama/releases/download/v1/x.zip", false)]
    [InlineData("https://evil.example/ollama/ollama/releases/download/v1/x.zip", false)]
    [InlineData("https://github.com.evil.example/ollama/ollama/releases/download/v1/x.zip", false)]
    [InlineData("https://github.com/other/ollama/releases/download/v1/x.zip", false)]
    [InlineData("https://github.com/ollama/ollama/releases/download/../../../evil/x.zip", false)]
    [InlineData("", false)]
    public void Only_the_fixed_github_repository_is_trusted(string url, bool trusted) =>
        Assert.Equal(trusted, OllamaRelease.IsTrustedUrl(url));

    [Fact]
    public void Fixed_api_url_points_to_the_official_repository()
    {
        Assert.Equal("https://api.github.com/repos/ollama/ollama/releases/latest", OllamaRelease.ApiUrl);
    }
}

public class ZipGuardTests
{
    [Theory]
    [InlineData("ollama.exe", true)]
    [InlineData("lib/ollama/cuda_v12/ggml.dll", true)]
    [InlineData("lib\\ollama\\a.dll", true)]
    [InlineData("a/./b.txt", true)]
    [InlineData("../evil.txt", false)]
    [InlineData("..\\evil.txt", false)]
    [InlineData("a/../../evil.txt", false)]
    [InlineData("a\\..\\..\\evil.txt", false)]
    [InlineData("/etc/passwd", false)]
    [InlineData("\\\\serveur\\partage\\x", false)]
    [InlineData("C:\\Windows\\System32\\x.dll", false)]
    [InlineData("C:/x", false)]
    [InlineData("a.txt:flux", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Entry_names_cannot_escape(string? name, bool safe) => Assert.Equal(safe, ZipGuard.IsSafeEntryName(name));

    [Fact]
    public void Resolved_paths_stay_inside_the_target_folder()
    {
        var root = Path.Combine(Path.GetTempPath(), "selectbar-zip-test");
        Assert.True(ZipGuard.TryResolve(root, "lib/ollama/a.dll", out var full));
        Assert.StartsWith(Path.GetFullPath(root), full);
        Assert.False(ZipGuard.TryResolve(root, "../selectbar-zip-test-evil/x", out var evil));
        Assert.Equal("", evil);
        Assert.False(ZipGuard.TryResolve(root, "sub/../../x", out _));
    }
}

[Collection("Localization")]
public class DiskSpaceTests
{
    [Fact]
    public void Required_is_twice_the_engine_plus_the_model()
    {
        Assert.Equal(2 * 210_482_819L + 1_930_000_000L, DiskSpace.Required(210_482_819, 1_930_000_000));
        Assert.Equal(1_930_000_000L, DiskSpace.Required(0, 1_930_000_000));   // moteur déjà installé
    }

    [Fact]
    public void Check_reports_how_much_is_missing()
    {
        Assert.Null(DiskSpace.Check(10_000_000_000, 210_482_819, 1_930_000_000));
        Assert.Null(DiskSpace.Check(DiskSpace.Required(100, 200), 100, 200));          // pile assez
        Assert.Equal(1, DiskSpace.Missing(DiskSpace.Required(100, 200) - 1, DiskSpace.Required(100, 200)));
        var msg = DiskSpace.Check(1_000_000_000, 1_471_164_488, 3_350_000_000)!;
        Assert.Contains("il manque", msg);
        Assert.Contains("Go", msg);
        Assert.Equal(0, DiskSpace.Missing(5, 3));
    }
}

[Collection("Localization")]
public class ModelCatalogTests
{
    private static long Gib(double g) => (long)(g * 1024 * 1024 * 1024);

    [Fact]
    public void Catalog_uses_exact_registry_tags_sizes_and_order()
    {
        Assert.Equal(new[] { "qwen2.5:1.5b", "qwen2.5:3b", "gemma3:4b" }, ModelCatalog.Main.Select(m => m.Id).ToArray());
        Assert.Equal(new[] { "Léger", "Équilibré", "Performant" }, ModelCatalog.Main.Select(m => m.Label).ToArray());
        Assert.Equal("qwen2.5:7b", ModelCatalog.Powerful.Id);
        Assert.Equal(new[] { 990_000_000L, 1_930_000_000L, 3_350_000_000L }, ModelCatalog.Main.Select(m => m.SizeBytes).ToArray());
        Assert.Equal(4_680_000_000L, ModelCatalog.Powerful.SizeBytes);
        Assert.Equal("≈ 1,0 Go", ModelCatalog.Light.SizeText);
        Assert.Equal("≈ 1,9 Go", ModelCatalog.Balanced.SizeText);
        Assert.Equal("≈ 3,4 Go", ModelCatalog.Performant.SizeText);
        Assert.Equal("≈ 4,7 Go", ModelCatalog.Powerful.SizeText);
        Assert.Equal(ModelCatalog.Balanced, ModelCatalog.Find("QWEN2.5:3B"));   // valeur (record), les libellés étant lus à chaque accès
        Assert.Null(ModelCatalog.Find("llama3.2:1b"));
    }

    [Theory]
    [InlineData(2.0, "qwen2.5:1.5b")]
    [InlineData(5.9, "qwen2.5:1.5b")]
    [InlineData(6.0, "qwen2.5:3b")]
    [InlineData(8.0, "qwen2.5:3b")]
    [InlineData(11.9, "qwen2.5:3b")]
    [InlineData(12.0, "gemma3:4b")]
    [InlineData(16.0, "gemma3:4b")]
    [InlineData(64.0, "gemma3:4b")]
    public void Recommendation_follows_physical_memory(double gib, string expected) =>
        Assert.Equal(expected, ModelCatalog.Recommend(Gib(gib)).Id);

    [Fact]
    public void Powerful_model_needs_16_go_and_ram_is_shown_rounded()
    {
        Assert.False(ModelCatalog.AllowsPowerful(Gib(8)));
        Assert.False(ModelCatalog.AllowsPowerful(Gib(15)));
        Assert.True(ModelCatalog.AllowsPowerful(Gib(15.7)));   // un PC « 16 Go » annonce souvent 15,7 Gio
        Assert.True(ModelCatalog.AllowsPowerful(Gib(32)));
        Assert.Equal("Ton PC : 16 Go de mémoire", ModelCatalog.FormatRam(Gib(15.7)));
        Assert.Equal("Ton PC : 8 Go de mémoire", ModelCatalog.FormatRam(Gib(8)));
        Assert.Equal("Recommandé pour ton PC", ModelCatalog.RecommendedBadge);
    }

    [Fact]
    public void Recommendation_uses_cores_and_avx2_too()
    {
        Assert.Equal(ModelCatalog.Light, ModelCatalog.Recommend(Gib(8), 4, true));    // 8 Go mais 4 cœurs
        Assert.Equal(ModelCatalog.Light, ModelCatalog.Recommend(Gib(16), 8, false));  // 16 Go mais sans AVX2
        Assert.Equal(ModelCatalog.Performant, ModelCatalog.Recommend(Gib(16), 8, true));
        Assert.Equal(ModelCatalog.Balanced, ModelCatalog.Recommend(Gib(8), 8, true));
        Assert.Equal(ModelCatalog.Balanced, ModelCatalog.Recommend(Gib(11.4), 6, true));
        Assert.Equal(ModelCatalog.Performant, ModelCatalog.Recommend(Gib(11.9), 6, true)); // un PC « 12 Go » annonce 11,9 Gio
        Assert.Equal(ModelCatalog.Performant, ModelCatalog.Recommend(Gib(12), 6, true));
        Assert.Equal(ModelCatalog.Light, ModelCatalog.Recommend(Gib(6), 8, true));
        Assert.Equal(ModelCatalog.Balanced, ModelCatalog.Recommend(Gib(7.8), 8, true));  // un PC « 8 Go » annonce 7,8 Gio
    }

    [Fact]
    public void Speed_hint_is_given_for_each_model()
    {
        Assert.Equal("Sur ce PC, la première réponse peut prendre jusqu'à une minute.", ModelCatalog.SpeedHint(ModelCatalog.Light));
        Assert.Contains("deux minutes", ModelCatalog.SpeedHint(ModelCatalog.Balanced));
        Assert.Contains("trois minutes", ModelCatalog.SpeedHint(ModelCatalog.Performant));
        Assert.Contains("plusieurs minutes", ModelCatalog.SpeedHint(ModelCatalog.Powerful));
    }
}

[Collection("Localization")]
public class TransferMeterTests
{
    [Fact]
    public void Speed_and_remaining_time_are_computed()
    {
        var m = new TransferMeter();
        m.Update(0, 5_000_000, 0);
        var s = m.Update(1_000_000, 5_000_000, 1000);
        Assert.Equal(1_000_000, s.BytesPerSecond, 1);
        Assert.Equal(0.2, s.Fraction!.Value, 3);
        Assert.Equal(TimeSpan.FromSeconds(4), s.Remaining);
        Assert.Equal("20 % — 1 Mo sur 5 Mo — 1 Mo/s — 4 s restantes", s.Describe());
    }

    [Fact]
    public void Speed_uses_only_the_last_seconds()
    {
        var m = new TransferMeter();
        m.Update(0, 100_000_000, 0);
        m.Update(10_000_000, 100_000_000, 10_000);
        var s = m.Update(11_000_000, 100_000_000, 11_000);
        Assert.Equal(1_000_000, s.BytesPerSecond, 1);
    }

    [Fact]
    public void Unknown_total_or_no_progress_gives_no_estimate()
    {
        var m = new TransferMeter();
        var s = m.Update(500, 0, 0);
        Assert.Null(s.Fraction);
        Assert.Null(s.Remaining);
        var stalled = new TransferMeter();
        stalled.Update(100, 1000, 0);
        Assert.Null(stalled.Update(100, 1000, 2000).Remaining);
        Assert.Equal(1.0, new TransferStatus(2000, 1000, 0, null).Fraction);
    }

    [Fact]
    public void Durations_are_readable()
    {
        Assert.Equal("45 s", TransferMeter.FormatDuration(TimeSpan.FromSeconds(45)));
        Assert.Equal("1 min 20 s", TransferMeter.FormatDuration(TimeSpan.FromSeconds(80)));
        Assert.Equal("1 h 05 min", TransferMeter.FormatDuration(TimeSpan.FromSeconds(3900)));
        Assert.Equal("1 s", TransferMeter.FormatDuration(TimeSpan.FromMilliseconds(100)));
    }
}

[Collection("Localization")]
public class EngineSetupTests
{
    [Fact]
    public void Process_environment_pins_models_folder_and_local_address()
    {
        var root = Path.Combine(Path.GetTempPath(), "SelectoAI", "ollama");
        var p = new OllamaPaths(root);
        var env = p.ProcessEnvironment();
        Assert.Equal(Path.Combine(root, "models"), env["OLLAMA_MODELS"]);
        Assert.Equal("127.0.0.1:11434", env["OLLAMA_HOST"]);
        Assert.Equal("30m", env["OLLAMA_KEEP_ALIVE"]);
        Assert.Equal(3, env.Count);
        Assert.Equal(Path.Combine(root, "app", "ollama.exe"), p.Exe);
        Assert.Equal(Path.Combine(root, "download", "ollama.zip.part"), p.ZipPart);
        Assert.EndsWith(Path.Combine("SelectoAI", "ollama"), OllamaPaths.Resolve(Path.Combine(Path.GetTempPath(), "selecto-none-" + Guid.NewGuid().ToString("N")), _ => false).Root);
    }

    [Fact]
    public void Existing_ollama_is_reused_otherwise_install_or_start()
    {
        Assert.Equal(EnginePlan.UseExisting, EngineDecision.Decide(true, false));
        Assert.Equal(EnginePlan.UseExisting, EngineDecision.Decide(true, true));
        Assert.Equal(EnginePlan.StartInstalled, EngineDecision.Decide(false, true));
        Assert.Equal(EnginePlan.NeedInstall, EngineDecision.Decide(false, false));
    }

    [Fact]
    public void Wizard_steps_follow_engine_and_model()
    {
        Assert.Equal(new[] { StepState.Active, StepState.Todo, StepState.Todo }, AssistantSteps.Compute(false, false));
        Assert.Equal(new[] { StepState.Active, StepState.Todo, StepState.Todo }, AssistantSteps.Compute(false, true));
        Assert.Equal(new[] { StepState.Done, StepState.Active, StepState.Todo }, AssistantSteps.Compute(true, false));
        Assert.Equal(new[] { StepState.Done, StepState.Done, StepState.Done }, AssistantSteps.Compute(true, true));
        Assert.Equal(new[] { StepState.Done, StepState.Active, StepState.Todo }, AssistantSteps.Compute(true, true, changingModel: true));
        Assert.Equal("✓", AssistantSteps.Glyph(StepState.Done));
    }

    [Fact]
    public void Failures_have_a_french_message_with_the_solution()
    {
        Assert.Contains("connexion", SetupErrors.Friendly(new System.Net.Http.HttpRequestException("x")));
        Assert.Contains("antivirus", SetupErrors.Friendly(new System.ComponentModel.Win32Exception(5)));
        Assert.Contains("antivirus", SetupErrors.Friendly(new UnauthorizedAccessException()));
        Assert.Contains("place", SetupErrors.Friendly(new IOException("plein", unchecked((int)0x80070070))));
        Assert.Contains("trop lente", SetupErrors.Friendly(new TimeoutException()));
        Assert.Contains("Réessaie", SetupErrors.Friendly(new InvalidOperationException()));
    }
}

public class EngineHookTests
{
    private sealed class Boom : System.Net.Http.HttpMessageHandler
    {
        public int Calls;
        protected override System.Threading.Tasks.Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request, System.Threading.CancellationToken ct)
        { Calls++; throw new System.Net.Http.HttpRequestException("refusé"); }
    }

    private static SelectBar.Settings.AppSettings Configured()
    {
        var s = new SelectBar.Settings.AppSettings();
        LlmPresets.Apply(s, LlmPresets.Ollama);
        s.LlmModel = "qwen2.5:3b";
        return s;
    }

    [Fact]
    public async System.Threading.Tasks.Task Missing_engine_sends_the_user_to_the_assistant_without_any_request()
    {
        var s = Configured();
        var h = new Boom();
        var svc = new ResultService(() => s, new System.Net.Http.HttpClient(h), () => null, _ => System.Threading.Tasks.Task.FromResult(EngineEnsure.NotInstalled));
        var p = await svc.RunAsync(new ResultRequest { Kind = ResultKind.Summarize, Text = "Texte" }, System.Threading.CancellationToken.None);
        Assert.True(p.NeedsAiSetup);
        Assert.Contains("pas installée", p.Error);
        Assert.Equal(0, h.Calls);
    }

    [Fact]
    public async System.Threading.Tasks.Task Started_engine_lets_the_request_go_through()
    {
        var s = Configured();
        var h = new Boom();
        var asked = 0;
        var svc = new ResultService(() => s, new System.Net.Http.HttpClient(h), () => null, _ => { asked++; return System.Threading.Tasks.Task.FromResult(EngineEnsure.Ready); });
        var p = await svc.RunAsync(new ResultRequest { Kind = ResultKind.Summarize, Text = "Texte" }, System.Threading.CancellationToken.None);
        Assert.Equal(1, asked);
        Assert.Equal(1, h.Calls);
        Assert.False(p.NeedsAiSetup);
    }
}

[Collection("Localization")]
public class AssistantStateTests
{
    private static readonly string[] Qwen = { "qwen2.5:1.5b" };

    private static AssistantState C(bool reachable, string[] models, string? configured, bool ours, bool running) =>
        AssistantLogic.ComputeAssistantState(reachable, models, configured, ours, running);

    [Fact]
    public void Everything_ok_is_ready_without_message()
    {
        var s = C(true, Qwen, "qwen2.5:1.5b", true, true);
        Assert.Equal(AssistantStage.Ready, s.Stage);
        Assert.Null(s.Message);
        Assert.False(s.ClearActiveModel);
        Assert.Equal(AssistantStage.Ready, C(true, Qwen, "QWEN2.5:1.5B", true, true).Stage);
    }

    [Fact]
    public void Engine_gone_and_nothing_installed_asks_for_installation_and_forgets_the_model()
    {
        var s = C(false, Array.Empty<string>(), "qwen2.5:1.5b", false, false);
        Assert.Equal(AssistantStage.InstallEngine, s.Stage);
        Assert.Contains("n'est plus installé", s.Message);
        Assert.True(s.ClearActiveModel);
        var fresh = C(false, Array.Empty<string>(), "", false, false);
        Assert.Equal(AssistantStage.InstallEngine, fresh.Stage);
        Assert.Null(fresh.Message);
        Assert.False(fresh.ClearActiveModel);
    }

    [Fact]
    public void Engine_installed_by_selectbar_but_off_is_started_and_the_model_is_kept()
    {
        var s = C(false, Array.Empty<string>(), "qwen2.5:1.5b", true, false);
        Assert.Equal(AssistantStage.StartEngine, s.Stage);
        Assert.False(s.ClearActiveModel);
        Assert.Equal(AssistantStage.StartEngine, C(false, Array.Empty<string>(), "qwen2.5:1.5b", false, true).Stage);
    }

    [Fact]
    public void Missing_model_goes_to_the_model_step_with_a_clear_message()
    {
        var s = C(true, new[] { "gemma3:4b" }, "qwen2.5:1.5b", true, true);
        Assert.Equal(AssistantStage.ChooseModel, s.Stage);
        Assert.Equal("Le modèle qwen2.5:1.5b n'est plus installé.", s.Message);
        Assert.True(s.ClearActiveModel);
        var none = C(true, Array.Empty<string>(), "", true, true);
        Assert.Equal(AssistantStage.ChooseModel, none.Stage);
        Assert.Null(none.Message);
    }

    [Fact]
    public void External_ollama_ok_is_ready_and_external_ollama_gone_asks_for_installation()
    {
        Assert.Equal(AssistantStage.Ready, C(true, Qwen, "qwen2.5:1.5b", false, false).Stage);
        var gone = C(false, Array.Empty<string>(), "qwen2.5:1.5b", false, false);
        Assert.Equal(AssistantStage.InstallEngine, gone.Stage);
        Assert.True(gone.ClearActiveModel);
    }
}
