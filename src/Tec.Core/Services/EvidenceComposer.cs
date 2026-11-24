using Tec.Core.Abstractions;
using Tec.Core.Model;

namespace Tec.Core.Services;

public sealed class EvidenceComposer(
    IHighlightColorSelector colorSelector,
    IInteractionCaptionFormatter captionFormatter,
    IFrameAnnotator annotator) : IEvidenceComposer
{
    public Evidence Compose(int sequence, InteractionSnapshot snapshot)
    {
        var interaction = snapshot.Interaction;
        var highlight = ResolveHighlight(interaction, snapshot.Frame);
        var color = colorSelector.Select(snapshot.Frame, highlight);
        var caption = captionFormatter.Format(sequence, interaction);
        var image = annotator.Annotate(new AnnotationRequest(snapshot.Frame, interaction, caption, highlight, color));

        return new Evidence(sequence, interaction, caption, highlight, color, image, snapshot.CapturedAt);
    }

    /// <summary>Priority: explicit region, UI element, whole capture.</summary>
    internal static ScreenRect ResolveHighlight(Interaction interaction, CapturedFrame frame)
    {
        if (interaction.HighlightBounds is { IsEmpty: false } explicitBounds)
            return explicitBounds;
        if (interaction.Element?.Bounds is { IsEmpty: false } elementBounds)
            return elementBounds;
        return frame.Bounds;
    }
}
