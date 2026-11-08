namespace SelectBar.Input;

/// <summary>
/// Garde anti-clic accidentel : la barre et le popup ignorent tout clic pendant 400 ms après leur affichage
/// (le 3e clic d'un triple-clic ne doit jamais tomber sur un bouton fraîchement apparu).
/// </summary>
public sealed class ClickGuard
{
    public const int DelayMs = 400;
    private long _shownAt;
    private bool _armed;

    /// <summary>À appeler quand la fenêtre vient d'être affichée (<paramref name="nowMs"/> = Environment.TickCount64).</summary>
    public void Arm(long nowMs) { _shownAt = nowMs; _armed = true; }

    public void Reset() => _armed = false;

    public bool IsAccidental(long nowMs) => _armed && Ignore(_shownAt, nowMs);

    /// <summary>Un clic à <paramref name="nowMs"/> sur une fenêtre affichée à <paramref name="shownAtMs"/> est-il à ignorer ?</summary>
    public static bool Ignore(long shownAtMs, long nowMs, int delayMs = DelayMs)
    {
        var elapsed = nowMs - shownAtMs;
        return elapsed >= 0 && elapsed < delayMs;
    }
}
