using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Input;
using Tec.App.Composition;
using Tec.App.Localization;
using Tec.App.Mvvm;
using Tec.App.Services;
using Tec.Core.Diagnostics;
using Tec.Core.Localization;
using Tec.Core.Model;
using Tec.Windows.Processes;

namespace Tec.App.ViewModels;

public sealed record EvidenceItem(int Sequence, string Title, string Details, string ImagePath);

public sealed record CaptureAreaOption(CaptureArea Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>A process the tester can mark to be followed by the session.</summary>
public sealed class ProcessOption(TargetProcessInfo process, Action onChanged) : ObservableObject
{
    private bool _isSelected;

    public TargetProcessInfo Process { get; } = process;
    public string Name { get; } = process.ToString();

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value)) onChanged();
        }
    }

    public override string ToString() => Name;
}

public sealed class MainViewModel : ObservableObject
{
    private const string LogCategory = "UI";

    private readonly ITargetProcessCatalog _catalog;
    private readonly IUserDialogs _dialogs;
    private readonly string _logDirectory;
    private readonly IUserSettingsStore _settingsStore;
    private readonly Localizer _localizer = Localizer.Instance;

    private RunningSession? _session;
    private IReadOnlyList<CaptureAreaOption> _captureAreas = [];
    private CaptureAreaOption _selectedArea = null!;
    private string _outputRoot;
    private string _sessionName = string.Empty;
    private string _sessionNamePreview = string.Empty;
    private Func<string> _status;
    private bool _captureClicks = true;
    private bool _captureKeyboardCommands = true;
    private bool _captureTextInput = true;
    private bool _captureWindows = true;
    private bool _captureContentChanges = true;
    private bool _minimizeOnStart = true;
    private int _windowSettleDelayMs = 250;
    private EvidenceItem? _selectedEvidence;

    public MainViewModel(ITargetProcessCatalog catalog, IUserDialogs dialogs, IUserSettingsStore settingsStore, string logDirectory)
    {
        _catalog = catalog;
        _dialogs = dialogs;
        _settingsStore = settingsStore;
        _logDirectory = logDirectory;
        _outputRoot = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TEC", "Evidencias");
        _status = () => _localizer["StatusReady"];

        _localizer.Language = TecLanguage.Find(settingsStore.Load().Language) ?? TecLanguage.FromCulture(CultureInfo.CurrentUICulture);
        BuildCaptureAreas(CaptureArea.Window);

        RefreshProcessesCommand = new RelayCommand(RefreshProcesses, () => !IsRunning);
        LaunchCommand = new RelayCommand(Launch, () => !IsRunning);
        BrowseOutputCommand = new RelayCommand(BrowseOutput, () => !IsRunning);
        StartCommand = new AsyncRelayCommand(StartAsync, () => !IsRunning && HasSelectedProcesses);
        StopCommand = new AsyncRelayCommand(StopAsync, () => IsRunning);
        OpenReportCommand = new RelayCommand(OpenReport, () => CurrentSessionDirectory is not null);
        OpenFolderCommand = new RelayCommand(OpenFolder, () => CurrentSessionDirectory is not null);
        OpenEvidenceCommand = new RelayCommand(OpenEvidence, () => SelectedEvidence is not null);
        OpenLogsCommand = new RelayCommand(OpenLogs);

        RefreshProcesses();
    }

    public ObservableCollection<ProcessOption> Processes { get; } = [];
    public ObservableCollection<EvidenceItem> Evidences { get; } = [];
    public IReadOnlyList<CaptureAreaOption> CaptureAreas { get => _captureAreas; private set => SetProperty(ref _captureAreas, value); }
    public IReadOnlyList<TecLanguage> Languages { get; } = TecLanguage.All;

    public ICommand RefreshProcessesCommand { get; }
    public ICommand LaunchCommand { get; }
    public ICommand BrowseOutputCommand { get; }
    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand OpenReportCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand OpenEvidenceCommand { get; }
    public ICommand OpenLogsCommand { get; }

