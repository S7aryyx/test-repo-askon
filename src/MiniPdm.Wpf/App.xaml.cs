using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using MiniPdm.Application.Interfaces;
using MiniPdm.Application.Services;
using MiniPdm.Infrastructure.Cad;
using MiniPdm.Infrastructure.Database;
using MiniPdm.Infrastructure.Repositories;
using MiniPdm.Wpf.ViewModels;

namespace MiniPdm.Wpf;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _serviceProvider;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        var connectionString = Environment.GetEnvironmentVariable("MINI_PDM_CONNECTION")
            ?? "Host=94.41.85.115;Port=54321;Database=askon_db;Username=postgres;Password=1111";

        services.AddSingleton(new DbSession(connectionString));
        services.AddSingleton<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<IPdmRepository, PdmRepository>();
        services.AddSingleton<IImportLogRepository, ImportLogRepository>();
        services.AddSingleton<ICadFileLocator, CadFileLocator>();
        services.AddSingleton<ICadDocumentReader, JsonCadDocumentReader>();
        services.AddSingleton<IImportService, ImportService>();
        services.AddSingleton<ICalculationService, CalculationService>();
        services.AddSingleton<IVersionService, VersionService>();
        services.AddSingleton<IDialogService, WpfDialogService>();
        services.AddSingleton<MainViewModel>();

        _serviceProvider = services.BuildServiceProvider();
        var mainWindow = new MainWindow { DataContext = _serviceProvider.GetRequiredService<MainViewModel>() };
        MainWindow = mainWindow;
        mainWindow.Show();

        try
        {
            await ((MainViewModel)mainWindow.DataContext).LoadAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Не удалось подключиться к PostgreSQL.\n\n{ex.Message}", "Mini-PDM", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
        }
        base.OnExit(e);
    }
}
