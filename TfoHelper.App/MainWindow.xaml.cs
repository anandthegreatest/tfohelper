using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using TfoHelper.Logging;
using TfoHelper.Configuration;
using TfoHelper.CyberArk;
using TfoHelper.Reporting;
using TfoHelper.ToadAutomation;
using TfoHelper.WebViewHost;

namespace TfoHelper.App
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml.
    /// Orchestrates the application flow: Login, Metadata Capture, CyberArk Auth, Toad Automation, and Reporting.
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly IConfigurationService _config;
        private readonly ICyberArkService _cyberArk;
        private readonly IToadAutomationService _toadAutomation;
        private readonly IReportingService _reporting;
        private readonly ILogger<MainWindow> _logger;
        private readonly ILogger<LoginWebView> _webViewLogger; // To pass to LoginWebView

        private LoginWebView _loginWebView;
        private string _traceId;
        private Dictionary<string, string> _capturedMetadata;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class.
        /// </summary>
        /// <param name="config">The configuration service.</param>
        /// <param name="cyberArk">The CyberArk service.</param>
        /// <param name="toadAutomation">The Toad automation service.</param>
        /// <param name="reporting">The reporting service.</param>
        /// <param name="logger">The logger for MainWindow.</param>
        /// <param name="webViewLogger">The logger for LoginWebView.</param>
        /// <param name="loggerService">The logger service to access TraceId.</param>
        public MainWindow(
            IConfigurationService config,
            ICyberArkService cyberArk,
            IToadAutomationService toadAutomation,
            IReportingService reporting,
            ILogger<MainWindow> logger,
            ILogger<LoginWebView> webViewLogger,
            LoggerService loggerService) // To get TraceId
        {
            InitializeComponent();
            _config = config;
            _cyberArk = cyberArk;
            _toadAutomation = toadAutomation;
            _reporting = reporting;
            _logger = logger;
            _webViewLogger = webViewLogger;
            _traceId = loggerService.GetTraceId();
            
            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await InitializeWebViewAsync();
        }

        /// <summary>
        /// Initializes the WebView control and sets up event handlers.
        /// </summary>
        private async Task InitializeWebViewAsync()
        {
            try
            {
                _loginWebView = new LoginWebView(_webViewLogger, _config.VaultMapConfig, _config.AppSettings.WebViewUserDataFolder);
                // _loginWebView.MetadataCaptured += OnMetadataCaptured; // Removed as per new flow
                _loginWebView.SamlResponseCaptured += OnSamlResponseCaptured;
                
                WebViewContainer.Children.Add(_loginWebView);
                
                await _loginWebView.InitializeWebView2Async();

                _logger.LogInformation("Navigating to Initial URL: {Url}", _config.AppSettings.InitialUrl);
                _loginWebView.Navigate(_config.AppSettings.InitialUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize WebView.");
                ShowError($"Failed to initialize WebView: {ex.Message}");
            }
        }



        /// <summary>
        /// Handles the SAML response captured event from the WebView.
        /// Initiates the automation flow (CyberArk Logon, Get Password, Launch Toad).
        /// </summary>
        /// <param name="samlResponse">The captured SAML response string.</param>
        /// <param name="metadata">The captured cookies/metadata.</param>
        private async void OnSamlResponseCaptured(string samlResponse, Dictionary<string, string> metadata)
        {
            _logger.LogInformation("SAML Response captured.");
            _capturedMetadata = metadata; // Update metadata from cookies
            
            // UI Update
            WebViewContainer.Visibility = Visibility.Collapsed;
            StatusOverlay.Visibility = Visibility.Visible;
            StatusText.Text = "Authenticating with CyberArk...";

            try
            {
                await ProcessAutomationFlow(samlResponse);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Automation flow failed.");
                ShowError("Automation failed: " + ex.Message);
                await _reporting.ReportFailureAsync(_traceId, _capturedMetadata?["username"] ?? "unknown", ex.Message);
            }
        }

        /// <summary>
        /// Orchestrates the automation steps: CyberArk Logon, Get Password, Launch Toad.
        /// </summary>
        /// <param name="samlResponse">The SAML response used for authentication.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task ProcessAutomationFlow(string samlResponse)
        {
            // Map keys from cookies or fallback
            var vaultName = _capturedMetadata.ContainsKey("vaultName") ? _capturedMetadata["vaultName"] : throw new Exception("vaultName not found in cookies.");
            var ticketId = _capturedMetadata.ContainsKey("TicketId") ? _capturedMetadata["TicketId"] : "N/A";
            
            // AccountId usually maps to username in this context, or we use explicit username if present
            var username = _capturedMetadata.ContainsKey("AccountId") ? _capturedMetadata["AccountId"] : (_capturedMetadata.ContainsKey("username") ? _capturedMetadata["username"] : throw new Exception("AccountId/username not found."));
            
            // objectName usually maps to TNS string
            var tns = _capturedMetadata.ContainsKey("objectName") ? _capturedMetadata["objectName"] : (_capturedMetadata.ContainsKey("TNSString") ? _capturedMetadata["TNSString"] : throw new Exception("objectName/TNSString not found."));

            // 1. Logon
            var token = await _cyberArk.LogonAsync(samlResponse, vaultName);
            
            // 2. Get Password
            StatusText.Text = "Retrieving Password...";
            var password = await _cyberArk.GetPasswordAsync(token, vaultName, ticketId);

            // 3. Launch Toad
            StatusText.Text = "Launching Toad for Oracle...";
            bool success = await _toadAutomation.LaunchAndLoginAsync(tns, username, password);

            if (success)
            {
                StatusText.Text = "Success! Toad Launched.";
                await _reporting.ReportSuccessAsync(_traceId, username);
                await Task.Delay(2000);
                Application.Current.Shutdown();
            }
            else
            {
                throw new Exception("Failed to launch or login to Toad.");
            }
        }

        /// <summary>
        /// Displays an error message to the user.
        /// </summary>
        /// <param name="message">The error message to display.</param>
        private void ShowError(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            // Optionally shutdown or reset
        }
    }
}
