using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Logging;
using TfoHelper.Configuration;

namespace TfoHelper.Logging
{
    public class LoggerService : ILogger
    {
        private readonly string _logFilePath;
        private readonly string _traceId;
        private readonly LogLevel _minLevel;
        private readonly object _lock = new object();

        public LoggerService(IConfigurationService config)
        {
            _traceId = Guid.NewGuid().ToString();
            var logDir = config.AppSettings.Logging.LogDirectory;
            var minLevelStr = config.AppSettings.Logging.MinimumLevel;

            if (!Enum.TryParse(minLevelStr, true, out _minLevel))
            {
                _minLevel = LogLevel.Information;
            }

            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            string fileName = $"tfohelper-{timestamp}-{_traceId}.log";
            _logFilePath = Path.Combine(logDir, fileName);
        }

        public IDisposable BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= _minLevel;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            if (string.IsNullOrEmpty(message) && exception == null)
            {
                return;
            }

            // Security: Basic sanitization (naive approach, real world needs more robust masking)
            if (message.Contains("password", StringComparison.OrdinalIgnoreCase) || 
                message.Contains("token", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("SAMLResponse", StringComparison.OrdinalIgnoreCase))
            {
                message = "[REDACTED SENSITIVE DATA]";
            }

            var logEntry = new
            {
                timestamp = DateTime.UtcNow.ToString("o"),
                traceId = _traceId,
                level = logLevel.ToString(),
                message = message,
                eventId = eventId.Id,
                exception = exception?.ToString()
            };

            string jsonLine = JsonSerializer.Serialize(logEntry);

            lock (_lock)
            {
                try
                {
                    File.AppendAllText(_logFilePath, jsonLine + Environment.NewLine);
                }
                catch
                {
                    // Fail silently or write to stderr if file logging fails
                }
            }
        }
        
        public string GetTraceId() => _traceId;
    }

    // Provider to integrate with Microsoft.Extensions.Logging
    public class FileLoggerProvider : ILoggerProvider
    {
        private readonly LoggerService _logger;

        public FileLoggerProvider(LoggerService logger)
        {
            _logger = logger;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return _logger;
        }

        public void Dispose()
        {
        }
    }
}
