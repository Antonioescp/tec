using System.Globalization;
using Tec.Core.Abstractions;
using Tec.Core.Model;

namespace Tec.Core.Services;

/// <summary>Leyendas en español, p. ej. <c>#003 · Click izquierdo: botón "Procesar"</c>.</summary>
public sealed class SpanishCaptionFormatter : IInteractionCaptionFormatter
{
    private const int MaxValueLength = 120;
    private const string Separator = "   ·   ";

    public InteractionCaption Format(int sequence, Interaction interaction)
    {
        var title = $"#{sequence:000} · {interaction.Kind.DisplayName}";
        var subject = DescribeSubject(interaction);
        if (!string.IsNullOrWhiteSpace(subject)) title += $": {subject}";

        var details = new List<string>
        {
            interaction.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
        };
        if (!string.IsNullOrWhiteSpace(interaction.WindowTitle))
            details.Add($"Ventana: \"{interaction.WindowTitle}\"");
        if (interaction.Point is { } p)
            details.Add($"Posición: ({p.X}, {p.Y})");
        details.AddRange(interaction.Notes);

        return new InteractionCaption(title, string.Join(Separator, details));
    }

    private static string? DescribeSubject(Interaction interaction)
    {
        var element = interaction.Element;
        var kind = interaction.Kind;

        if (kind == InteractionKind.KeyboardCommand)
            return element is null ? interaction.Description : $"{interaction.Description} en {element.Describe()}";

        if (kind == InteractionKind.TextInput)
        {
            if (element is null) return interaction.Description;
            var value = element.IsPassword ? "••••••" : Truncate(element.Value);
            return value is null ? element.Describe() : $"{element.Describe()} = \"{value}\"";
        }

        if (kind == InteractionKind.WindowOpened)
            return string.IsNullOrWhiteSpace(interaction.WindowTitle) ? interaction.Description : $"\"{interaction.WindowTitle}\"";

        return element?.Describe() ?? interaction.Description;
    }

    private static string? Truncate(string? value) =>
        value is { Length: > MaxValueLength } ? value[..MaxValueLength] + "…" : value;
}
