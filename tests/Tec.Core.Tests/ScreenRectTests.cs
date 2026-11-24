using Tec.Core.Model;

namespace Tec.Core.Tests;

public class ScreenRectTests
{
    [Fact]
    public void Intersect_returns_overlap() =>
        Assert.Equal(new ScreenRect(5, 5, 5, 5), new ScreenRect(0, 0, 10, 10).Intersect(new ScreenRect(5, 5, 10, 10)));

    [Fact]
    public void Intersect_without_overlap_is_empty() =>
        Assert.True(new ScreenRect(0, 0, 10, 10).Intersect(new ScreenRect(20, 20, 5, 5)).IsEmpty);

    [Fact]
    public void Union_covers_both() =>
        Assert.Equal(new ScreenRect(-5, 0, 20, 30), new ScreenRect(0, 0, 10, 10).Union(new ScreenRect(-5, 10, 20, 20)));

    [Fact]
    public void Union_with_empty_returns_other() =>
        Assert.Equal(new ScreenRect(1, 2, 3, 4), ScreenRect.Empty.Union(new ScreenRect(1, 2, 3, 4)));
}
