using System.Collections.Concurrent;
using System.Threading.Channels;
using Tec.Core.Abstractions;
using Tec.Core.Diagnostics;
using Tec.Core.Model;

namespace Tec.Core.Services;

/// <summary>
/// Orquestador: escucha las fuentes, toma una captura por cada interacción aceptada
/// por el filtro, compone la evidencia y la entrega a los destinos.
/// </summary>
/// <remarks>
/// Las capturas se toman en paralelo a la detección (respetando el retraso de cada
/// interacción) pero serializadas entre sí; la composición y escritura se hacen en un
/// único consumidor para no bloquear la captura.
/// </remarks>
public sealed class EvidenceCollector : IAsyncDisposable
{
    private const string LogCategory = "Recolector";

    private readonly IReadOnlyList<IInteractionSource> _sources;
    private readonly IInteractionFilter _filter;
    private readonly IInteractionSnapshotter _snapshotter;
    private readonly IEvidenceComposer _composer;
    private readonly IReadOnlyList<IEvidenceSink> _sinks;
    private readonly SemaphoreSlim _captureGate = new(1, 1);
    private readonly ConcurrentDictionary<int, Task> _pendingCaptures = new();
    private readonly Lock _stateLock = new();

    private Channel<(int Sequence, InteractionSnapshot Snapshot)>? _channel;
    private Task? _consumer;
    private EvidenceSession? _session;
    private int _sequence;
    private volatile bool _running;

    public EvidenceCollector(
        IEnumerable<IInteractionSource> sources,
        IInteractionFilter filter,
        IInteractionSnapshotter snapshotter,
        IEvidenceComposer composer,
        IEnumerable<IEvidenceSink> sinks)
    {
        _sources = [.. sources];
        _filter = filter;
        _snapshotter = snapshotter;
        _composer = composer;
        _sinks = [.. sinks];
    }

    public event EventHandler<Evidence>? EvidenceCollected;
    public event EventHandler<Exception>? ErrorOccurred;

    public bool IsRunning => _running;
    public EvidenceSession? Session => _session;

    public async Task StartAsync(EvidenceSession session, CancellationToken cancellationToken = default)
    {
        lock (_stateLock)
        {
            if (_running || _session is not null)
                throw new InvalidOperationException("La sesión ya fue iniciada.");
            _session = session;
        }

        foreach (var sink in _sinks)
            await sink.OnSessionStartedAsync(session, cancellationToken).ConfigureAwait(false);

        var channel = Channel.CreateUnbounded<(int, InteractionSnapshot)>(new UnboundedChannelOptions { SingleReader = true });
        _channel = channel;
        _consumer = Task.Run(() => ConsumeAsync(session, channel.Reader), CancellationToken.None);
        _running = true;

        foreach (var source in _sources)
        {
            source.InteractionDetected += OnInteractionDetected;
            try
            {
                source.Start();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"No se pudo iniciar la fuente '{source.Name}': {ex.Message}", ex);
            }
            Log.Info(LogCategory, $"Fuente iniciada: {source.Name}");
        }
    }

    public async Task StopAsync()
    {
        if (!_running) return;
        _running = false;
        Log.Info(LogCategory, $"Deteniendo: {_sequence} interacciones capturadas, {_pendingCaptures.Count} capturas pendientes.");

        foreach (var source in _sources)
        {
            source.InteractionDetected -= OnInteractionDetected;
            TryRun(source.Stop);
        }

        await Task.WhenAll(_pendingCaptures.Values).ConfigureAwait(false);
        _channel?.Writer.TryComplete();
        if (_consumer is not null) await _consumer.ConfigureAwait(false);

        foreach (var sink in _sinks)
        {
            try
            {
                await sink.OnSessionCompletedAsync(_session!, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                ReportError(ex, $"Falló el cierre del destino {sink.GetType().Name}.");
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _captureGate.Dispose();
    }

    private void OnInteractionDetected(object? sender, Interaction interaction)
    {
        // Puede ejecutarse dentro de un hook del sistema: trabajo mínimo y nada bloqueante.
        if (!_running || !_filter.ShouldCapture(interaction)) return;

        var sequence = Interlocked.Increment(ref _sequence);
        Log.Debug(LogCategory, $"#{sequence} {interaction.Kind} detectado (PID {interaction.ProcessId}, ventana 0x{interaction.WindowHandle:X}).");
        var writer = _channel!.Writer;
        var task = Task.Run(() => CaptureAsync(sequence, interaction, writer));
        _pendingCaptures[sequence] = task;
        task.ContinueWith(_ => _pendingCaptures.TryRemove(sequence, out var _), TaskScheduler.Default);
    }

    private async Task CaptureAsync(int sequence, Interaction interaction, ChannelWriter<(int, InteractionSnapshot)> writer)
    {
        try
        {
            if (interaction.CaptureDelay > TimeSpan.Zero)
                await Task.Delay(interaction.CaptureDelay).ConfigureAwait(false);

            await _captureGate.WaitAsync().ConfigureAwait(false);
            InteractionSnapshot snapshot;
            try
            {
                snapshot = _snapshotter.TakeSnapshot(interaction);
            }
            finally
            {
                _captureGate.Release();
            }

            writer.TryWrite((sequence, snapshot));
        }
        catch (Exception ex)
        {
            ReportError(ex, $"Falló la captura de la interacción #{sequence} ({interaction.Kind}).");
        }
    }

    private async Task ConsumeAsync(EvidenceSession session, ChannelReader<(int Sequence, InteractionSnapshot Snapshot)> reader)
    {
        await foreach (var (sequence, snapshot) in reader.ReadAllAsync().ConfigureAwait(false))
        {
            try
            {
                var evidence = _composer.Compose(sequence, snapshot);
                foreach (var sink in _sinks)
                    await sink.WriteAsync(session, evidence, CancellationToken.None).ConfigureAwait(false);
                EvidenceCollected?.Invoke(this, evidence);
                Log.Debug(LogCategory, $"#{sequence} evidencia guardada: {evidence.Caption.Title}");
            }
            catch (Exception ex)
            {
                ReportError(ex, $"Falló la composición o el guardado de la evidencia #{sequence}.");
            }
        }
    }

    private void TryRun(Action action)
    {
        try { action(); }
        catch (Exception ex) { ReportError(ex, "Falló la detención de una fuente."); }
    }

    private void ReportError(Exception ex, string context)
    {
        Log.Error(LogCategory, context, ex);
        try { ErrorOccurred?.Invoke(this, ex); }
        catch (Exception handlerError) { Log.Error(LogCategory, "Falló el manejador de ErrorOccurred.", handlerError); }
    }
}
