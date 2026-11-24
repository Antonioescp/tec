namespace Tec.Core.Model;

/// <summary>
/// Interaction kind. It is an open type: new kinds can be defined
/// without modifying the core (open/closed principle).
/// </summary>
public sealed record InteractionKind(string Code, string DisplayName, InteractionCategory Category)
{
    public static readonly InteractionKind LeftClick = new("click", "Click izquierdo", InteractionCategory.Pointer);
    public static readonly InteractionKind RightClick = new("click-derecho", "Click derecho", InteractionCategory.Pointer);
    public static readonly InteractionKind MiddleClick = new("click-central", "Click central", InteractionCategory.Pointer);
    public static readonly InteractionKind DoubleClick = new("doble-click", "Doble click", InteractionCategory.Pointer);
    public static readonly InteractionKind KeyboardCommand = new("comando", "Comando de teclado", InteractionCategory.Keyboard);
    public static readonly InteractionKind TextInput = new("texto", "Texto ingresado", InteractionCategory.Keyboard);
    public static readonly InteractionKind WindowOpened = new("ventana", "Ventana mostrada", InteractionCategory.Application);
    public static readonly InteractionKind ContentChanged = new("cambio", "Cambio en pantalla", InteractionCategory.Application);

    public override string ToString() => DisplayName;
}

public enum InteractionCategory
{
    Pointer,
    Keyboard,
    Application,
}

/// <summary>Information about the UI element involved in the interaction.</summary>
/// <param name="Name">Element name (e.g. a button's text).</param>
/// <param name="ControlType">Control type as the operating system names it, in its language (e.g. "botón").</param>
/// <param name="AutomationId">UI Automation identifier, used when the element has no name.</param>
/// <param name="Bounds">Element bounds in physical screen coordinates.</param>
/// <param name="Value">Current value (e.g. the text of a field); <c>null</c> for passwords.</param>
/// <param name="IsPassword">Whether the element is a password field.</param>
/// <param name="ControlTypeId">Language-independent control type (e.g. UI Automation's "Button").</param>
public sealed record UiElementInfo(
    string? Name,
    string? ControlType,
    string? AutomationId,
    ScreenRect? Bounds,
    string? Value = null,
    bool IsPassword = false,
    string? ControlTypeId = null);

/// <summary>
/// Language-independent note about an interaction. The caption formatter turns
/// <see cref="Code"/> into text; <see cref="Detail"/> is shown as is.
/// </summary>
public sealed record InteractionNote(string Code, string? Detail = null)
{
    public static readonly InteractionNote WindowClosedBeforeCapture = new("window-closed");
    public static readonly InteractionNote ScreenStillChanging = new("screen-still-changing");
    public static readonly InteractionNote ElementTimeout = new("element-timeout");

    public static InteractionNote EnrichmentFailed(string detail) => new("enrichment-failed", detail);
}

/// <summary>Interaction detected by a source and enriched during capture.</summary>
public sealed record Interaction
{
    public required InteractionKind Kind { get; init; }

    /// <summary>When the interaction happened.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Source-specific text that is not translated, e.g. a window title.</summary>
    public string? Description { get; init; }

    /// <summary>Keys of a keyboard command, as language-independent names (e.g. "Ctrl", "Delete").</summary>
    public IReadOnlyList<string> Keys { get; init; } = [];

    /// <summary>Fraction of the window that changed (0-1), for screen changes.</summary>
    public double? ChangedFraction { get; init; }

    public ScreenPoint? Point { get; init; }

    /// <summary>Region to highlight. If null, the element bounds or the whole capture are used.</summary>
    public ScreenRect? HighlightBounds { get; init; }

    public int? ProcessId { get; init; }

    /// <summary>Handle of the top-level window involved (0 if unknown).</summary>
    public nint WindowHandle { get; init; }

    public string? WindowTitle { get; init; }

    public UiElementInfo? Element { get; init; }

    /// <summary>Delay before capturing (e.g. so a window finishes painting).</summary>
    public TimeSpan CaptureDelay { get; init; } = TimeSpan.Zero;

    public IReadOnlyList<InteractionNote> Notes { get; init; } = [];

    public Interaction WithNote(InteractionNote note) => this with { Notes = [.. Notes, note] };
}
