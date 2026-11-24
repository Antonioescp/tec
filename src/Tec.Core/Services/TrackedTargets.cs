using Tec.Core.Diagnostics;
using Tec.Core.Model;

namespace Tec.Core.Services;

/// <summary>
/// The applications a session follows. When one of them stops running it is recorded here so
/// the report and the interface can say so instead of the evidence simply stopping.
/// </summary>
public sealed class TrackedTargets(IReadOnlyList<TargetProcessInfo> targets)
{
    private const string LogCategory = "objetivos";

    private readonly object _gate = new();
    private readonly List<string> _lostNames = [];

    public IReadOnlyList<TargetProcessInfo> Targets { get; } = targets;

    /// <summary>Process identifiers of the followed applications, for the interaction filter.</summary>
    public int[] ProcessIds { get; } = [.. targets.Select(t => t.ProcessId)];

    /// <summary>Names of the followed applications that stopped running, in the order they disappeared.</summary>
    public IReadOnlyList<string> LostNames
    {
        get
        {
            lock (_gate) return [.. _lostNames];
        }
    }

    public event EventHandler<TargetProcessInfo>? TargetLost;

    /// <summary>Records that a followed application no longer exists and notifies the listeners.</summary>
    public void ReportLost(TargetProcessInfo target)
    {
        lock (_gate)
        {
            if (!_lostNames.Contains(target.ProcessName, StringComparer.OrdinalIgnoreCase))
                _lostNames.Add(target.ProcessName);
        }

        Log.Info(LogCategory, $"La aplicación seguida {target.ProcessName} (PID {target.ProcessId}) dejó de existir.");
        TargetLost?.Invoke(this, target);
    }
}
