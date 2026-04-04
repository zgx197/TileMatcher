using System;
using Godot;
using TileMatcher.Pets;

namespace TileMatcher.Home;

/// <summary>
/// 首页宠物乐园中的单只宠物组件。
/// 负责展示宠物卡片、生活状态切换、气泡出现节奏以及轻微移动表现。
/// </summary>
public partial class PetActorView : PanelContainer
{
    private enum PetLifeState
    {
        Resting,
        Wandering,
        Watching,
        Playing,
    }

    /// <summary>状态气泡文本。</summary>
    private Label _bubbleLabel = null!;

    /// <summary>宠物名称文本。</summary>
    private Label _nameLabel = null!;

    /// <summary>宠物物种文本。</summary>
    private Label _speciesLabel = null!;

    /// <summary>宠物补充描述文本。</summary>
    private Label _detailLabel = null!;

    /// <summary>承载轻微移动与呼吸效果的视觉根节点。</summary>
    private Control _motionRoot = null!;

    /// <summary>生活状态切换定时器。</summary>
    private Godot.Timer _stateTimer = null!;

    /// <summary>控制气泡显示时长的定时器。</summary>
    private Godot.Timer _bubbleTimer = null!;

    /// <summary>当前气泡的淡入淡出补间。</summary>
    private Tween? _bubbleTween;

    /// <summary>当前状态位移的补间。</summary>
    private Tween? _movementTween;

    /// <summary>运行时随机数发生器。</summary>
    private readonly RandomNumberGenerator _random = new();

    /// <summary>当前组件绑定的宠物定义。</summary>
    private PetDefinition? _definition;

    /// <summary>当前组件绑定的宠物存档。</summary>
    private OwnedPetData? _ownedPet;

    /// <summary>当前宠物在乐园中的顺序索引。</summary>
    private int _parkIndex;

    /// <summary>当前生活状态。</summary>
    private PetLifeState _currentLifeState;

    /// <summary>状态位移的当前目标偏移。</summary>
    private Vector2 _stateOffset = Vector2.Zero;

    /// <summary>呼吸和轻微漂浮使用的时间累积器。</summary>
    private float _motionClock;

    /// <summary>初始化组件节点、定时器与初始表现。</summary>
    public override void _Ready()
    {
        _motionRoot = GetNode<Control>("MotionRoot");
        _bubbleLabel = GetNode<Label>("MotionRoot/Margin/Stack/Bubble");
        _nameLabel = GetNode<Label>("MotionRoot/Margin/Stack/Name");
        _speciesLabel = GetNode<Label>("MotionRoot/Margin/Stack/Species");
        _detailLabel = GetNode<Label>("MotionRoot/Margin/Stack/Detail");
        _stateTimer = GetNode<Godot.Timer>("StateTimer");
        _bubbleTimer = GetNode<Godot.Timer>("BubbleTimer");
        _stateTimer.Timeout += OnStateTimerTimeout;
        _bubbleTimer.Timeout += OnBubbleTimerTimeout;
        _random.Randomize();

        _bubbleLabel.Modulate = new Color(0.18f, 0.29f, 0.24f, 0.0f);
        _bubbleLabel.Scale = new Vector2(0.96f, 0.96f);

        if (_definition is not null && _ownedPet is not null)
        {
            RefreshView();
            ApplyLifeStatePresentation(true);
        }
    }

    /// <summary>持续给宠物卡片增加轻微的呼吸和漂浮感。</summary>
    public override void _Process(double delta)
    {
        _motionClock += (float)delta;

        var breatheAmount = Mathf.Sin(_motionClock * 2.1f + (_parkIndex * 0.65f)) * 0.012f;
        var bobAmount = Mathf.Sin(_motionClock * 1.7f + (_parkIndex * 0.9f)) * 1.8f;

        _motionRoot.Position = _stateOffset + new Vector2(0.0f, bobAmount);
        _motionRoot.Scale = Vector2.One * (1.0f + breatheAmount);
    }

    /// <summary>把宠物定义、存档和顺序索引写入组件。</summary>
    public void Configure(PetDefinition definition, OwnedPetData ownedPet, int parkIndex)
    {
        _definition = definition;
        _ownedPet = ownedPet;
        _parkIndex = parkIndex;
        _currentLifeState = ResolveInitialState(ownedPet, parkIndex);

        if (IsNodeReady())
        {
            RefreshView();
            ApplyLifeStatePresentation(true);
        }
    }

