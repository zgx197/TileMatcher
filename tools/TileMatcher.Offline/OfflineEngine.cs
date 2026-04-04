namespace TileMatcher.Offline;

internal static partial class OfflinePipeline
{
    private static bool ValidateLayout(IReadOnlyList<OfflineTileData> tiles, OfflineLayoutRules rules)
    {
        var layerGroups = tiles.GroupBy(tile => tile.GZ).OrderBy(group => group.Key).ToList();
        if (layerGroups.Count < rules.MinLayerCount || layerGroups.Count > rules.MaxLayerCount)
        {
            return false;
        }

        if (layerGroups.First().Count() < rules.MinBottomLayerTileCount)
        {
            return false;
        }

        foreach (var layer in layerGroups)
        {
            var layerTiles = layer.ToList();
            for (var i = 0; i < layerTiles.Count; i++)
            {
                for (var j = i + 1; j < layerTiles.Count; j++)
                {
                    if (OverlapsXY(layerTiles[i], layerTiles[j]))
                    {
                        return false;
                    }
                }
            }
        }

        if (rules.RequireUpperLayerStrictlySmaller)
        {
            for (var i = 1; i < layerGroups.Count; i++)
            {
                if (layerGroups[i].Count() >= layerGroups[i - 1].Count())
                {
                    return false;
                }
            }
        }

        if (rules.RequireStrictSupport)
        {
            var layerMap = layerGroups.ToDictionary(group => group.Key, group => group.ToList());
            foreach (var tile in tiles.Where(tile => tile.GZ > 0))
            {
                if (!layerMap.TryGetValue(tile.GZ - 1, out var lowerLayer)
                    || !HasFullSupportFromLowerLayer(tile, lowerLayer))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool TryAssignSolvablePairs(
        IReadOnlyList<OfflineTileData> tiles,
        Random rng,
        out Dictionary<int, string> typeByTileId)
    {
        typeByTileId = [];
        for (var attempt = 0; attempt < 96; attempt++)
        {
            var simulationTiles = tiles.Select(tile => tile.Clone()).ToList();
            var removedPairs = new List<(int FirstId, int SecondId)>();
            if (!TryBuildRemovalSequence(simulationTiles, rng, removedPairs))
            {
                continue;
            }

            var pairTypes = BuildPairTypeSequence(removedPairs.Count, rng);
            for (var i = 0; i < removedPairs.Count; i++)
            {
                typeByTileId[removedPairs[i].FirstId] = pairTypes[i];
                typeByTileId[removedPairs[i].SecondId] = pairTypes[i];
            }

            return true;
        }

        return false;
    }

    private static bool TryBuildRemovalSequence(
        List<OfflineTileData> simulationTiles,
        Random rng,
        List<(int FirstId, int SecondId)> removedPairs)
    {
        while (simulationTiles.Count > 0)
        {
            var activeTiles = simulationTiles.Where(tile => !tile.Removed).ToList();
            var movableTiles = activeTiles.Where(tile => CanParticipateInMatch(tile, activeTiles)).ToList();
            if (movableTiles.Count < 2)
            {
                return false;
            }

            Shuffle(rng, movableTiles);
            var first = movableTiles[0];
            var second = movableTiles[1];
            removedPairs.Add((first.Id, second.Id));
            simulationTiles.RemoveAll(tile => tile.Id == first.Id || tile.Id == second.Id);
        }

        return true;
    }

    private static List<string> BuildPairTypeSequence(int pairCount, Random rng)
    {
        var sequence = new List<string>(pairCount);
        for (var i = 0; i < pairCount; i++)
        {
            sequence.Add(PairTypePool[rng.Next(PairTypePool.Length)]);
        }

        return sequence;
    }

    private static StateEvaluation EvaluateState(
        IReadOnlyList<OfflineTileData> tiles,
        ulong stateMask,
        int maxSolutionCountSearch,
        Dictionary<ulong, StateEvaluation> cache)
    {
        if (stateMask == 0)
        {
            return StateEvaluation.Success();
        }

        if (cache.TryGetValue(stateMask, out var cached))
        {
            return cached;
        }

        var legalPairs = GetLegalPairs(tiles, stateMask);
        if (legalPairs.Count == 0)
        {
            var deadEnd = StateEvaluation.DeadEnd();
            cache[stateMask] = deadEnd;
            return deadEnd;
        }

        var solutionCount = 0;
        var visitedStateCount = 1;
        var deadEndStateCount = 0;
        var branchSum = legalPairs.Count;
        var nonTerminalStateCount = 1;
        var hasSolution = false;

        foreach (var pair in legalPairs)
        {
            var nextMask = stateMask & ~BitFor(pair.FirstIndex) & ~BitFor(pair.SecondIndex);
            var child = EvaluateState(tiles, nextMask, maxSolutionCountSearch, cache);
            visitedStateCount += child.VisitedStateCount;
            deadEndStateCount += child.DeadEndStateCount;
            branchSum += child.BranchSum;
            nonTerminalStateCount += child.NonTerminalStateCount;
            hasSolution |= child.HasSolution;
            solutionCount += child.SolutionCountEstimate;
            if (solutionCount >= maxSolutionCountSearch)
            {
                solutionCount = maxSolutionCountSearch;
                break;
            }
        }

        var result = new StateEvaluation(
            hasSolution,
            solutionCount,
            visitedStateCount,
            deadEndStateCount,
            branchSum,
            nonTerminalStateCount);
        cache[stateMask] = result;
        return result;
    }

    private static double EstimateRandomPlaySurvival(IReadOnlyList<OfflineTileData> tiles, int simulationCount, int randomSeed)
    {
        if (simulationCount <= 0)
        {
            return 0.0;
        }

        var solvedCount = 0;
        for (var i = 0; i < simulationCount; i++)
        {
            var rng = new Random(randomSeed + i * 97);
            var stateMask = BuildInitialMask(tiles.Count);
            while (stateMask != 0)
            {
                var legalPairs = GetLegalPairs(tiles, stateMask);
                if (legalPairs.Count == 0)
                {
                    break;
                }

                var nextPair = legalPairs[rng.Next(legalPairs.Count)];
                stateMask &= ~BitFor(nextPair.FirstIndex);
                stateMask &= ~BitFor(nextPair.SecondIndex);
            }

            if (stateMask == 0)
            {
                solvedCount++;
            }
        }

        return Math.Round((double)solvedCount / simulationCount, 4);
    }

    private static List<LegalPair> GetLegalPairs(IReadOnlyList<OfflineTileData> tiles, ulong stateMask)
    {
        var activeTiles = new List<OfflineTileData>();
        for (var i = 0; i < tiles.Count; i++)
        {
            if ((stateMask & BitFor(i)) != 0)
            {
                activeTiles.Add(tiles[i]);
            }
        }

        var movable = activeTiles.Where(tile => CanParticipateInMatch(tile, activeTiles)).ToList();
        var pairs = new List<LegalPair>();
        for (var i = 0; i < movable.Count; i++)
        {
            for (var j = i + 1; j < movable.Count; j++)
            {
                if (movable[i].Type != movable[j].Type)
                {
                    continue;
                }

                pairs.Add(new LegalPair(movable[i].Id - 1, movable[j].Id - 1));
            }
        }

        return pairs;
    }

    private static bool CanParticipateInMatch(OfflineTileData tile, IReadOnlyCollection<OfflineTileData> activeTiles)
    {
        var hasAboveOverlap = HasAnyAboveOverlap(tile, activeTiles);
        var hasLeftNeighbor = HasLeftNeighbor(tile, activeTiles);
        var hasRightNeighbor = HasRightNeighbor(tile, activeTiles);
        var hasTopNeighbor = HasTopNeighbor(tile, activeTiles);
        var hasBottomNeighbor = HasBottomNeighbor(tile, activeTiles);
        return !hasAboveOverlap && !(hasLeftNeighbor && hasRightNeighbor) && !(hasTopNeighbor && hasBottomNeighbor);
    }

    private static bool HasAnyAboveOverlap(OfflineTileData tile, IEnumerable<OfflineTileData> allTiles)
    {
        foreach (var other in allTiles)
        {
            if (other.Id == tile.Id || other.GZ <= tile.GZ)
            {
                continue;
            }

            if (OverlapsXY(tile, other))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasLeftNeighbor(OfflineTileData tile, IEnumerable<OfflineTileData> allTiles)
    {
        return allTiles.Any(other => other.Id != tile.Id
            && other.GZ == tile.GZ
            && other.GX + other.FootprintWidth == tile.GX
            && RangesOverlap(other.GY, other.GY + other.FootprintHeight, tile.GY, tile.GY + tile.FootprintHeight));
    }

    private static bool HasRightNeighbor(OfflineTileData tile, IEnumerable<OfflineTileData> allTiles)
    {
        return allTiles.Any(other => other.Id != tile.Id
            && other.GZ == tile.GZ
            && other.GX == tile.GX + tile.FootprintWidth
            && RangesOverlap(other.GY, other.GY + other.FootprintHeight, tile.GY, tile.GY + tile.FootprintHeight));
    }

    private static bool HasTopNeighbor(OfflineTileData tile, IEnumerable<OfflineTileData> allTiles)
    {
        return allTiles.Any(other => other.Id != tile.Id
            && other.GZ == tile.GZ
            && other.GY + other.FootprintHeight == tile.GY
            && RangesOverlap(other.GX, other.GX + other.FootprintWidth, tile.GX, tile.GX + tile.FootprintWidth));
    }

    private static bool HasBottomNeighbor(OfflineTileData tile, IEnumerable<OfflineTileData> allTiles)
    {
        return allTiles.Any(other => other.Id != tile.Id
            && other.GZ == tile.GZ
            && other.GY == tile.GY + tile.FootprintHeight
            && RangesOverlap(other.GX, other.GX + other.FootprintWidth, tile.GX, tile.GX + tile.FootprintWidth));
    }

    private static bool OverlapsXY(OfflineTileData a, OfflineTileData b)
    {
        return a.GX < b.GX + b.FootprintWidth
            && a.GX + a.FootprintWidth > b.GX
            && a.GY < b.GY + b.FootprintHeight
            && a.GY + a.FootprintHeight > b.GY;
    }

    private static bool HasFullSupportFromLowerLayer(OfflineTileData tile, IReadOnlyCollection<OfflineTileData> lowerLayerTiles)
    {
        if (tile.GZ <= 0)
        {
            return true;
        }

        for (var y = tile.GY; y < tile.GY + tile.FootprintHeight; y++)
        {
            for (var x = tile.GX; x < tile.GX + tile.FootprintWidth; x++)
            {
                var covered = false;
                foreach (var lowerTile in lowerLayerTiles)
                {
                    if (x >= lowerTile.GX
                        && x < lowerTile.GX + lowerTile.FootprintWidth
                        && y >= lowerTile.GY
                        && y < lowerTile.GY + lowerTile.FootprintHeight)
                    {
                        covered = true;
                        break;
                    }
                }

                if (!covered)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool RangesOverlap(int aMin, int aMax, int bMin, int bMax)
    {
        return aMin < bMax && aMax > bMin;
    }

    private static ulong BuildInitialMask(int tileCount)
    {
        return tileCount == 64 ? ulong.MaxValue : (1UL << tileCount) - 1UL;
    }

    private static ulong BitFor(int index) => 1UL << index;

    private readonly record struct LegalPair(int FirstIndex, int SecondIndex);

    private readonly record struct StateEvaluation(
        bool HasSolution,
        int SolutionCountEstimate,
        int VisitedStateCount,
        int DeadEndStateCount,
        int BranchSum,
        int NonTerminalStateCount)
    {
        public static StateEvaluation Success() => new(true, 1, 1, 0, 0, 0);
        public static StateEvaluation DeadEnd() => new(false, 0, 1, 1, 0, 0);
    }
}
