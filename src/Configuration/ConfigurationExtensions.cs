using Microsoft.Extensions.Configuration;

namespace Configuration
{
    public static class ConfigurationExtensions
    {
        public static IConfigurationBuilder AddDefaultConfiguration(this IConfigurationBuilder configBuilder, bool isInCluster)
        {
            configBuilder.AddEnvironmentVariables();

            if (isInCluster)
            {
                configBuilder.AddServiceFabricConfig();
            }

            var environment = configBuilder.Build()["ASPNETCORE_ENVIRONMENT"];

            configBuilder.AddYamlFile("appsettings.yml", true, true);

            if (!string.IsNullOrEmpty(environment))
            {
                configBuilder.AddYamlFile($"appsettings.{environment}.yml", true, true);
            }

            var config = configBuilder.Build();

            var vault = config["KeyVault:Name"];
            var clientId = config["KeyVault:ClientId"];
            var clientSecret = config["KeyVault:ClientSecret"];

            if (vault != null && clientId != null && clientSecret != null)
            {
                configBuilder.AddAzureKeyVault(
                    $"https://{vault}.vault.azure.net/",
                    clientId,
                    clientSecret);
            }

            return configBuilder;
        }
    }
}