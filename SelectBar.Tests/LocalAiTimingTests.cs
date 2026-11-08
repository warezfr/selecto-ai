using System;
using SelectBar.Online;
using Xunit;

namespace SelectBar.Tests;

public class LocalAiTimingTests
{
    private static readonly DateTime T0 = new(2026, 10, 23, 10, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Engine_start_waits_90_seconds()
    {
        Assert.Equal(90, OllamaEngineInfo.StartTimeoutSeconds);
        Assert.Equal(TimeSpan.FromSeconds(90), LocalAiTiming.EngineStartTimeout);
    }

    [Fact]
    public void Chat_timeout_is_longer_for_a_cold_model()
    {
        Assert.Equal(TimeSpan.FromSeconds(60), LocalAiTiming.ChatTimeout(warm: true));
        Assert.Equal(TimeSpan.FromSeconds(180), LocalAiTiming.ChatTimeout(warm: false));
        Assert.Equal("30m", LocalAiTiming.KeepAlive);
    }

    [Theory]
    [InlineData(12.4, "12,4 s")]
    [InlineData(0.0, "0,0 s")]
    [InlineData(-3.0, "0,0 s")]
    [InlineData(61.27, "61,3 s")]
    public void Durations_are_written_in_french(double seconds, string expected) =>
        Assert.Equal(expected, LocalAiTiming.FormatDuration(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void Startup_summary_lists_ram_cores_avx2_and_model()
    {
        Assert.Equal("IA locale : RAM 8,0 Go, 4 cœurs logiques, AVX2 oui, modèle qwen2.5:1.5b",
            LocalAiTiming.StartupSummary(8.0, 4, true, "qwen2.5:1.5b"));
        Assert.Equal("IA locale : RAM 16,0 Go, 8 cœurs logiques, AVX2 non, modèle aucun",
            LocalAiTiming.StartupSummary(16.0, 8, false, ""));
    }

    [Fact]
    public void Model_is_cold_until_it_is_loaded()
    {
        var t = new ModelWarmTracker();
        Assert.Equal(ModelWarmState.Cold, t.State("qwen2.5:1.5b", T0));

        t.BeginWarming("qwen2.5:1.5b");
        Assert.Equal(ModelWarmState.Warming, t.State("qwen2.5:1.5b", T0));

        t.MarkWarm("qwen2.5:1.5b", T0);
        Assert.Equal(ModelWarmState.Warm, t.State("qwen2.5:1.5b", T0));
    }

    [Fact]
    public void Warm_model_goes_back_to_cold_after_30_minutes_without_call()
    {
        var t = new ModelWarmTracker();
        t.MarkWarm("m", T0);
        Assert.Equal(ModelWarmState.Warm, t.State("m", T0.AddMinutes(30)));
        Assert.Equal(ModelWarmState.Cold, t.State("m", T0.AddMinutes(31)));
    }

    [Fact]
    public void A_successful_call_restarts_the_idle_timer()
    {
        var t = new ModelWarmTracker();
        t.MarkWarm("m", T0);
        t.MarkWarm("m", T0.AddMinutes(25));
        Assert.Equal(ModelWarmState.Warm, t.State("m", T0.AddMinutes(50)));
        Assert.Equal(ModelWarmState.Cold, t.State("m", T0.AddMinutes(56)));
    }

    [Fact]
    public void Failed_loading_returns_to_cold_but_keeps_a_ready_model()
    {
        var t = new ModelWarmTracker();
        t.BeginWarming("m");
        t.Failed("m");
        Assert.Equal(ModelWarmState.Cold, t.State("m", T0));

        t.MarkWarm("m", T0);
        t.Failed("m");                                   // un appel raté sur un modèle prêt ne le décharge pas
        Assert.Equal(ModelWarmState.Warm, t.State("m", T0));
    }

    [Fact]
    public void Another_model_is_always_cold()
    {
        var t = new ModelWarmTracker();
        t.MarkWarm("qwen2.5:1.5b", T0);
        Assert.Equal(ModelWarmState.Cold, t.State("gemma3:4b", T0));
        Assert.Equal(ModelWarmState.Warm, t.State("QWEN2.5:1.5B", T0));   // sans tenir compte de la casse
    }

    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(false, true, true, false)]    // préférence désactivée
    [InlineData(true, false, true, false)]    // IA en ligne choisie
    [InlineData(true, true, false, false)]    // moteur non installé
    public void Warmup_only_for_local_ai_with_engine_installed(bool pref, bool local, bool installed, bool expected) =>
        Assert.Equal(expected, WarmupPlan.ShouldWarm(pref, local, installed));

    [Fact]
    public void Warmup_waits_up_to_20_seconds_polling_every_500_ms()
    {
        Assert.Equal(TimeSpan.FromSeconds(20), WarmupRetryPolicy.ReadyDeadline);
        Assert.Equal(TimeSpan.FromMilliseconds(500), WarmupRetryPolicy.ReadyPollInterval);
    }

    [Fact]
    public void Warmup_makes_at_most_three_attempts()
    {
        Assert.Equal(3, WarmupRetryPolicy.MaxAttempts);
        Assert.True(WarmupRetryPolicy.ShouldRetry(1));
        Assert.True(WarmupRetryPolicy.ShouldRetry(2));
        Assert.False(WarmupRetryPolicy.ShouldRetry(3));
        Assert.False(WarmupRetryPolicy.ShouldRetry(4));
    }

    [Theory]
    [InlineData(1, 1.0)]
    [InlineData(2, 2.0)]
    [InlineData(3, 4.0)]
    [InlineData(9, 4.0)]
    [InlineData(0, 1.0)]
    public void Warmup_backoff_is_1_2_4_seconds(int failedAttempt, double seconds) =>
        Assert.Equal(TimeSpan.FromSeconds(seconds), WarmupRetryPolicy.DelayAfterFailure(failedAttempt));
}
