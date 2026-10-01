using System.Windows.Input;
using MiniPdm.Domain.Models;
using MiniPdm.Wpf.Commands;

namespace MiniPdm.Wpf.ViewModels;

public sealed class ImportReportViewModel
{
    public ImportResult Result { get; }
    public event Action? CloseRequested;
    public ICommand CloseCommand { get; }

    public ImportReportViewModel(ImportResult result)
    {
        Result = result;
        CloseCommand = new RelayCommand(() => CloseRequested?.Invoke());
    }

    public IEnumerable<ImportItem> Items => Result.Items;
    public int AcceptedCount => Result.AcceptedCount;
    public int RejectedCount => Result.RejectedCount;
    public int WarningCount => Result.WarningCount;
}
