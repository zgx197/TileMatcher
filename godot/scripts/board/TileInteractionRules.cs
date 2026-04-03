using System.Collections.Generic;
using Godot;
using TileMatcher.Grid;
using AppTileData = TileMatcher.Data.TileData;

namespace TileMatcher.Board;

/// <summary>
/// 统一管理“单张牌当前交互状态”和“两张牌是否允许配对”的规则入口。
/// </summary>
/// <remarks>
/// 这份规则层的目标，是把下列两类问题显式拆开：
/// 1. 几何 / 输入问题：鼠标点到了谁、拖拽碰到了谁。
/// 2. 业务合法性问题：这张牌是否自由、这两张牌是否允许消除。
///
/// 当前版本明确采用一套收敛规则：
/// - 只要一张牌被卡住，它就不能被拿起
/// - 只要一张牌被卡住，它也不能作为被动目标参与配对消除
///
/// 因此“可被拖起”和“可参与消除”当前虽然概念上可分离，
/// 但业务上先统一收敛为同一个判断结果，避免点击链路和拖拽链路出现语义分叉。
/// </remarks>
public static class TileInteractionRules
{
    /// <summary>
    /// 统一验证两张牌是否满足一次合法配对，并返回结构化结果。
    /// </summary>
    public static MatchValidationResult ValidateMatchPair(
        AppTileData first,
        AppTileData second,
        IReadOnlyCollection<AppTileData> activeTiles)
    {
        if (first.Removed || second.Removed)
        {
            return MatchValidationResult.Fail(
                MatchFailureKind.Removed,
                "目标已移除");
        }

        if (first.Id == second.Id)
        {
            return MatchValidationResult.Fail(
                MatchFailureKind.SameTile,
                "不能与自己配对");
        }

        if (first.Type != second.Type)
        {
            return MatchValidationResult.Fail(
                MatchFailureKind.TypeMismatch,
                "牌面不同，无法消除");
        }

        var firstState = Evaluate(first, activeTiles);
        if (!firstState.CanParticipateInMatch)
        {
            return MatchValidationResult.Fail(
                MatchFailureKind.SourceBlocked,
                firstState.GetPlayerHintText(),
                firstState.PrimaryBlockReason,
                firstState);
        }

        var secondState = Evaluate(second, activeTiles);
        if (!secondState.CanParticipateInMatch)
        {
            return MatchValidationResult.Fail(
                MatchFailureKind.TargetBlocked,
                secondState.GetPlayerHintText(),
                TileBlockReason.None,
                default,
                secondState.PrimaryBlockReason,
                secondState);
        }

        return MatchValidationResult.Success();
    }

    /// <summary>
    /// 评估一张牌在当前棋盘快照中的交互状态。
    /// </summary>
    public static TileInteractionState Evaluate(AppTileData tile, IReadOnlyCollection<AppTileData> activeTiles)
    {
        var hasAboveOverlap = GridMath.HasAnyAboveOverlap(tile, activeTiles);
        var hasLeftNeighbor = GridMath.HasLeftNeighbor(tile, activeTiles);
        var hasRightNeighbor = GridMath.HasRightNeighbor(tile, activeTiles);
        var hasTopNeighbor = GridMath.HasTopNeighbor(tile, activeTiles);
        var hasBottomNeighbor = GridMath.HasBottomNeighbor(tile, activeTiles);

        return new TileInteractionState(
            hasAboveOverlap,
            hasLeftNeighbor,
            hasRightNeighbor,
            hasTopNeighbor,
            hasBottomNeighbor);
    }

    /// <summary>
    /// 判断一张牌当前是否允许参与配对消除。
    /// </summary>
    public static bool CanParticipateInMatch(AppTileData tile, IReadOnlyCollection<AppTileData> activeTiles)
    {
        if (tile.Removed)
        {
            return false;
        }

        return Evaluate(tile, activeTiles).CanParticipateInMatch;
    }

    /// <summary>
    /// 统一验证两张牌是否满足一次合法配对。
    /// </summary>
    /// <remarks>
    /// 这里不关心输入来源是“点击”还是“拖拽”，只关心业务语义本身是否成立。
    /// 后续无论是提示系统、自动求解还是关卡可解性分析，都应该复用这一个入口。
    /// </remarks>
    public static bool TryValidateMatchPair(
        AppTileData first,
        AppTileData second,
        IReadOnlyCollection<AppTileData> activeTiles,
        out string failureReason)
    {
        var result = ValidateMatchPair(first, second, activeTiles);
        failureReason = result.PlayerMessage;
        return result.IsValid;
    }
}

/// <summary>
/// 单张牌的主要锁定原因。
/// </summary>
public enum TileBlockReason
{
    None = 0,
    Above = 1,
    LeftRight = 2,
    TopBottom = 3,
}

