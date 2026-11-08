using System;

namespace SelectBar.UI;

/// <summary>Côté de l'écran occupé par la barre des tâches.</summary>
public enum TaskbarEdge { Bottom, Top, Left, Right }

/// <summary>Placement PUR du flyout de la zone de notification (pixels écran ; aucune dépendance WPF).</summary>
public static class FlyoutPlacement
{
    /// <summary>
    /// Côté de la barre des tâches, déduit de la différence entre les limites de l'écran et sa zone de travail.
    /// La plus grande réduction l'emporte ; barre masquée automatiquement (aucune différence) : le bas, usage le plus courant.
    /// </summary>
    public static TaskbarEdge DetectEdge(int screenLeft, int screenTop, int screenRight, int screenBottom,
        int workLeft, int workTop, int workRight, int workBottom)
    {
        var left = workLeft - screenLeft;
        var top = workTop - screenTop;
        var right = screenRight - workRight;
        var bottom = screenBottom - workBottom;
        var max = Math.Max(Math.Max(left, top), Math.Max(right, bottom));
        if (max <= 0) return TaskbarEdge.Bottom;
        if (bottom == max) return TaskbarEdge.Bottom;
        if (top == max) return TaskbarEdge.Top;
        if (left == max) return TaskbarEdge.Left;
        return TaskbarEdge.Right;
    }

    /// <summary>
    /// Coin haut-gauche du flyout : collé au bord de la barre des tâches (marge <paramref name="gap"/>), centré sur l'ancre
    /// (curseur ou icône) dans l'axe de la barre, puis ramené entièrement dans la zone de travail.
    /// </summary>
    public static (int X, int Y) Place(TaskbarEdge edge, int anchorX, int anchorY, int width, int height,
        int workLeft, int workTop, int workRight, int workBottom, int gap = 12)
    {
        int x, y;
        switch (edge)
        {
            case TaskbarEdge.Top:
                x = anchorX - width / 2; y = workTop + gap; break;
            case TaskbarEdge.Left:
                x = workLeft + gap; y = anchorY - height / 2; break;
            case TaskbarEdge.Right:
                x = workRight - width - gap; y = anchorY - height / 2; break;
            default:
                x = anchorX - width / 2; y = workBottom - height - gap; break;
        }
        x = Math.Max(workLeft, Math.Min(x, workRight - width));
        y = Math.Max(workTop, Math.Min(y, workBottom - height));
        return (x, y);
    }
}
