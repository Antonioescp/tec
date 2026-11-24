using System.Windows.Automation;
using Tec.Core.Abstractions;
using Tec.Core.Model;

namespace Tec.Windows.Uia;

/// <summary>
/// Identifies the interaction's UI element with UI Automation:
/// the one under the pointer (clicks) or the one that had focus (keyboard).
/// </summary>
/// <remarks>
/// UI Automation queries the target process; if its UI thread is busy the call can
/// block, so a timeout is applied and processing continues without the element.
/// </remarks>
public sealed class UiaElementEnricher(FocusTracker? focusTracker = null, TimeSpan? timeout = null) : IInteractionEnricher
{
    private readonly TimeSpan _timeout = timeout ?? TimeSpan.FromMilliseconds(1500);

    public Interaction Enrich(Interaction interaction)
    {
        if (interaction.Element is not null) return interaction;
        if (interaction.Kind.Category is not (InteractionCategory.Pointer or InteractionCategory.Keyboard)) return interaction;

        var task = Task.Run(() => Resolve(interaction));
        if (!task.Wait(_timeout))
            return interaction.WithNote(InteractionNote.ElementTimeout);

        return task.Result is { } element ? interaction with { Element = element } : interaction;
    }

    private UiElementInfo? Resolve(Interaction interaction)
    {
        try
        {
            if (interaction.Kind.Category == InteractionCategory.Pointer && interaction.Point is { } p)
            {
                var hit = AutomationElement.FromPoint(new System.Windows.Point(p.X, p.Y));
                return hit is null ? null : UiaElements.ToInfo(UiaElements.PromoteToInteractive(hit));
            }

            var focused = focusTracker?.GetFocusedAt(interaction.Timestamp) ?? AutomationElement.FocusedElement;
            if (focused is null) return null;
            if (interaction.ProcessId is { } pid && UiaElements.GetProcessId(focused) != pid) return null;
            return UiaElements.ToInfo(focused);
        }
        catch (ElementNotAvailableException)
        {
            return null;
        }
    }
}
