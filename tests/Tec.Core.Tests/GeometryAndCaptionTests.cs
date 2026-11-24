using Tec.Core.Model;
using Tec.Core.Services;

namespace Tec.Core.Tests;

public class ScreenRectTests
{
    [Fact]
    public void Intersect_returns_overlap() =>
        Assert.Equal(new ScreenRect(5, 5, 5, 5), new ScreenRect(0, 0, 10, 10).Intersect(new ScreenRect(5, 5, 10, 10)));

    [Fact]
    public void Intersect_without_overlap_is_empty() =>
        Assert.True(new ScreenRect(0, 0, 10, 10).Intersect(new ScreenRect(20, 20, 5, 5)).IsEmpty);

    [Fact]
    public void Union_covers_both() =>
        Assert.Equal(new ScreenRect(-5, 0, 20, 30), new ScreenRect(0, 0, 10, 10).Union(new ScreenRect(-5, 10, 20, 20)));

    [Fact]
    public void Union_with_empty_returns_other() =>
        Assert.Equal(new ScreenRect(1, 2, 3, 4), ScreenRect.Empty.Union(new ScreenRect(1, 2, 3, 4)));
}

public class SpanishCaptionFormatterTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 10, 20, 30, TimeSpan.Zero);
    private readonly SpanishCaptionFormatter _formatter = new();

    [Fact]
    public void Click_describes_the_element()
    {
        var caption = _formatter.Format(3, new Interaction
        {
            Kind = InteractionKind.LeftClick,
            Timestamp = Now,
            Point = new ScreenPoint(10, 20),
            WindowTitle = "Principal",
            Element = new UiElementInfo("Procesar", "botón", null, null),
        });

        Assert.Equal("#003 · Click izquierdo: botón \"Procesar\"", caption.Title);
        Assert.Contains("Ventana: \"Principal\"", caption.Details);
        Assert.Contains("Posición: (10, 20)", caption.Details);
    }

    [Fact]
    public void Keyboard_command_includes_keys_and_element()
    {
        var caption = _formatter.Format(1, new Interaction
        {
            Kind = InteractionKind.KeyboardCommand,
            Timestamp = Now,
            Description = "Ctrl+S",
            Element = new UiElementInfo("Nombre", "edición", null, null),
        });

        Assert.Equal("#001 · Comando de teclado: Ctrl+S en edición \"Nombre\"", caption.Title);
    }

    [Fact]
    public void Text_input_masks_passwords()
    {
        var caption = _formatter.Format(2, new Interaction
        {
            Kind = InteractionKind.TextInput,
            Timestamp = Now,
            Element = new UiElementInfo("Clave", "edición", null, null, Value: "secreto", IsPassword: true),
        });

        Assert.DoesNotContain("secreto", caption.Title);
        Assert.Contains("••••••", caption.Title);
    }

    [Fact]
    public void Window_opened_uses_title_and_notes()
    {
        var caption = _formatter.Format(4, new Interaction
        {
            Kind = InteractionKind.WindowOpened,
            Timestamp = Now,
            WindowTitle = "Espere…",
        }.WithNote("La ventana se cerró antes de la captura"));

        Assert.Equal("#004 · Ventana mostrada: \"Espere…\"", caption.Title);
        Assert.Contains("se cerró", caption.Details);
    }

    [Fact]
    public void Custom_kinds_are_supported_without_changes()
    {
        var custom = new InteractionKind("arrastre", "Arrastrar y soltar", InteractionCategory.Pointer);
        var caption = _formatter.Format(5, new Interaction { Kind = custom, Timestamp = Now, Description = "Archivo.txt" });
        Assert.Equal("#005 · Arrastrar y soltar: Archivo.txt", caption.Title);
    }
}
