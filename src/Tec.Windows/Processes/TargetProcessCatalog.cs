using System.Diagnostics;
using Tec.Core.Model;

namespace Tec.Windows.Processes;

public interface ITargetProcessCatalog
{
    IReadOnlyList<TargetProcessInfo> GetCandidates();
    TargetProcessInfo Launch(string executablePath, TimeSpan waitForWindow);
}

/// <summary>Procesos con ventana principal, excluyendo al propio TEC.</summary>
public sealed class TargetProcessCatalog : ITargetProcessCatalog
{
    public IReadOnlyList<TargetProcessInfo> GetCandidates()
    {
        var ownId = Environment.ProcessId;
        var result = new List<TargetProcessInfo>();
        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                try
                {
                    if (process.Id == ownId || process.MainWindowHandle == 0) continue;
                    result.Add(new TargetProcessInfo(process.Id, process.ProcessName, process.MainWindowTitle));
                }
                catch (InvalidOperationException)
                {
                    // El proceso terminó mientras se enumeraba.
                }
            }
        }
        return [.. result.OrderBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase).ThenBy(p => p.ProcessId)];
    }

    public TargetProcessInfo Launch(string executablePath, TimeSpan waitForWindow)
    {
        var process = Process.Start(new ProcessStartInfo(executablePath)
        {
            UseShellExecute = true,
            WorkingDirectory = System.IO.Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory,
        }) ?? throw new InvalidOperationException($"No se pudo iniciar {executablePath}.");

        var deadline = DateTime.UtcNow + waitForWindow;
        while (DateTime.UtcNow < deadline && !process.HasExited)
        {
            process.Refresh();
            if (process.MainWindowHandle != 0) break;
            Thread.Sleep(100);
        }

        return new TargetProcessInfo(process.Id, process.ProcessName, process.HasExited ? string.Empty : process.MainWindowTitle);
    }
}
