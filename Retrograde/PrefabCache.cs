using System;
using System.Collections.Generic;
using FrankyCLI;

namespace FrankyCLI.Retrograde
{
    public static class PrefabCache
    {
        private static readonly Dictionary<string, RoomPrefab> Prefabs = new(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Dictionary<int, List<RgConnectorInstance>>> ConnectorCache = new(StringComparer.OrdinalIgnoreCase);
        private static readonly object PrefabLock = new();
        private static readonly object ConnectorLock = new();

        public static RoomPrefab GetPrefab(string editorId)
        {
            if (string.IsNullOrWhiteSpace(editorId))
                throw new ArgumentException("Prefab editor ID cannot be null or empty.", nameof(editorId));

            lock (PrefabLock)
            {
                if (!Prefabs.TryGetValue(editorId, out var prefab))
                {
                    prefab = new RoomPrefab(editorId);
                    Prefabs[editorId] = prefab;
                }

                return prefab;
            }
        }

        public static List<RgConnectorInstance> GetConnectors(RoomPrefab prefab, int yawSteps = 0)
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));

            var key = prefab.PrefabEditorId ?? string.Empty;
            lock (ConnectorLock)
            {
                if (!ConnectorCache.TryGetValue(key, out var yawCache))
                {
                    yawCache = new Dictionary<int, List<RgConnectorInstance>>();
                    ConnectorCache[key] = yawCache;
                }

                if (!yawCache.TryGetValue(yawSteps, out var connectors))
                {
                    connectors = ConnectorUtils.BuildConnectors(prefab, yawSteps);
                    yawCache[yawSteps] = connectors;
                }

                return connectors;
            }
        }
    }
}
