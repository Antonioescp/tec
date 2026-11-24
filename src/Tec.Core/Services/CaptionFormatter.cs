using System.Globalization;
using Tec.Core.Abstractions;
using Tec.Core.Localization;
using Tec.Core.Model;

namespace Tec.Core.Services;

/// <summary>
/// Captions in the given language, e.g. <c>#003 · Left click: button "Process"</c> or
/// <c>#003 · Click izquierdo: botón "Procesar"</c>.
/// </summary>
/// <remarks>
/// Interactions carry language-independent data (kind codes, key names, UI Automation
/// control types, note codes) that is translated with <c>Resources/Captions*.resx</c>.
/// Missing entries fall back to the raw value, so new kinds, keys or notes still produce
/// a readable caption.
/// </remarks>
public sealed class CaptionFormatter(TecLanguage language) : IInteractionCaptionFormatter
{
    private const int MaxValueLength = 120;
    private const string Separator = "   ·   ";
    private const string MaskedValue = "••••••";

    private readonly Texts _texts = new(CoreResources.Captions, language.Culture);

    public TecLanguage Language { get; } = language;

    public InteractionCaption Format(int sequence, Interaction interaction)
    {
        var title = $"#{sequence.ToString("000", CultureInfo.InvariantCulture)} · {KindName(interaction.Kind)}";
        var subject = DescribeSubject(interaction);
        if (!string.IsNullOrWhiteSpace(subject)) title += $": {subject}";

        var details = new List<string>
        {
            interaction.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
        };
        if (!string.IsNullOrWhiteSpace(interaction.ProcessName))
            details.Add(_texts.Format("App", interaction.ProcessName));
        if (!string.IsNullOrWhiteSpace(interaction.WindowTitle))
            details.Add(_texts.Format("Window", interaction.WindowTitle));
        if (interaction.Point is { } p)
            details.Add(_texts.Format("Position", p.X, p.Y));
        details.AddRange(interaction.Notes.Select(DescribeNote));

        return new InteractionCaption(title, string.Join(Separator, details));
    }

    private string? DescribeSubject(Interaction interaction)
    {
        var element = interaction.Element;
        var kind = interaction.Kind;

        if (kind == InteractionKind.KeyboardCommand)
        {
            var keys = DescribeKeys(interaction);
            return element is null ? keys : _texts.Format("CommandOnElement", keys, DescribeElement(element));
        }

        if (kind == InteractionKind.TextInput)
        {
            if (element is null) return interaction.Description;
            var value = element.IsPassword ? MaskedValue : Truncate(element.Value);
            return value is null ? DescribeElement(element) : $"{DescribeElement(element)} = \"{value}\"";
        }

        if (kind == InteractionKind.WindowOpened)
            return string.IsNullOrWhiteSpace(interaction.WindowTitle) ? interaction.Description : $"\"{interaction.WindowTitle}\"";

        if (kind == InteractionKind.ContentChanged && interaction.ChangedFraction is { } fraction)
            return _texts.Format("ContentChanged", fraction);

        return element is null ? interaction.Description : DescribeElement(element);
    }

    private string KindName(InteractionKind kind) => _texts.Find("Kind_" + kind.Code) ?? kind.DisplayName;

    private string? DescribeKeys(Interaction interaction) =>
        interaction.Keys.Count == 0
            ? interaction.Description
            : string.Join("+", interaction.Keys.Select(key => _texts.Find("Key_" + key) ?? key));

    private string DescribeElement(UiElementInfo element)
    {
        var type = (element.ControlTypeId is { } id ? _texts.Find("ControlType_" + id) : null)
            ?? (string.IsNullOrWhiteSpace(element.ControlType) ? _texts.Get("UnknownElement") : element.ControlType);
        var label = !string.IsNullOrWhiteSpace(element.Name) ? element.Name : element.AutomationId;
        return string.IsNullOrWhiteSpace(label) ? type : $"{type} \"{label}\"";
    }

    private string DescribeNote(InteractionNote note) =>
        _texts.Find("Note_" + note.Code) is { } format
            ? string.Format(_texts.Culture, format, note.Detail)
            : note.Detail ?? note.Code;

    private static string? Truncate(string? value) =>
        value is { Length: > MaxValueLength } ? value[..MaxValueLength] + "…" : value;
}
