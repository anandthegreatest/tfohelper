using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TfoHelper.Configuration;

namespace TfoHelper.Reporting
{
    /// <summary>
    /// Defines the contract for reporting automation results (success or failure) to a central service.
    /// </summary>
    public interface IReportingService
    {
        /// <summary>
        /// Reports a successful automation run.
        /// </summary>
        /// <param name="traceId">The unique trace ID of the operation.</param>
        /// <param name="username">The username associated with the operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task ReportSuccessAsync(string traceId, string username);

        /// <summary>
        /// Reports a failed automation run.
        /// </summary>
        /// <param name="traceId">The unique trace ID of the operation.</param>
        /// <param name="username">The username associated with the operation.</param>
        /// <param name="errorMessage">The error message describing the failure.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        Task ReportFailureAsync(string traceId, string username, string errorMessage);
    }

    /// <summary>
    /// Implements the <see cref="IReportingService"/> to send reports via HTTP POST.
    /// </summary>
    public class ReportingService : IReportingService
    {
        private readonly HttpClient _httpClient;
        private readonly ReportingConfig _config;
        private readonly ILogger<ReportingService> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReportingService"/> class.
        /// </summary>
        /// <param name="httpClient">The HTTP client used for sending reports.</param>
        /// <param name="configService">The configuration service to access reporting settings.</param>
        /// <param name="logger">The logger for logging service activities.</param>
        public ReportingService(HttpClient httpClient, IConfigurationService configService, ILogger<ReportingService> logger)
        {
            _httpClient = httpClient;
            _config = configService.ReportingConfig;
            _logger = logger;

            if (_config.RequireHttps && _config.ReportingBaseUrl.StartsWith("http://"))
            {
                // Enforce HTTPS if configured
                _config.ReportingBaseUrl = _config.ReportingBaseUrl.Replace("http://", "https://");
            }
            
            _httpClient.BaseAddress = new Uri(_config.ReportingBaseUrl);
        }

        /// <summary>
        /// Reports a successful automation run.
        /// </summary>
        /// <param name="traceId">The unique trace ID of the operation.</param>
        /// <param name="username">The username associated with the operation.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task ReportSuccessAsync(string traceId, string username)
        {
            await SendReportAsync(traceId, "success", username, null);
        }

        /// <summary>
        /// Reports a failed automation run.
        /// </summary>
        /// <param name="traceId">The unique trace ID of the operation.</param>
        /// <param name="username">The username associated with the operation.</param>
        /// <param name="errorMessage">The error message describing the failure.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        public async Task ReportFailureAsync(string traceId, string username, string errorMessage)
        {
            await SendReportAsync(traceId, "failure", username, errorMessage);
        }

        /// <summary>
        /// Sends the report payload to the configured reporting endpoint with retry logic.
        /// </summary>
        /// <param name="traceId">The unique trace ID.</param>
        /// <param name="status">The status of the operation ("success" or "failure").</param>
        /// <param name="username">The username.</param>
        /// <param name="errorMessage">The error message (if any).</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private async Task SendReportAsync(string traceId, string status, string username, string errorMessage)
        {
            var report = new
            {
                traceId = traceId,
                status = status,
                username = username,
                hostname = Environment.MachineName,
                timestamp = DateTime.UtcNow,
                error = errorMessage
            };

            int retryCount = 0;
            while (retryCount <= _config.RetryCount)
            {
                try
                {
                    var response = await _httpClient.PostAsJsonAsync(_config.ReportingPath, report);
                    if (response.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Report sent successfully for TraceId: {TraceId}", traceId);
                        return;
                    }
                    else
                    {
                        _logger.LogWarning("Failed to send report. Status: {StatusCode}", response.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception sending report for TraceId: {TraceId}", traceId);
                }

                retryCount++;
                if (retryCount <= _config.RetryCount)
                {
                    await Task.Delay(TimeSpan.FromSeconds(_config.RetryDelaySeconds));
                }
            }
            
            _logger.LogError("Failed to send report after {RetryCount} attempts.", _config.RetryCount);
        }
    }
}
