// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using MediaBrowser.Model.Plugins;

namespace ComSkipper.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public bool EnableComSkipper { get; set; } = true;

        public bool DisableMessage { get; set; } = false;

        public bool RealTimeEnabled { get; set; } = false;

        public bool ShowTimeInMessage { get; set; } = false;

        public int MessageDisplayTimeSeconds { get; set; } = 1;

        public string MainMessageText { get; set; } = "Commercial Skipped";
    }
}
