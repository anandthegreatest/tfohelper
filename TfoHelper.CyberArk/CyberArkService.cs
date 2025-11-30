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
    /// <summary>
    /// Defines the contract for interacting with the CyberArk API.
    /// </summary>
    public interface ICyberArkService
    {
        /// <summary>
        /// Authenticates with CyberArk using a SAML response.
        /// </summary>
        /// <param name="samlResponse">The SAML response string obtained from the IDP.</param>
        /// <param name="vaultName">The name of the vault to authenticate against.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the authentication token.</returns>
        Task<string> LogonAsync(string samlResponse, string vaultName);

        /// <summary>
        /// Retrieves a password from the CyberArk vault.
        /// </summary>
        /// <param name="token">The authentication token obtained from <see cref="LogonAsync"/>.</param>
        /// <param name="vaultName">The name of the vault containing the password.</param>
        /// <param name="ticketId">The ticket ID associated with the request.</param>
        /// <param name="reason">The reason for retrieving the password. Defaults to "Automated Access".</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the retrieved password.</returns>
        Task<string> GetPasswordAsync(string token, string vaultName, string ticketId, string reason = "Automated Access");
    }

    /// <summary>
    /// Implements the <see cref="ICyberArkService"/> to interact with CyberArk via HTTP APIs.
    /// </summary>
    public class CyberArkService : ICyberArkService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfigurationService _configService;
        private readonly ILogger<CyberArkService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="CyberArkService"/> class.
        /// </summary>
        /// <param name="httpClient">The HTTP client used for making API requests.</param>
        /// <param name="configService">The configuration service for accessing vault settings.</param>
        /// <param name="logger">The logger for logging service activities.</param>
        public CyberArkService(HttpClient httpClient, IConfigurationService configService, ILogger<CyberArkService> logger)
        {
            _httpClient = httpClient;
            _configService = configService;
            _logger = logger;
        }

        /// <summary>
        /// Authenticates with CyberArk using a SAML response.
        /// </summary>
        /// <param name="samlResponse">The SAML response string obtained from the IDP.</param>
        /// <param name="vaultName">The name of the vault to authenticate against.</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the authentication token.</returns>
        /// <exception cref="ArgumentException">Thrown when the specified vault name is not found in the configuration.</exception>
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

        /// <summary>
        /// Retrieves a password from the CyberArk vault.
        /// </summary>
        /// <param name="token">The authentication token obtained from <see cref="LogonAsync"/>.</param>
        /// <param name="vaultName">The name of the vault containing the password.</param>
        /// <param name="ticketId">The ticket ID associated with the request.</param>
        /// <param name="reason">The reason for retrieving the password. Defaults to "Automated Access".</param>
        /// <returns>A task that represents the asynchronous operation. The task result contains the retrieved password.</returns>
        /// <exception cref="ArgumentException">Thrown when the specified vault name is not found in the configuration.</exception>
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
