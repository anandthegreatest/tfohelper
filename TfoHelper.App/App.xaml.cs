using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TfoHelper.Configuration;
using TfoHelper.Logging;
using TfoHelper.Reporting;
using TfoHelper.CyberArk;
using TfoHelper.ToadAutomation;
using System.Net.Http;

namespace TfoHelper.App
{
    public partial class App : Application
    {
        private IServiceProvider _serviceProvider;
        private ILogger<App> _logger;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            // Global Exception Handling
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;

            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            _serviceProvider = serviceCollection.BuildServiceProvider();

            _logger = _serviceProvider.GetRequiredService<ILogger<App>>();
            _logger.LogInformation("Application Starting...");

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            // Configuration
            services.AddSingleton<IConfigurationService, ConfigurationService>();

            // Logging
            services.AddSingleton<LoggerService>();
            services.AddSingleton<ILoggerProvider, FileLoggerProvider>();
            services.AddLogging(builder =>
            {
                builder.ClearProviders(); // Remove default providers if any
                // We need to register our provider manually or via builder
                // Since we registered ILoggerProvider, AddLogging should pick it up if we use AddProvider?
                // Or we can just use our LoggerService directly.
                // Let's use the standard way:
                var sp = services.BuildServiceProvider(); // Temporary SP to get config/logger service? No, circular dependency.
                // We can register the provider instance later or use a factory.
            });
            // Re-register logging correctly
            services.AddSingleton<ILoggerFactory, LoggerFactory>(sp => new LoggerFactory(new[] { sp.GetRequiredService<ILoggerProvider>() }));
            services.Add(ServiceDescriptor.Singleton(typeof(ILogger<>), typeof(Logger<>)));

            // Http Client
            services.AddHttpClient();

            // Services
            services.AddSingleton<ICyberArkService, CyberArkService>();
            services.AddSingleton<IReportingService, ReportingService>();
            services.AddSingleton<IToadAutomationService, ToadAutomationService>();

            // Windows
            services.AddTransient<MainWindow>();
        }

        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            _logger?.LogCritical(e.Exception, "Unhandled Dispatcher Exception");
            MessageBox.Show("An unexpected error occurred. Please contact support.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
            Shutdown();
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            _logger?.LogCritical(ex, "Unhandled AppDomain Exception");
            MessageBox.Show("A critical error occurred. The application will terminate.", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void Application_Exit(object sender, ExitEventArgs e)
        {
            _logger?.LogInformation("Application Exiting...");
        }
    }
}
