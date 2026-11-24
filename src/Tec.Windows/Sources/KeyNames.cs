namespace Tec.Windows.Sources;

/// <summary>Classification and readable names of virtual keys.</summary>
internal static class KeyNames
{
    public const int VK_BACK = 0x08, VK_TAB = 0x09, VK_RETURN = 0x0D, VK_SHIFT = 0x10, VK_CONTROL = 0x11, VK_MENU = 0x12;
    public const int VK_PAUSE = 0x13, VK_CAPITAL = 0x14, VK_ESCAPE = 0x1B, VK_SPACE = 0x20;
    public const int VK_PRIOR = 0x21, VK_NEXT = 0x22, VK_END = 0x23, VK_HOME = 0x24;
    public const int VK_LEFT = 0x25, VK_UP = 0x26, VK_RIGHT = 0x27, VK_DOWN = 0x28;
    public const int VK_SNAPSHOT = 0x2C, VK_INSERT = 0x2D, VK_DELETE = 0x2E;
    public const int VK_LWIN = 0x5B, VK_RWIN = 0x5C, VK_APPS = 0x5D;
    public const int VK_NUMPAD0 = 0x60, VK_DIVIDE = 0x6F, VK_F1 = 0x70, VK_F24 = 0x87;
    public const int VK_NUMLOCK = 0x90, VK_SCROLL = 0x91;
    public const int VK_LSHIFT = 0xA0, VK_RSHIFT = 0xA1, VK_LCONTROL = 0xA2, VK_RCONTROL = 0xA3, VK_LMENU = 0xA4, VK_RMENU = 0xA5;
    public const int VK_OEM_1 = 0xBA, VK_OEM_8 = 0xDF, VK_OEM_102 = 0xE2;

    public static bool IsShift(int vk) => vk is VK_SHIFT or VK_LSHIFT or VK_RSHIFT;
    public static bool IsControl(int vk) => vk is VK_CONTROL or VK_LCONTROL or VK_RCONTROL;
    public static bool IsAlt(int vk) => vk is VK_MENU or VK_LMENU or VK_RMENU;
    public static bool IsWin(int vk) => vk is VK_LWIN or VK_RWIN;
    public static bool IsModifier(int vk) => IsShift(vk) || IsControl(vk) || IsAlt(vk) || IsWin(vk);

    public static bool IsFunctionKey(int vk) => vk is >= VK_F1 and <= VK_F24;

    /// <summary>Keys that produce (or delete) text inside a field.</summary>
    public static bool IsTextKey(int vk) =>
        vk is (>= 0x30 and <= 0x39) or (>= 0x41 and <= 0x5A) // digits and letters
            or (>= VK_NUMPAD0 and <= VK_DIVIDE)
            or (>= VK_OEM_1 and <= VK_OEM_8) or VK_OEM_102
            or VK_SPACE or VK_BACK;

    /// <summary>Navigation or state keys that do not produce evidence on their own.</summary>
    public static bool IsNavigationKey(int vk) =>
        vk is VK_TAB or (>= VK_PRIOR and <= VK_DOWN) or VK_INSERT or VK_CAPITAL or VK_NUMLOCK or VK_SCROLL;

    /// <summary>Keys of a combination, as language-independent names (e.g. ["Ctrl", "S"]).</summary>
    public static IReadOnlyList<string> Combination(bool ctrl, bool alt, bool shift, bool win, int vk)
    {
        var keys = new List<string>(5);
        if (ctrl) keys.Add("Ctrl");
        if (alt) keys.Add("Alt");
        if (shift) keys.Add("Shift");
        if (win) keys.Add("Win");
        keys.Add(Name(vk));
        return keys;
    }

    /// <summary>
    /// Language-independent key name. The caption formatter translates it
    /// (e.g. "Delete" is "Supr" in Spanish).
    /// </summary>
    public static string Name(int vk) => vk switch
    {
        VK_RETURN => "Enter",
        VK_ESCAPE => "Esc",
        VK_TAB => "Tab",
        VK_BACK => "Backspace",
        VK_SPACE => "Space",
        VK_DELETE => "Delete",
        VK_INSERT => "Insert",
        VK_HOME => "Home",
        VK_END => "End",
        VK_PRIOR => "PageUp",
        VK_NEXT => "PageDown",
        VK_LEFT => "Left",
        VK_UP => "Up",
        VK_RIGHT => "Right",
        VK_DOWN => "Down",
        VK_APPS => "Menu",
        VK_PAUSE => "Pause",
        VK_SNAPSHOT => "PrintScreen",
        >= VK_F1 and <= VK_F24 => $"F{vk - VK_F1 + 1}",
        >= 0x30 and <= 0x39 or >= 0x41 and <= 0x5A => ((char)vk).ToString(),
        >= VK_NUMPAD0 and <= VK_NUMPAD0 + 9 => $"Num{vk - VK_NUMPAD0}",
        _ => $"0x{vk:X2}",
    };
}
