using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SelectBar.Settings;
using Xunit;

namespace SelectBar.Tests;

/// <summary>Contrôles de texte seulement sur l'installateur (rien n'est compilé ni exécuté).</summary>
public class InstallerScriptTests
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
    public void Iss_contains_the_key_directives()
    {
        var iss = Read("installer", "SelectoAI.iss");
        Assert.Matches(new Regex(@"^AppId=\{\{[0-9A-F\-]{36}\}\s*$", RegexOptions.Multiline), iss);
        Assert.Contains("PrivilegesRequired=lowest", iss);
        Assert.Contains("PrivilegesRequiredOverridesAllowed=dialog", iss);
        Assert.Contains("ArchitecturesAllowed=", iss);
        Assert.Contains("ArchitecturesInstallIn64BitMode=", iss);
        Assert.Contains("Name: \"french\"", iss);
        Assert.Contains("Name: \"english\"", iss);
        Assert.Contains("license-fr.txt", iss);
        Assert.Contains("license-en.txt", iss);
        Assert.Contains("Name: \"spanish\"", iss);
        Assert.Contains("license-es.txt", iss);
        Assert.Contains(@"SelectBar\Assets\SelectoAI.ico", iss);
        Assert.Contains("SetupIconFile=", iss);
        Assert.Contains("CloseApplications=yes", iss);
        Assert.Contains("RestartApplications=no", iss);
        Assert.Contains("PURGEDATA", iss);
    }

    [Fact]
    public void Iss_mutex_is_the_one_defined_in_Program()
    {
        var program = Read("SelectBar", "Program.cs");
        var m = Regex.Match(program, "new Mutex\\(true, @?\"([^\"]+)\"");
        Assert.True(m.Success, "mutex d'instance unique introuvable dans Program.cs");
        Assert.Contains("AppMutex=" + m.Groups[1].Value, Read("installer", "SelectoAI.iss"));
    }

    [Fact]
    public void Iss_uses_the_same_startup_entry_and_data_folder_as_the_application()
    {
        var iss = Read("installer", "SelectoAI.iss");
        Assert.Contains("ValueName: \"" + StartupEntries.Name + "\"", iss);
        Assert.Contains("RunValueName = '" + StartupEntries.Name + "'", iss);
        Assert.Contains("AppDataFolder = '" + AppPaths.FolderName + "'", iss);
    }

    [Theory]
    [InlineData("license-fr.txt")]
    [InlineData("license-en.txt")]
    [InlineData("license-es.txt")]
    public void License_files_carry_the_mit_text(string file)
    {
        var path = Path.Combine(RepoRoot(), "installer", file);
        Assert.True(File.Exists(path), path);
        var text = File.ReadAllText(path).Replace("\r\n", "\n");
        Assert.Contains("Copyright (c) 2026 EL MAHDI ZINEDDINE", text);
        Assert.Contains("Permission is hereby granted, free of charge", text);
        Assert.DoesNotContain("VALIDAT", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("VALIDER", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Iss_and_scripts_have_one_site_edition_and_the_4_0_0_names()
    {
        var iss = Read("installer", "SelectoAI.iss");
        Assert.Contains("OutputBaseFilename=SelectoAI-Setup-{#Arch}-{#Version}", iss);
        Assert.Contains("#define Version \"4.0.0\"", iss);
        Assert.DoesNotContain("{#Edition}", iss);
        Assert.DoesNotContain("/DEdition", iss);
        Assert.DoesNotContain("Selecto AI Pro\"", iss.Replace("Selecto AI Pro.lnk\"", ""));
        foreach (var script in new[] { "build-installer.ps1", "build-setup.ps1", "build-editions.ps1", "build-msix.ps1", "build-portable.ps1" })
        {
            var text = Read("scripts", script);
            Assert.DoesNotContain("'Pro'", text);
            Assert.DoesNotContain("EDITION_PRO", text);
            Assert.DoesNotContain("-Edition Demo", text);
        }
        Assert.Contains("SelectoAI-Setup-$a-$version.exe", Read("scripts", "build-installer.ps1"));
        Assert.Contains("SelectoAI-$a-portable-$version.zip", Read("scripts", "build-portable.ps1"));
        Assert.Contains("SelectoAI-$Version.msixbundle", Read("scripts", "build-msix.ps1"));
    }

    [Fact]
    public void Custom_messages_exist_in_every_installer_language()
    {
        // Chaque clé [CustomMessages] doit exister en français, en anglais et en espagnol (pas d'arabe dans l'installateur).
        var iss = Read("installer", "SelectoAI.iss");
        var block = Section(iss, "[CustomMessages]", "[Tasks]");
        var keys = new[] { "french", "english", "spanish" }
            .ToDictionary(lang => lang, lang => Regex.Matches(block, "^" + lang + @"\.(\w+)=", RegexOptions.Multiline)
                .Select(m => m.Groups[1].Value).OrderBy(k => k, StringComparer.Ordinal).ToList());
        Assert.NotEmpty(keys["french"]);
        Assert.Equal(keys["french"], keys["english"]);
        Assert.Equal(keys["french"], keys["spanish"]);
    }

    [Fact]
    public void Build_script_references_the_expected_outputs()
    {
        var ps = Read("scripts", "build-installer.ps1");
        Assert.Contains("SelectoAI-Setup-$a-$version.exe", ps);
        Assert.Contains("SHA256SUMS.txt", ps);
        Assert.Contains("latest.json", ps);
        Assert.Contains("SELECTO_SIGN_PFX", ps);
        Assert.Contains("SELECTO_SIGN_THUMBPRINT", ps);
        Assert.Contains("JRSoftware.InnoSetup", ps);
        Assert.Contains("installer\\SelectoAI.iss", ps);
        // L'installation d'Inno Setup n'est autorisée que si -InstallInno est passé.
        Assert.Contains("if ($InstallInno)", ps);
    }

    [Fact]
    public void Build_script_publishes_download_urls_and_notes()
    {
        var ps = Read("scripts", "build-installer.ps1");
        Assert.Contains("DownloadBaseUrl", ps);
        Assert.Contains("https://selecto.span.observer/downloads/", ps);
        Assert.Contains("https://github.com/warezfr/selecto-ai/releases/download/v$version/", ps);   // entrée Site
        Assert.Contains("[string]$Notes", ps);
        Assert.Contains("notes = $notesValue", ps);
        Assert.DoesNotContain("A COMPLETER", ps);
    }

    [Fact]
    public void Installer_relaunches_the_app_after_a_silent_install()
    {
        var iss = Read("installer", "SelectoAI.iss");
        var start = iss.IndexOf("[Run]", StringComparison.Ordinal);
        var end = iss.IndexOf("[Code]", StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        var run = iss.Substring(start, end - start);
        Assert.Contains("Filename: \"{app}\\{#ExeName}\"", run);
        Assert.Contains("postinstall", run);
        Assert.DoesNotContain("skipifsilent", run);
    }

    /// <summary>Extrait le texte entre deux marqueurs (début inclus, fin exclue).</summary>
    private static string Section(string text, string start, string end)
    {
        var s = text.IndexOf(start, StringComparison.Ordinal);
        Assert.True(s >= 0, "début introuvable : " + start);
        var e = text.IndexOf(end, s + start.Length, StringComparison.Ordinal);
        Assert.True(e > s, "fin introuvable : " + end);
        return text.Substring(s, e - s);
    }

    [Fact]
    public void Installer_closes_the_running_app_before_inno_checks_the_mutex()
    {
        var iss = Read("installer", "SelectoAI.iss");
        var init = Section(iss, "function InitializeSetup: Boolean;", "function PrepareToInstall");
        // Silencieux : fermeture sans question ; interactif : question Oui/Non.
        Assert.Contains("WizardSilent", init);
        Assert.Contains("MsgBox(", init);
        Assert.Contains("CloseRunningApp", init);
        var close = Section(iss, "function CloseRunningApp: Boolean;", "function InitializeSetup: Boolean;");
        // Fermeture douce d'abord, puis forcée (/F) seulement si le mutex reste pris.
        Assert.Contains("taskkill", close);
        Assert.Contains("'/IM {#ExeName}'", close);
        Assert.Contains("'/F /IM {#ExeName}'", close);
        Assert.Contains("CheckForMutexes(MutexName)", close);
    }

    [Fact]
    public void Installer_removes_the_legacy_v2_uninstall_entry_only_for_the_same_folder()
    {
        var iss = Read("installer", "SelectoAI.iss");
        Assert.Contains("LegacyUninstallKeyPath = 'Software\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\SelectoAI';", iss);
        var step = Section(iss, "procedure CurStepChanged(CurStep: TSetupStep);", "function DirSizeBytes");
        Assert.Contains("ssPostInstall", step);
        Assert.Contains("RemoveLegacyEntry(HKCU)", step);
        Assert.Contains("IsAdminInstallMode", step);
        Assert.Contains("RemoveLegacyEntry(HKLM)", step);
        var legacy = Section(iss, "procedure RemoveLegacyEntry(Root: Integer);", "procedure CurStepChanged");
        Assert.Contains("'InstallLocation'", legacy);
        Assert.Contains("RemoveBackslashUnlessRoot", legacy);
        Assert.Contains("CompareText", legacy);
        Assert.Contains("RegDeleteKeyIncludingSubkeys(Root, LegacyUninstallKeyPath)", legacy);
        Assert.Contains("Uninstall.exe", legacy);
    }

    [Fact]
    public void Uninstaller_removes_empty_leftover_folders_without_deleting_the_app_folder_tree()
    {
        var iss = Read("installer", "SelectoAI.iss");
        var post = Section(iss, "if CurUninstallStep = usPostUninstall then", "end;");
        Assert.Contains("RemoveEmptyDirs(ExpandConstant('{app}'))", post);
        var fn = Section(iss, "function RemoveEmptyDirs(", "procedure CurUninstallStepChanged");
        Assert.Contains("RemoveDir(", fn);
        Assert.Contains("AttrReparsePoint", fn);
        // Jamais de suppression récursive du dossier d'installation.
        Assert.DoesNotContain("DelTree", fn);
        Assert.False(Regex.IsMatch(iss, @"DelTree\([^;]*\{app\}"), "DelTree ne doit pas viser {app}");
    }
}
