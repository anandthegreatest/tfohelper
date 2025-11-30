using System.Collections.Generic;

namespace TfoHelper.Configuration
{
    /// <summary>
    /// Represents the root application settings loaded from the configuration file.
    /// </summary>
    public class AppSettings
    {
        /// <summary>
        /// Gets or sets the initial URL to navigate to in the WebView.
        /// </summary>
        public string InitialUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the directory path where WebView2 user data (cookies, cache) will be stored.
        /// </summary>
        public string WebViewUserDataFolder { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the logging configuration options.
        /// </summary>
        public LoggingOptions Logging { get; set; } = new LoggingOptions();

        /// <summary>
        /// Gets or sets the timeout configuration options.
        /// </summary>
        public TimeoutOptions Timeouts { get; set; } = new TimeoutOptions();

        /// <summary>
        /// Gets or sets the Toad for Oracle automation options.
        /// </summary>
        public ToadOptions Toad { get; set; } = new ToadOptions();
    }

    /// <summary>
    /// Represents the logging configuration settings.
    /// </summary>
    public class LoggingOptions
    {
        /// <summary>
        /// Gets or sets the directory path where log files will be saved.
        /// </summary>
        public string LogDirectory { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the minimum log level (e.g., "INFO", "DEBUG", "ERROR").
        /// </summary>
        public string MinimumLevel { get; set; } = "INFO";
    }

    /// <summary>
    /// Represents the timeout durations for various operations.
    /// </summary>
    public class TimeoutOptions
    {
        /// <summary>
        /// Gets or sets the timeout in seconds for WebView operations.
        /// Default is 120 seconds.
        /// </summary>
        public int WebViewTimeoutSeconds { get; set; } = 120;

        /// <summary>
        /// Gets or sets the timeout in seconds for launching the Toad application.
        /// Default is 60 seconds.
        /// </summary>
        public int ToadLaunchTimeoutSeconds { get; set; } = 60;

        /// <summary>
        /// Gets or sets the timeout in seconds for the Toad login process.
        /// Default is 30 seconds.
        /// </summary>
        public int ToadLoginTimeoutSeconds { get; set; } = 30;
    }

    /// <summary>
    /// Represents the configuration options for automating Toad for Oracle.
    /// </summary>
    public class ToadOptions
    {
        /// <summary>
        /// Gets or sets the file path to the Toad executable.
        /// </summary>
        public string ToadExecutablePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the command-line arguments to pass when starting Toad.
        /// </summary>
        public string Arguments { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents the CyberArk integration configuration settings.
    /// </summary>
    public class CyberArkConfig
    {
        /// <summary>
        /// Gets or sets the default API use method (e.g., "legacy").
        /// </summary>
        public string DefaultApiUse { get; set; } = "legacy";

        /// <summary>
        /// Gets or sets the default setting for concurrent sessions ("yes" or "no").
        /// </summary>
        public string DefaultConcurrentSession { get; set; } = "yes";
    }

    /// <summary>
    /// Represents a collection of vault configurations, mapped by vault name.
    /// </summary>
    public class VaultMapConfig : Dictionary<string, VaultEntry>
    {
    }

    /// <summary>
    /// Represents the configuration for a specific CyberArk vault.
    /// </summary>
    public class VaultEntry
    {
        /// <summary>
        /// Gets or sets the IDP-initiated login URL for the vault.
        /// </summary>
        public string IdpInitiatedUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the URL for CyberArk SAML logon.
        /// </summary>
        public string CyberArkSamlLogonUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the URL for retrieving the password value from CyberArk.
        /// </summary>
        public string CyberArkGetPasswordValueUrl { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents the configuration settings for the reporting service.
    /// </summary>
    public class ReportingConfig
    {
        /// <summary>
        /// Gets or sets the base URL for the reporting API.
        /// </summary>
        public string ReportingBaseUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the specific path for the reporting endpoint.
        /// </summary>
        public string ReportingPath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether HTTPS is required for reporting.
        /// Default is true.
        /// </summary>
        public bool RequireHttps { get; set; } = true;

        /// <summary>
        /// Gets or sets the number of retry attempts for sending a report.
        /// Default is 3.
        /// </summary>
        public int RetryCount { get; set; } = 3;

        /// <summary>
        /// Gets or sets the delay in seconds between retry attempts.
        /// Default is 2 seconds.
        /// </summary>
        public int RetryDelaySeconds { get; set; } = 2;
    }
}
