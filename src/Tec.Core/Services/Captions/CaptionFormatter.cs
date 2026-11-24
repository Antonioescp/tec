using System.Globalization;
using Tec.Core.Abstractions;
using Tec.Core.Model;

namespace Tec.Core.Services.Captions;

/// <summary>
/// Captions in the given language, e.g. <c>#003 · Left click: button "Process"</c> or
/// <c>#003 · Click izquierdo: botón "Procesar"</c>.
/// </summary>
public sealed class CaptionFormatter(CaptionLanguage language) : IInteractionCaptionFormatter
{
    private const int MaxValueLength = 120;
    private const string Separator = "   ·   ";
    private const string MaskedValue = "••••••";

    public CaptionLanguage Language { get; } = language;

    public InteractionCaption Format(int sequence, Interaction interaction)
    {
        var title = $"#{sequence.ToString("000", CultureInfo.InvariantCulture)} · {KindName(interaction.Kind)}";
        var subject = DescribeSubject(interaction);
        if (!string.IsNullOrWhiteSpace(subject)) title += $": {subject}";

        var details = new List<string>
        {
            interaction.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
        };
        if (!string.IsNullOrWhiteSpace(interaction.WindowTitle))
            details.Add(Text(Language.WindowFormat, interaction.WindowTitle));
        if (interaction.Point is { } p)
            details.Add(Text(Language.PositionFormat, p.X, p.Y));
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
            return element is null ? keys : Text(Language.CommandOnElementFormat, keys, DescribeElement(element));
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
            return Text(Language.ContentChangedFormat, fraction);

        return element is null ? interaction.Description : DescribeElement(element);
    }

    private string KindName(InteractionKind kind) =>
        Language.InteractionKinds.GetValueOrDefault(kind.Code, kind.DisplayName);

    private string? DescribeKeys(Interaction interaction) =>
        interaction.Keys.Count == 0
            ? interaction.Description
            : string.Join("+", interaction.Keys.Select(key => Language.Keys.GetValueOrDefault(key, key)));

    private string DescribeElement(UiElementInfo element)
    {
        var type = element.ControlTypeId is { } id && Language.ControlTypes.TryGetValue(id, out var name)
            ? name
            : !string.IsNullOrWhiteSpace(element.ControlType) ? element.ControlType : Language.UnknownElement;
        var label = !string.IsNullOrWhiteSpace(element.Name) ? element.Name : element.AutomationId;
        return string.IsNullOrWhiteSpace(label) ? type : $"{type} \"{label}\"";
    }

    private string DescribeNote(InteractionNote note) =>
        Language.Notes.TryGetValue(note.Code, out var format)
            ? Text(format, note.Detail)
            : note.Detail ?? note.Code;

    private string Text(string format, params object?[] args) => string.Format(Language.Culture, format, args);

    private static string? Truncate(string? value) =>
        value is { Length: > MaxValueLength } ? value[..MaxValueLength] + "…" : value;
}
