using Microsoft.Extensions.Configuration;

namespace Configuration
{
    public class ServiceFabricConfigSource : IConfigurationSource
    {
        public ServiceFabricConfigSource(string packageName)
        {
            PackageName = packageName;
        }

        public string PackageName { get; set; }

        public IConfigurationProvider Build(IConfigurationBuilder builder)
        {
            return new ServiceFabricConfigurationProvider(PackageName);
        }
    }
}