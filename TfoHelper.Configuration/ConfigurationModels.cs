using System.Collections.Generic;

namespace TfoHelper.Configuration
{
    public class AppSettings
    {
        public string InitialUrl { get; set; } = string.Empty;
        public string WebViewUserDataFolder { get; set; } = string.Empty;
        public LoggingOptions Logging { get; set; } = new LoggingOptions();
        public TimeoutOptions Timeouts { get; set; } = new TimeoutOptions();
        public ToadOptions Toad { get; set; } = new ToadOptions();
    }

    public class LoggingOptions
    {
        public string LogDirectory { get; set; } = string.Empty;
        public string MinimumLevel { get; set; } = "INFO";
    }

    public class TimeoutOptions
    {
        public int WebViewTimeoutSeconds { get; set; } = 120;
        public int ToadLaunchTimeoutSeconds { get; set; } = 60;
        public int ToadLoginTimeoutSeconds { get; set; } = 30;
    }

    public class ToadOptions
    {
        public string ToadExecutablePath { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
    }

    public class CyberArkConfig
    {
        public string DefaultApiUse { get; set; } = "legacy";
        public string DefaultConcurrentSession { get; set; } = "yes";
    }

    public class VaultMapConfig : Dictionary<string, VaultEntry>
    {
    }

    public class VaultEntry
    {
        public string IdpInitiatedUrl { get; set; } = string.Empty;
        public string CyberArkSamlLogonUrl { get; set; } = string.Empty;
        public string CyberArkGetPasswordValueUrl { get; set; } = string.Empty;
    }

    public class ReportingConfig
    {
        public string ReportingBaseUrl { get; set; } = string.Empty;
        public string ReportingPath { get; set; } = string.Empty;
        public bool RequireHttps { get; set; } = true;
        public int RetryCount { get; set; } = 3;
        public int RetryDelaySeconds { get; set; } = 2;
    }
}
