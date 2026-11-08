using System;
using System.IO;
using SelectBar.Setup.Logic;

namespace SelectBar.Setup.Install;

/// <summary>Journal %TEMP%\SelectoAI-Setup.log (ne lève jamais d'exception).</summary>
internal static class SetupLog
{
    private static readonly object Gate = new();

    public static string FilePath => System.IO.Path.Combine(System.IO.Path.GetTempPath(), InstallPaths.LogFileName);

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
                File.AppendAllText(FilePath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "  " + message + Environment.NewLine);
        }
        catch { }
    }
}
