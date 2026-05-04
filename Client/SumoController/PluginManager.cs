using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Sumo.Core;
using SumoController.Models;

namespace SumoController
{
    public class PluginManager
    {
        private static readonly string PluginsDir = Path.Combine(AppContext.BaseDirectory, "plugins");
        private static readonly List<IPlugin> _loadedPlugins = new List<IPlugin>();

        public static void LoadAndStartPlugins(AppConfig config)
        {
            if (config.Plugins == null || config.Plugins.Count == 0)
            {
                Console.WriteLine("[PluginManager] No plugins configured.");
                return;
            }

            foreach (var pluginConfig in config.Plugins)
            {
                if (!pluginConfig.Enabled)
                {
                    Console.WriteLine($"[PluginManager] Plugin {pluginConfig.Name} is disabled. Skipping.");
                    continue;
                }

                string filePath = Path.Combine(PluginsDir, pluginConfig.File);
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"[PluginManager] Plugin file not found: {filePath}");
                    continue;
                }

                try
                {
                    Console.WriteLine($"[PluginManager] Loading assembly: {pluginConfig.File}...");
                    
                    // 动态加载编译好的插件 DLL (后缀为 .cipx)，使用 AssemblyLoadContext 实现隔离加载
                    var loadContext = new AssemblyLoadContext($"Plugin_{pluginConfig.Name}", isCollectible: true);
                    Assembly assembly = loadContext.LoadFromAssemblyPath(filePath);
                    
                    // 查找所有实现了 IPlugin 接口且可以被实例化的类
                    var pluginTypes = assembly.GetTypes()
                        .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                    foreach (var type in pluginTypes)
                    {
                        var pluginInstance = (IPlugin?)Activator.CreateInstance(type);
                        if (pluginInstance != null)
                        {
                            Console.WriteLine($"[PluginManager] Initializing plugin: {pluginInstance.Name} (v{pluginInstance.Version})");
                            pluginInstance.Initialize(config.ServerUrl, pluginConfig.Config);
                            
                            Console.WriteLine($"[PluginManager] Starting plugin: {pluginInstance.Name}");
                            pluginInstance.Start();
                            
                            // 保存引用以便退出时清理
                            _loadedPlugins.Add(pluginInstance);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PluginManager] Failed to load or start plugin {pluginConfig.Name}: {ex.Message}");
                }
            }
        }

        public static void StopAll()
        {
            Console.WriteLine("[PluginManager] Stopping all plugins...");
            foreach (var plugin in _loadedPlugins)
            {
                try
                {
                    plugin.Stop();
                    Console.WriteLine($"[PluginManager] Stopped plugin: {plugin.Name}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PluginManager] Error stopping plugin {plugin.Name}: {ex.Message}");
                }
            }
            _loadedPlugins.Clear();
        }
    }
}
