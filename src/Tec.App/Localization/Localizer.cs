using System.ComponentModel;
using System.Resources;
using Tec.Core.Localization;

namespace Tec.App.Localization;

/// <summary>
/// Interface texts from <c>Resources/Strings*.resx</c> in the current language. XAML binds
/// to its indexer (<c>{Binding [Start], Source={x:Static loc:Localizer.Instance}}</c>), so
/// changing <see cref="Language"/> updates every text on screen without restarting.
/// </summary>
public sealed class Localizer : INotifyPropertyChanged
{
    private static readonly ResourceManager Resources = new("Tec.App.Resources.Strings", typeof(Localizer).Assembly);

    private TecLanguage _language = TecLanguage.Spanish;
    private Texts _texts = new(Resources, TecLanguage.Spanish.Culture);

    public static Localizer Instance { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public TecLanguage Language
    {
        get => _language;
        set
        {
            if (value == _language) return;
            _language = value;
            _texts = new Texts(Resources, value.Culture);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Language)));
            // "Item[]" tells WPF that every indexer binding must be refreshed.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        }
    }

    public string this[string key] => _texts.Get(key);

    public string Format(string key, params object?[] args) => _texts.Format(key, args);
}
