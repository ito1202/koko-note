using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using FlashMem.Application.Configuration;
using FlashMem.Application.UI;
using FlashMem.Desktop.Services;
using FlashMem.Desktop.ViewModels;
using FlashMem.Desktop.Views;
using FlashMem.Infrastructure.Configuration;
using FlashMem.Infrastructure.Persistence;
using FlashMem.Infrastructure.Security;
using FlashMemAppNotes = FlashMem.Application.Notes;

namespace FlashMem.Desktop;

public partial class App : Avalonia.Application
{
    private readonly bool _isMacOs = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
    private IClassicDesktopStyleApplicationLifetime? _desktopLifetime;
    private MainWindow? _mainWindow;
    private ICursorPositionProvider? _cursorPositionProvider;
    private IGlobalHotkeyService? _hotkeyService;
    private IAppSettingsStore? _settingsStore;
    private IAutoStartService? _autoStartService;
    private AppSettings _settings = AppSettingsDefaults.Create(RuntimeInformation.IsOSPlatform(OSPlatform.OSX));

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _desktopLifetime = desktop;
            DisableAvaloniaDataAnnotationValidation();
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.Startup += OnDesktopStartup;
            desktop.Exit += OnDesktopExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async void OnDesktopStartup(object? sender, ControlledApplicationLifetimeStartupEventArgs e)
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FlashMem");
        Directory.CreateDirectory(appDataPath);

        _settingsStore = new JsonAppSettingsStore(Path.Combine(appDataPath, "settings.json"));
        _autoStartService = new DesktopAutoStartService();
        _settings = await _settingsStore.LoadAsync(_isMacOs);
        await _autoStartService.SetEnabledAsync(_settings.StartAtLoginEnabled);

        var noteStorePath = Path.Combine(appDataPath, "notes.enc.json");
        var password = ResolveEncryptionPassword();
        var noteStore = new EncryptedNoteStore(noteStorePath, new AesGcmArgon2Encryptor());
        var workspace = await noteStore.LoadAsync(password);
        var editorService = new FlashMemAppNotes.NoteEditorService(workspace);
        var mainViewModel = new MainWindowViewModel(editorService, noteStore, password);

        _cursorPositionProvider = new CursorPositionProvider();
        _mainWindow = new MainWindow
        {
            DataContext = mainViewModel,
        };

        if (_desktopLifetime is not null)
        {
            _desktopLifetime.MainWindow = _mainWindow;
        }

        await RestartHotkeyServiceAsync();
        _mainWindow.Hide();
    }

    private void OnDesktopExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        _hotkeyService?.Dispose();
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

    private async Task RestartHotkeyServiceAsync()
    {
        _hotkeyService?.Dispose();
        _hotkeyService = new ModifierDoubleTapGlobalHotkeyService(
            new GlobalHotkeyOptions(
                TapKey: _settings.HotkeyTapKey,
                DoubleTapWindow: TimeSpan.FromMilliseconds(_settings.HotkeyDoubleTapWindowMs)));
        _hotkeyService.Triggered += OnGlobalHotkeyTriggered;
        await _hotkeyService.StartAsync();
    }

    private void OnGlobalHotkeyTriggered(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() => TogglePopup());
    }

    private void TogglePopup()
    {
        if (_mainWindow is null || _cursorPositionProvider is null)
        {
            return;
        }

        ShowPopupAtCursor();
    }

    private void ShowPopupAtCursor()
    {
        if (_mainWindow is null || _cursorPositionProvider is null)
        {
            return;
        }

        var cursor = _cursorPositionProvider.GetCursorPosition();
        var targetScreen = _mainWindow.Screens.ScreenFromPoint(cursor) ?? _mainWindow.Screens.Primary;
        var area = targetScreen?.WorkingArea ?? new PixelRect(0, 0, 1920, 1080);
        var position = PopupPositionCalculator.Calculate(
            cursorX: cursor.X,
            cursorY: cursor.Y,
            popupWidth: (int)_mainWindow.Width,
            popupHeight: (int)_mainWindow.Height,
            workingArea: new ScreenBounds(area.X, area.Y, area.Width, area.Height));

        _mainWindow.Position = new PixelPoint(position.X, position.Y);
        _mainWindow.Show();
        _mainWindow.Activate();
        _mainWindow.Focus();
    }

    private void OpenSettingsWindow()
    {
        if (_mainWindow is null || _settingsStore is null || _autoStartService is null)
        {
            return;
        }

        var settingsWindow = new SettingsWindow(_settings);
        settingsWindow.Closed += async (_, _) =>
        {
            if (settingsWindow.ResultSettings is null)
            {
                return;
            }

            _settings = AppSettingsDefaults.Sanitize(settingsWindow.ResultSettings, _isMacOs);
            await _settingsStore.SaveAsync(_settings);
            await _autoStartService.SetEnabledAsync(_settings.StartAtLoginEnabled);
            await RestartHotkeyServiceAsync();
        };
        settingsWindow.Show();
        settingsWindow.Activate();
    }

    private void DisableAvaloniaDataAnnotationValidation()
    {
        var dataValidationPluginsToRemove =
            BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

        foreach (var plugin in dataValidationPluginsToRemove)
        {
            BindingPlugins.DataValidators.Remove(plugin);
        }
    }

    private void OnTrayClicked(object? sender, EventArgs e)
    {
        TogglePopup();
    }

    private void OnTrayOpenClicked(object? sender, EventArgs e)
    {
        ShowPopupAtCursor();
    }

    private void OnTraySettingsClicked(object? sender, EventArgs e)
    {
        OpenSettingsWindow();
    }

    private void OnTrayExitClicked(object? sender, EventArgs e)
    {
        _desktopLifetime?.Shutdown();
    }
}
