using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Threading;
using System.Linq;
using Avalonia.Markup.Xaml;
using FlashMem.Application.UI;
using FlashMem.Desktop.Services;
using FlashMem.Desktop.ViewModels;
using FlashMem.Desktop.Views;
using FlashMem.Domain.Input;
using FlashMem.Infrastructure.Persistence;
using FlashMem.Infrastructure.Security;
using FlashMemAppNotes = FlashMem.Application.Notes;

namespace FlashMem.Desktop;

public partial class App : Avalonia.Application
{
    private IGlobalHotkeyService? _hotkeyService;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
            // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
            DisableAvaloniaDataAnnotationValidation();
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "FlashMem");
            var storePath = Path.Combine(appDataPath, "notes.enc.json");
            var password = ResolveEncryptionPassword();
            var store = new EncryptedNoteStore(storePath, new AesGcmArgon2Encryptor());
            var workspace = store.LoadAsync(password).GetAwaiter().GetResult();
            var editorService = new FlashMemAppNotes.NoteEditorService(workspace);
            var cursorPositionProvider = new CursorPositionProvider();
            var mainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(editorService, store, password),
            };

            desktop.MainWindow = mainWindow;
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.Exit += (_, _) => _hotkeyService?.Dispose();
            desktop.Startup += (_, _) =>
            {
                mainWindow.Hide();
                StartHotkeyLoop(mainWindow, cursorPositionProvider);
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        // Get an array of plugins to remove
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        // remove each entry found
        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }

    private static string ResolveEncryptionPassword()
    {
        var password = Environment.GetEnvironmentVariable("FLASH_MEM_PASSWORD");
        if (!string.IsNullOrWhiteSpace(password))
        {
            return password;
        }

        return "flash-mem-dev-only-change-this";
    }

    private void StartHotkeyLoop(MainWindow window, ICursorPositionProvider cursorPositionProvider)
    {
        _hotkeyService = new CtrlDoubleTapGlobalHotkeyService(new DoubleTapHotkeyDetector(TimeSpan.FromMilliseconds(300)));
        _hotkeyService.Triggered += (_, _) =>
        {
            Dispatcher.UIThread.Post(() => TogglePopup(window, cursorPositionProvider));
        };

        _ = _hotkeyService.StartAsync();
    }

    private static void TogglePopup(MainWindow window, ICursorPositionProvider cursorPositionProvider)
    {
        if (window.IsVisible)
        {
            window.Hide();
            return;
        }

        var cursor = cursorPositionProvider.GetCursorPosition();
        var targetScreen = window.Screens.ScreenFromPoint(cursor) ?? window.Screens.Primary;
        var area = targetScreen?.WorkingArea ?? new PixelRect(0, 0, 1920, 1080);
        var position = PopupPositionCalculator.Calculate(
            cursorX: cursor.X,
            cursorY: cursor.Y,
            popupWidth: (int)window.Width,
            popupHeight: (int)window.Height,
            workingArea: new ScreenBounds(area.X, area.Y, area.Width, area.Height));

        window.Position = new PixelPoint(position.X, position.Y);
        window.Show();
        window.Activate();
        window.Focus();
    }
}
