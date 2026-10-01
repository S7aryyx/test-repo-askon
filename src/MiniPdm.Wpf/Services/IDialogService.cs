using MiniPdm.Domain.Models;

namespace MiniPdm.Wpf;

public interface IDialogService
{
    string? SelectFolder();
    void ShowImportReport(ImportResult result);
    void ShowMessage(string message, string title);
}
