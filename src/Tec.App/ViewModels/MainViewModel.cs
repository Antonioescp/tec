using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using Tec.App.Composition;
using Tec.App.Mvvm;
using Tec.App.Services;
using Tec.Core.Diagnostics;
using Tec.Core.Model;
using Tec.Core.Services.Captions;
using Tec.Windows.Processes;

namespace Tec.App.ViewModels;

public sealed record EvidenceItem(int Sequence, string Title, string Details, string ImagePath);

public sealed record CaptureAreaOption(CaptureArea Value, string Label);

public sealed class MainViewModel : ObservableObject
{
    private const string LogCategory = "UI";

    private readonly ITargetProcessCatalog _catalog;
    private readonly IUserDialogs _dialogs;
    private readonly string _logDirectory;

    private RunningSession? _session;
    private TargetProcessInfo? _selectedProcess;
    private CaptureAreaOption _selectedArea;
    private CaptionLanguage _captionLanguage = CaptionLanguage.Spanish;
    private string _outputRoot;
    private string _status = "Selecciona la aplicación a probar y presiona Iniciar.";
    private bool _captureClicks = true;
    private bool _captureKeyboardCommands = true;
    private bool _captureTextInput = true;
    private bool _captureWindows = true;
    private bool _captureContentChanges = true;
    private bool _minimizeOnStart = true;
    private int _windowSettleDelayMs = 250;
    private EvidenceItem? _selectedEvidence;

    public MainViewModel(ITargetProcessCatalog catalog, IUserDialogs dialogs, string logDirectory)
    {
        _catalog = catalog;
        _dialogs = dialogs;
        _logDirectory = logDirectory;
        _outputRoot = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TEC", "Evidencias");

        CaptureAreas =
        [
            new(CaptureArea.Window, "Ventana de la aplicación"),
            new(CaptureArea.Monitor, "Monitor completo"),
            new(CaptureArea.AllScreens, "Todas las pantallas"),
        ];
        _selectedArea = CaptureAreas[0];

        RefreshProcessesCommand = new RelayCommand(RefreshProcesses, () => !IsRunning);
        LaunchCommand = new RelayCommand(Launch, () => !IsRunning);
        BrowseOutputCommand = new RelayCommand(BrowseOutput, () => !IsRunning);
        StartCommand = new AsyncRelayCommand(StartAsync, () => !IsRunning && SelectedProcess is not null);
        StopCommand = new AsyncRelayCommand(StopAsync, () => IsRunning);
        OpenReportCommand = new RelayCommand(OpenReport, () => CurrentSessionDirectory is not null);
        OpenFolderCommand = new RelayCommand(OpenFolder, () => CurrentSessionDirectory is not null);
        OpenEvidenceCommand = new RelayCommand(OpenEvidence, () => SelectedEvidence is not null);
        OpenLogsCommand = new RelayCommand(OpenLogs);

        RefreshProcesses();
    }

    public ObservableCollection<TargetProcessInfo> Processes { get; } = [];
    public ObservableCollection<EvidenceItem> Evidences { get; } = [];
    public IReadOnlyList<CaptureAreaOption> CaptureAreas { get; }
    public IReadOnlyList<CaptionLanguage> CaptionLanguages { get; } = CaptionLanguage.All;

    public ICommand RefreshProcessesCommand { get; }
    public ICommand LaunchCommand { get; }
    public ICommand BrowseOutputCommand { get; }
    public ICommand StartCommand { get; }
    public ICommand StopCommand { get; }
    public ICommand OpenReportCommand { get; }
    public ICommand OpenFolderCommand { get; }
    public ICommand OpenEvidenceCommand { get; }
    public ICommand OpenLogsCommand { get; }

