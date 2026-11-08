using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SelectBar.Settings;
using SelectBar.Setup.Logic;
using Xunit;

namespace SelectBar.Setup.Tests;

public class SetupArgsTests
{
    [Fact]
    public void Defaults_to_interactive_install()
    {
        var o = SetupArgs.Parse(Array.Empty<string>());
        Assert.Equal(SetupMode.Install, o.Mode);
        Assert.False(o.Silent);
        Assert.False(o.Demo);
        Assert.Null(o.Dir);
    }

    [Theory]
    [InlineData("/S")]
    [InlineData("--silent")]
    [InlineData("/SILENT")]
    [InlineData("/VERYSILENT")]
    public void Silent_spellings(string arg) => Assert.True(SetupArgs.Parse(new[] { arg }).Silent);

    [Fact]
    public void Inno_compatible_options()
    {
        var o = SetupArgs.Parse(new[] { "/S", "/DIR=\"D:\\Mes Apps\\Selecto\"", "/PURGEDATA", "/NORESTART", "/FORCE", "/SUPPRESSMSGBOXES", "/CURRENTUSER" });
        Assert.True(o.Silent);
        Assert.Equal(@"D:\Mes Apps\Selecto", o.Dir);
        Assert.True(o.PurgeData);
        Assert.True(o.Force);
        Assert.Empty(o.Unknown);
    }

    [Fact]
    public void Uninstall_and_relocation()
    {
        var o = SetupArgs.Parse(new[] { "--uninstall", @"--relocated=C:\Users\a\AppData\Local\Programs\Selecto AI" });
        Assert.Equal(SetupMode.Uninstall, o.Mode);
        Assert.True(o.Relocated);
        Assert.Equal(@"C:\Users\a\AppData\Local\Programs\Selecto AI", o.RelocatedDir);
    }

    [Fact]
    public void Tasks_list_enables_shortcuts()
    {
        var o = SetupArgs.Parse(new[] { "/TASKS=\"desktopicon,startup\"" });
        Assert.True(o.DesktopShortcut);
        Assert.True(o.Startup);
        var only = SetupArgs.Parse(new[] { "/TASKS=startup" });
        Assert.False(only.DesktopShortcut);
        Assert.True(only.Startup);
    }

    [Fact]
    public void Demo_options()
    {
        var o = SetupArgs.Parse(new[] { "--demo", "--screen=progress", "--hold", "--theme=dark", "--lang=en" });
        Assert.True(o.Demo);
        Assert.Equal(SetupScreen.Progress, o.DemoScreen);
        Assert.True(o.Hold);
        Assert.Equal("dark", o.Theme);
        Assert.Equal("en", o.Lang);
    }

    [Theory]
    [InlineData("welcome", SetupScreen.Welcome)]
    [InlineData("progress", SetupScreen.Progress)]
    [InlineData("done", SetupScreen.Done)]
    [InlineData("error", SetupScreen.Error)]
    [InlineData("uninstall", SetupScreen.UninstallConfirm)]
    [InlineData("UNINSTALL-PROGRESS", SetupScreen.UninstallProgress)]
    [InlineData("uninstall-done", SetupScreen.UninstallDone)]
    public void Screen_names(string name, SetupScreen expected) => Assert.Equal(expected, SetupArgs.ParseScreen(name));

    [Fact]
    public void Unknown_values_are_collected_not_fatal()
    {
        var o = SetupArgs.Parse(new[] { "--screen=nope", "--theme=pink", "/WHAT", "stray" });
        Assert.Null(o.DemoScreen);
        Assert.Null(o.Theme);
        Assert.Equal(3, o.Unknown.Count); // --screen=nope, /WHAT, stray
    }
}

