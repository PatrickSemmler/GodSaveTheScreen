using System.Globalization;
using System.Windows.Controls;
using System.Windows.Markup;

namespace GodSaveTheScreen.App.Controls;

/// <summary>Eigenschaften der Auswahl bzw. der Werkzeug-Standardwerte (Form, Text, Marker, Sprechblase …) in aufklappbaren Abschnitten plus Ebenenliste.</summary>
public partial class PropertyPanel : UserControl
{
    public PropertyPanel()
    {
        // Zahlen in Kurzfassungen (StringFormat) im Format der Benutzerkultur statt en-US.
        Language = XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag);
        InitializeComponent();
    }
}
