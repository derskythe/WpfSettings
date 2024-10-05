using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace WpfSettings;


/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public sealed partial class App : Application
{
    // ReSharper disable FieldCanBeMadeReadOnly.Local
    // ReSharper disable InconsistentNaming
    private static readonly Logger Log = LogManager.GetCurrentClassLogger();

    // ReSharper restore InconsistentNaming
    // ReSharper restore FieldCanBeMadeReadOnly.Local
    private ServiceProvider serviceProvider;
    private Logger _Logger;

    public App()
    {
        var nlogFilePath = Path.Combine(Directory.GetCurrentDirectory(), "NLog.config");

        if (!File.Exists(nlogFilePath))
        {
            // Fatal error, just trying to give as more info as we can
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine(@"Fatal error! No logger config file!");

            throw new ApplicationException("Fatal error! No logger config file!");
        }

        _Logger = LogManager.Setup().LoadConfigurationFromFile(nlogFilePath, false).LogFactory.GetCurrentClassLogger();

        try
        {
            var execDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            _Logger.Info($"Started. {execDir}");

            var services = new ServiceCollection();
            ConfigureServices(services);
            serviceProvider = services.BuildServiceProvider();

            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        }
        catch (Exception exp)
        {
            _Logger.Error(exp, exp.Message);
        }
    }

    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        _Logger.Error(e.ExceptionObject as Exception, "Unhandled exception");
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;
        services.AddApplicationInsightsTelemetryWorkerService();

        services.AddSingleton<MainWindow>();
    }
}
