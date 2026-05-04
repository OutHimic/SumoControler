using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace SumoController.Models
{
    public class AppConfig
    {
        [YamlMember(Alias = "server_url")]
        public string ServerUrl { get; set; } = string.Empty;

        [YamlMember(Alias = "plugins")]
        public List<PluginConfig> Plugins { get; set; } = new List<PluginConfig>();
    }

    public class PluginConfig
    {
        [YamlMember(Alias = "name")]
        public string Name { get; set; } = string.Empty;

        [YamlMember(Alias = "version")]
        public string Version { get; set; } = string.Empty;

        [YamlMember(Alias = "file")]
        public string File { get; set; } = string.Empty;

        [YamlMember(Alias = "enabled")]
        public bool Enabled { get; set; } = true;

        [YamlMember(Alias = "config")]
        public Dictionary<string, object>? Config { get; set; }
    }
}
