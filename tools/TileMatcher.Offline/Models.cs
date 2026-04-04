using System.Text.Json.Serialization;

namespace TileMatcher.Offline;

public sealed class OfflineBatchConfig
{
    public string BatchName { get; set; } = "mvp-batch";
    public int CandidateCount { get; set; } = 24;
    public int MaxAcceptedLevels { get; set; } = 8;
    public int? RandomSeed { get; set; } = 20260404;
    public string AnalysisOutputDir { get; set; } = "artifacts/mahjong-mvp/analysis";
    public string RuntimeOutputDir { get; set; } = "artifacts/mahjong-mvp/runtime-levels";
    public OfflineLayoutRules Rules { get; set; } = new();
    public OfflineFilterRules Filter { get; set; } = new();
    public OfflineHiddenFaceConfig HiddenFace { get; set; } = new();
}

public sealed class OfflineHiddenFaceConfig
{
    public int StartLevel { get; set; } = int.MaxValue;
    public int HiddenCount { get; set; }
    public double HiddenRatio { get; set; }
    public int MaxHiddenCount { get; set; } = 6;
}

public sealed class OfflineLayoutRules
{
    public int MinLayerCount { get; set; } = 4;
    public int MaxLayerCount { get; set; } = 5;
    public int MinBottomLayerTileCount { get; set; } = 12;
    public bool RequireStrictSupport { get; set; } = true;
    public bool RequireUpperLayerStrictlySmaller { get; set; } = true;
    public int RandomWidthMin { get; set; } = 4;
    public int RandomWidthMax { get; set; } = 5;
    public int RandomHeightMin { get; set; } = 5;
    public int RandomHeightMax { get; set; } = 6;
    public double BottomLayerHoleChance { get; set; } = 0.16;
    public int GenerationMaxAttempts { get; set; } = 48;
    public int TileWidthUnits { get; set; } = 4;
    public int TileHeightUnits { get; set; } = 6;
    public int BottomLayerStepX { get; set; } = 4;
    public int BottomLayerStepY { get; set; } = 6;
    public OfflineLayerOffsetMode UpperLayerOffsetMode { get; set; } = OfflineLayerOffsetMode.HalfXY;
}

public sealed class OfflineFilterRules
{
    public int MinTileCount { get; set; } = 16;
    public int MaxTileCount { get; set; } = 48;
    public int MinLayerCount { get; set; } = 4;
    public int MaxLayerCount { get; set; } = 5;
    public int MinInitialBranchCount { get; set; } = 2;
    public int MinSolutionCountEstimate { get; set; } = 1;
    public double MaxDeadEndRate { get; set; } = 0.35;
    public double MinRandomPlaySurvivalRate { get; set; } = 0.18;
    public double AutoAcceptScore { get; set; } = 0.62;
    public double NeedsReviewScore { get; set; } = 0.42;
    public int RandomSimulationCount { get; set; } = 48;
    public int MaxSolutionCountSearch { get; set; } = 256;
}

public enum OfflineLayerOffsetMode
{
    None = 0,
    HalfX = 1,
    HalfY = 2,
    HalfXY = 3,
}

public sealed class OfflineTileShape
{
    public int WidthUnits { get; init; }
    public int HeightUnits { get; init; }
}

public sealed class OfflineTileData
{
    public int Id { get; init; }
    public string Type { get; init; } = string.Empty;
    public int GX { get; init; }
    public int GY { get; init; }
    public int GZ { get; init; }
    public OfflineTileShape Shape { get; init; } = new() { WidthUnits = 4, HeightUnits = 6 };
    public bool Removed { get; set; }

    [JsonPropertyName("face_hidden_initial")]
    public bool FaceHiddenInitial { get; init; }

    [JsonIgnore]
    public int FootprintWidth => Shape.WidthUnits;

    [JsonIgnore]
    public int FootprintHeight => Shape.HeightUnits;

    public OfflineTileData Clone()
    {
        return new OfflineTileData
        {
            Id = Id,
            Type = Type,
            GX = GX,
            GY = GY,
            GZ = GZ,
            Shape = new OfflineTileShape
            {
                WidthUnits = Shape.WidthUnits,
                HeightUnits = Shape.HeightUnits,
            },
            Removed = Removed,
            FaceHiddenInitial = FaceHiddenInitial,
        };
    }
}

public sealed class OfflineLevelLayout
{
    public string CandidateId { get; init; } = string.Empty;
    public int LevelId { get; init; }
    public List<OfflineTileData> Tiles { get; init; } = [];
}

public sealed class OfflineCandidateRecord
{
    public string CandidateId { get; init; } = string.Empty;
    public int BatchIndex { get; init; }
    public int Seed { get; init; }
    public OfflineLevelLayout Layout { get; init; } = new();
    public OfflineEvaluation Evaluation { get; init; } = new();
    public OfflineFilterResult FilterResult { get; init; } = new();
}

public sealed class OfflineEvaluation
{
    public bool HasSolution { get; init; }
    public int SolutionCountEstimate { get; init; }
    public int InitialBranchCount { get; init; }
    public double AverageBranchCount { get; init; }
    public double DeadEndRate { get; init; }
    public double RandomPlaySurvivalRate { get; init; }
    public int TileCount { get; init; }
    public int LayerCount { get; init; }
    public int SearchVisitedStateCount { get; init; }
    public int SearchDeadEndStateCount { get; init; }
}

public enum OfflineFilterDecision
{
    AutoReject = 0,
    NeedsReview = 1,
    AutoAccept = 2,
}

public sealed class OfflineFilterResult
{
    public OfflineFilterDecision Decision { get; init; } = OfflineFilterDecision.AutoReject;
    public string DifficultyBucket { get; init; } = "rejected";
    public double RecommendationScore { get; init; }
    public List<string> RejectReasons { get; init; } = [];
    public List<string> Tags { get; init; } = [];

    [JsonIgnore]
    public bool NeedsManualReview => Decision == OfflineFilterDecision.NeedsReview;
}

public sealed class OfflineBatchSummary
{
    public string BatchName { get; init; } = string.Empty;
    public int CandidateCount { get; init; }
    public int AcceptedCount { get; init; }
    public int NeedsReviewCount { get; init; }
    public int RejectedCount { get; init; }
    public string AnalysisOutputDir { get; init; } = string.Empty;
    public string RuntimeOutputDir { get; init; } = string.Empty;
}

public sealed class OfflineRuntimeLevel
{
    public int LevelNumber { get; init; }
    public string CandidateId { get; init; } = string.Empty;
    public string DifficultyBucket { get; init; } = string.Empty;
    public double RecommendationScore { get; init; }
    public OfflineLevelLayout Layout { get; init; } = new();
}
