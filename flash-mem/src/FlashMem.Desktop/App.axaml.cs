using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using FlashMem.Desktop.ViewModels;
using FlashMem.Desktop.Views;
using FlashMem.Infrastructure.Persistence;
using FlashMem.Infrastructure.Security;
using FlashMemAppNotes = FlashMem.Application.Notes;

namespace FlashMem.Desktop;

public partial class App : Avalonia.Application
{
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

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(editorService, store, password),
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
}
