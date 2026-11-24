using System.Collections.Concurrent;
using Tec.Core.Abstractions;
using Tec.Core.Localization;
using Tec.Core.Model;
using Tec.Core.Services;

namespace Tec.Core.Tests;

public class EvidenceCollectorTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 10, 0, 0, TimeSpan.Zero);
    private static readonly EvidenceSession Session = new("s1", "app", Now, "unused");

    [Fact]
    public async Task Collects_only_interactions_accepted_by_the_filter()
    {
        var source = new FakeSource();
        var sink = new RecordingSink();
        var collector = CreateCollector(source, sink, new ProcessInteractionFilter([42]));

        await collector.StartAsync(Session);
        source.Emit(Click(processId: 42));
        source.Emit(Click(processId: 7));
        source.Emit(Click(processId: 42));
        await collector.StopAsync();

        Assert.Equal([1, 2], sink.Evidences.Select(e => e.Sequence).Order());
        Assert.True(sink.Started);
        Assert.True(sink.Completed);
    }

    [Fact]
    public async Task Carries_the_application_name_and_works_without_it()
    {
        var source = new FakeSource();
        var sink = new RecordingSink();
        var collector = CreateCollector(source, sink, new ProcessInteractionFilter([42, 7]));

        await collector.StartAsync(Session);
        source.Emit(Click(processId: 42) with { ProcessName = "Postman" });
        source.Emit(Click(processId: 7));
        await collector.StopAsync();

        var names = sink.Evidences.Select(e => e.Interaction.ProcessName).ToList();
        Assert.Equal(2, names.Count);
        Assert.Contains("Postman", names);
        Assert.Contains(null, names);
    }

    [Fact]
    public async Task Highlights_the_element_and_uses_snapshot_enrichment()
    {
        var source = new FakeSource();
        var sink = new RecordingSink();
        var element = new UiElementInfo("Procesar", "botón", null, new ScreenRect(10, 10, 30, 10));
        var collector = CreateCollector(source, sink, new ProcessInteractionFilter([1]),
            enricher: new DelegateEnricher(i => i with { Element = element }));

        await collector.StartAsync(Session);
        source.Emit(Click(processId: 1));
        await collector.StopAsync();

        var evidence = Assert.Single(sink.Evidences);
        Assert.Equal(element.Bounds, evidence.HighlightBounds);
        Assert.Equal(RgbColor.Red, evidence.HighlightColor);
        Assert.Contains("botón \"Procesar\"", evidence.Caption.Title);
    }

    [Fact]
    public async Task Waits_for_the_capture_delay_before_stopping()
    {
        var source = new FakeSource();
        var sink = new RecordingSink();
        var collector = CreateCollector(source, sink, new ProcessInteractionFilter([1]));

        await collector.StartAsync(Session);
        source.Emit(Click(processId: 1) with { Kind = InteractionKind.WindowOpened, CaptureDelay = TimeSpan.FromMilliseconds(150) });
        await collector.StopAsync();

        Assert.Single(sink.Evidences);
    }

    [Fact]
    public async Task Errors_are_reported_and_do_not_stop_the_session()
    {
        var source = new FakeSource();
        var sink = new RecordingSink();
        var capturer = new FakeCapturer { FailNext = true };
        var collector = CreateCollector(source, sink, new ProcessInteractionFilter([1]), capturer: capturer);
        var errors = new ConcurrentBag<Exception>();
        collector.ErrorOccurred += (_, ex) => errors.Add(ex);

        await collector.StartAsync(Session);
        source.Emit(Click(processId: 1));
        await Task.Delay(100);
        source.Emit(Click(processId: 1));
        await collector.StopAsync();

        Assert.Single(errors);
        Assert.Single(sink.Evidences);
        Assert.Equal(2, sink.Evidences.Single().Sequence);
    }

    [Fact]
    public async Task Ignores_interactions_after_stop()
    {
        var source = new FakeSource();
        var sink = new RecordingSink();
        var collector = CreateCollector(source, sink, new ProcessInteractionFilter([1]));

        await collector.StartAsync(Session);
        await collector.StopAsync();
        source.Emit(Click(processId: 1));

        Assert.Empty(sink.Evidences);
        Assert.False(source.Running);
    }

    private static Interaction Click(int processId) => new()
    {
        Kind = InteractionKind.LeftClick,
        Timestamp = Now,
        Point = new ScreenPoint(20, 15),
        ProcessId = processId,
    };

    private static EvidenceCollector CreateCollector(
        FakeSource source,
        RecordingSink sink,
        IInteractionFilter filter,
        IInteractionEnricher? enricher = null,
        FakeCapturer? capturer = null)
    {
        var snapshotter = new InteractionSnapshotter(
            new FixedRegionProvider(new ScreenRect(0, 0, 100, 50)),
            capturer ?? new FakeCapturer(),
            enricher is null ? [] : [enricher],
            new FixedClock(Now));
        var composer = new EvidenceComposer(
            new ContrastingHighlightColorSelector(),
            new CaptionFormatter(TecLanguage.Spanish),
            new PassThroughAnnotator());
        return new EvidenceCollector([source], filter, snapshotter, composer, [sink]);
    }

    private sealed class FakeSource : IInteractionSource
    {
        public string Name => "fake";
        public bool Running { get; private set; }
        public event EventHandler<Interaction>? InteractionDetected;
        public void Start() => Running = true;
        public void Stop() => Running = false;
        public void Dispose() => Stop();
        public void Emit(Interaction interaction) => InteractionDetected?.Invoke(this, interaction);
    }

    private sealed class FakeCapturer : IScreenCapturer
    {
        public bool FailNext { get; set; }

        public CapturedFrame Capture(ScreenRect region)
        {
            if (FailNext)
            {
                FailNext = false;
                throw new InvalidOperationException("captura fallida");
            }
            return new CapturedFrame(region, RasterImage.Solid(region.Width, region.Height, RgbColor.White));
        }
    }

    private sealed class FixedRegionProvider(ScreenRect region) : ICaptureRegionProvider
    {
        public ScreenRect GetRegion(Interaction interaction) => region;
    }

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now => now;
    }

    private sealed class DelegateEnricher(Func<Interaction, Interaction> enrich) : IInteractionEnricher
    {
        public Interaction Enrich(Interaction interaction) => enrich(interaction);
    }

    private sealed class PassThroughAnnotator : IFrameAnnotator
    {
        public RasterImage Annotate(AnnotationRequest request) => request.Frame.Image;
    }

    private sealed class RecordingSink : IEvidenceSink
    {
        private readonly ConcurrentQueue<Evidence> _evidences = new();
        public IReadOnlyList<Evidence> Evidences => [.. _evidences];
        public bool Started { get; private set; }
        public bool Completed { get; private set; }

        public Task OnSessionStartedAsync(EvidenceSession session, CancellationToken cancellationToken)
        {
            Started = true;
            return Task.CompletedTask;
        }

        public Task WriteAsync(EvidenceSession session, Evidence evidence, CancellationToken cancellationToken)
        {
            _evidences.Enqueue(evidence);
            return Task.CompletedTask;
        }

        public Task OnSessionCompletedAsync(EvidenceSession session, CancellationToken cancellationToken)
        {
            Completed = true;
            return Task.CompletedTask;
        }
    }
}
