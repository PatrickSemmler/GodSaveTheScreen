using System.Windows;

namespace GodSaveTheScreen.App.Services;

public sealed class WindowVisibilityService : IWindowVisibilityService
{
    public IDisposable HideAll()
    {
        var hidden = Application.Current.Windows
            .OfType<Window>()
            .Where(w => w.IsVisible)
            .ToList();
        foreach (var window in hidden)
        {
            window.Hide();
        }

        return new Restorer(hidden);
    }

    private sealed class Restorer : IDisposable
    {
        private readonly List<Window> _windows;

        public Restorer(List<Window> windows) => _windows = windows;

        public void Dispose()
        {
            foreach (var window in _windows)
            {
                window.Show();
            }

            _windows.Clear();
        }
    }
}
