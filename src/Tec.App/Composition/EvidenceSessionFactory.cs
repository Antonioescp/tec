using Tec.Core.Abstractions;
using Tec.Core.Model;
using Tec.Core.Reporting;
using Tec.Core.Services;
using Tec.Windows.Capture;
using Tec.Windows.Imaging;
using Tec.Windows.Sources;
using Tec.Windows.Uia;

namespace Tec.App.Composition;

public enum CaptureArea
{
    Window,
    Monitor,
    AllScreens,
}

public sealed record SessionSettings
{
    public required TargetProcessInfo Target { get; init; }
    public required string OutputRoot { get; init; }
    public CaptureArea Area { get; init; } = CaptureArea.Window;
    public bool CaptureClicks { get; init; } = true;
    public bool CaptureKeyboardCommands { get; init; } = true;
    public bool CaptureTextInput { get; init; } = true;
    public bool CaptureWindows { get; init; } = true;
    public bool CaptureContentChanges { get; init; } = true;
    public TimeSpan WindowSettleDelay { get; init; } = TimeSpan.FromMilliseconds(250);
}

/// <summary>Sesión en curso: el recolector y los recursos que le pertenecen.</summary>
public sealed class RunningSession(
    EvidenceSession session,
    EvidenceCollector collector,
    IEvidenceFileNamer fileNamer,
    IReadOnlyList<IDisposable> ownedResources) : IAsyncDisposable
{
    public EvidenceSession Session { get; } = session;
    public EvidenceCollector Collector { get; } = collector;

    public string GetImagePath(Evidence evidence) =>
        System.IO.Path.Combine(Session.OutputDirectory, fileNamer.GetImageFileName(evidence));

    public async ValueTask DisposeAsync()
    {
        await Collector.DisposeAsync();
        foreach (var resource in ownedResources) resource.Dispose();
    }
}

/// <summary>
/// Composition root: el único lugar que conoce las implementaciones concretas.
/// Para soportar otra tecnología (WinForms, web, ...) basta con registrar aquí
/// nuevas fuentes, enriquecedores o destinos.
/// </summary>
public static class EvidenceSessionFactory
{
    public static RunningSession Create(SessionSettings settings)
    {
        var clock = SystemClock.Instance;
        var owned = new List<IDisposable>();

        var sources = CreateSources(settings, clock);
        owned.AddRange(sources);

        var focusTracker = new FocusTracker(clock);
        focusTracker.Start();
        owned.Add(focusTracker);

        var filter = new ProcessInteractionFilter([settings.Target.ProcessId]);

        var snapshotter = new InteractionSnapshotter(
            CreateRegionProvider(settings.Area),
            new GdiScreenCapturer(),
            [new WindowInfoEnricher(), new UiaElementEnricher(focusTracker)],
            clock);

        var composer = new EvidenceComposer(
            new ContrastingHighlightColorSelector(),
            new SpanishCaptionFormatter(),
            new GdiFrameAnnotator());

        var encoder = new PngImageEncoder();
        var namer = new SequentialEvidenceFileNamer(encoder.FileExtension);
        IEvidenceSink[] sinks =
        [
            new ImageFileEvidenceSink(encoder, namer),
            new JsonLinesEvidenceLogSink(namer),
            new HtmlReportSink(namer, clock),
        ];

        var collector = new EvidenceCollector(sources, filter, snapshotter, composer, sinks);
        var session = EvidenceSession.Create(settings.OutputRoot, settings.Target.ProcessName, clock.Now);
        return new RunningSession(session, collector, namer, owned);
    }

    private static List<IInteractionSource> CreateSources(SessionSettings settings, IClock clock)
    {
        var sources = new List<IInteractionSource>();
        if (settings.CaptureClicks)
            sources.Add(new MouseClickSource(clock));
        if (settings.CaptureKeyboardCommands || settings.CaptureTextInput)
            sources.Add(new KeyboardSource(clock, new KeyboardCaptureOptions
            {
                CaptureCommands = settings.CaptureKeyboardCommands,
                CaptureTextInput = settings.CaptureTextInput,
            }));
        if (settings.CaptureWindows)
            sources.Add(new WindowShownSource(clock, new WindowCaptureOptions { SettleDelay = settings.WindowSettleDelay }));
        if (settings.CaptureContentChanges)
            sources.Add(new ContentChangeSource(
                clock,
                new TargetForegroundWindowProvider([settings.Target.ProcessId]),
                new PrintWindowContentCapturer()));
        return sources;
    }

    private static ICaptureRegionProvider CreateRegionProvider(CaptureArea area) => area switch
    {
        CaptureArea.Monitor => new MonitorCaptureRegionProvider(),
        CaptureArea.AllScreens => new VirtualScreenRegionProvider(),
        _ => new WindowCaptureRegionProvider(),
    };
}
