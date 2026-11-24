using Tec.Core.Localization;
using Tec.Core.Model;
using Tec.Core.Services;

namespace Tec.Core.Tests;

public class CaptionFormatterTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 13, 10, 20, 30, TimeSpan.Zero);
    private static readonly CaptionFormatter Spanish = new(TecLanguage.Spanish);
    private static readonly CaptionFormatter English = new(TecLanguage.English);

    private static Interaction Click() => new()
    {
        Kind = InteractionKind.LeftClick,
        Timestamp = Now,
        Point = new ScreenPoint(10, 20),
        WindowTitle = "Principal",
        Element = new UiElementInfo("Procesar", "botón", null, null, ControlTypeId: "Button"),
    };

    [Fact]
    public void Click_describes_the_element_in_spanish()
    {
        var caption = Spanish.Format(3, Click());

        Assert.Equal("#003 · Click izquierdo: botón \"Procesar\"", caption.Title);
        Assert.Contains("Ventana: \"Principal\"", caption.Details);
        Assert.Contains("Posición: (10, 20)", caption.Details);
    }

    [Fact]
    public void Click_describes_the_element_in_english()
    {
        var caption = English.Format(3, Click());

        Assert.Equal("#003 · Left click: button \"Procesar\"", caption.Title);
        Assert.Contains("Window: \"Principal\"", caption.Details);
        Assert.Contains("Position: (10, 20)", caption.Details);
    }

    [Fact]
    public void Click_details_name_the_application_when_it_is_known()
    {
        var interaction = Click() with { ProcessName = "Postman" };

        Assert.Contains("Aplicación: Postman", Spanish.Format(3, interaction).Details);
        Assert.Contains("App: Postman", English.Format(3, interaction).Details);
    }

    [Fact]
    public void Click_details_omit_the_application_when_it_is_unknown()
    {
        var details = English.Format(3, Click()).Details;

        Assert.DoesNotContain("App:", details);
        Assert.Contains("Window: \"Principal\"", details);
    }

    [Fact]
    public void Control_type_without_id_uses_the_name_given_by_the_system()
    {
        var interaction = Click() with { Element = new UiElementInfo("Procesar", "botón", null, null) };

        Assert.Equal("#003 · Left click: botón \"Procesar\"", English.Format(3, interaction).Title);
    }

    [Fact]
    public void Element_without_type_uses_the_generic_name()
    {
        var interaction = Click() with { Element = new UiElementInfo("Procesar", null, null, null) };

        Assert.Equal("#003 · Click izquierdo: elemento \"Procesar\"", Spanish.Format(3, interaction).Title);
        Assert.Equal("#003 · Left click: element \"Procesar\"", English.Format(3, interaction).Title);
    }

    [Fact]
    public void Keyboard_command_translates_keys_and_element()
    {
        var interaction = new Interaction
        {
            Kind = InteractionKind.KeyboardCommand,
            Timestamp = Now,
            Keys = ["Ctrl", "Delete"],
            Element = new UiElementInfo("Nombre", "edición", null, null, ControlTypeId: "Edit"),
        };

        Assert.Equal("#001 · Comando de teclado: Ctrl+Supr en edición \"Nombre\"", Spanish.Format(1, interaction).Title);
        Assert.Equal("#001 · Keyboard command: Ctrl+Del on edit \"Nombre\"", English.Format(1, interaction).Title);
    }

    [Fact]
    public void Keyboard_double_tap_joins_both_keys()
    {
        var interaction = new Interaction { Kind = InteractionKind.KeyboardCommand, Timestamp = Now, Keys = ["Shift", "Shift"] };

        Assert.Equal("#002 · Keyboard command: Shift+Shift", English.Format(2, interaction).Title);
    }

    [Fact]
    public void Text_input_masks_passwords()
    {
        var caption = English.Format(2, new Interaction
        {
            Kind = InteractionKind.TextInput,
            Timestamp = Now,
            Element = new UiElementInfo("Clave", "edición", null, null, Value: "secreto", IsPassword: true, ControlTypeId: "Edit"),
        });

        Assert.DoesNotContain("secreto", caption.Title);
        Assert.Equal("#002 · Text input: edit \"Clave\" = \"••••••\"", caption.Title);
    }

    [Fact]
    public void Window_opened_uses_title_and_translated_notes()
    {
        var interaction = new Interaction
        {
            Kind = InteractionKind.WindowOpened,
            Timestamp = Now,
            WindowTitle = "Espere…",
        }.WithNote(InteractionNote.WindowClosedBeforeCapture);

        var spanish = Spanish.Format(4, interaction);
        var english = English.Format(4, interaction);

        Assert.Equal("#004 · Ventana mostrada: \"Espere…\"", spanish.Title);
        Assert.Contains("La ventana se cerró antes de la captura", spanish.Details);
        Assert.Equal("#004 · Window shown: \"Espere…\"", english.Title);
        Assert.Contains("The window closed before the capture", english.Details);
    }

    [Fact]
    public void Notes_include_their_detail()
    {
        var interaction = new Interaction { Kind = InteractionKind.LeftClick, Timestamp = Now }
            .WithNote(InteractionNote.EnrichmentFailed("UiaElementEnricher: timeout"));

        Assert.Contains("Could not get additional information (UiaElementEnricher: timeout)", English.Format(1, interaction).Details);
    }

    [Fact]
    public void Unknown_notes_show_their_detail_or_code()
    {
        var interaction = new Interaction { Kind = InteractionKind.LeftClick, Timestamp = Now }
            .WithNote(new InteractionNote("custom", "Texto libre"))
            .WithNote(new InteractionNote("other-code"));

        var details = English.Format(1, interaction).Details;
        Assert.Contains("Texto libre", details);
        Assert.Contains("other-code", details);
    }

    [Fact]
    public void Content_change_formats_the_fraction_with_the_language_culture()
    {
        var interaction = new Interaction { Kind = InteractionKind.ContentChanged, Timestamp = Now, ChangedFraction = 0.25 };

        Assert.Equal("#006 · Screen change: 25% of the window changed", English.Format(6, interaction).Title);
        Assert.StartsWith("#006 · Cambio en pantalla: cambió el 25", Spanish.Format(6, interaction).Title, StringComparison.Ordinal);
    }

    [Fact]
    public void Custom_kinds_fall_back_to_their_display_name()
    {
        var custom = new InteractionKind("arrastre", "Arrastrar y soltar", InteractionCategory.Pointer);
        var interaction = new Interaction { Kind = custom, Timestamp = Now, Description = "Archivo.txt" };

        Assert.Equal("#005 · Arrastrar y soltar: Archivo.txt", English.Format(5, interaction).Title);
    }

    [Theory]
    [InlineData("es")]
    [InlineData("EN")]
    public void Languages_can_be_found_by_code(string code) =>
        Assert.NotNull(TecLanguage.Find(code));

    [Fact]
    public void Unsupported_windows_language_falls_back_to_spanish() =>
        Assert.Equal(TecLanguage.Spanish, TecLanguage.FromCulture(System.Globalization.CultureInfo.GetCultureInfo("fr-FR")));

    [Fact]
    public void Every_language_names_every_built_in_kind()
    {
        InteractionKind[] kinds =
        [
            InteractionKind.LeftClick, InteractionKind.RightClick, InteractionKind.MiddleClick, InteractionKind.DoubleClick,
            InteractionKind.KeyboardCommand, InteractionKind.TextInput, InteractionKind.WindowOpened, InteractionKind.ContentChanged,
        ];
        foreach (var language in TecLanguage.All)
        {
            var texts = new Texts(CoreResources.Captions, language.Culture);
            Assert.All(kinds, kind => Assert.NotNull(texts.Find("Kind_" + kind.Code)));
        }
    }
}