/// <summary>
/// 一次配对尝试失败的原因类型。
/// </summary>
public enum MatchFailureKind
{
    None = 0,
    Removed = 1,
    SameTile = 2,
    TypeMismatch = 3,
    SourceBlocked = 4,
    TargetBlocked = 5,
}

/// <summary>
/// 一次配对尝试的结构化校验结果。
/// </summary>
public readonly record struct MatchValidationResult(
    bool IsValid,
    MatchFailureKind FailureKind,
    string PlayerMessage,
    TileBlockReason SourceBlockReason,
    TileInteractionState SourceState,
    TileBlockReason TargetBlockReason,
    TileInteractionState TargetState)
{
    public static MatchValidationResult Success()
    {
        return new MatchValidationResult(
            true,
            MatchFailureKind.None,
            string.Empty,
            TileBlockReason.None,
            default,
            TileBlockReason.None,
            default);
    }

    public static MatchValidationResult Fail(
        MatchFailureKind failureKind,
        string playerMessage,
        TileBlockReason sourceBlockReason = TileBlockReason.None,
        TileInteractionState sourceState = default,
        TileBlockReason targetBlockReason = TileBlockReason.None,
        TileInteractionState targetState = default)
    {
        return new MatchValidationResult(
            false,
            failureKind,
            playerMessage,
            sourceBlockReason,
            sourceState,
            targetBlockReason,
            targetState);
    }
}

/// <summary>
/// 单张牌在当前棋盘快照中的交互状态。
/// </summary>
/// <remarks>
/// 这里显式保留各个方向的阻塞信息，而不是只压缩成一个布尔值，
/// 是为了后续做三件事：
/// 1. 调试日志里清楚说明“为什么这张牌被卡住”
/// 2. UI 高亮或调试面板能逐项展示阻塞来源
/// 3. 后续若玩法调整为“可拿起但不可配对”等更细分语义时，有足够信息继续演进
/// </remarks>
public readonly struct TileInteractionState(
    bool hasAboveOverlap,
    bool hasLeftNeighbor,
    bool hasRightNeighbor,
    bool hasTopNeighbor,
    bool hasBottomNeighbor)
{
    public bool HasAboveOverlap { get; } = hasAboveOverlap;

    public bool HasLeftNeighbor { get; } = hasLeftNeighbor;

    public bool HasRightNeighbor { get; } = hasRightNeighbor;

    public bool HasTopNeighbor { get; } = hasTopNeighbor;

    public bool HasBottomNeighbor { get; } = hasBottomNeighbor;

    /// <summary>左右同时被夹住时，视为横向阻塞成立。</summary>
    public bool IsBlockedHorizontally => HasLeftNeighbor && HasRightNeighbor;

    /// <summary>上下同时被夹住时，视为纵向阻塞成立。</summary>
    public bool IsBlockedVertically => HasTopNeighbor && HasBottomNeighbor;

    /// <summary>
    /// 当前版本下，“自由牌”的定义。
    /// 只要有上层压住，或左右成对夹住，或上下成对夹住，就不自由。
    /// </summary>
    public bool IsFree => !HasAboveOverlap && !IsBlockedHorizontally && !IsBlockedVertically;

    /// <summary>
    /// 当前版本里，可被玩家拿起和可参与配对先统一为同一语义。
    /// </summary>
    public bool CanBePicked => IsFree;

    /// <summary>
    /// 当前版本里，只要牌不自由，就不允许作为主动方或被动方参与配对。
    /// </summary>
    public bool CanParticipateInMatch => IsFree;

    /// <summary>
    /// 将阻塞状态归纳成一个最主要的玩家提示原因。
    /// </summary>
    public TileBlockReason PrimaryBlockReason
    {
        get
        {
            if (HasAboveOverlap)
            {
                return TileBlockReason.Above;
            }

            if (IsBlockedHorizontally)
            {
                return TileBlockReason.LeftRight;
            }

            if (IsBlockedVertically)
            {
                return TileBlockReason.TopBottom;
            }

            return TileBlockReason.None;
        }
    }

    /// <summary>
    /// 返回玩家能直接理解的锁定提示文案。
    /// </summary>
    public string GetPlayerHintText()
    {
        return PrimaryBlockReason switch
        {
            TileBlockReason.Above => "被上层压住",
            TileBlockReason.LeftRight => "被左右锁住",
            TileBlockReason.TopBottom => "被上下锁住",
            _ => "当前不可移动",
        };
    }

    /// <summary>
    /// 为日志和调试面板生成可读摘要。
    /// </summary>
    public string BuildDebugSummary()
    {
        return $"free={IsFree}, above={HasAboveOverlap}, left={HasLeftNeighbor}, right={HasRightNeighbor}, top={HasTopNeighbor}, bottom={HasBottomNeighbor}";
    }
}
