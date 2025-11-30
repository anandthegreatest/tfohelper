using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TfoHelper.Configuration;

namespace TfoHelper.Reporting
{
    public interface IReportingService
    {
        Task ReportSuccessAsync(string traceId, string username);
        Task ReportFailureAsync(string traceId, string username, string errorMessage);
    }

    public class ReportingService : IReportingService
    {
        private readonly HttpClient _httpClient;
        private readonly ReportingConfig _config;
        private readonly ILogger<ReportingService> _logger;

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

        public async Task ReportSuccessAsync(string traceId, string username)
        {
            await SendReportAsync(traceId, "success", username, null);
        }

        public async Task ReportFailureAsync(string traceId, string username, string errorMessage)
        {
            await SendReportAsync(traceId, "failure", username, errorMessage);
        }

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
