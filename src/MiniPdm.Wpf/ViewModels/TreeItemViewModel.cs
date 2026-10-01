using System.Collections.ObjectModel;

namespace MiniPdm.Wpf.ViewModels;

public sealed class TreeItemViewModel
{
    public Guid ObjectId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Designation { get; init; } = string.Empty;
    public string ObjectType { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public ObservableCollection<TreeItemViewModel> Children { get; } = new();

    public string DisplayName => string.IsNullOrWhiteSpace(Designation)
        ? $"{Name} × {Quantity}"
        : $"{Designation} — {Name} × {Quantity}";
}
