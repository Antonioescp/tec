using System.Globalization;

namespace Tec.Core.Services.Captions;

public sealed partial class CaptionLanguage
{
    public static CaptionLanguage English { get; } = new()
    {
        Code = "en",
        Name = "English",
        Culture = CultureInfo.GetCultureInfo("en-US"),
        InteractionKinds = new Dictionary<string, string>
        {
            ["click"] = "Left click",
            ["click-derecho"] = "Right click",
            ["click-central"] = "Middle click",
            ["doble-click"] = "Double click",
            ["comando"] = "Keyboard command",
            ["texto"] = "Text input",
            ["ventana"] = "Window shown",
            ["cambio"] = "Screen change",
        },
        ControlTypes = new Dictionary<string, string>
        {
            ["AppBar"] = "app bar",
            ["Button"] = "button",
            ["Calendar"] = "calendar",
            ["CheckBox"] = "check box",
            ["ComboBox"] = "combo box",
            ["Custom"] = "custom",
            ["DataGrid"] = "data grid",
            ["DataItem"] = "data item",
            ["Document"] = "document",
            ["Edit"] = "edit",
            ["Group"] = "group",
            ["Header"] = "header",
            ["HeaderItem"] = "header item",
            ["Hyperlink"] = "link",
            ["Image"] = "image",
            ["List"] = "list",
            ["ListItem"] = "list item",
            ["Menu"] = "menu",
            ["MenuBar"] = "menu bar",
            ["MenuItem"] = "menu item",
            ["Pane"] = "pane",
            ["ProgressBar"] = "progress bar",
            ["RadioButton"] = "radio button",
            ["ScrollBar"] = "scroll bar",
            ["SemanticZoom"] = "semantic zoom",
            ["Separator"] = "separator",
            ["Slider"] = "slider",
            ["Spinner"] = "spinner",
            ["SplitButton"] = "split button",
            ["StatusBar"] = "status bar",
            ["Tab"] = "tab",
            ["TabItem"] = "tab item",
            ["Table"] = "table",
            ["Text"] = "text",
            ["Thumb"] = "thumb",
            ["TitleBar"] = "title bar",
            ["ToolBar"] = "toolbar",
            ["ToolTip"] = "tooltip",
            ["Tree"] = "tree",
            ["TreeItem"] = "tree item",
            ["Window"] = "window",
        },
        Keys = new Dictionary<string, string>
        {
            ["Delete"] = "Del",
            ["PageUp"] = "PgUp",
            ["PageDown"] = "PgDn",
            ["Left"] = "←",
            ["Up"] = "↑",
            ["Right"] = "→",
            ["Down"] = "↓",
            ["PrintScreen"] = "PrtSc",
        },
        Notes = new Dictionary<string, string>
        {
            ["window-closed"] = "The window closed before the capture",
            ["screen-still-changing"] = "The screen was still changing (animation in progress)",
            ["element-timeout"] = "The application did not respond in time to identify the element",
            ["enrichment-failed"] = "Could not get additional information ({0})",
        },
        UnknownElement = "element",
        WindowFormat = "Window: \"{0}\"",
        PositionFormat = "Position: ({0}, {1})",
        CommandOnElementFormat = "{0} on {1}",
        ContentChangedFormat = "{0:P0} of the window changed",
    };
}
