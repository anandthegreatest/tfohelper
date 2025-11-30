using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using Microsoft.Extensions.Logging;
using TfoHelper.Configuration;

namespace TfoHelper.Logging
{
    /// <summary>
    /// Implements a custom file-based logger that logs messages in JSON format.
    /// Supports log level filtering and sensitive data redaction.
    /// </summary>
    public class LoggerService : ILogger
    {
        private readonly string _logFilePath;
        private readonly string _traceId;
        private readonly LogLevel _minLevel;
        private readonly object _lock = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="LoggerService"/> class.
        /// </summary>
        /// <param name="config">The configuration service to access logging settings.</param>
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

        /// <summary>
        /// Begins a logical operation scope.
        /// </summary>
        /// <typeparam name="TState">The type of the state to begin scope for.</typeparam>
        /// <param name="state">The identifier for the scope.</param>
        /// <returns>An IDisposable that ends the logical operation scope on disposal.</returns>
        public IDisposable BeginScope<TState>(TState state) => null;

        /// <summary>
        /// Checks if the given log level is enabled.
        /// </summary>
        /// <param name="logLevel">level to be checked.</param>
        /// <returns>true if enabled; false otherwise.</returns>
        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel >= _minLevel;
        }

        /// <summary>
        /// Writes a log entry.
        /// </summary>
        /// <typeparam name="TState">The type of the object to be written.</typeparam>
        /// <param name="logLevel">Entry will be written on this level.</param>
        /// <param name="eventId">Id of the event.</param>
        /// <param name="state">The entry to be written. Can be also an object.</param>
        /// <param name="exception">The exception related to this entry.</param>
        /// <param name="formatter">Function to create a <c>string</c> message of the <paramref name="state"/> and <paramref name="exception"/>.</param>
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
        
        /// <summary>
        /// Gets the unique trace ID for the current logger instance.
        /// </summary>
        /// <returns>A string representing the trace ID.</returns>
        public string GetTraceId() => _traceId;
    }

    /// <summary>
    /// A provider that creates instances of <see cref="LoggerService"/>.
    /// </summary>
    public class FileLoggerProvider : ILoggerProvider
    {
        private readonly LoggerService _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileLoggerProvider"/> class.
        /// </summary>
        /// <param name="logger">The logger instance to provide.</param>
        public FileLoggerProvider(LoggerService logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Creates a new <see cref="ILogger"/> instance.
        /// </summary>
        /// <param name="categoryName">The category name for messages produced by the logger.</param>
        /// <returns>The instance of <see cref="ILogger"/> that was created.</returns>
        public ILogger CreateLogger(string categoryName)
        {
            return _logger;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
        }
    }
}