    public CaptureAreaOption SelectedArea { get => _selectedArea; set => SetProperty(ref _selectedArea, value); }

    /// <summary>Applications the next session will follow.</summary>
    public IReadOnlyList<TargetProcessInfo> SelectedProcesses =>
        [.. Processes.Where(p => p.IsSelected).Select(p => p.Process)];

    public bool HasSelectedProcesses => Processes.Any(p => p.IsSelected);

    /// <summary>Language of the interface, captions and report. It is remembered between runs.</summary>
    public TecLanguage Language
    {
        get => _localizer.Language;
        set
        {
            if (value is null || value == _localizer.Language) return;
            _localizer.Language = value;
            BuildCaptureAreas(SelectedArea.Value);
            OnPropertyChanged(nameof(Language));
            OnPropertyChanged(nameof(Status));
            _settingsStore.Save(_settingsStore.Load() with { Language = value.Code });
            Log.Info(LogCategory, $"Idioma cambiado a {value.Code}.");
        }
    }
    public string OutputRoot { get => _outputRoot; set => SetProperty(ref _outputRoot, value); }

    /// <summary>Name the next session will use; empty means the one built from the applications.</summary>
    public string SessionName { get => _sessionName; set => SetProperty(ref _sessionName, value); }

    /// <summary>Folder a nameless session would get; shown grey inside the name field while it is empty.</summary>
    public string SessionNamePreview { get => _sessionNamePreview; private set => SetProperty(ref _sessionNamePreview, value); }
    /// <summary>Status line; it is kept as a function so it is rewritten when the language changes.</summary>
    public string Status => _status();
    public bool CaptureClicks { get => _captureClicks; set => SetProperty(ref _captureClicks, value); }
    public bool CaptureKeyboardCommands { get => _captureKeyboardCommands; set => SetProperty(ref _captureKeyboardCommands, value); }
    public bool CaptureTextInput { get => _captureTextInput; set => SetProperty(ref _captureTextInput, value); }
    public bool CaptureWindows { get => _captureWindows; set => SetProperty(ref _captureWindows, value); }
    public bool CaptureContentChanges { get => _captureContentChanges; set => SetProperty(ref _captureContentChanges, value); }
    public bool MinimizeOnStart { get => _minimizeOnStart; set => SetProperty(ref _minimizeOnStart, value); }
    public int WindowSettleDelayMs { get => _windowSettleDelayMs; set => SetProperty(ref _windowSettleDelayMs, Math.Clamp(value, 0, 5000)); }
    public EvidenceItem? SelectedEvidence { get => _selectedEvidence; set => SetProperty(ref _selectedEvidence, value); }

    public bool IsRunning => _session?.Collector.IsRunning == true;
    public bool IsIdle => !IsRunning;
    public string? CurrentSessionDirectory { get; private set; }

    private void RefreshProcesses()
    {
        HashSet<int> selectedIds = [.. Processes.Where(p => p.IsSelected).Select(p => p.Process.ProcessId)];
        Processes.Clear();
        try
        {
            foreach (var process in _catalog.GetCandidates())
                Processes.Add(new ProcessOption(process, OnProcessSelectionChanged) { IsSelected = selectedIds.Contains(process.ProcessId) });
        }
        catch (Exception ex)
        {
            ShowError(_localizer["ErrorProcessList"], ex);
        }
        Log.Debug(LogCategory, $"Lista de aplicaciones actualizada: {Processes.Count} procesos.");
        OnProcessSelectionChanged();
    }

    /// <summary>Keeps the marks of the application list in sync with the Start button.</summary>
    private void OnProcessSelectionChanged()
    {
        OnPropertyChanged(nameof(HasSelectedProcesses));
        OnPropertyChanged(nameof(SelectedProcesses));
        RefreshSessionNamePreview();
        CommandManager.InvalidateRequerySuggested();
    }

