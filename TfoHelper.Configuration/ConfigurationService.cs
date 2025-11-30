using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace TfoHelper.Configuration
{
    /// <summary>
    /// Defines the contract for a service that provides application configuration.
    /// </summary>
    public interface IConfigurationService
    {
        /// <summary>
        /// Gets the main application settings.
        /// </summary>
        AppSettings AppSettings { get; }

        /// <summary>
        /// Gets the CyberArk integration configuration.
        /// </summary>
        CyberArkConfig CyberArkConfig { get; }

        /// <summary>
        /// Gets the configuration mapping for different vaults.
        /// </summary>
        VaultMapConfig VaultMapConfig { get; }

        /// <summary>
        /// Gets the reporting service configuration.
        /// </summary>
        ReportingConfig ReportingConfig { get; }
    }

    /// <summary>
    /// Provides access to application configuration settings loaded from JSON files.
    /// </summary>
    public class ConfigurationService : IConfigurationService
    {
        /// <summary>
        /// Gets the main application settings.
        /// </summary>
        public AppSettings AppSettings { get; private set; }

        /// <summary>
        /// Gets the CyberArk integration configuration.
        /// </summary>
        public CyberArkConfig CyberArkConfig { get; private set; }

        /// <summary>
        /// Gets the configuration mapping for different vaults.
        /// </summary>
        public VaultMapConfig VaultMapConfig { get; private set; }

        /// <summary>
        /// Gets the reporting service configuration.
        /// </summary>
        public ReportingConfig ReportingConfig { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationService"/> class.
        /// Loads configuration from 'appsettings.json', 'cyberark.json', 'vaultmap.json', and 'reporting.json'.
        /// </summary>
        public ConfigurationService()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile("cyberark.json", optional: false, reloadOnChange: true)
                .AddJsonFile("vaultmap.json", optional: false, reloadOnChange: true)
                .AddJsonFile("reporting.json", optional: false, reloadOnChange: true);

            IConfigurationRoot configuration = builder.Build();

            AppSettings = new AppSettings();
            configuration.Bind(AppSettings);

            CyberArkConfig = configuration.Get<CyberArkConfig>() ?? new CyberArkConfig();
            VaultMapConfig = configuration.Get<VaultMapConfig>() ?? new VaultMapConfig();
            ReportingConfig = configuration.Get<ReportingConfig>() ?? new ReportingConfig();
        }
    }
}
