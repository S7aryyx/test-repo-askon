using System.Windows;
using System.Windows.Controls;
using MiniPdm.Wpf.ViewModels;

namespace MiniPdm.Wpf;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is MainViewModel viewModel && e.NewValue is TreeItemViewModel item)
        {
            viewModel.SelectTreeItem(item);
        }
    }
}
