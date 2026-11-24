using System.Windows.Automation;
using Tec.Core.Model;

namespace Tec.Windows.Uia;

/// <summary>Utilidades de UI Automation compartidas.</summary>
internal static class UiaElements
{
    private const int MaxValueLength = 500;

    private static readonly HashSet<ControlType> InteractiveTypes =
    [
        ControlType.Button, ControlType.CheckBox, ControlType.RadioButton, ControlType.ComboBox,
        ControlType.Edit, ControlType.Hyperlink, ControlType.ListItem, ControlType.MenuItem,
        ControlType.TabItem, ControlType.TreeItem, ControlType.DataItem, ControlType.SplitButton,
        ControlType.Slider, ControlType.Spinner, ControlType.HeaderItem, ControlType.Document,
    ];

    /// <summary>
    /// Si el elemento es un texto o imagen dentro de un control (p. ej. el TextBlock de un
    /// botón) devuelve el control interactivo que lo contiene.
    /// </summary>
    public static AutomationElement PromoteToInteractive(AutomationElement element, int maxLevels = 5)
    {
        var walker = TreeWalker.ControlViewWalker;
        var current = element;
        for (var level = 0; level <= maxLevels && current is not null; level++)
        {
            var type = current.Current.ControlType;
            if (InteractiveTypes.Contains(type)) return current;
            if (type == ControlType.Window) break;
            current = walker.GetParent(current);
        }
        return element;
    }

    public static UiElementInfo ToInfo(AutomationElement element)
    {
        var current = element.Current;
        return new UiElementInfo(
            Name: NullIfEmpty(current.Name),
            ControlType: NullIfEmpty(current.LocalizedControlType),
            AutomationId: NullIfEmpty(current.AutomationId),
            Bounds: ToScreenRect(current.BoundingRectangle),
            Value: ReadValue(element),
            IsPassword: current.IsPassword);
    }

    public static UiElementInfo RefreshValue(UiElementInfo info, AutomationElement element)
    {
        try
        {
            return info with { Value = ReadValue(element), Bounds = ToScreenRect(element.Current.BoundingRectangle) ?? info.Bounds };
        }
        catch (ElementNotAvailableException)
        {
            return info;
        }
    }

    public static int? GetProcessId(AutomationElement element)
    {
        try { return element.Current.ProcessId; }
        catch (ElementNotAvailableException) { return null; }
    }

    private static string? ReadValue(AutomationElement element)
    {
        if (element.Current.IsPassword) return null;
        if (!element.TryGetCurrentPattern(ValuePattern.Pattern, out var pattern)) return null;
        var value = ((ValuePattern)pattern).Current.Value;
        return value is { Length: > MaxValueLength } ? value[..MaxValueLength] : value;
    }

    private static ScreenRect? ToScreenRect(System.Windows.Rect rect)
    {
        if (rect.IsEmpty || double.IsInfinity(rect.Width) || double.IsInfinity(rect.Height)) return null;
        var result = ScreenRect.FromEdges(
            (int)Math.Floor(rect.Left), (int)Math.Floor(rect.Top),
            (int)Math.Ceiling(rect.Right), (int)Math.Ceiling(rect.Bottom));
        return result.IsEmpty ? null : result;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
