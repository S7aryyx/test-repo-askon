using System.Collections.ObjectModel;
using MiniPdm.Application.Interfaces;
using MiniPdm.Domain.Entities;
using MiniPdm.Domain.Enums;
using MiniPdm.Domain.Models;
using MiniPdm.Wpf.Commands;
using MiniPdm.Wpf;

namespace MiniPdm.Wpf.ViewModels;

public sealed class MainViewModel : BaseViewModel
{
    private readonly IPdmRepository _repository;
    private readonly IImportService _importService;
    private readonly ICalculationService _calculationService;
    private readonly IVersionService _versionService;
    private readonly IDialogService _dialogService;

    private string _searchText = string.Empty;
    private TreeItemViewModel? _selectedTreeItem;
    private PdmObject? _selectedAssembly;
    private PdmObject? _selectedObject;
    private ObjectVersion? _selectedVersion;
    private string _massResult = string.Empty;
    private bool _isBusy;

    public ObservableCollection<PdmObject> Assemblies { get; } = new();
    public ObservableCollection<PdmObject> SearchResults { get; } = new();
    public ObservableCollection<TreeItemViewModel> Tree { get; } = new();
    public ObservableCollection<SpecificationRow> Specification { get; } = new();

    public string SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }

    public PdmObject? SelectedAssembly
    {
        get => _selectedAssembly;
        set
        {
            if (SetProperty(ref _selectedAssembly, value) && value is not null)
                _ = LoadObjectAsync(value.Id);
        }
    }

    public TreeItemViewModel? SelectedTreeItem
    {
        get => _selectedTreeItem;
        private set => SetProperty(ref _selectedTreeItem, value);
    }

    public void SelectTreeItem(TreeItemViewModel item)
    {
        SelectedTreeItem = item;
        _ = LoadObjectAsync(item.ObjectId);
    }

    public PdmObject? SelectedObject { get => _selectedObject; private set => SetProperty(ref _selectedObject, value); }

    public ObjectVersion? SelectedVersion
    {
        get => _selectedVersion;
        private set
        {
            if (SetProperty(ref _selectedVersion, value))
            {
                OnPropertyChanged(nameof(CanApprove));
                OnPropertyChanged(nameof(CanCancel));
                OnPropertyChanged(nameof(StateText));
                OnPropertyChanged(nameof(VersionText));
                OnPropertyChanged(nameof(MaterialText));
                OnPropertyChanged(nameof(MassText));
            }
        }
    }

    public string StateText => SelectedVersion?.State.ToString() ?? "—";
    public string VersionText => SelectedVersion?.VersionNo.ToString() ?? "—";
    public string MaterialText => SelectedVersion?.Material ?? "—";
    public string MassText => SelectedVersion?.MassKg?.ToString("0.######") ?? "—";
    public string MassResult { get => _massResult; private set => SetProperty(ref _massResult, value); }
    public bool CanApprove => SelectedVersion?.State == ObjectState.InWork;
    public bool CanCancel => SelectedVersion?.State is ObjectState.InWork or ObjectState.Approved;
    public bool IsBusy { get => _isBusy; private set => SetProperty(ref _isBusy, value); }

    public AsyncRelayCommand ImportCommand { get; }
    public AsyncRelayCommand SearchCommand { get; }
    public AsyncRelayCommand LoadAssembliesCommand { get; }
    public AsyncRelayCommand CalculateMassCommand { get; }
    public AsyncRelayCommand LoadSpecificationCommand { get; }
    public AsyncRelayCommand ApproveCommand { get; }
    public AsyncRelayCommand CancelCommand { get; }

    public MainViewModel(IPdmRepository repository, IImportService importService, ICalculationService calculationService, IVersionService versionService, IDialogService dialogService)
    {
        _repository = repository;
        _importService = importService;
        _calculationService = calculationService;
        _versionService = versionService;
        _dialogService = dialogService;

        ImportCommand = new AsyncRelayCommand(ImportAsync);
        SearchCommand = new AsyncRelayCommand(SearchAsync);
        LoadAssembliesCommand = new AsyncRelayCommand(LoadAssembliesAsync);
        CalculateMassCommand = new AsyncRelayCommand(CalculateMassAsync);
        LoadSpecificationCommand = new AsyncRelayCommand(LoadSpecificationAsync);
        ApproveCommand = new AsyncRelayCommand(() => ChangeStateAsync(ObjectState.Approved));
        CancelCommand = new AsyncRelayCommand(() => ChangeStateAsync(ObjectState.Cancelled));
    }

    public Task LoadAsync() => LoadAssembliesAsync();

    private async Task ImportAsync()
    {
        var folder = _dialogService.SelectFolder();
        if (folder is null) return;

        await RunBusyAsync(async () =>
        {
            try
            {
                var result = await _importService.ImportAsync(folder, CancellationToken.None);
                await LoadAssembliesAsync();
                _dialogService.ShowImportReport(result);
            }
            catch (Exception ex)
            {
                _dialogService.ShowMessage(ex.Message, "Ошибка импорта");
            }
        });
    }

    private async Task SearchAsync()
    {
        SearchResults.Clear();
        if (string.IsNullOrWhiteSpace(SearchText)) return;
        var result = await _repository.SearchAsync(SearchText.Trim(), CancellationToken.None);
        foreach (var item in result) SearchResults.Add(item);
    }

    private async Task LoadAssembliesAsync()
    {
        var result = await _repository.GetAssembliesAsync(CancellationToken.None);
        Assemblies.Clear();
        foreach (var item in result) Assemblies.Add(item);
        if (SelectedAssembly is not null)
            SelectedAssembly = Assemblies.FirstOrDefault(x => x.Id == SelectedAssembly.Id);
    }

    private async Task LoadObjectAsync(Guid objectId)
    {
        var obj = await _repository.GetByIdAsync(objectId, CancellationToken.None);
        if (obj is null) return;

        SelectedObject = obj;
        SelectedVersion = await _repository.GetCurrentVersionAsync(obj.Id, CancellationToken.None);
        Tree.Clear();
        Specification.Clear();
        MassResult = string.Empty;

        if (obj.ObjectType != ObjectType.Assembly) return;

        var tree = await _calculationService.GetTreeAsync(obj.Id, CancellationToken.None);
        BuildTree(tree);
        var specification = await _calculationService.GetSpecificationAsync(obj.Id, CancellationToken.None);
        foreach (var row in specification) Specification.Add(row);
    }

    private void BuildTree(IReadOnlyList<BomNode> nodes)
    {
        var items = nodes.ToDictionary(
            x => string.Join("/", x.Path),
            x => new TreeItemViewModel
            {
                ObjectId = x.ObjectId,
                Name = x.Name,
                Designation = x.Designation ?? "—",
                ObjectType = x.ObjectType,
                Quantity = x.Quantity
            });

        foreach (var node in nodes.OrderBy(x => x.Level))
        {
            var key = string.Join("/", node.Path);
            var current = items[key];
            if (node.Path.Length == 1)
            {
                Tree.Add(current);
                continue;
            }

            var parentKey = string.Join("/", node.Path.Take(node.Path.Length - 1));
            if (items.TryGetValue(parentKey, out var parent))
                parent.Children.Add(current);
        }
    }

    private async Task CalculateMassAsync()
    {
        if (SelectedObject?.ObjectType != ObjectType.Assembly) return;
        var result = await _calculationService.CalculateMassAsync(SelectedObject.Id, CancellationToken.None);
        MassResult = result.Success
            ? $"Масса: {result.MassKg:0.######} кг"
            : $"{result.Error} Компоненты без массы: {string.Join(", ", result.MissingMassItems)}";
    }

    private async Task LoadSpecificationAsync()
    {
        if (SelectedObject?.ObjectType != ObjectType.Assembly) return;
        Specification.Clear();
        var rows = await _calculationService.GetSpecificationAsync(SelectedObject.Id, CancellationToken.None);
        foreach (var row in rows) Specification.Add(row);
    }

    private async Task ChangeStateAsync(ObjectState state)
    {
        if (SelectedVersion is null) return;
        try
        {
            await _versionService.ChangeStateAsync(SelectedVersion.Id, state, CancellationToken.None);
            if (SelectedObject is not null) await LoadObjectAsync(SelectedObject.Id);
            await LoadAssembliesAsync();
        }
        catch (Exception ex)
        {
            _dialogService.ShowMessage(ex.Message, "Смена состояния");
        }
    }

    private async Task RunBusyAsync(Func<Task> action)
    {
        IsBusy = true;
        try { await action(); }
        finally { IsBusy = false; }
    }
}
