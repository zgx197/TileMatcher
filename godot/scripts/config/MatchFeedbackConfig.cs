using Godot;

namespace TileMatcher.Config;

[GlobalClass]
/// <summary>
/// 消除反馈配置资源。
/// </summary>
/// <remarks>
/// 这份资源只负责“交互表现层”的参数，不参与布局生成和业务规则判定。
/// 这样做的目的是把以下两类关注点拆开：
/// 1. 通用逻辑层：哪些牌可以被选中、拖拽、配对、消除。
/// 2. 表现层：拖拽命中容差是多少、碰撞前要悬浮多久、分数字体多大、闪光有多强。
///
/// 未来如果要做“普通反馈 / 强反馈 / 商业化手游反馈”三套手感，只需要切换不同资源，
/// 不需要再进入 BoardController 修改硬编码常量。
/// </remarks>
public partial class MatchFeedbackConfig : Resource
{
    /// <summary>
    /// 拖拽接触容差。
    /// 数值越大，拖拽牌越容易在“看起来已经碰到了”的瞬间就触发配对命中。
    /// </summary>
    [Export(PropertyHint.Range, "0,128,1")]
    public float DragContactTolerance { get; set; } = 28.0f;

    /// <summary>两张牌飞向预备位的时长。</summary>
    [Export(PropertyHint.Range, "0.05,1,0.01")]
    public double StageMoveDuration { get; set; } = 0.24;

    /// <summary>停留在预备位上的时间，用来制造“蓄力一下再撞”的仪式感。</summary>
    [Export(PropertyHint.Range, "0,0.5,0.01")]
    public double StageHoldDuration { get; set; } = 0.08;

    /// <summary>从预备位撞向中点的时长。</summary>
    [Export(PropertyHint.Range, "0.05,1,0.01")]
    public double CrashDuration { get; set; } = 0.28;

    /// <summary>碰撞后淡出和缩放收尾的时长。</summary>
    [Export(PropertyHint.Range, "0.05,1,0.01")]
    public double FadeDuration { get; set; } = 0.24;

    /// <summary>
    /// 预备位横向间距系数。
    /// 1 表示大致以两张牌半宽相加作为展开距离，小于 1 会更紧凑。
    /// </summary>
    [Export(PropertyHint.Range, "0.2,1.5,0.01")]
    public float StageGapFactor { get; set; } = 0.96f;

    /// <summary>碰撞前整体向上抬升的高度。</summary>
    [Export(PropertyHint.Range, "0,160,1")]
    public float StageLiftHeight { get; set; } = 36.0f;

    /// <summary>第一段悬浮时的轻微放大倍率。</summary>
    [Export(PropertyHint.Range, "1,1.5,0.01")]
    public float StageScale { get; set; } = 1.08f;

    /// <summary>碰撞瞬间的峰值缩放倍率。</summary>
    [Export(PropertyHint.Range, "1,1.6,0.01")]
    public float ImpactScale { get; set; } = 1.12f;

    /// <summary>牌面碰撞后的统一染色，用于强调命中已经成立。</summary>
    [Export]
    public Color ImpactTintColor { get; set; } = new(1.10f, 0.95f, 0.70f, 1.0f);

    /// <summary>单次消除的基础分值。</summary>
    [Export(PropertyHint.Range, "1,10000,1")]
    public int MatchScore { get; set; } = 100;

    /// <summary>爆点的基础尺寸，单位为屏幕像素。</summary>
    [Export(PropertyHint.Range, "8,256,1")]
    public float FlashSize { get; set; } = 84.0f;

    /// <summary>爆点扩散到最大时的倍率。</summary>
    [Export(PropertyHint.Range, "1,4,0.01")]
    public float FlashExpandScale { get; set; } = 1.65f;

    /// <summary>白色爆点持续时长。</summary>
    [Export(PropertyHint.Range, "0.03,0.6,0.01")]
    public double FlashDuration { get; set; } = 0.16;

    /// <summary>爆点峰值透明度。</summary>
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float FlashMaxAlpha { get; set; } = 0.92f;

    /// <summary>爆点颜色，当前默认使用白色偏暖色，避免纯白刺眼。</summary>
    [Export]
    public Color FlashColor { get; set; } = new(1.0f, 0.98f, 0.92f, 1.0f);

    /// <summary>分数文本总持续时长。</summary>
    [Export(PropertyHint.Range, "0.1,2,0.01")]
    public double ScorePopupDuration { get; set; } = 0.92;

    /// <summary>分数文本初始缩放。</summary>
    [Export(PropertyHint.Range, "0.2,2,0.01")]
    public float ScoreStartScale { get; set; } = 0.84f;

    /// <summary>分数文本第一拍弹到的最大倍率。</summary>
    [Export(PropertyHint.Range, "0.5,3,0.01")]
    public float ScorePeakScale { get; set; } = 1.42f;

    /// <summary>分数文本回落后的稳定倍率。</summary>
    [Export(PropertyHint.Range, "0.5,3,0.01")]
    public float ScoreSettleScale { get; set; } = 1.10f;

    /// <summary>分数文本整体上浮距离。</summary>
    [Export(PropertyHint.Range, "0,240,1")]
    public float ScoreFloatDistance { get; set; } = 104.0f;

    /// <summary>分数弹跳第一段时长。</summary>
    [Export(PropertyHint.Range, "0.03,1,0.01")]
    public double ScoreBounceUpDuration { get; set; } = 0.16;

    /// <summary>分数弹跳第二段时长。</summary>
    [Export(PropertyHint.Range, "0.03,1,0.01")]
    public double ScoreBounceDownDuration { get; set; } = 0.18;

    /// <summary>分数字体大小。</summary>
    [Export(PropertyHint.Range, "12,128,1")]
    public int ScoreFontSize { get; set; } = 56;

    /// <summary>描边宽度。较强的描边能让分数在复杂牌堆背景上也保持清晰。</summary>
    [Export(PropertyHint.Range, "0,16,1")]
    public int ScoreOutlineSize { get; set; } = 5;

    /// <summary>分数文本颜色。</summary>
    [Export]
    public Color ScoreFontColor { get; set; } = new(1.0f, 0.98f, 0.72f, 1.0f);

    /// <summary>分数描边颜色。默认偏暖棕色，既有手游感，也不会像纯黑描边那样过硬。</summary>
    [Export]
    public Color ScoreOutlineColor { get; set; } = new(0.41f, 0.20f, 0.05f, 1.0f);

    /// <summary>分数出现时的峰值透明度。</summary>
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float ScoreMaxAlpha { get; set; } = 1.0f;
}