    /// <summary>
    /// Folder the session would get if the name field is left empty. The clock shown here is the one
    /// of this moment: the real folder takes the clock of the start.
    /// </summary>
    private void RefreshSessionNamePreview() =>
        SessionNamePreview = EvidenceSession.PreviewSessionName(
            [.. SelectedProcesses.Select(p => p.ProcessName)], DateTimeOffset.Now);

    private void Launch()
    {
        var path = _dialogs.PickExecutable(_localizer["PickExecutableTitle"], _localizer["ExecutableFilter"]);
        if (path is null) return;
        Log.Info(LogCategory, $"Iniciando aplicación: {path}");
        try
        {
            var launched = _catalog.Launch(path, TimeSpan.FromSeconds(15));
            Log.Info(LogCategory, $"Aplicación iniciada: {launched} (PID {launched.ProcessId}).");
            RefreshProcesses();
            // The application just launched joins the marked ones without clearing the rest.
            var option = Processes.FirstOrDefault(p => p.Process.ProcessId == launched.ProcessId)
                ?? AddProcessOption(launched);
            option.IsSelected = true;
            SetStatus(() => _localizer.Format("StatusAppLaunched", launched));
        }
        catch (Exception ex)
        {
            ShowError(_localizer.Format("ErrorLaunch", path), ex);
        }
    }

    private ProcessOption AddProcessOption(TargetProcessInfo process)
    {
        var option = new ProcessOption(process, OnProcessSelectionChanged);
        Processes.Add(option);
        return option;
    }

    private void BrowseOutput()
    {
        var folder = _dialogs.PickFolder(OutputRoot, _localizer["PickFolderTitle"]);
        if (folder is not null) OutputRoot = folder;
    }

    private async Task StartAsync()
    {
        var targets = SelectedProcesses;
        if (targets.Count == 0) return;
        try
        {
            var settings = new SessionSettings
            {
                Targets = targets,
                OutputRoot = OutputRoot,
                SessionName = SessionName,
                Area = SelectedArea.Value,
                Language = Language,
                CaptureClicks = CaptureClicks,
                CaptureKeyboardCommands = CaptureKeyboardCommands,
                CaptureTextInput = CaptureTextInput,
                CaptureWindows = CaptureWindows,
                CaptureContentChanges = CaptureContentChanges,
                WindowSettleDelay = TimeSpan.FromMilliseconds(WindowSettleDelayMs),
            };
            Log.Info(LogCategory, $"Iniciando sesión con {targets.Count} aplicación(es): {string.Join(", ", targets.Select(t => t.ToString()))}.");
            if (!string.IsNullOrWhiteSpace(SessionName)) Log.Info(LogCategory, $"Nombre de la sesión: {SessionName}.");

            var session = await Task.Run(() => EvidenceSessionFactory.Create(settings));
            session.Collector.EvidenceCollected += (_, evidence) => OnEvidenceCollected(session, evidence);
            session.Collector.ErrorOccurred += (_, ex) => OnUi(() => SetStatus(() => _localizer.Format("StatusError", ex.Message)));
            session.Targets.TargetLost += (_, target) => OnUi(() =>
                // Only the status line: a modal dialog would take the focus away from the app under test.
                SetStatus(() => _localizer.Format("StatusTargetLost", target.ProcessName)));
            try
            {
                await session.Collector.StartAsync(session.Session);
            }
            catch
            {
                // Release the hooks that were installed before the failure.
                await DisposeQuietlyAsync(session);
                throw;
            }

            _session = session;
            Evidences.Clear();
            CurrentSessionDirectory = session.Session.OutputDirectory;
            SetStatus(() => _localizer.Format("StatusRecording", string.Join(" + ", targets.Select(t => t.ProcessName))));
            NotifyRunningChanged();
            Log.Info(LogCategory, $"Sesión iniciada en {session.Session.OutputDirectory}");

            if (MinimizeOnStart) _dialogs.MinimizeMainWindow();
        }
        catch (Exception ex)
        {
            ShowError(_localizer["ErrorStart"], ex);
        }
    }

