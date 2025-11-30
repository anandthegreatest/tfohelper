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

            InitializeWebView();
        }

        private void InitializeWebView()
        {
            _loginWebView = new LoginWebView(_webViewLogger, _config.VaultMapConfig, _config.AppSettings.WebViewUserDataFolder);
            _loginWebView.MetadataCaptured += OnMetadataCaptured;
            _loginWebView.SamlResponseCaptured += OnSamlResponseCaptured;
            
            WebViewContainer.Children.Add(_loginWebView);
            
            _logger.LogInformation("Navigating to Initial URL: {Url}", _config.AppSettings.InitialUrl);
            _loginWebView.Navigate(_config.AppSettings.InitialUrl);
        }

        private void OnMetadataCaptured(Dictionary<string, string> metadata)
        {
            _logger.LogInformation("Metadata captured: {Metadata}", string.Join(", ", metadata.Keys));
            _capturedMetadata = metadata;

            // Inject scraping script if not already done by the page interaction? 
            // The prompt says "Inject JavaScript to scrape... Post data back".
            // Assuming the page interaction triggers the post message or we inject a script that listens.
            // In LoginWebView we have InjectScrapingScriptAsync.
            // But here we received the metadata, so scraping is done.
            
            // Validate Vault
            if (metadata.TryGetValue("vaultName", out var vaultName))
            {
                if (_config.VaultMapConfig.TryGetValue(vaultName, out var vaultEntry))
                {
                    _logger.LogInformation("Vault '{VaultName}' found. Navigating to IDP.", vaultName);
                    // Navigate to IdpInitiatedUrl
                    _loginWebView.Navigate(vaultEntry.IdpInitiatedUrl);
                }
                else
                {
                    ShowError($"Vault '{vaultName}' not configured.");
                }
            }
            else
            {
                ShowError("Vault name not found in captured metadata.");
            }
        }

        private async void OnSamlResponseCaptured(string samlResponse)
        {
            _logger.LogInformation("SAML Response captured.");
            
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

        private async Task ProcessAutomationFlow(string samlResponse)
        {
            var vaultName = _capturedMetadata["vaultName"];
            var ticketId = _capturedMetadata.ContainsKey("TicketId") ? _capturedMetadata["TicketId"] : "N/A";
            var username = _capturedMetadata["username"];
            var tns = _capturedMetadata["TNSString"];

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

        private void ShowError(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            // Optionally shutdown or reset
        }
    }
}