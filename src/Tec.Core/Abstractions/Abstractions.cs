using Tec.Core.Model;

namespace Tec.Core.Abstractions;

/// <summary>Source of interactions (mouse, keyboard, application events, ...).</summary>
public interface IInteractionSource : IDisposable
{
    string Name { get; }

    /// <summary>
    /// Raised when an interaction is detected. Handlers must be fast:
    /// they may run inside a system hook.
    /// </summary>
    event EventHandler<Interaction>? InteractionDetected;

    void Start();
    void Stop();
}

/// <summary>Decides whether an interaction becomes evidence.</summary>
public interface IInteractionFilter
{
    bool ShouldCapture(Interaction interaction);
}

/// <summary>Adds information to an interaction (UI element, window, ...).</summary>
public interface IInteractionEnricher
{
    Interaction Enrich(Interaction interaction);
}

/// <summary>Determines which screen region is captured for an interaction.</summary>
public interface ICaptureRegionProvider
{
    ScreenRect GetRegion(Interaction interaction);
}

/// <summary>Captures a screen region.</summary>
public interface IScreenCapturer
{
    CapturedFrame Capture(ScreenRect region);
}

/// <summary>Takes the snapshot of an interaction (region + capture + enrichment).</summary>
public interface IInteractionSnapshotter
{
    InteractionSnapshot TakeSnapshot(Interaction interaction);
}

/// <summary>Picks a highlight color that contrasts with the background.</summary>
public interface IHighlightColorSelector
{
    RgbColor Select(CapturedFrame frame, ScreenRect highlight);
}

/// <summary>Produces the text that describes an interaction.</summary>
public interface IInteractionCaptionFormatter
{
    InteractionCaption Format(int sequence, Interaction interaction);
}

public sealed record AnnotationRequest(
    CapturedFrame Frame,
    Interaction Interaction,
    InteractionCaption Caption,
    ScreenRect Highlight,
    RgbColor Color);

/// <summary>Draws the highlight box and caption on the capture.</summary>
public interface IFrameAnnotator
{
    RasterImage Annotate(AnnotationRequest request);
}

/// <summary>Turns a capture into final evidence.</summary>
public interface IEvidenceComposer
{
    Evidence Compose(int sequence, InteractionSnapshot snapshot);
}

/// <summary>Encodes images to a file format.</summary>
public interface IImageEncoder
{
    string FileExtension { get; }
    void Encode(RasterImage image, Stream output);
}

/// <summary>Evidence file names, shared by all sinks.</summary>
public interface IEvidenceFileNamer
{
    string GetImageFileName(Evidence evidence);
}

/// <summary>Evidence destination (files, report, log, ...).</summary>
public interface IEvidenceSink
{
    Task OnSessionStartedAsync(EvidenceSession session, CancellationToken cancellationToken);
    Task WriteAsync(EvidenceSession session, Evidence evidence, CancellationToken cancellationToken);
    Task OnSessionCompletedAsync(EvidenceSession session, CancellationToken cancellationToken);
}

public interface IClock
{
    DateTimeOffset Now { get; }
}