    private async Task StopAsync()
    {
        if (_session is not { } session) return;
        SetStatus(() => _localizer["StatusStopping"]);
        Log.Info(LogCategory, "Deteniendo la sesión.");
        try
        {
            await session.DisposeAsync();
            var count = Evidences.Count;
            var directory = CurrentSessionDirectory;
            SetStatus(() => _localizer.Format("StatusFinished", count, directory));
            Log.Info(LogCategory, $"Sesión terminada: {Evidences.Count} evidencias.");
        }
        catch (Exception ex)
        {
            SetStatus(() => _localizer["StatusFinishedWithErrors"]);
            ShowError(_localizer["ErrorStop"], ex);
        }
        _session = null;
        NotifyRunningChanged();
        RefreshSessionNamePreview();
        _dialogs.RestoreMainWindow();
    }

    /// <summary>Called when the window closes so the final report is not lost.</summary>
    public Task ShutdownAsync() => IsRunning ? StopAsync() : Task.CompletedTask;

    private void OnEvidenceCollected(RunningSession session, Evidence evidence)
    {
        var item = new EvidenceItem(evidence.Sequence, evidence.Caption.Title, evidence.Caption.Details, session.GetImagePath(evidence));
        OnUi(() =>
        {
            // Keep sequence order even if captures finish out of order.
            var index = 0;
            while (index < Evidences.Count && Evidences[index].Sequence < item.Sequence) index++;
            Evidences.Insert(index, item);
            var count = Evidences.Count;
            SetStatus(() => _localizer.Format("StatusRecordingCount", count));
        });
    }

    private void OpenReport() => OpenPath(System.IO.Path.Combine(CurrentSessionDirectory!, Tec.Core.Reporting.HtmlReportSink.FileName));
    private void OpenFolder() => OpenPath(CurrentSessionDirectory!);
    private void OpenEvidence() => OpenPath(SelectedEvidence!.ImagePath);

    private void OpenLogs()
    {
        System.IO.Directory.CreateDirectory(_logDirectory);
        OpenPath(_logDirectory);
    }

    private void OpenPath(string path)
    {
        try { _dialogs.OpenInShell(path); }
        catch (Exception ex) { ShowError(_localizer.Format("ErrorOpen", path), ex); }
    }

    /// <summary>Logs the full error (with stack trace) and shows the user a summary.</summary>
    private void ShowError(string message, Exception ex)
    {
        Log.Error(LogCategory, message, ex);
        _dialogs.ShowError(_localizer.Format("ErrorDetails", message, ex.Message, _logDirectory));
    }

    private void SetStatus(Func<string> status)
    {
        _status = status;
        OnPropertyChanged(nameof(Status));
    }

    private void BuildCaptureAreas(CaptureArea selected)
    {
        CaptureAreas =
        [
            new(CaptureArea.Window, _localizer["AreaWindow"]),
            new(CaptureArea.Monitor, _localizer["AreaMonitor"]),
            new(CaptureArea.AllScreens, _localizer["AreaAllScreens"]),
        ];
        SelectedArea = CaptureAreas.First(area => area.Value == selected);
    }

    private static async Task DisposeQuietlyAsync(RunningSession session)
    {
        try { await session.DisposeAsync(); }
        catch (Exception ex) { Log.Warning(LogCategory, "Error al liberar una sesión que no pudo iniciar.", ex); }
    }

    private void NotifyRunningChanged()
    {
        OnPropertyChanged(nameof(IsRunning));
        OnPropertyChanged(nameof(IsIdle));
        OnPropertyChanged(nameof(CurrentSessionDirectory));
        CommandManager.InvalidateRequerySuggested();
    }

    private static void OnUi(Action action) => Application.Current?.Dispatcher.BeginInvoke(action);
}