public class SetupFlowTests
{
    [Fact]
    public void Happy_path_install()
    {
        var s = SetupFlow.Initial(SetupMode.Install);
        Assert.Equal(SetupScreen.Welcome, s);
        s = SetupFlow.Next(s, SetupEvent.StartInstall, SetupMode.Install);
        Assert.Equal(SetupScreen.Progress, s);
        s = SetupFlow.Next(s, SetupEvent.InstallSucceeded, SetupMode.Install);
        Assert.Equal(SetupScreen.Done, s);
        Assert.True(SetupFlow.IsTerminal(s));
    }

    [Fact]
    public void Failure_then_retry_returns_to_progress()
    {
        var s = SetupFlow.Next(SetupScreen.Progress, SetupEvent.InstallFailed, SetupMode.Install);
        Assert.Equal(SetupScreen.Error, s);
        Assert.Equal(SetupScreen.Progress, SetupFlow.Next(s, SetupEvent.Retry, SetupMode.Install));
    }

    [Fact]
    public void Cancel_during_install_goes_back_to_welcome()
    {
        Assert.True(SetupFlow.CanCancel(SetupScreen.Progress));
        Assert.Equal(SetupScreen.Welcome, SetupFlow.Next(SetupScreen.Progress, SetupEvent.Cancel, SetupMode.Install));
    }

    [Fact]
    public void Uninstall_path_and_retry()
    {
        var s = SetupFlow.Initial(SetupMode.Uninstall);
        Assert.Equal(SetupScreen.UninstallConfirm, s);
        s = SetupFlow.Next(s, SetupEvent.StartUninstall, SetupMode.Uninstall);
        Assert.Equal(SetupScreen.UninstallProgress, s);
        Assert.False(SetupFlow.CanCancel(s));
        var err = SetupFlow.Next(s, SetupEvent.UninstallFailed, SetupMode.Uninstall);
        Assert.Equal(SetupScreen.Error, err);
        Assert.Equal(SetupScreen.UninstallProgress, SetupFlow.Next(err, SetupEvent.Retry, SetupMode.Uninstall));
        Assert.Equal(SetupScreen.UninstallDone, SetupFlow.Next(s, SetupEvent.UninstallSucceeded, SetupMode.Uninstall));
    }

    [Fact]
    public void Invalid_transitions_keep_the_screen()
    {
        Assert.Equal(SetupScreen.Welcome, SetupFlow.Next(SetupScreen.Welcome, SetupEvent.InstallSucceeded, SetupMode.Install));
        Assert.Equal(SetupScreen.Done, SetupFlow.Next(SetupScreen.Done, SetupEvent.StartInstall, SetupMode.Install));
        Assert.Equal(SetupScreen.UninstallDone, SetupFlow.Next(SetupScreen.UninstallDone, SetupEvent.Retry, SetupMode.Uninstall));
    }

    [Fact]
    public void Busy_screens()
    {
        Assert.True(SetupFlow.IsBusy(SetupScreen.Progress));
        Assert.True(SetupFlow.IsBusy(SetupScreen.UninstallProgress));
        Assert.False(SetupFlow.IsBusy(SetupScreen.Welcome));
    }
}

public class VersionLogicTests
{
    [Theory]
    [InlineData("2.0.0", "2.0.0", 0)]
    [InlineData("2.0.1", "2.0.0", 1)]
    [InlineData("2.0.0", "2.1.0", -1)]
    [InlineData("10.0.0", "9.9.9", 1)]
    [InlineData("2.0", "2.0.0.0", 0)]
    [InlineData("2.0.0.1", "2.0.0", 1)]
    [InlineData("2.0.0-Demo", "2.0.0", 0)]
    [InlineData("", "1.0.0", -1)]
    public void Compare(string a, string b, int expected) => Assert.Equal(expected, VersionLogic.Compare(a, b));

    [Fact]
    public void Downgrade_is_refused_unless_forced()
    {
        Assert.True(VersionLogic.IsDowngrade("2.0.0", "2.1.0", false));
        Assert.False(VersionLogic.IsDowngrade("2.0.0", "2.1.0", true));
        Assert.False(VersionLogic.IsDowngrade("2.1.0", "2.1.0", false));
        Assert.False(VersionLogic.IsDowngrade("2.0.0", null, false));
        Assert.False(VersionLogic.IsDowngrade("2.0.0", "", false));
    }

