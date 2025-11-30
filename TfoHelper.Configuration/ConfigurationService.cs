using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace TfoHelper.Configuration
{
    public interface IConfigurationService
    {
        AppSettings AppSettings { get; }
        CyberArkConfig CyberArkConfig { get; }
        VaultMapConfig VaultMapConfig { get; }
        ReportingConfig ReportingConfig { get; }
    }

    public class ConfigurationService : IConfigurationService
    {
        public AppSettings AppSettings { get; private set; }
        public CyberArkConfig CyberArkConfig { get; private set; }
        public VaultMapConfig VaultMapConfig { get; private set; }
        public ReportingConfig ReportingConfig { get; private set; }

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

            CyberArkConfig = new CyberArkConfig();
            configuration.Bind(CyberArkConfig); // Assuming root level binding or specific section if structured differently. 
            // However, the prompt implies separate files. 
            // If they are separate files, we might need to bind them individually or add them as sources.
            // The ConfigurationBuilder merges them. 
            // Let's assume the JSON structure in the files matches the class structure or is at root.
            // Based on prompt:
            // cyberark.json: { "DefaultApiUse": ... } -> Matches CyberArkConfig properties directly.
            // vaultmap.json: { "VAULT_A": ... } -> Matches VaultMapConfig (Dictionary).
            // reporting.json: { "ReportingBaseUrl": ... } -> Matches ReportingConfig.
            
            // Since they are merged into one IConfiguration, we need to be careful about collisions or bind carefully.
            // But here the keys seem distinct enough or we can bind to the root.
            
            CyberArkConfig = configuration.Get<CyberArkConfig>() ?? new CyberArkConfig();
            VaultMapConfig = configuration.Get<VaultMapConfig>() ?? new VaultMapConfig();
            ReportingConfig = configuration.Get<ReportingConfig>() ?? new ReportingConfig();
        }
    }
}
