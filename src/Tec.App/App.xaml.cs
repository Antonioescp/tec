using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Threading;
using Tec.App.Localization;
using Tec.App.Services;
using Tec.App.ViewModels;
using Tec.Core.Diagnostics;
using Tec.Core.Services;
using Tec.Windows.Processes;

namespace Tec.App;

[SuppressMessage("Reliability", "CA1001", Justification = "WPF does not dispose Application; the log is closed in OnExit.")]
public partial class App : Application
{
    private const string Category = "App";

    /// <summary>Log folder: %LOCALAPPDATA%\TEC\logs.</summary>
    public static string LogDirectory { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TEC", "logs");

    /// <summary>Preferences file: %LOCALAPPDATA%\TEC\settings.json.</summary>
    public static string SettingsFile { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TEC", "settings.json");

    private FileLogWriter? _logWriter;

    protected override void OnStartup(StartupEventArgs e)
    {
        ConfigureLogging();
        base.OnStartup(e);

        var viewModel = new MainViewModel(new TargetProcessCatalog(), new WpfUserDialogs(), new JsonUserSettingsStore(SettingsFile), LogDirectory);
        var window = new MainWindow(viewModel);
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Info(Category, $"TEC terminó (código {e.ApplicationExitCode}).");
        _logWriter?.Dispose();
        base.OnExit(e);
    }

    private void ConfigureLogging()
    {
        try
        {
            _logWriter = new FileLogWriter(LogDirectory, SystemClock.Instance);
            Log.Configure(_logWriter);
        }
        catch (Exception ex)
        {
            // The app keeps working without logs.
            System.Diagnostics.Debug.WriteLine($"No se pudo configurar el log: {ex}");
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            Log.Error(Category, $"Excepción no controlada (terminando: {args.IsTerminating}).", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Log.Error(Category, "Excepción no observada en una tarea.", args.Exception);
            args.SetObserved();
        };

        var version = typeof(App).Assembly.GetName().Version;
        Log.Info(Category, $"TEC {version} iniciado. PID {Environment.ProcessId}, {Environment.OSVersion}, " +
                           $".NET {Environment.Version}, {(Environment.Is64BitProcess ? "64" : "32")} bits, " +
                           $"usuario administrador: {IsAdministrator()}.");
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(Category, "Excepción no controlada en la interfaz.", e.Exception);
        MessageBox.Show(
            Localizer.Instance.Format("ErrorDetails", Localizer.Instance["ErrorUnexpected"], e.Exception.Message, LogDirectory),
            "TEC", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true; // keep the app open so the current session is not lost
    }

    private static bool IsAdministrator()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        return new System.Security.Principal.WindowsPrincipal(identity)
            .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }
}
