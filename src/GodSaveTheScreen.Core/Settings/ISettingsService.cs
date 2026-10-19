namespace GodSaveTheScreen.Core.Settings;

public interface ISettingsService
{
    AppSettings Current { get; set; }

    event EventHandler? Changed;

    Task SaveAsync(CancellationToken cancellationToken = default);
}
