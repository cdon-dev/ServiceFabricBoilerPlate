using Microsoft.Extensions.Configuration;

namespace Configuration
{
    public static class ServiceFabricConfigExtensions
    {
        public static IConfigurationBuilder AddServiceFabricConfig(this IConfigurationBuilder builder, string packageName = "Config")
        {
            return builder.Add(new ServiceFabricConfigSource(packageName));
        }
    }
}