using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Retrograde;

public static class ConnectorSelectionUtil
{
    public static P3Float CalculateClusterCenter(List<PlacedRoom> placedRooms, List<OpenConnector> openConnectors)
    {
        if (placedRooms != null && placedRooms.Count > 0)
        {
            float sumX = 0;
            float sumY = 0;
            float sumZ = 0;
            foreach (var room in placedRooms)
            {
                sumX += room.WorldPos.X;
                sumY += room.WorldPos.Y;
                sumZ += room.WorldPos.Z;
            }

            float count = placedRooms.Count;
            return new P3Float(sumX / count, sumY / count, sumZ / count);
        }

        if (openConnectors != null && openConnectors.Count > 0)
        {
            float sumX = 0;
            float sumY = 0;
            float sumZ = 0;
            foreach (var connector in openConnectors)
            {
                sumX += connector.WorldPos.X;
                sumY += connector.WorldPos.Y;
                sumZ += connector.WorldPos.Z;
            }

            float count = openConnectors.Count;
            return new P3Float(sumX / count, sumY / count, sumZ / count);
        }

        return new P3Float(0, 0, 0);
    }

    public static int ChooseConnectorIndexNearCenter(List<OpenConnector> openConnectors, P3Float clusterCenter, int sampleSize)
    {
        if (openConnectors == null || openConnectors.Count == 0)
            throw new ArgumentException("openConnectors cannot be empty when choosing an index.", nameof(openConnectors));

        var prioritized = openConnectors
            .Select((c, idx) => new
            {
                Index = idx,
                DistSq = MathUtil.DistanceSquared(c.WorldPos, clusterCenter)
            })
            .OrderBy(p => p.DistSq)
            .ToList();

        int takeCount = Math.Min(sampleSize, prioritized.Count);
        return prioritized[RandomProvider.Random.Next(takeCount)].Index;
    }

    public static OpenConnector ChooseFarthestOpenConnector(List<OpenConnector> openConnectors, P3Float clusterCenter)
    {
        if (openConnectors == null || openConnectors.Count == 0)
            throw new ArgumentException("openConnectors cannot be empty when choosing a connector.", nameof(openConnectors));

        float maxDist = float.MinValue;
        OpenConnector best = openConnectors[0];
        for (int i = 0; i < openConnectors.Count; i++)
        {
            var dist = MathUtil.DistanceSquared(openConnectors[i].WorldPos, clusterCenter);
            if (dist > maxDist)
            {
                maxDist = dist;
                best = openConnectors[i];
            }
        }
        return best;
    }

    public static RgConnectorInstance ChooseMostOutwardConnector(List<RgConnectorInstance> compatibles, P3Float targetWorldPos, P3Float clusterCenter)
    {
        if (compatibles == null || compatibles.Count == 0)
            throw new ArgumentException("compatibles cannot be empty when choosing a connector.", nameof(compatibles));

        RgConnectorInstance best = compatibles[0];
        float bestDist = MathUtil.DistanceSquared(targetWorldPos - best.LocalPos, clusterCenter);

        foreach (var c in compatibles)
        {
            float dist = MathUtil.DistanceSquared(targetWorldPos - c.LocalPos, clusterCenter);
            if (dist > bestDist)
            {
                bestDist = dist;
                best = c;
            }
        }

        return best;
    }
}
