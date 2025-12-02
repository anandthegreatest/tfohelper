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

            try
            {
                var serviceCollection = new ServiceCollection();
                ConfigureServices(serviceCollection);
                _serviceProvider = serviceCollection.BuildServiceProvider();

                _logger = _serviceProvider.GetRequiredService<ILogger<App>>();
                _logger.LogInformation("Application Starting...");

                var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                string errorMessage = $"Startup failed: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}";
                if (ex.InnerException != null)
                {
                    errorMessage += $"\n\nInner Exception: {ex.InnerException.Message}";
                }
                MessageBox.Show(errorMessage, "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
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
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.Services.AddSingleton<ILoggerProvider, FileLoggerProvider>();
            });

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
            
            string message = $"An unexpected error occurred: {e.Exception.Message}";
            if (_logger == null)
            {
                message += "\n\n(Logger was not initialized)";
            }
            
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
            
            string message = "A critical error occurred.";
            if (ex != null)
            {
                message += $" {ex.Message}";
            }
             if (_logger == null)
            {
                message += "\n\n(Logger was not initialized)";
            }

            MessageBox.Show(message, "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
