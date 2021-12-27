using MediaBrowser.Model.Plugins;

namespace ComSkipper.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public bool EnableComSkipper { get; set; }

        public bool DisableMessage { get; set; }
    }
}