    /// <summary>根据当前定义和状态刷新组件上的文本与样式。</summary>
    private void RefreshView()
    {
        if (_definition is null || _ownedPet is null)
        {
            return;
        }

        AddThemeStyleboxOverride("panel", CreateCardStyle(_definition.ColorHex));
        _nameLabel.Text = _definition.DisplayName;
        _speciesLabel.Text = _definition.Species;
        _detailLabel.Text = BuildDetailText(_definition, _parkIndex);
        UpdateBubbleText();
    }

    /// <summary>状态定时器触发时切换到下一种生活状态，并重新播放气泡与移动节奏。</summary>
    private void OnStateTimerTimeout()
    {
        if (_definition is null || _ownedPet is null)
        {
            return;
        }

        _currentLifeState = PickNextLifeState(_currentLifeState);
        _ownedPet.CurrentParkState = BuildPersistentStateText(_definition, _currentLifeState);
        ApplyLifeStatePresentation(false);
    }

    /// <summary>气泡显示时长结束后自动隐藏，让气泡以节奏化的方式出现。</summary>
    private void OnBubbleTimerTimeout()
    {
        HideBubble();
    }

    /// <summary>根据当前状态统一刷新文本、位移和气泡出现节奏。</summary>
    private void ApplyLifeStatePresentation(bool immediate)
    {
        if (_definition is null)
        {
            return;
        }

        UpdateBubbleText();
        _detailLabel.Text = BuildDetailText(_definition, _parkIndex);
        AnimateToStateOffset(ResolveStateOffset(_currentLifeState), immediate);
        ShowBubble();
        StartNextBubbleTimer();
        StartNextStateTimer(immediate);
    }

    /// <summary>刷新气泡文本。</summary>
    private void UpdateBubbleText()
    {
        if (_definition is null)
        {
            return;
        }

        _bubbleLabel.Text = $"状态气泡：{BuildBubbleText(_definition, _currentLifeState)}";
    }

    /// <summary>显示状态气泡并做轻微淡入。</summary>
    private void ShowBubble()
    {
        _bubbleTween?.Kill();
        _bubbleLabel.Visible = true;
        _bubbleTween = CreateTween();
        _bubbleTween.SetTrans(Tween.TransitionType.Sine);
        _bubbleTween.SetEase(Tween.EaseType.Out);
        _bubbleTween.TweenProperty(_bubbleLabel, "modulate", new Color(0.18f, 0.29f, 0.24f, 0.92f), 0.22f);
        _bubbleTween.Parallel().TweenProperty(_bubbleLabel, "scale", Vector2.One, 0.22f);
    }

    /// <summary>隐藏状态气泡，让下一个状态刷新时再重新出现。</summary>
    private void HideBubble()
    {
        _bubbleTween?.Kill();
        _bubbleTween = CreateTween();
        _bubbleTween.SetTrans(Tween.TransitionType.Sine);
        _bubbleTween.SetEase(Tween.EaseType.In);
        _bubbleTween.TweenProperty(_bubbleLabel, "modulate", new Color(0.18f, 0.29f, 0.24f, 0.0f), 0.20f);
        _bubbleTween.Parallel().TweenProperty(_bubbleLabel, "scale", new Vector2(0.96f, 0.96f), 0.20f);
    }

    /// <summary>把视觉根节点移动到当前状态对应的轻微偏移位置。</summary>
    private void AnimateToStateOffset(Vector2 targetOffset, bool immediate)
    {
        _movementTween?.Kill();
        _stateOffset = targetOffset;

        if (immediate)
        {
            _motionRoot.Position = targetOffset;
            return;
        }

        _movementTween = CreateTween();
        _movementTween.SetTrans(Tween.TransitionType.Sine);
        _movementTween.SetEase(Tween.EaseType.Out);
        _movementTween.TweenProperty(_motionRoot, "position", targetOffset, _random.RandfRange(0.65f, 1.10f));
    }

    /// <summary>根据当前状态选择下一种状态，尽量避免连续停在同一状态。</summary>
    private PetLifeState PickNextLifeState(PetLifeState currentState)
    {
        var candidates = new[]
        {
            PetLifeState.Resting,
            PetLifeState.Wandering,
            PetLifeState.Watching,
            PetLifeState.Playing,
        };

        var nextState = currentState;
        while (nextState == currentState)
        {
            nextState = candidates[_random.RandiRange(0, candidates.Length - 1)];
        }

        return nextState;
    }

