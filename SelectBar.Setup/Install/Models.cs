using SelectBar.Setup.Logic;

namespace SelectBar.Setup.Install;

internal sealed record ProgressInfo(InstallStep Step, double Percent, string Detail);

internal sealed record InstallRequest(string Dir, bool DesktopShortcut, bool Startup, bool Force, string Lang);

internal sealed record UninstallRequest(string Dir, bool PurgeData, string Lang);

internal sealed record OpResult(bool Success, bool Cancelled, string? Message, string? Details, bool WasRunning, string? RunningExe)
{
    public static OpResult Ok(bool wasRunning = false, string? exe = null) => new(true, false, null, null, wasRunning, exe);
    public static OpResult Fail(string message, string? details = null) => new(false, false, message, details ?? message, false, null);
    public static OpResult Cancel(string message) => new(false, true, message, null, false, null);
}
