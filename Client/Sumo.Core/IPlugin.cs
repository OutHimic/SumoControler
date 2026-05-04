using System.Collections.Generic;

namespace Sumo.Core
{
    public interface IPlugin
    {
        string Name { get; }
        string Version { get; }
        string Description { get; }

        void Initialize(string serverUrl, Dictionary<string, object>? config);
        void Start();
        void Stop();
    }
}
