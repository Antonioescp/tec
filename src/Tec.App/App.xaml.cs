using System.Windows;
using System.Windows.Threading;
using Tec.App.Services;
using Tec.App.ViewModels;
using Tec.Core.Diagnostics;
using Tec.Core.Services;
using Tec.Windows.Processes;

namespace Tec.App;

public partial class App : Application
{
    private const string Category = "App";

    /// <summary>Carpeta de los logs: %LOCALAPPDATA%\TEC\logs.</summary>
    public static string LogDirectory { get; } = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TEC", "logs");

    private FileLogWriter? _logWriter;

    protected override void OnStartup(StartupEventArgs e)
    {
        ConfigureLogging();
        base.OnStartup(e);

        var viewModel = new MainViewModel(new TargetProcessCatalog(), new WpfUserDialogs(), LogDirectory);
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
            // Sin logs la app sigue funcionando.
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
            $"Ocurrió un error inesperado.\n\n{e.Exception.Message}\n\nDetalle en el log: {LogDirectory}",
            "TEC", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true; // la app sigue abierta para no perder la sesión en curso
    }

    private static bool IsAdministrator()
    {
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        return new System.Security.Principal.WindowsPrincipal(identity)
            .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }
}
