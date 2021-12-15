using System;
using System.Collections.Generic;
using System.IO;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using ComSkipper.Configuration;

namespace ComSkipper
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages, IHasThumbImage
    {
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer) : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
        }

        public override string Name => "Com Skipper";


        public override string Description => "Commercial Skipper for Emby";


        public static Plugin Instance { get; private set; }

        private Guid _id = new Guid("1024CC72-802F-4EFB-89FB-F190AFF2A42E");
                                      
        public override Guid Id => _id;

        public Stream GetThumbImage()
        {
            var type = GetType();
            return type.Assembly.GetManifestResourceStream(type.Namespace + ".logo.png");
        }

        public ImageFormat ThumbImageFormat => ImageFormat.Png;

        public IEnumerable<PluginPageInfo> GetPages() => new[]
        {
            new PluginPageInfo
            {
                Name = "ComSkipperConfigurationPage",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.ComSkipper.html"
            },
            new PluginPageInfo
            {
                Name = "ComSkipperConfigurationPageJS",
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.ComSkipper.js",
            }
        };
    }
}
