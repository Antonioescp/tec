using Tec.Core.Diagnostics;
using static Tec.Windows.Native.NativeMethods;

namespace Tec.Windows.Native;

/// <summary>
/// Hilo dedicado con bucle de mensajes Win32. Los hooks de bajo nivel y los WinEvent
/// hooks se ejecutan en el hilo que los instala, que debe procesar mensajes.
/// </summary>
internal sealed class MessageLoopThread(string name, Action install, Action uninstall) : IDisposable
{
    private Thread? _thread;
    private uint _threadId;

    public bool IsRunning => _thread is not null;

    public void Start()
    {
        if (_thread is not null) return;

        Exception? installError = null;
        using var ready = new ManualResetEventSlim();

        var thread = new Thread(() =>
        {
            _threadId = GetCurrentThreadId();
            PeekMessage(out _, 0, 0, 0, PM_NOREMOVE); // fuerza la creación de la cola de mensajes

            try
            {
                install();
            }
            catch (Exception ex)
            {
                installError = ex;
                ready.Set();
                return;
            }

            ready.Set();
            try
            {
                while (GetMessage(out var msg, 0, 0, 0) > 0)
                {
                    TranslateMessage(ref msg);
                    DispatchMessage(ref msg);
                }
            }
            catch (Exception ex)
            {
                Log.Error(name, "El bucle de mensajes terminó por un error; la fuente dejó de capturar.", ex);
            }
            finally
            {
                try { uninstall(); }
                catch (Exception ex) { Log.Warning(name, "No se pudo desinstalar el hook.", ex); }
            }
        })
        {
            IsBackground = true,
            Name = name,
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();

        if (installError is not null)
            throw new InvalidOperationException($"No se pudo iniciar '{name}': {installError.Message}", installError);

        _thread = thread;
    }

    public void Stop()
    {
        var thread = _thread;
        if (thread is null) return;
        _thread = null;
        PostThreadMessage(_threadId, WM_QUIT, 0, 0);
        thread.Join(TimeSpan.FromSeconds(2));
    }

    public void Dispose() => Stop();
}
