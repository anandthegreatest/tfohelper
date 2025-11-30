using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TfoHelper.Configuration;

namespace TfoHelper.CyberArk
{
    public interface ICyberArkService
    {
        Task<string> LogonAsync(string samlResponse, string vaultName);
        Task<string> GetPasswordAsync(string token, string vaultName, string ticketId, string reason = "Automated Access");
    }

    public class CyberArkService : ICyberArkService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfigurationService _configService;
        private readonly ILogger<CyberArkService> _logger;

        public CyberArkService(HttpClient httpClient, IConfigurationService configService, ILogger<CyberArkService> logger)
        {
            _httpClient = httpClient;
            _configService = configService;
            _logger = logger;
        }

        public async Task<string> LogonAsync(string samlResponse, string vaultName)
        {
            if (!_configService.VaultMapConfig.TryGetValue(vaultName, out var vaultEntry))
            {
                throw new ArgumentException($"Vault '{vaultName}' not found in configuration.");
            }

            var logonUrl = vaultEntry.CyberArkSamlLogonUrl;
            var payload = new
            {
                samlResponse = samlResponse,
                apiUse = _configService.CyberArkConfig.DefaultApiUse,
                concurrentSession = _configService.CyberArkConfig.DefaultConcurrentSession
            };

            // Using FormUrlEncodedContent as SAML Logon usually expects form data or JSON? 
            // Prompt says "Body: samlResponse, apiUse, concurrentSession". Usually CyberArk REST API expects JSON.
            // Let's assume JSON based on "Strongly typed HTTP clients" and typical REST API usage.
            
            try 
            {
                var response = await _httpClient.PostAsJsonAsync(logonUrl, payload);
                response.EnsureSuccessStatusCode();

                var token = await response.Content.ReadAsStringAsync();
                // Strip quotes if present (CyberArk sometimes returns "token")
                return token.Trim('"');
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CyberArk Logon failed for vault {VaultName}", vaultName);
                throw;
            }
        }

        public async Task<string> GetPasswordAsync(string token, string vaultName, string ticketId, string reason = "Automated Access")
        {
            if (!_configService.VaultMapConfig.TryGetValue(vaultName, out var vaultEntry))
            {
                throw new ArgumentException($"Vault '{vaultName}' not found in configuration.");
            }

            var passwordUrl = vaultEntry.CyberArkGetPasswordValueUrl;
            
            var request = new HttpRequestMessage(HttpMethod.Post, passwordUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            
            var payload = new
            {
                TicketingSystem = "Service Now", // Hardcoded per prompt example or could be config
                TicketId = ticketId,
                Reason = reason
            };

            request.Content = JsonContent.Create(payload);

            try
            {
                var response = await _httpClient.SendAsync(request);
                response.EnsureSuccessStatusCode();

                var password = await response.Content.ReadAsStringAsync();
                // Password might be returned as raw string or JSON. 
                // Prompt says "Result: Plain text password".
                // CyberArk GetPasswordValue usually returns the password string directly if using the right endpoint, 
                // or a JSON object. Assuming plain text based on prompt.
                return password.Trim('"'); // Trim quotes just in case it's a JSON string
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CyberArk GetPassword failed for vault {VaultName}", vaultName);
                throw;
            }
        }
    }
}