    /// <summary>根据存档里的初始状态文案决定一个较稳定的初始生活状态。</summary>
    private static PetLifeState ResolveInitialState(OwnedPetData ownedPet, int parkIndex)
    {
        var stateText = ownedPet.CurrentParkState ?? string.Empty;
        if (stateText.Contains("休息", StringComparison.Ordinal) || stateText.Contains("晒太阳", StringComparison.Ordinal))
        {
            return PetLifeState.Resting;
        }

        if (stateText.Contains("散步", StringComparison.Ordinal) || stateText.Contains("巡逻", StringComparison.Ordinal))
        {
            return PetLifeState.Wandering;
        }

        if (stateText.Contains("观察", StringComparison.Ordinal) || stateText.Contains("张望", StringComparison.Ordinal))
        {
            return PetLifeState.Watching;
        }

        return parkIndex % 2 == 0 ? PetLifeState.Resting : PetLifeState.Playing;
    }

    /// <summary>按当前状态生成玩家可见的气泡文案。</summary>
    private static string BuildBubbleText(PetDefinition definition, PetLifeState state)
    {
        return state switch
        {
            PetLifeState.Resting => $"{definition.DisplayName} 正在{definition.ParkActivityText}",
            PetLifeState.Wandering => $"{definition.DisplayName} 在乐园里慢慢散步",
            PetLifeState.Watching => $"{definition.DisplayName} 正在四处观察新朋友",
            PetLifeState.Playing => $"{definition.DisplayName} 想和你一起玩一会",
            _ => $"{definition.DisplayName} 正在熟悉新家",
        };
    }

    /// <summary>生成写回运行时宠物对象的状态文案。</summary>
    private static string BuildPersistentStateText(PetDefinition definition, PetLifeState state)
    {
        return state switch
        {
            PetLifeState.Resting => definition.ParkActivityText,
            PetLifeState.Wandering => "散步中",
            PetLifeState.Watching => "观察四周",
            PetLifeState.Playing => "想玩球",
            _ => definition.ParkActivityText,
        };
    }

    /// <summary>按入住顺序生成一段轻量补充说明。</summary>
    private static string BuildDetailText(PetDefinition definition, int parkIndex)
    {
        return parkIndex switch
        {
            0 => $"{definition.DisplayName} 是第一位入住伙伴，最先把乐园热闹起来。",
            1 => $"{definition.DisplayName} 已经开始和其他伙伴互相熟悉了。",
            _ => $"{definition.DisplayName} 正在慢慢形成自己的生活节奏。",
        };
    }

    /// <summary>根据当前状态生成一个轻微位移目标，让宠物看起来在乐园中活动。</summary>
    private Vector2 ResolveStateOffset(PetLifeState state)
    {
        return state switch
        {
            PetLifeState.Resting => new Vector2(_random.RandfRange(-2.0f, 2.0f), _random.RandfRange(2.0f, 5.0f)),
            PetLifeState.Wandering => new Vector2(_random.RandfRange(-10.0f, 10.0f), _random.RandfRange(-1.0f, 4.0f)),
            PetLifeState.Watching => new Vector2(_random.RandfRange(-4.0f, 4.0f), _random.RandfRange(-6.0f, -2.0f)),
            PetLifeState.Playing => new Vector2(_random.RandfRange(-12.0f, 12.0f), _random.RandfRange(-3.0f, 3.0f)),
            _ => Vector2.Zero,
        };
    }

    /// <summary>启动下一次状态切换计时，让每只宠物有自己的刷新节奏。</summary>
    private void StartNextStateTimer(bool initialRefresh)
    {
        var waitTime = initialRefresh
            ? _random.RandfRange(1.4f, 2.4f)
            : _random.RandfRange(3.2f, 5.0f);

        _stateTimer.Start(waitTime);
    }

    /// <summary>启动气泡隐藏计时，让气泡出现一段时间后自动收起。</summary>
    private void StartNextBubbleTimer()
    {
        _bubbleTimer.Start(_random.RandfRange(1.6f, 2.4f));
    }

    /// <summary>根据宠物主色创建统一的卡片样式。</summary>
    private static StyleBoxFlat CreateCardStyle(string colorHex)
    {
        var baseColor = Color.FromString(colorHex, new Color(0.92f, 0.85f, 0.72f, 1.0f));
        return new StyleBoxFlat
        {
            BgColor = baseColor.Lightened(0.12f),
            BorderColor = baseColor.Darkened(0.18f),
            CornerRadiusTopLeft = 24,
            CornerRadiusTopRight = 24,
            CornerRadiusBottomRight = 24,
            CornerRadiusBottomLeft = 24,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            ShadowColor = new Color(0, 0, 0, 0.10f),
            ShadowSize = 6,
        };
    }
}
