using Avalonia.Controls;
using FlashMem.Application.Configuration;

namespace FlashMem.Desktop.Views;

public partial class SettingsWindow : Window
{
    private static readonly HotkeyTapKey[] SupportedTapKeys =
    [
        HotkeyTapKey.Shift,
        HotkeyTapKey.Control,
        HotkeyTapKey.Alt,
        HotkeyTapKey.Command,
    ];

    public SettingsWindow()
        : this(AppSettingsDefaults.Create(isMacOs: true))
    {
    }

    public SettingsWindow(AppSettings settings)
    {
        InitializeComponent();

        HotkeyCombo.ItemsSource = SupportedTapKeys.Select(ToDisplayName).ToList();
        HotkeyCombo.SelectedIndex = Array.IndexOf(SupportedTapKeys, settings.HotkeyTapKey);
        if (HotkeyCombo.SelectedIndex < 0)
        {
            HotkeyCombo.SelectedIndex = 0;
        }

        DoubleTapWindowTextBox.Text = settings.HotkeyDoubleTapWindowMs.ToString();
        StartAtLoginCheckBox.IsChecked = settings.StartAtLoginEnabled;
    }

    public AppSettings? ResultSettings { get; private set; }

    private void OnCancelClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close();
    }

    private void OnSaveClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var tapKey = SelectedTapKey();
        if (!int.TryParse(DoubleTapWindowTextBox.Text, out var ms))
        {
            ms = 300;
        }

        ms = Math.Clamp(ms, AppSettingsDefaults.MinHotkeyWindowMs, AppSettingsDefaults.MaxHotkeyWindowMs);

        ResultSettings = new AppSettings(
            HotkeyTapKey: tapKey,
            HotkeyDoubleTapWindowMs: ms,
            StartAtLoginEnabled: StartAtLoginCheckBox.IsChecked ?? true);
        Close();
    }

    private HotkeyTapKey SelectedTapKey()
    {
        var index = HotkeyCombo.SelectedIndex;
        if (index < 0 || index >= SupportedTapKeys.Length)
        {
            return HotkeyTapKey.Shift;
        }

        return SupportedTapKeys[index];
    }

    private static string ToDisplayName(HotkeyTapKey key)
    {
        return key switch
        {
            HotkeyTapKey.Shift => "Shift",
            HotkeyTapKey.Control => "Control",
            HotkeyTapKey.Alt => "Alt (Option)",
            HotkeyTapKey.Command => "Command",
            _ => key.ToString(),
        };
    }
}
