using Tec.Core.Abstractions;
using Tec.Core.Diagnostics;
using Tec.Core.Model;
using Tec.Windows.Native;

namespace Tec.Windows.Sources;

/// <summary>Base for hook-based sources that need a thread with a message loop.</summary>
public abstract class MessageLoopInteractionSource : IInteractionSource
{
    private readonly MessageLoopThread _loop;

    protected MessageLoopInteractionSource(string name, IClock clock)
    {
        Name = name;
        Clock = clock;
        _loop = new MessageLoopThread($"TEC {name}", Install, Uninstall);
    }

    public string Name { get; }
    protected IClock Clock { get; }

    public event EventHandler<Interaction>? InteractionDetected;

    public void Start() => _loop.Start();

    public void Stop()
    {
        _loop.Stop();
        OnStopped();
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }

    /// <summary>Installs the hook (runs on the message loop thread).</summary>
    protected abstract void Install();

    /// <summary>Uninstalls the hook (runs on the message loop thread).</summary>
    protected abstract void Uninstall();

    protected virtual void OnStopped()
    {
    }

    protected void Raise(Interaction interaction)
    {
        try
        {
            InteractionDetected?.Invoke(this, interaction);
        }
        catch (Exception ex)
        {
            // An exception must never escape a hook callback.
            Log.Error(Name, $"Error al procesar la interacción {interaction.Kind}.", ex);
        }
    }
}
