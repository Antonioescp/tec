using Tec.Core.Abstractions;
using Tec.Core.Model;
using Tec.Windows.Native;

namespace Tec.Windows.Capture;

/// <summary>
/// Fills in the window title and, for shown windows, uses their current bounds
/// (once painted) as the region to highlight.
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
                ? interaction.WithNote("La ventana se cerró antes de la captura")
                : interaction;
        }

        if (string.IsNullOrWhiteSpace(interaction.WindowTitle))
            interaction = interaction with { WindowTitle = Win32Window.GetTitle(hwnd) };

        if (interaction.Kind == InteractionKind.WindowOpened && interaction.HighlightBounds is null)
            interaction = interaction with { HighlightBounds = Win32Window.GetBounds(hwnd) };

        return interaction;
    }
}
