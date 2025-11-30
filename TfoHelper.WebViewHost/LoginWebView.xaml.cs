using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using Microsoft.Extensions.Logging;
using TfoHelper.Configuration;

namespace TfoHelper.WebViewHost
{
    public partial class LoginWebView : UserControl
    {
        private readonly ILogger _logger;
        private readonly VaultMapConfig _vaultMap;
        private string _userDataFolder;

        public event Action<Dictionary<string, string>> MetadataCaptured;
        public event Action<string> SamlResponseCaptured;

        public LoginWebView(ILogger logger, VaultMapConfig vaultMap, string userDataFolder)
        {
            InitializeComponent();
            _logger = logger;
            _vaultMap = vaultMap;
            _userDataFolder = userDataFolder;
            InitializeAsync();
        }

        // Default constructor for XAML designer (optional, but good practice)
        public LoginWebView()
        {
            InitializeComponent();
        }

        private async void InitializeAsync()
        {
            try
            {
                var env = await CoreWebView2Environment.CreateAsync(userDataFolder: _userDataFolder);
                await webView.EnsureCoreWebView2Async(env);

                webView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
                webView.CoreWebView2.WebResourceRequested += CoreWebView2_WebResourceRequested;
                
                // Add filter for SAML interception (echo.asp)
                webView.CoreWebView2.AddWebResourceRequestedFilter("*echo.asp*", CoreWebView2WebResourceContext.All);
            }
            catch (Exception ex)
            {
                // _logger?.LogError(ex, "Failed to initialize WebView2");
                // Handle initialization error
            }
        }

        public void Navigate(string url)
        {
            if (webView.CoreWebView2 != null)
            {
                webView.CoreWebView2.Navigate(url);
            }
        }

        private void CoreWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                var json = e.TryGetWebMessageAsString();
                var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                
                // Validate required fields
                if (data != null && data.ContainsKey("vaultName"))
                {
                    MetadataCaptured?.Invoke(data);
                }
            }
            catch (Exception ex)
            {
                // _logger?.LogError(ex, "Error processing web message");
            }
        }

        private async void CoreWebView2_WebResourceRequested(object sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            // Check if this is the SAML response POST
            // The prompt says "Detect POST request to echo.asp"
            // And "Intercept request body and extract the samlResponse string"
            
            var request = e.Request;
            if (request.Method == "POST" && request.Uri.Contains("echo.asp"))
            {
                // We need to read the request body.
                // WebView2 allows getting the content.
                if (request.Content != null)
                {
                    try 
                    {
                        using (var stream = request.Content)
                        using (var reader = new StreamReader(stream))
                        {
                            var body = await reader.ReadToEndAsync();
                            // Body is likely URL encoded: SAMLResponse=...
                            // We need to parse it.
                            var samlResponse = ExtractSamlResponse(body);
                            if (!string.IsNullOrEmpty(samlResponse))
                            {
                                SamlResponseCaptured?.Invoke(samlResponse);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        // _logger?.LogError(ex, "Error reading SAML response");
                    }
                }
            }
        }

        private string ExtractSamlResponse(string body)
        {
            // Simple parsing for "SAMLResponse="
            // In a real app, use System.Web.HttpUtility.ParseQueryString
            var parts = body.Split('&');
            foreach (var part in parts)
            {
                if (part.StartsWith("SAMLResponse="))
                {
                    var encoded = part.Substring("SAMLResponse=".Length);
                    return System.Net.WebUtility.UrlDecode(encoded);
                }
            }
            return null;
        }

        public async Task InjectScrapingScriptAsync()
        {
            // Example script to scrape data and post back
            // This should be refined based on actual page structure
            string script = @"
                (function() {
                    function scrape() {
                        // Replace these selectors with actual ones
                        var data = {
                            ObjectID: document.getElementById('ObjectID')?.value,
                            accountID: document.getElementById('accountID')?.value,
                            vaultName: document.getElementById('vaultName')?.value,
                            TNSString: document.getElementById('TNSString')?.value,
                            username: document.getElementById('username')?.value,
                            TicketId: document.getElementById('TicketId')?.value
                        };
                        window.chrome.webview.postMessage(JSON.stringify(data));
                    }
                    // Attach to a button or run periodically?
                    // Prompt says 'User interacts... Inject JavaScript to scrape'
                    // Maybe we attach to a specific event or just run it when requested.
                    scrape();
                })();
            ";
            await webView.CoreWebView2.ExecuteScriptAsync(script);
        }
    }
}
