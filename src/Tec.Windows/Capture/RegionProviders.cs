using Tec.Core.Abstractions;
using Tec.Core.Model;
using Tec.Windows.Native;

namespace Tec.Windows.Capture;

/// <summary>
/// Captures the interaction's window together with its owner window (e.g. the "please
/// wait" dialog and the main window behind it), with a small margin.
/// </summary>
public sealed class WindowCaptureRegionProvider(int margin = 8) : ICaptureRegionProvider
{
    public ScreenRect GetRegion(Interaction interaction)
    {
        var virtualScreen = Win32Window.GetVirtualScreen();
        var root = ResolveWindow(interaction);

        var region = Win32Window.GetBounds(root);
        var owner = Win32Window.GetOwner(root);
        if (Win32Window.IsVisible(owner))
            region = region.Union(Win32Window.GetBounds(owner));

        if (interaction.Point is { } p && !region.IsEmpty && !region.Contains(p))
            region = region.Union(new ScreenRect(p.X - 20, p.Y - 20, 40, 40));

        region = region.Inflate(margin).Intersect(virtualScreen);
        return region.IsEmpty ? virtualScreen : region;
    }

    internal static nint ResolveWindow(Interaction interaction)
    {
        if (interaction.WindowHandle != 0) return Win32Window.GetRoot(interaction.WindowHandle);
        if (interaction.Point is { } p) return Win32Window.GetRoot(Win32Window.FromPoint(p));
        return 0;
    }
}

/// <summary>Captures the whole monitor where the interaction happened.</summary>
public sealed class MonitorCaptureRegionProvider : ICaptureRegionProvider
{
    public ScreenRect GetRegion(Interaction interaction) =>
        Win32Window.GetMonitorBounds(WindowCaptureRegionProvider.ResolveWindow(interaction), interaction.Point);
}

/// <summary>Captures the whole virtual desktop (all monitors).</summary>
public sealed class VirtualScreenRegionProvider : ICaptureRegionProvider
{
    public ScreenRect GetRegion(Interaction interaction) => Win32Window.GetVirtualScreen();
}