    public TargetProcessInfo? SelectedProcess { get => _selectedProcess; set => SetProperty(ref _selectedProcess, value); }
    public CaptureAreaOption SelectedArea { get => _selectedArea; set => SetProperty(ref _selectedArea, value); }
    public CaptionLanguage CaptionLanguage { get => _captionLanguage; set => SetProperty(ref _captionLanguage, value); }
    public string OutputRoot { get => _outputRoot; set => SetProperty(ref _outputRoot, value); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
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
        var selectedId = SelectedProcess?.ProcessId;
        Processes.Clear();
        try
        {
            foreach (var process in _catalog.GetCandidates()) Processes.Add(process);
        }
        catch (Exception ex)
        {
            ShowError("No se pudo obtener la lista de aplicaciones.", ex);
        }
        Log.Debug(LogCategory, $"Lista de aplicaciones actualizada: {Processes.Count} procesos.");
        SelectedProcess = Processes.FirstOrDefault(p => p.ProcessId == selectedId);
    }

    private void Launch()
    {
        var path = _dialogs.PickExecutable();
        if (path is null) return;
        Log.Info(LogCategory, $"Iniciando aplicación: {path}");
        try
        {
            var launched = _catalog.Launch(path, TimeSpan.FromSeconds(15));
            Log.Info(LogCategory, $"Aplicación iniciada: {launched} (PID {launched.ProcessId}).");
            RefreshProcesses();
            SelectedProcess = Processes.FirstOrDefault(p => p.ProcessId == launched.ProcessId);
            if (SelectedProcess is null)
            {
                Processes.Add(launched);
                SelectedProcess = launched;
            }
            Status = $"Aplicación iniciada: {launched}";
        }
        catch (Exception ex)
        {
            ShowError($"No se pudo iniciar la aplicación {path}.", ex);
        }
    }

    private void BrowseOutput()
    {
        var folder = _dialogs.PickFolder(OutputRoot);
        if (folder is not null) OutputRoot = folder;
    }

    private async Task StartAsync()
    {
        if (SelectedProcess is not { } target) return;
        try
        {
            var settings = new SessionSettings
            {
                Target = target,
                OutputRoot = OutputRoot,
                Area = SelectedArea.Value,
                CaptionLanguage = CaptionLanguage,
                CaptureClicks = CaptureClicks,
                CaptureKeyboardCommands = CaptureKeyboardCommands,
                CaptureTextInput = CaptureTextInput,
                CaptureWindows = CaptureWindows,
                CaptureContentChanges = CaptureContentChanges,
                WindowSettleDelay = TimeSpan.FromMilliseconds(WindowSettleDelayMs),
            };
            Log.Info(LogCategory, $"Iniciando sesión: {settings}");

            var session = await Task.Run(() => EvidenceSessionFactory.Create(settings));
            session.Collector.EvidenceCollected += (_, evidence) => OnEvidenceCollected(session, evidence);
            session.Collector.ErrorOccurred += (_, ex) => OnUi(() => Status = $"Error: {ex.Message} (detalle en el log)");
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
            Status = $"Grabando evidencia de {target.ProcessName}. Las interacciones con TEC no se capturan.";
            NotifyRunningChanged();
            Log.Info(LogCategory, $"Sesión iniciada en {session.Session.OutputDirectory}");

            if (MinimizeOnStart) _dialogs.MinimizeMainWindow();
        }
        catch (Exception ex)
        {
            ShowError("No se pudo iniciar la sesión.", ex);
        }
    }

    private async Task StopAsync()
    {
        if (_session is not { } session) return;
        Status = "Deteniendo y generando el reporte...";
        Log.Info(LogCategory, "Deteniendo la sesión.");
        try
        {
            await session.DisposeAsync();
            Status = $"Sesión terminada: {Evidences.Count} evidencias en {CurrentSessionDirectory}";
            Log.Info(LogCategory, $"Sesión terminada: {Evidences.Count} evidencias.");
        }
        catch (Exception ex)
        {
            Status = "La sesión terminó con errores (detalle en el log).";
            ShowError("Ocurrió un error al detener la sesión o al generar el reporte.", ex);
        }
        _session = null;
        NotifyRunningChanged();
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
            Status = $"Grabando… {Evidences.Count} evidencias";
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
        catch (Exception ex) { ShowError($"No se pudo abrir {path}.", ex); }
    }

    /// <summary>Logs the full error (with stack trace) and shows the user a summary.</summary>
    private void ShowError(string message, Exception ex)
    {
        Log.Error(LogCategory, message, ex);
        _dialogs.ShowError($"{message}\n\n{ex.Message}\n\nDetalle en el log: {_logDirectory}");
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
