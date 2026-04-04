namespace TileMatcher.Offline;

internal static class OfflineTestHooks
{
    public static OfflineEvaluation EvaluateLayout(OfflineLevelLayout layout, OfflineFilterRules rules, int randomSeed = 20260404)
    {
        return OfflinePipeline.EvaluateForTests(layout, rules, randomSeed);
    }

    public static OfflineFilterResult FilterLayout(OfflineLevelLayout layout, OfflineEvaluation evaluation, OfflineFilterRules rules)
    {
        return OfflinePipeline.FilterForTests(layout, evaluation, rules);
    }

    public static void ExportRuntimeLevels(
        string runtimeDir,
        IReadOnlyList<OfflineCandidateRecord> accepted,
        OfflineHiddenFaceConfig? hiddenFaceConfig = null)
    {
        OfflinePipeline.ExportRuntimeLevelsForTests(runtimeDir, accepted, hiddenFaceConfig);
    }
}
