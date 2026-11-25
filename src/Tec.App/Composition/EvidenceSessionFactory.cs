using Tec.Core.Abstractions;
using Tec.Core.Localization;
using Tec.Core.Model;
using Tec.Core.Reporting;
using Tec.Core.Services;
using Tec.Windows.Capture;
using Tec.Windows.Imaging;
using Tec.Windows.Processes;
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
    public required IReadOnlyList<TargetProcessInfo> Targets { get; init; }
    public required string OutputRoot { get; init; }

    /// <summary>Name the tester gave the session; empty means the one built from the applications.</summary>
    public string? SessionName { get; init; }

    public CaptureArea Area { get; init; } = CaptureArea.Window;
    public TecLanguage Language { get; init; } = TecLanguage.Spanish;
    public bool CaptureClicks { get; init; } = true;
    public bool CaptureKeyboardCommands { get; init; } = true;
    public bool CaptureTextInput { get; init; } = true;
    public bool CaptureWindows { get; init; } = true;
    public bool CaptureContentChanges { get; init; } = true;
    public TimeSpan WindowSettleDelay { get; init; } = TimeSpan.FromMilliseconds(250);
}

/// <summary>Running session: the collector and the resources it owns.</summary>
public sealed class RunningSession(
    EvidenceSession session,
    EvidenceCollector collector,
    IEvidenceFileNamer fileNamer,
    IReadOnlyList<IDisposable> ownedResources,
    TrackedTargets trackedTargets) : IAsyncDisposable
{
    public EvidenceSession Session { get; } = session;
    public EvidenceCollector Collector { get; } = collector;

    /// <summary>The applications this session follows, and which of them have stopped.</summary>
    public TrackedTargets Targets { get; } = trackedTargets;

    public string GetImagePath(Evidence evidence) =>
        System.IO.Path.Combine(Session.OutputDirectory, fileNamer.GetImageFileName(evidence));

    public async ValueTask DisposeAsync()
    {
        await Collector.DisposeAsync();
        foreach (var resource in ownedResources) resource.Dispose();
    }
}

/// <summary>
/// Composition root: the only place that knows the concrete implementations.
/// To support another technology (WinForms, web, ...) just register new
/// sources, enrichers or sinks here.
/// </summary>
public static class EvidenceSessionFactory
{
    public static RunningSession Create(SessionSettings settings)
    {
        var clock = SystemClock.Instance;
        var owned = new List<IDisposable>();

        var targets = new TrackedTargets(settings.Targets);
        var watcher = new TrackedProcessWatcher(targets);
        owned.Add(watcher);

        var sources = CreateSources(settings, [.. targets.ProcessIds], clock);
        owned.AddRange(sources);

        var focusTracker = new FocusTracker(clock);
        focusTracker.Start();
        owned.Add(focusTracker);

        var filter = new ProcessInteractionFilter(targets.ProcessIds);

        var snapshotter = new InteractionSnapshotter(
            CreateRegionProvider(settings.Area),
            new GdiScreenCapturer(),
            [new WindowInfoEnricher(), new UiaElementEnricher(focusTracker)],
            clock);

        var composer = new EvidenceComposer(
            new ContrastingHighlightColorSelector(),
            new CaptionFormatter(settings.Language),
            new GdiFrameAnnotator());

        var encoder = new PngImageEncoder();
        var namer = new SequentialEvidenceFileNamer(encoder.FileExtension);
        IEvidenceSink[] sinks =
        [
            new ImageFileEvidenceSink(encoder, namer),
            new JsonLinesEvidenceLogSink(namer),
            new HtmlReportSink(namer, clock, settings.Language, () => targets.LostNames),
        ];

        var collector = new EvidenceCollector(sources, filter, snapshotter, composer, sinks);
        var session = EvidenceSession.Create(settings.OutputRoot, [.. settings.Targets.Select(t => t.ProcessName)], settings.SessionName, clock.Now);

        // The folder is taken here, so a second session asking for the same name takes the next one.
        System.IO.Directory.CreateDirectory(session.OutputDirectory);

        var running = new RunningSession(session, collector, namer, owned, targets);

        // Watching starts once everything is in place, so a lost application is never missed silently.
        watcher.Start();
        return running;
    }

    private static List<IInteractionSource> CreateSources(SessionSettings settings, IReadOnlyList<int> processIds, IClock clock)
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
                new TargetForegroundWindowProvider(processIds),
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
