using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using SelectBar.Setup.Logic;

namespace SelectBar.Setup;

/// <summary>Version et architecture gravées dans l'exécutable par le script de build (AssemblyMetadata).</summary>
internal static class BuildInfo
{
    private static string? Meta(string key)
        => Assembly.GetExecutingAssembly().GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a => a.Key == key)?.Value;

    public static string Version => Meta("AppVersion") is { Length: > 0 } v ? v : "4.0.0";
    public static string Arch => Meta("Arch") is { Length: > 0 } a ? a : (RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "arm64" : "x64");
    public static string DisplayName => SelectBar.Setup.Logic.InstallPaths.DisplayName;

    private static OverlayInfo? _overlay;
    private static bool _overlayRead;

    /// <summary>Position de la charge utile ajoutée à la fin de ce Setup (null : exécutable sans charge utile, p. ex. Uninstall.exe ou développement).</summary>
    public static OverlayInfo? Overlay
    {
        get
        {
            if (!_overlayRead) { _overlay = OverlayTrailer.TryReadFile(Environment.ProcessPath); _overlayRead = true; }
            return _overlay;
        }
    }

    public static bool HasPayload => Overlay != null;

    /// <summary>Flux navigable sur la charge utile (zip) lue dans ce Setup ; à libérer par l'appelant.</summary>
    public static Stream? OpenPayload()
    {
        if (Overlay is not { } o) return null;
        var fs = new FileStream(Environment.ProcessPath!, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 16);
        return new OverlayTrailer.Slice(fs, o.PayloadOffset, o.PayloadLength);
    }
}
