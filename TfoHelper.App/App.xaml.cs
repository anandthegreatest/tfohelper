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
    /// <summary>
    /// Interaction logic for App.xaml.
    /// Manages application startup, dependency injection configuration, and global exception handling.
    /// </summary>
    public partial class App : Application
    {
        private IServiceProvider _serviceProvider;
        private ILogger<App> _logger;

        /// <summary>
        /// Handles the startup event of the application.
        /// Configures services and shows the main window.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The startup event arguments.</param>
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

        /// <summary>
        /// Configures the dependency injection container with application services.
        /// </summary>
        /// <param name="services">The service collection to add services to.</param>
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

        /// <summary>
        /// Handles unhandled exceptions thrown on the UI thread.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The exception event arguments.</param>
        private void App_DispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            _logger?.LogCritical(e.Exception, "Unhandled Dispatcher Exception");
            MessageBox.Show("An unexpected error occurred. Please contact support.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
            Shutdown();
        }

        /// <summary>
        /// Handles unhandled exceptions thrown in the current AppDomain (non-UI thread).
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The exception event arguments.</param>
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            var ex = e.ExceptionObject as Exception;
            _logger?.LogCritical(ex, "Unhandled AppDomain Exception");
            MessageBox.Show("A critical error occurred. The application will terminate.", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        /// <summary>
        /// Handles the exit event of the application.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The exit event arguments.</param>
        private void Application_Exit(object sender, ExitEventArgs e)
        {
            _logger?.LogInformation("Application Exiting...");
        }
    }
}
