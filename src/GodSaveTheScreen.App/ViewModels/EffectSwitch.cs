namespace GodSaveTheScreen.App.ViewModels;

/// <summary>
/// Ein/Aus-Schalter für einen optionalen Effekt, der über einen „Aus“-Wert abgebildet wird (Stärke 0, transparente Farbe).
/// Beim Ausschalten wird der bisherige Wert gemerkt und beim Einschalten wiederhergestellt (sonst <c>fallback</c>).
/// </summary>
public sealed class EffectSwitch<T>
    where T : struct
{
    private readonly Func<T, bool> _isOn;
    private readonly T _off;
    private readonly T _fallback;
    private T? _last;

    public EffectSwitch(Func<T, bool> isOn, T off, T fallback)
    {
        _isOn = isOn;
        _off = off;
        _fallback = fallback;
    }

    /// <summary>Schalterstellung zum aktuellen Wert; <c>null</c> bei gemischten Werten.</summary>
    public bool? IsOn(T? current) => current is { } value ? _isOn(value) : null;

    /// <summary>Wert, der zum Umschalten gesetzt werden muss.</summary>
    public T Switch(T? current, bool on)
    {
        if (on)
        {
            return current is { } value && _isOn(value) ? value : _last ?? _fallback;
        }

        if (current is { } previous && _isOn(previous))
        {
            _last = previous;
        }

        return _off;
    }
}
