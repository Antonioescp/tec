namespace Tec.Core.Model;

/// <summary>
/// Tipo de interacción. Es un tipo abierto: se pueden definir nuevos tipos
/// sin modificar el núcleo (principio abierto/cerrado).
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

/// <summary>Información del elemento de UI involucrado en la interacción.</summary>
public sealed record UiElementInfo(
    string? Name,
    string? ControlType,
    string? AutomationId,
    ScreenRect? Bounds,
    string? Value = null,
    bool IsPassword = false)
{
    /// <summary>Descripción legible, p. ej. <c>botón "Procesar"</c>.</summary>
    public string Describe()
    {
        var type = string.IsNullOrWhiteSpace(ControlType) ? "elemento" : ControlType;
        var label = !string.IsNullOrWhiteSpace(Name) ? Name : AutomationId;
        return string.IsNullOrWhiteSpace(label) ? type : $"{type} \"{label}\"";
    }
}

/// <summary>Interacción detectada por una fuente y enriquecida durante la captura.</summary>
public sealed record Interaction
{
    public required InteractionKind Kind { get; init; }

    /// <summary>Momento en que ocurrió la interacción.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Texto propio de la fuente, p. ej. "Ctrl+S".</summary>
    public string? Description { get; init; }

    public ScreenPoint? Point { get; init; }

    /// <summary>Región a resaltar. Si es nula se usa la del elemento o la de toda la captura.</summary>
    public ScreenRect? HighlightBounds { get; init; }

    public int? ProcessId { get; init; }

    /// <summary>Handle de la ventana de nivel superior involucrada (0 si se desconoce).</summary>
    public nint WindowHandle { get; init; }

    public string? WindowTitle { get; init; }

    public UiElementInfo? Element { get; init; }

    /// <summary>Espera antes de capturar (p. ej. para que una ventana termine de dibujarse).</summary>
    public TimeSpan CaptureDelay { get; init; } = TimeSpan.Zero;

    public IReadOnlyList<string> Notes { get; init; } = [];

    public Interaction WithNote(string note) => this with { Notes = [.. Notes, note] };
}
