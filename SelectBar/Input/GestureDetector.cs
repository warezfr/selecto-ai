using System;

namespace SelectBar.Input;

public enum GestureKind { None, Drag, DoubleClick, TripleClick }

/// <summary>Détecte glisser (&gt; 40 px), double et triple clic à partir d'événements bouton gauche.</summary>
public sealed class GestureDetector
{
    public double DragThreshold { get; set; } = 40;
    public int MultiClickMs { get; set; } = 500;
    public double MultiClickRadius { get; set; } = 4;

    private int _clickCount;
    private long _lastDownMs = long.MinValue / 2;
    private int _lastX, _lastY, _downX, _downY;

    public void OnMouseDown(int x, int y, long timeMs)
    {
        if (_clickCount > 0 && timeMs - _lastDownMs <= MultiClickMs && Dist(x, y, _lastX, _lastY) <= MultiClickRadius)
            _clickCount++;
        else
            _clickCount = 1;
        _lastDownMs = timeMs; _lastX = x; _lastY = y; _downX = x; _downY = y;
    }

    public GestureKind OnMouseUp(int x, int y)
    {
        if (Dist(x, y, _downX, _downY) > DragThreshold) { _clickCount = 0; return GestureKind.Drag; }
        if (_clickCount == 2) return GestureKind.DoubleClick;
        if (_clickCount >= 3) return GestureKind.TripleClick;
        return GestureKind.None;
    }

    public void Reset() => _clickCount = 0;

    private static double Dist(int x1, int y1, int x2, int y2)
    {
        double dx = x1 - x2, dy = y1 - y2;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
