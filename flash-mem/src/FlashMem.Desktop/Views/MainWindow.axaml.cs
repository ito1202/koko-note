using Avalonia.Controls;
using Avalonia.Input;
using FlashMem.Desktop.ViewModels;

namespace FlashMem.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Deactivated += (_, _) => Hide();
    }

    private async void OnArchiveClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.ArchiveSelectedAsync();
        }
    }

    private async void OnNewClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.CreateNoteAsync();
        }
    }

    private async void OnDeleteClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.DeleteSelectedAsync();
        }
    }

    private async void OnSaveClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.SaveNowAsync();
        }
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel viewModel)
        {
            return;
        }

        if (e.Key == Key.Up)
        {
            viewModel.MoveSelectionUp();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down)
        {
            viewModel.MoveSelectionDown();
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.Key == Key.S)
        {
            _ = viewModel.SaveNowAsync();
            e.Handled = true;
        }
    }
}
