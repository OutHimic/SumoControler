using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using SumoController.Models;

namespace SumoController
{
    public class UpdateManager
    {
        private static readonly string BaseDir = AppContext.BaseDirectory;
        private static readonly string LocalConfigPath = Path.Combine(BaseDir, "config.yml");
        private static readonly string PluginsDir = Path.Combine(BaseDir, "plugins");

        public static async Task<AppConfig> SyncConfigAndPluginsAsync()
        {
            // 确保插件目录存在
            if (!Directory.Exists(PluginsDir))
            {
                Directory.CreateDirectory(PluginsDir);
            }

            // 1. 读取或生成本地配置获取 ServerUrl
            string serverUrl = "http://manager.craftime.cn:50000";
            if (!File.Exists(LocalConfigPath))
            {
                var defaultConfig = $"server_url: \"{serverUrl}\"\nplugins: []";
                File.WriteAllText(LocalConfigPath, defaultConfig);
            }
            else
            {
                var localYaml = File.ReadAllText(LocalConfigPath);
                var localDeserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
                var localCfg = localDeserializer.Deserialize<AppConfig>(localYaml);
                if (localCfg != null && !string.IsNullOrEmpty(localCfg.ServerUrl))
                {
                    serverUrl = localCfg.ServerUrl;
                }
            }

            Console.WriteLine($"[Update] Using Server URL: {serverUrl}");

            // 2. 从服务端下载最新配置
            using var client = new HttpClient();
            string remoteConfigUrl = $"{serverUrl.TrimEnd('/')}/static/config.yml";
            string remoteYaml;
            try
            {
                Console.WriteLine("[Update] Fetching latest config from server...");
                remoteYaml = await client.GetStringAsync(remoteConfigUrl);
                File.WriteAllText(LocalConfigPath, remoteYaml); // 覆盖本地配置
                Console.WriteLine("[Update] Downloaded latest config.yml from server.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Update] Failed to fetch remote config: {ex.Message}. Using local config.");
                remoteYaml = File.ReadAllText(LocalConfigPath);
            }

            // 3. 解析最终配置并下载插件
            var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
            var appConfig = deserializer.Deserialize<AppConfig>(remoteYaml);

            if (appConfig?.Plugins != null)
            {
                foreach (var plugin in appConfig.Plugins)
                {
                    if (plugin.Enabled && !string.IsNullOrEmpty(plugin.File))
                    {
                        string localFilePath = Path.Combine(PluginsDir, plugin.File);
                        string remoteFileUrl = $"{serverUrl.TrimEnd('/')}/static/{plugin.File}";
                        
                        // 简单逻辑：每次启动如果文件不存在则下载
                        // （后续可加入哈希校验或版本比对逻辑来支持热更新）
                        if (!File.Exists(localFilePath))
                        {
                            try
                            {
                                Console.WriteLine($"[Update] Downloading plugin: {plugin.Name} ({plugin.File})...");
                                var fileBytes = await client.GetByteArrayAsync(remoteFileUrl);
                                File.WriteAllBytes(localFilePath, fileBytes);
                                Console.WriteLine($"[Update] Successfully downloaded {plugin.File}.");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"[Update] Failed to download {plugin.File}: {ex.Message}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"[Update] Plugin {plugin.Name} already exists. Skipping download.");
                        }
                    }
                }
            }

            return appConfig ?? new AppConfig();
        }
    }
}
