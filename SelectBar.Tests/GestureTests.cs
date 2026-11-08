using SelectBar.Input;
using Xunit;

namespace SelectBar.Tests;

public class GestureTests
{
    [Fact]
    public void Drag_over_40px()
    {
        var g = new GestureDetector();
        g.OnMouseDown(0, 0, 0);
        Assert.Equal(GestureKind.Drag, g.OnMouseUp(41, 0));
    }

    [Fact]
    public void Short_move_is_not_a_drag()
    {
        var g = new GestureDetector();
        g.OnMouseDown(0, 0, 0);
        Assert.Equal(GestureKind.None, g.OnMouseUp(40, 0));
    }

    [Fact]
    public void Double_and_triple_click()
    {
        var g = new GestureDetector();
        g.OnMouseDown(10, 10, 1000); Assert.Equal(GestureKind.None, g.OnMouseUp(10, 10));
        g.OnMouseDown(11, 10, 1200); Assert.Equal(GestureKind.DoubleClick, g.OnMouseUp(11, 10));
        g.OnMouseDown(11, 11, 1400); Assert.Equal(GestureKind.TripleClick, g.OnMouseUp(11, 11));
    }

    [Fact]
    public void Slow_or_distant_clicks_do_not_chain()
    {
        var g = new GestureDetector();
        g.OnMouseDown(10, 10, 1000); g.OnMouseUp(10, 10);
        g.OnMouseDown(10, 10, 2000); Assert.Equal(GestureKind.None, g.OnMouseUp(10, 10));
        g.OnMouseDown(100, 100, 2100); Assert.Equal(GestureKind.None, g.OnMouseUp(100, 100));
    }
}
