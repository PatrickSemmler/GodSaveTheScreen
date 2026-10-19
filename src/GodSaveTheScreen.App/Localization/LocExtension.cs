using System.ComponentModel;
using System.Windows.Markup;
using GodSaveTheScreen.App.Resources;

namespace GodSaveTheScreen.App.Localization;

/// <summary>XAML-Zugriff auf <c>Strings.resx</c>: <c>{l:Loc Schluessel}</c>. Im Designer wird der Schlüssel angezeigt.</summary>
[MarkupExtensionReturnType(typeof(string))]
public sealed class LocExtension : MarkupExtension
{
    public LocExtension()
    {
    }

    public LocExtension(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        if (LicenseManager.UsageMode == LicenseUsageMode.Designtime || string.IsNullOrEmpty(Key))
        {
            return Key;
        }

        return Strings.ResourceManager.GetString(Key) ?? $"[{Key}]";
    }
}
