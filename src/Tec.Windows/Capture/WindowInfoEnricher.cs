using System.Diagnostics;
using Tec.Core.Abstractions;
using Tec.Core.Model;
using Tec.Windows.Native;

namespace Tec.Windows.Capture;

/// <summary>
/// Fills in the window title, the application owning the window and, for shown windows,
/// uses their current bounds (once painted) as the region to highlight.
/// </summary>
public sealed class WindowInfoEnricher : IInteractionEnricher
{
    public Interaction Enrich(Interaction interaction)
    {
        var hwnd = interaction.WindowHandle;
        if (hwnd == 0) return interaction;

        if (!Win32Window.Exists(hwnd))
        {
            return interaction.Kind == InteractionKind.WindowOpened
                ? interaction.WithNote(InteractionNote.WindowClosedBeforeCapture)
                : interaction;
        }

        if (string.IsNullOrWhiteSpace(interaction.WindowTitle))
            interaction = interaction with { WindowTitle = Win32Window.GetTitle(hwnd) };

        if (string.IsNullOrWhiteSpace(interaction.ProcessName) && Win32Window.GetProcessId(hwnd) is { } processId)
            interaction = interaction with { ProcessName = GetProcessName(processId) };

        if (interaction.Kind == InteractionKind.WindowOpened && interaction.HighlightBounds is null)
            interaction = interaction with { HighlightBounds = Win32Window.GetBounds(hwnd) };

        return interaction;
    }

    /// <summary>Application name, or null when the process is gone or its name is unavailable.</summary>
    private static string? GetProcessName(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }
}
