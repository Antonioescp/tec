using Tec.Core.Model;

namespace Tec.Core.Abstractions;

/// <summary>Origen de interacciones (mouse, teclado, eventos de la aplicación, ...).</summary>
public interface IInteractionSource : IDisposable
{
    string Name { get; }

    /// <summary>
    /// Se dispara cuando se detecta una interacción. Los manejadores deben ser rápidos:
    /// pueden ejecutarse dentro de un hook del sistema.
    /// </summary>
    event EventHandler<Interaction>? InteractionDetected;

    void Start();
    void Stop();
}

/// <summary>Decide si una interacción debe convertirse en evidencia.</summary>
public interface IInteractionFilter
{
    bool ShouldCapture(Interaction interaction);
}

/// <summary>Completa una interacción con información adicional (elemento de UI, ventana, ...).</summary>
public interface IInteractionEnricher
{
    Interaction Enrich(Interaction interaction);
}

/// <summary>Determina qué región de la pantalla se captura para una interacción.</summary>
public interface ICaptureRegionProvider
{
    ScreenRect GetRegion(Interaction interaction);
}

/// <summary>Captura una región de la pantalla.</summary>
public interface IScreenCapturer
{
    CapturedFrame Capture(ScreenRect region);
}

/// <summary>Toma la captura de una interacción (región + captura + enriquecimiento).</summary>
public interface IInteractionSnapshotter
{
    InteractionSnapshot TakeSnapshot(Interaction interaction);
}

/// <summary>Elige un color de resaltado que contraste con el fondo.</summary>
public interface IHighlightColorSelector
{
    RgbColor Select(CapturedFrame frame, ScreenRect highlight);
}

/// <summary>Genera el texto que describe una interacción.</summary>
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

/// <summary>Dibuja el recuadro y la leyenda sobre la captura.</summary>
public interface IFrameAnnotator
{
    RasterImage Annotate(AnnotationRequest request);
}

/// <summary>Convierte una captura en una evidencia final.</summary>
public interface IEvidenceComposer
{
    Evidence Compose(int sequence, InteractionSnapshot snapshot);
}

/// <summary>Codifica imágenes a un formato de archivo.</summary>
public interface IImageEncoder
{
    string FileExtension { get; }
    void Encode(RasterImage image, Stream output);
}

/// <summary>Nombres de archivo de las evidencias, compartidos por todos los destinos.</summary>
public interface IEvidenceFileNamer
{
    string GetImageFileName(Evidence evidence);
}

/// <summary>Destino de las evidencias (archivos, reporte, bitácora, ...).</summary>
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
