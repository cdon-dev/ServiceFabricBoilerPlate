using System.Fabric;
using Microsoft.Extensions.Configuration;

namespace Configuration
{
    public class ServiceFabricConfigurationProvider : ConfigurationProvider
    {
        private readonly CodePackageActivationContext _context;
        private readonly string _packageName;

        public ServiceFabricConfigurationProvider(string packageName)
        {
            _packageName = packageName;
            _context = FabricRuntime.GetActivationContext();
            _context.ConfigurationPackageModifiedEvent += (sender, e) =>
            {
                LoadPackage(e.NewPackage, true);
                OnReload(); // Notify the change
            };
        }

        public override void Load()
        {
            var config = _context.GetConfigurationPackageObject(_packageName);
            LoadPackage(config);
        }

        private void LoadPackage(ConfigurationPackage config, bool reload = false)
        {
            if (reload)
            {
                Data.Clear(); // Rememove the old keys on re-load
            }

            foreach (var section in config.Settings.Sections)
            {
                foreach (var param in section.Parameters)
                {
                    Data[$"{section.Name}:{param.Name}"] = param.IsEncrypted ? param.DecryptValue().ToUnsecureString() : param.Value;
                }
            }
        }
    }
}