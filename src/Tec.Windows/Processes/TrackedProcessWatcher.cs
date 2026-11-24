using System.Diagnostics;
using Tec.Core.Model;
using Tec.Core.Services;

namespace Tec.Windows.Processes;

/// <summary>
/// Watches the followed processes and tells <see cref="TrackedTargets"/> when one of them exits,
/// so the session keeps going and the report says which application disappeared.
/// </summary>
public sealed class TrackedProcessWatcher(TrackedTargets targets) : IDisposable
{
    private readonly List<Process> _watched = [];
    private bool _started;

    public void Start()
    {
        if (_started) return;
        _started = true;
        foreach (var target in targets.Targets) Watch(target);
    }

    public void Dispose()
    {
        foreach (var process in _watched) process.Dispose();
        _watched.Clear();
    }

    private void Watch(TargetProcessInfo target)
    {
        Process process;
        try
        {
            process = Process.GetProcessById(target.ProcessId);
        }
        catch (ArgumentException)
        {
            // The process was gone before the session could watch it.
            targets.ReportLost(target);
            return;
        }

        try
        {
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => targets.ReportLost(target);
            _watched.Add(process);
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
        {
            process.Dispose();
        }
    }
}