    [Fact]
    public void Newest_picks_the_highest_non_empty()
    {
        Assert.Equal("2.1.0", VersionLogic.Newest(new string?[] { "2.0.0", null, "2.1.0", "" }));
        Assert.Null(VersionLogic.Newest(new string?[] { null, "" }));
    }
}

public class InstallPathsTests
{
    private const string Local = @"C:\Users\Jean\AppData\Local";

    [Fact]
    public void Default_dir_is_per_user_programs()
        => Assert.Equal(@"C:\Users\Jean\AppData\Local\Programs\Selecto AI", InstallPaths.DefaultInstallDir(Local + "\\"));

    [Fact]
    public void Requested_dir_wins_then_existing_then_default()
    {
        Assert.Equal(@"D:\Apps\Selecto", InstallPaths.ChooseInstallDir("\"D:\\Apps\\Selecto\\\"", @"E:\Old", Local));
        Assert.Equal(@"E:\Old\Selecto", InstallPaths.ChooseInstallDir(null, @"E:\Old\Selecto", Local));
        Assert.Equal(InstallPaths.DefaultInstallDir(Local), InstallPaths.ChooseInstallDir("relative\\dir", "nonsense", Local));
    }

    [Theory]
    [InlineData(@"C:\Users\a\Selecto AI", DirCheck.Ok)]
    [InlineData(@"c:/Apps/Selecto", DirCheck.Ok)]
    [InlineData("", DirCheck.Empty)]
    [InlineData("   ", DirCheck.Empty)]
    [InlineData(@"Selecto", DirCheck.NotAbsolute)]
    [InlineData(@"\\server\share\x", DirCheck.NotAbsolute)]
    [InlineData(@"C:\", DirCheck.DriveRoot)]
    [InlineData(@"C:", DirCheck.DriveRoot)]
    [InlineData(@"C:\a|b", DirCheck.InvalidChars)]
    [InlineData(@"C:\a\..\b", DirCheck.InvalidChars)]
    [InlineData(@"C:\a\b ", DirCheck.Ok)] // l'espace final est retiré par la normalisation
    public void Validation(string dir, DirCheck expected) => Assert.Equal(expected, InstallPaths.ValidateDir(dir));

    [Fact]
    public void Too_long_is_rejected() => Assert.Equal(DirCheck.TooLong, InstallPaths.ValidateDir(@"C:\" + new string('a', 250)));

    [Theory]
    [InlineData(@"C:\Users\a\AppData\Local\Programs\Selecto AI", true)]
    [InlineData(@"D:\Selecto AI", false)] // un seul niveau : refusé pour la suppression récursive
    [InlineData(@"C:\Windows\System32", false)]
    [InlineData(@"C:\Users\Jean", false)] // un profil entier
    [InlineData(@"C:\Program Files\Selecto AI", true)]
    [InlineData(@"C:\Users", false)]
    [InlineData(@"C:\Program Files", false)]
    [InlineData(@"C:\", false)]
    public void Safe_install_dir_guard(string dir, bool expected) => Assert.Equal(expected, InstallPaths.IsSafeInstallDir(dir));

    [Fact]
    public void Under_dir_is_case_insensitive_and_exact_on_boundaries()
    {
        Assert.True(InstallPaths.IsUnderDir(@"C:\Users\A\AppData\Local\SelectoAI\ollama\ollama.exe", @"c:\users\a\appdata\local\SelectoAI"));
        Assert.False(InstallPaths.IsUnderDir(@"C:\Users\A\AppData\Local\SelectoAI2\x.exe", @"C:\Users\A\AppData\Local\SelectoAI"));
        Assert.False(InstallPaths.IsUnderDir(@"C:\Users\A\AppData\Local\SelectoAI", @"C:\Users\A\AppData\Local\SelectoAI"));
        Assert.False(InstallPaths.IsUnderDir(null, "C:\\x"));
    }

    [Theory]
    [InlineData("SelectoAI.exe", "SelectoAI.exe")]
    [InlineData("runtimes/win-x64/native/a.dll", @"runtimes\win-x64\native\a.dll")]
    [InlineData(@"sub\b.dll", @"sub\b.dll")]
    [InlineData("SampleExtensions/", null)]
    [InlineData("../evil.dll", null)]
    [InlineData("a/../../evil.dll", null)]
    [InlineData("/abs.dll", null)]
    [InlineData("C:/abs.dll", null)]
    [InlineData("SelectoAI.pdb", null)]
    [InlineData("", null)]
    public void Zip_entry_names_are_sanitized(string entry, string? expected) => Assert.Equal(expected, InstallPaths.SafeRelativePath(entry));

    [Fact]
    public void Shortcut_plan_matches_the_inno_installer()
    {
        var dir = InstallPaths.DefaultInstallDir(Local);
        var list = InstallPaths.PlanShortcuts(dir, @"C:\Users\Jean\AppData\Roaming\Microsoft\Windows\Start Menu\Programs", @"C:\Users\Jean\Desktop", "Selecto AI", false, "Désinstaller Selecto AI");
        Assert.Equal(2, list.Count);
        Assert.EndsWith(@"Programs\Selecto AI\Selecto AI.lnk", list[0].LinkPath);
        Assert.Equal(dir + @"\SelectoAI.exe", list[0].Target);
        Assert.Equal(dir + @"\SelectoAI.ico", list[0].IconPath);
        Assert.Equal(dir + @"\Uninstall.exe", list[1].Target);
        Assert.Equal("--uninstall", list[1].Arguments);

        var withDesktop = InstallPaths.PlanShortcuts(dir, "S", @"C:\Users\Jean\Desktop", "Selecto AI", true, "x");
        Assert.Equal(3, withDesktop.Count);
        Assert.Equal(@"C:\Users\Jean\Desktop\Selecto AI.lnk", withDesktop[2].LinkPath);
    }

    [Fact]
    public void Data_dirs_use_the_app_folder_name()
    {
        Assert.Equal(@"C:\R\SelectoAI", InstallPaths.RoamingDataDir(@"C:\R"));
        Assert.Equal(@"C:\L\SelectoAI", InstallPaths.LocalDataDir(@"C:\L\"));
    }
}

public class UninstallEntryTests
{
    [Fact]
    public void Registers_everything_the_installed_apps_list_needs()
    {
        var dir = @"C:\Users\Jean\AppData\Local\Programs\Selecto AI";
        var v = UninstallEntry.Build("Selecto AI", "2.0.0", dir, 200L * 1024 * 1024).ToDictionary(x => x.Name);
        Assert.Equal("Selecto AI", v["DisplayName"].Text);
        Assert.Equal("2.0.0", v["DisplayVersion"].Text);
        Assert.Equal("EL MAHDI ZINEDDINE", v["Publisher"].Text);
        Assert.Equal(dir + @"\SelectoAI.ico", v["DisplayIcon"].Text);
        Assert.Equal(dir, v["InstallLocation"].Text);
        Assert.Equal("https://selecto.span.observer", v["URLInfoAbout"].Text);
        Assert.Equal("\"" + dir + "\\Uninstall.exe\" --uninstall", v["UninstallString"].Text);
        Assert.Equal("\"" + dir + "\\Uninstall.exe\" --uninstall --silent", v["QuietUninstallString"].Text);
        Assert.Equal(200 * 1024, v["EstimatedSize"].DWord);
        Assert.Equal(1, v["NoModify"].DWord);
        Assert.Equal(1, v["NoRepair"].DWord);
        Assert.Equal(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\SelectoAI", UninstallEntry.KeyPath);
    }
}

public class SizeFormatTests
{
    [Theory]
    [InlineData(0L, true, "fr", "0 Mo")]
    [InlineData(500L, true, "fr", "< 1 Mo")]
    [InlineData(12L * 1048576, true, "fr", "12 Mo")]
    [InlineData(12L * 1048576, true, "en", "12 MB")]
    [InlineData(1610612736L, true, "fr", "1,5 Go")]
    [InlineData(1610612736L, true, "en", "1.5 GB")]
    [InlineData(0L, false, "fr", "absent")]
    [InlineData(0L, false, "en", "not present")]
    public void Format(long bytes, bool exists, string lang, string expected) => Assert.Equal(expected, SizeFormat.Format(bytes, exists, lang));

    [Fact]
    public void Estimated_size_rounds_up_and_clamps()
    {
        Assert.Equal(0, SizeFormat.EstimatedSizeKb(0));
        Assert.Equal(1, SizeFormat.EstimatedSizeKb(1));
        Assert.Equal(2, SizeFormat.EstimatedSizeKb(1025));
        Assert.Equal(int.MaxValue, SizeFormat.EstimatedSizeKb(long.MaxValue));
    }

    [Fact]
    public void Total_ignores_negative_sizes() => Assert.Equal(30, SizeFormat.Total(new long[] { 10, -5, 20 }));
}

public class ProgressStepsTests
{
    [Fact]
    public void Steps_cover_0_to_100_without_gaps()
    {
        Assert.Equal(0, ProgressSteps.StartOf(InstallStep.Prepare));
        Assert.Equal(100, ProgressSteps.EndOf(InstallStep.Finalize));
        for (var s = InstallStep.Prepare; s < InstallStep.Finalize; s++)
            Assert.Equal(ProgressSteps.EndOf(s), ProgressSteps.StartOf(s + 1));
    }

    [Fact]
    public void Percent_is_clamped()
    {
        Assert.Equal(8, ProgressSteps.Percent(InstallStep.Copy, -1));
        Assert.Equal(85, ProgressSteps.Percent(InstallStep.Copy, 5));
        Assert.Equal(8, ProgressSteps.Percent(InstallStep.Copy, double.NaN));
    }

    [Theory]
    [InlineData(0, InstallStep.Prepare)]
    [InlineData(7.9, InstallStep.Prepare)]
    [InlineData(8, InstallStep.Copy)]
    [InlineData(84.9, InstallStep.Copy)]
    [InlineData(85, InstallStep.Shortcuts)]
    [InlineData(94, InstallStep.Finalize)]
    [InlineData(100, InstallStep.Finalize)]
    public void Step_for_percent(double percent, InstallStep expected) => Assert.Equal(expected, ProgressSteps.StepFor(percent));

    [Fact]
    public void Demo_progress_is_monotonic_and_lasts_12_seconds()
    {
        Assert.Equal(12, ProgressSteps.DemoDuration.TotalSeconds);
        var previous = -1.0;
        for (var ms = 0; ms <= 12500; ms += 50)
        {
            var p = ProgressSteps.DemoPercentAt(TimeSpan.FromMilliseconds(ms));
            Assert.InRange(p, 0, 100);
            Assert.True(p >= previous, $"régression à {ms} ms");
            previous = p;
        }
        Assert.Equal(0, ProgressSteps.DemoPercentAt(TimeSpan.Zero));
        Assert.Equal(100, ProgressSteps.DemoPercentAt(TimeSpan.FromSeconds(12)));
        Assert.Equal(InstallStep.Copy, ProgressSteps.StepFor(ProgressSteps.DemoPercentAt(TimeSpan.FromSeconds(5))));
    }
}

public class StringsTests
{
    [Fact]
    public void Every_key_has_both_languages_and_matching_placeholders()
    {
        foreach (var key in Strings.Keys)
        {
            var fr = Strings.Get(key, "fr");
            var en = Strings.Get(key, "en");
            Assert.False(string.IsNullOrWhiteSpace(fr), key + " (fr)");
            Assert.False(string.IsNullOrWhiteSpace(en), key + " (en)");
            var frHoles = Regex.Matches(fr, @"\{\d+\}").Select(m => m.Value).OrderBy(x => x).ToArray();
            var enHoles = Regex.Matches(en, @"\{\d+\}").Select(m => m.Value).OrderBy(x => x).ToArray();
            Assert.Equal(frHoles, enHoles);
        }
    }

    [Fact]
    public void Unknown_key_returns_the_key() => Assert.Equal("nope", Strings.Get("nope", "fr"));

    [Fact]
    public void Format_fills_placeholders() => Assert.Equal("Selecto AI est installé", Strings.Format("done.title", "fr", "Selecto AI"));

    [Theory]
    [InlineData("fr-FR", null, "fr")]
    [InlineData("en-US", null, "en")]
    [InlineData("de-DE", null, "fr")]
    [InlineData("de-DE", "en", "en")]
    [InlineData(null, "FR", "fr")]
    public void Language_resolution(string? ui, string? requested, string expected) => Assert.Equal(expected, Strings.ResolveLang(requested, ui));
}

/// <summary>Cohérence avec l'installateur Inno et l'application (qui restent la référence des comportements).</summary>
public class ParityTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "SelectBar.sln"))) dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    private static string Read(params string[] relative) => File.ReadAllText(Path.Combine(RepoRoot(), Path.Combine(relative)));

    [Fact]
    public void Inno_key_name_uses_the_inno_AppId()
    {
        var iss = Read("installer", "SelectoAI.iss");
        var id = Regex.Match(iss, @"^AppId=\{\{([0-9A-F\-]{36})\}", RegexOptions.Multiline).Groups[1].Value;
        Assert.Equal("{" + id + "}_is1", InstallPaths.InnoUninstallKeyName);
    }

    [Fact]
    public void Mutex_matches_the_application()
    {
        var program = Read("SelectBar", "Program.cs");
        var m = Regex.Match(program, "new Mutex\\(true, @?\"([^\"]+)\"");
        Assert.True(m.Success);
        Assert.Equal(m.Groups[1].Value, InstallPaths.MutexName);
    }

    [Fact]
    public void Constants_match_the_inno_script()
    {
        var iss = Read("installer", "SelectoAI.iss");
        Assert.Contains("#define Publisher \"" + InstallPaths.Publisher + "\"", iss);
        Assert.Contains("#define Website \"" + InstallPaths.Website + "\"", iss);
        Assert.Contains("RunValueName = '" + StartupEntries.Name + "'", iss);
        Assert.Contains("AppDataFolder = '" + InstallPaths.DataFolderName + "'", iss);
        Assert.Contains("Software\\SelectoAI", iss);
        Assert.Equal(@"Software\SelectoAI", InstallPaths.DataRegistryKey);
    }

    [Fact]
    public void Run_value_uses_the_applications_format()
    {
        var key = new FakeRunKey();
        StartupEntries.Apply(key, true, @"C:\x\SelectoAI.exe");
        Assert.Equal("\"C:\\x\\SelectoAI.exe\"", key.Values[StartupEntries.Name]);
    }

    private sealed class FakeRunKey : IRunKey
    {
        public System.Collections.Generic.Dictionary<string, string> Values { get; } = new();
        public string? Get(string name) => Values.TryGetValue(name, out var v) ? v : null;
        public void Set(string name, string value) => Values[name] = value;
        public void Delete(string name) => Values.Remove(name);
    }
}

public class OverlayTrailerTests
{
    private static byte[] Bytes(int n, byte seed) => Enumerable.Range(0, n).Select(i => (byte)(i * 7 + seed)).ToArray();

    private static byte[] Build(byte[] stub, byte[] payload) =>
        stub.Concat(payload).Concat(OverlayTrailer.Build(stub.Length, payload.Length)).ToArray();

    [Fact]
    public void Round_trip_finds_stub_and_payload()
    {
        var stub = Bytes(1000, 1);
        var payload = Bytes(777, 9);
        using var ms = new MemoryStream(Build(stub, payload));
        var info = OverlayTrailer.TryRead(ms);
        Assert.Equal(new OverlayInfo(1000, 777), info);
        Assert.Equal(1000, info!.Value.PayloadOffset);

        using var slice = new OverlayTrailer.Slice(ms, info.Value.PayloadOffset, info.Value.PayloadLength);
        var buf = new byte[payload.Length];
        var read = 0;
        while (read < buf.Length) { var n = slice.Read(buf, read, buf.Length - read); Assert.True(n > 0); read += n; }
        Assert.Equal(payload, buf);
        Assert.Equal(0, slice.Read(new byte[4], 0, 4));
    }

    [Fact]
    public void Slice_seeks_like_a_normal_stream()
    {
        var data = Bytes(100, 3);
        using var slice = new OverlayTrailer.Slice(new MemoryStream(data), 10, 50);
        Assert.Equal(50, slice.Length);
        slice.Seek(-5, SeekOrigin.End);
        var b = new byte[10];
        Assert.Equal(5, slice.Read(b, 0, 10));
        Assert.Equal(data.Skip(55).Take(5), b.Take(5));
        slice.Position = 0;
        Assert.Equal(1, slice.Read(b, 0, 1));
        Assert.Equal(data[10], b[0]);
    }

    [Fact]
    public void Trailer_is_24_bytes_little_endian()
    {
        var t = OverlayTrailer.Build(0x0102, 0x0304);
        Assert.Equal(OverlayTrailer.Size, t.Length);
        Assert.Equal("SELPAYL1", System.Text.Encoding.ASCII.GetString(t, 0, 8));
        Assert.Equal(new byte[] { 0x02, 0x01, 0, 0, 0, 0, 0, 0 }, t.Skip(8).Take(8).ToArray());
        Assert.Equal(new byte[] { 0x04, 0x03, 0, 0, 0, 0, 0, 0 }, t.Skip(16).Take(8).ToArray());
    }

    [Fact]
    public void Missing_or_inconsistent_trailer_is_ignored()
    {
        Assert.Null(OverlayTrailer.TryRead(new MemoryStream(Bytes(2000, 5))));                 // aucun trailer
        Assert.Null(OverlayTrailer.TryRead(new MemoryStream(new byte[10])));                   // trop court
        var good = Build(Bytes(100, 1), Bytes(50, 2));
        Assert.Null(OverlayTrailer.TryRead(new MemoryStream(good.Take(good.Length - 1).ToArray())));        // tronqué
        Assert.Null(OverlayTrailer.TryRead(new MemoryStream(new byte[] { 1 }.Concat(good).ToArray())));     // longueurs fausses
        Assert.Null(OverlayTrailer.TryRead(new MemoryStream(Bytes(10, 1).Concat(OverlayTrailer.Build(0, 0)).ToArray())));
    }

    [Fact]
    public void Copy_prefix_extracts_exactly_the_stub()
    {
        var stub = Bytes(200_000, 4);
        var payload = Bytes(90_000, 8);
        var dir = Path.Combine(Path.GetTempPath(), "selecto-ov-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var setup = Path.Combine(dir, "Setup.exe");
            var unins = Path.Combine(dir, "Uninstall.exe");
            File.WriteAllBytes(setup, Build(stub, payload));
            var info = OverlayTrailer.TryReadFile(setup);
            Assert.NotNull(info);
            OverlayTrailer.CopyPrefix(setup, unins, info!.Value.StubLength);
            Assert.Equal(stub, File.ReadAllBytes(unins));
            Assert.Null(OverlayTrailer.TryReadFile(unins));   // le stub n'a plus de charge utile
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }
}
