using System.Windows;
using Microsoft.Win32;
using MiniPdm.Domain.Models;

namespace MiniPdm.Wpf;

public sealed class WpfDialogService : IDialogService
{
    public string? SelectFolder()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Выберите папку CAD"
        };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public void ShowImportReport(ImportResult result)
    {
        var viewModel = new ViewModels.ImportReportViewModel(result);
        var window = new ImportReportWindow { DataContext = viewModel };
        viewModel.CloseRequested += window.Close;
        window.ShowDialog();
    }

    public void ShowMessage(string message, string title)
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
}
