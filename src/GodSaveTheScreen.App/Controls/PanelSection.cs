using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace GodSaveTheScreen.App.Controls;

/// <summary>Merkt sich, welche Abschnitte des Eigenschaftenpanels aufgeklappt sind.</summary>
public interface IPanelSectionStore
{
    bool IsExpanded(string key, bool fallback);

    void SetExpanded(string key, bool expanded);
}

/// <summary>
/// Aufklappbare Karte im Eigenschaftenpanel: Kopf mit Titel, Kurzfassung (nur zugeklappt) und optionalem Ein/Aus-Schalter.
/// Ist der Schalter aus, bleibt der Inhalt verborgen. Der Zustand wird über <see cref="StoreProperty"/> (vererbt) unter <see cref="SectionKey"/> gemerkt.
/// </summary>
[TemplatePart(Name = "PART_Switch", Type = typeof(ToggleButton))]
public class PanelSection : HeaderedContentControl
{
    public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(
        nameof(IsExpanded), typeof(bool), typeof(PanelSection),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, (d, _) => ((PanelSection)d).OnIsExpandedChanged()));

    public static readonly DependencyProperty SummaryProperty = DependencyProperty.Register(
        nameof(Summary), typeof(object), typeof(PanelSection));

    public static readonly DependencyProperty HasSwitchProperty = DependencyProperty.Register(
        nameof(HasSwitch), typeof(bool), typeof(PanelSection));

    /// <summary>Schalterstellung; <c>null</c> = gemischt (mehrere Objekte), der Inhalt bleibt dann sichtbar.</summary>
    public static readonly DependencyProperty IsOnProperty = DependencyProperty.Register(
        nameof(IsOn), typeof(bool?), typeof(PanelSection),
        new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty SectionKeyProperty = DependencyProperty.Register(
        nameof(SectionKey), typeof(string), typeof(PanelSection), new PropertyMetadata(null, (d, _) => ((PanelSection)d).LoadState()));

    public static readonly DependencyProperty DefaultExpandedProperty = DependencyProperty.Register(
        nameof(DefaultExpanded), typeof(bool), typeof(PanelSection), new PropertyMetadata(true, (d, _) => ((PanelSection)d).LoadState()));

    public static readonly DependencyProperty StoreProperty = DependencyProperty.RegisterAttached(
        "Store", typeof(IPanelSectionStore), typeof(PanelSection),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits, (d, _) => (d as PanelSection)?.LoadState()));

    private bool _loading;
    private ToggleButton? _switch;

    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public object? Summary
    {
        get => GetValue(SummaryProperty);
        set => SetValue(SummaryProperty, value);
    }

    public bool HasSwitch
    {
        get => (bool)GetValue(HasSwitchProperty);
        set => SetValue(HasSwitchProperty, value);
    }

    public bool? IsOn
    {
        get => (bool?)GetValue(IsOnProperty);
        set => SetValue(IsOnProperty, value);
    }

    public string? SectionKey
    {
        get => (string?)GetValue(SectionKeyProperty);
        set => SetValue(SectionKeyProperty, value);
    }

    public bool DefaultExpanded
    {
        get => (bool)GetValue(DefaultExpandedProperty);
        set => SetValue(DefaultExpandedProperty, value);
    }

    public static IPanelSectionStore? GetStore(DependencyObject element) => (IPanelSectionStore?)element.GetValue(StoreProperty);

    public static void SetStore(DependencyObject element, IPanelSectionStore? value) => element.SetValue(StoreProperty, value);

    public override void OnApplyTemplate()
    {
        if (_switch is not null)
        {
            _switch.Click -= OnSwitchClick;
        }

        base.OnApplyTemplate();
        _switch = GetTemplateChild("PART_Switch") as ToggleButton;
        if (_switch is not null)
        {
            _switch.Click += OnSwitchClick;
        }
    }

    /// <summary>Wer den Schalter einschaltet, will die Werte meist gleich anpassen: Abschnitt aufklappen.</summary>
    private void OnSwitchClick(object sender, RoutedEventArgs e)
    {
        if (_switch?.IsChecked == true)
        {
            IsExpanded = true;
        }
    }

    private void LoadState()
    {
        _loading = true;
        try
        {
            IsExpanded = GetStore(this) is { } store && SectionKey is { } key ? store.IsExpanded(key, DefaultExpanded) : DefaultExpanded;
        }
        finally
        {
            _loading = false;
        }
    }

    private void OnIsExpandedChanged()
    {
        if (!_loading && GetStore(this) is { } store && SectionKey is { } key)
        {
            store.SetExpanded(key, IsExpanded);
        }
    }
}
