using Tec.Core.Abstractions;
using Tec.Core.Diagnostics;
using Tec.Core.Model;

namespace Tec.Core.Services;

/// <summary>
/// Captures the screen first (to reflect the state at the moment of the interaction)
/// and then completes the interaction with the enrichers.
/// </summary>
public sealed class InteractionSnapshotter(
    ICaptureRegionProvider regionProvider,
    IScreenCapturer capturer,
    IEnumerable<IInteractionEnricher> enrichers,
    IClock clock) : IInteractionSnapshotter
{
    private readonly IReadOnlyList<IInteractionEnricher> _enrichers = [.. enrichers];

    public InteractionSnapshot TakeSnapshot(Interaction interaction)
    {
        var region = regionProvider.GetRegion(interaction);
        var frame = capturer.Capture(region);
        var capturedAt = clock.Now;

        foreach (var enricher in _enrichers)
        {
            try
            {
                interaction = enricher.Enrich(interaction);
            }
            catch (Exception ex)
            {
                Log.Warning("Captura", $"El enriquecedor {enricher.GetType().Name} falló para {interaction.Kind}.", ex);
                interaction = interaction.WithNote($"No se pudo obtener información adicional ({enricher.GetType().Name}: {ex.Message})");
            }
        }

        return new InteractionSnapshot(interaction, frame, capturedAt);
    }
}
