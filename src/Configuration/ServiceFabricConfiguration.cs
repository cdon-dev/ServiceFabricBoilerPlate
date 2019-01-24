using Microsoft.Extensions.PlatformAbstractions;

namespace Configuration
{
    internal class ServiceFabricConfiguration
    {
        public static bool IsInCluster => PlatformServices.Default.Application.ApplicationBasePath.Contains(".Code.");
    }
}