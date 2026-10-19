using System.Globalization;

namespace GodSaveTheScreen.App.Localization;

public static class CultureInitializer
{
    /// <summary>Setzt die UI-Kultur aus der Einstellung (<c>System</c> | <c>de</c> | <c>en</c>).</summary>
    public static void Apply(string? language)
    {
        if (string.IsNullOrWhiteSpace(language) || language.Equals("System", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        CultureInfo culture;
        try
        {
            culture = CultureInfo.GetCultureInfo(language);
        }
        catch (CultureNotFoundException)
        {
            return;
        }

        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }
}
