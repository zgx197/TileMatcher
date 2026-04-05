using System;
using Godot;
using TileMatcher.Pets;

namespace TileMatcher.Home;

/// <summary>
/// 宠物乐园中的单只宠物视图。
/// 当前阶段统一用彩色圆角矩形承载宠物，再配合轻微巡游和气泡文案表现状态。
/// </summary>
public partial class PetActorView : Control
{
    /// <summary>宠物在乐园中的简化生活状态。</summary>
    private enum PetLifeState
    {
        Resting,
        Wandering,
        Watching,
        Playing,
    }

    private static readonly string[] RestingTexts = ["正在晒太阳", "正趴着打盹", "正在懒洋洋休息"];
    private static readonly string[] WanderingTexts = ["正在慢慢散步", "正在巡逻乐园", "正在找舒服的角落"];
    private static readonly string[] WatchingTexts = ["正在四处张望", "正在观察新朋友", "正在悄悄看你"];
    private static readonly string[] PlayingTexts = ["正在开心玩耍", "正在追着空气跑", "正在原地蹦跶"];

    private const float ActorWidth = 112.0f;
    private const float ActorHeight = 132.0f;
    private const float BodyWidth = 88.0f;
    private const float BodyHeight = 66.0f;

    /// <summary>状态气泡面板。</summary>
    private PanelContainer _bubblePanel = null!;

    /// <summary>状态气泡文本。</summary>
    private Label _bubbleLabel = null!;

    /// <summary>宠物主体圆角矩形。</summary>
    private PanelContainer _actorBody = null!;

    /// <summary>宠物名字文本。</summary>
    private Label _nameLabel = null!;

    /// <summary>状态切换定时器。</summary>
    private Godot.Timer _stateTimer = null!;

    /// <summary>巡游换目标定时器。</summary>
    private Godot.Timer _wanderTimer = null!;

    /// <summary>气泡显示定时器。</summary>
    private Godot.Timer _bubbleTimer = null!;

    /// <summary>运行时随机数生成器。</summary>
    private readonly RandomNumberGenerator _random = new();

    /// <summary>当前绑定的宠物定义。</summary>
    private PetDefinition? _definition;

    /// <summary>当前绑定的宠物存档。</summary>
    private OwnedPetData? _ownedPet;

    /// <summary>当前宠物在乐园中的顺序索引。</summary>
    private int _parkIndex;

    /// <summary>乐园中的宠物总数。</summary>
    private int _parkCount = 1;

    /// <summary>当前生活状态。</summary>
    private PetLifeState _currentLifeState;

    /// <summary>宠物在乐园中的基准位置。</summary>
    private Vector2 _homePosition;

    /// <summary>当前巡游目标位置。</summary>
    private Vector2 _targetPosition;

    /// <summary>父容器上一次尺寸。</summary>
    private Vector2 _lastParentSize = Vector2.Zero;

    /// <summary>是否已经完成过初始摆放。</summary>
    private bool _placementInitialized;

    /// <summary>轻微漂浮与呼吸的时间累积。</summary>
    private float _motionClock;

    /// <summary>气泡动画。</summary>
    private Tween? _bubbleTween;

    /// <summary>初始化节点引用、定时器和默认表现。</summary>
    public override void _Ready()
    {
        CustomMinimumSize = new Vector2(ActorWidth, ActorHeight);
        MouseFilter = MouseFilterEnum.Ignore;

        _bubblePanel = GetNode<PanelContainer>("BubblePanel");
        _bubbleLabel = GetNode<Label>("BubblePanel/Margin/BubbleLabel");
        _actorBody = GetNode<PanelContainer>("ActorBody");
        _nameLabel = GetNode<Label>("NameLabel");
        _stateTimer = GetNode<Godot.Timer>("StateTimer");
        _wanderTimer = GetNode<Godot.Timer>("WanderTimer");
        _bubbleTimer = GetNode<Godot.Timer>("BubbleTimer");

        _stateTimer.Timeout += OnStateTimerTimeout;
        _wanderTimer.Timeout += OnWanderTimerTimeout;
        _bubbleTimer.Timeout += OnBubbleTimerTimeout;

        _random.Randomize();
        _bubblePanel.Visible = false;
        _bubblePanel.ZIndex = 20;
        _actorBody.ZIndex = 10;
        _bubblePanel.AddThemeStyleboxOverride("panel", CreateBubbleStyle());

        if (_definition is not null && _ownedPet is not null)
        {
            RefreshView();
            ApplyLifeStatePresentation(true);
        }
    }

    /// <summary>驱动宠物的巡游、漂浮和呼吸动画。</summary>
    public override void _Process(double delta)
    {
        if (_definition is null || _ownedPet is null)
        {
            return;
        }

        UpdatePlacementIfNeeded();

        _motionClock += (float)delta;
        Position = Position.MoveToward(_targetPosition, ResolveMoveSpeed(_currentLifeState) * (float)delta);

        var moveDistance = Position.DistanceTo(_targetPosition);
        var moveBlend = Mathf.Clamp(moveDistance / 28.0f, 0.1f, 1.0f);
        var bob = Mathf.Sin(_motionClock * 4.1f + (_parkIndex * 0.73f)) * 3.2f;
        var breathe = Mathf.Sin(_motionClock * 2.2f + (_parkIndex * 0.47f)) * 0.035f;
        var tilt = Mathf.Sin(_motionClock * 3.0f + (_parkIndex * 0.39f)) * 0.04f * moveBlend;

        _actorBody.Position = new Vector2((Size.X - BodyWidth) * 0.5f, 28.0f + bob);
        _actorBody.Scale = Vector2.One * (1.0f + breathe);
        _actorBody.Rotation = tilt;

        if (moveDistance < 2.0f && _wanderTimer.IsStopped())
        {
            StartNextWanderTimer(initialRefresh: false);
        }
    }

    /// <summary>
    /// 写入宠物定义、存档和乐园位置上下文。
    /// </summary>
    public void Configure(PetDefinition definition, OwnedPetData ownedPet, int parkIndex, int parkCount)
    {
        _definition = definition;
        _ownedPet = ownedPet;
        _parkIndex = parkIndex;
        _parkCount = Math.Max(1, parkCount);
        _currentLifeState = ResolveInitialState(ownedPet, parkIndex);

        if (IsNodeReady())
        {
            RefreshView();
            ApplyLifeStatePresentation(true);
        }
    }

    /// <summary>根据当前宠物定义和存档刷新基础视觉。</summary>
    private void RefreshView()
    {
        if (_definition is null || _ownedPet is null)
        {
            return;
        }

        var petName = ResolvePetName();
        var baseColor = ResolvePetColor();
        _nameLabel.Text = petName;
        _nameLabel.Modulate = baseColor.Darkened(0.55f);
        _actorBody.AddThemeStyleboxOverride("panel", CreateBodyStyle(baseColor));
        UpdateBubbleText();
    }

    /// <summary>把当前生活状态同步到文本、目标位置和定时器上。</summary>
    private void ApplyLifeStatePresentation(bool immediate)
    {
        if (_definition is null || _ownedPet is null)
        {
            return;
        }

        UpdateBubbleText();
        _ownedPet.CurrentParkState = BuildPersistentStateText(_currentLifeState);

        if (immediate)
        {
            UpdatePlacementIfNeeded(forceReset: true);
        }

        ChooseNextTarget();
        ShowBubble();
        StartNextBubbleTimer();
        StartNextStateTimer(immediate);
        StartNextWanderTimer(immediate);
    }

    /// <summary>状态切换计时结束后切到新的生活状态。</summary>
    private void OnStateTimerTimeout()
    {
        if (_definition is null || _ownedPet is null)
        {
            return;
        }

        _currentLifeState = PickNextLifeState(_currentLifeState);
        ApplyLifeStatePresentation(immediate: false);
    }

    /// <summary>巡游计时结束后重新挑选移动目标。</summary>
    private void OnWanderTimerTimeout()
    {
        ChooseNextTarget();
        StartNextWanderTimer(initialRefresh: false);
    }

    /// <summary>气泡显示时长结束后隐藏气泡。</summary>
    private void OnBubbleTimerTimeout()
    {
        HideBubble();
    }

    /// <summary>根据父容器尺寸和宠物顺序重新计算摆放位置。</summary>
    private void UpdatePlacementIfNeeded(bool forceReset = false)
    {
        if (GetParent() is not Control parent)
        {
            return;
        }

        if (!forceReset && _placementInitialized && parent.Size == _lastParentSize)
        {
            return;
        }

        _lastParentSize = parent.Size;
        var safeWidth = Mathf.Max(parent.Size.X, ActorWidth + 32.0f);
        var safeHeight = Mathf.Max(parent.Size.Y, ActorHeight + 32.0f);
        var columns = Math.Max(1, Mathf.CeilToInt(Mathf.Sqrt(_parkCount)));
        var rows = Math.Max(1, Mathf.CeilToInt((float)_parkCount / columns));
        var column = _parkIndex % columns;
        var row = _parkIndex / columns;
        var xSpacing = safeWidth / (columns + 1.0f);
        var ySpacing = (safeHeight - 36.0f) / (rows + 1.0f);
        var desiredCenter = new Vector2(
            xSpacing * (column + 1),
            26.0f + ySpacing * (row + 1));

        _homePosition = ClampToParent(desiredCenter - (Size * 0.5f));
        _targetPosition = _homePosition;
        Position = _homePosition;
        _placementInitialized = true;
    }

    /// <summary>在当前生活状态允许的活动范围内选一个巡游目标点。</summary>
    private void ChooseNextTarget()
    {
        var radius = ResolveRoamingRadius(_currentLifeState);
        var offset = new Vector2(
            _random.RandfRange(-radius.X, radius.X),
            _random.RandfRange(-radius.Y, radius.Y));

        _targetPosition = ClampToParent(_homePosition + offset);
    }

    /// <summary>把目标位置裁剪到父容器可视范围内。</summary>
    private Vector2 ClampToParent(Vector2 desiredPosition)
    {
        if (GetParent() is not Control parent)
        {
            return desiredPosition;
        }

        var maxX = Mathf.Max(0.0f, parent.Size.X - Size.X);
        var maxY = Mathf.Max(0.0f, parent.Size.Y - Size.Y);
        return new Vector2(
            Mathf.Clamp(desiredPosition.X, 0.0f, maxX),
            Mathf.Clamp(desiredPosition.Y, 0.0f, maxY));
    }

    /// <summary>根据当前状态刷新气泡文案。</summary>
    private void UpdateBubbleText()
    {
        if (_definition is null)
        {
            return;
        }

        _bubbleLabel.Text = BuildBubbleText(ResolvePetName(), _currentLifeState);
    }

    /// <summary>播放气泡出现动画。</summary>
    private void ShowBubble()
    {
        _bubbleTween?.Kill();
        _bubblePanel.Visible = true;
        _bubblePanel.Scale = new Vector2(0.92f, 0.92f);
        _bubblePanel.Modulate = new Color(1, 1, 1, 0);

        _bubbleTween = CreateTween();
        _bubbleTween.SetParallel(true);
        _bubbleTween.TweenProperty(_bubblePanel, "scale", Vector2.One, 0.22f)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
        _bubbleTween.TweenProperty(_bubblePanel, "modulate", Colors.White, 0.18f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.Out);
    }

    /// <summary>播放气泡消失动画。</summary>
    private void HideBubble()
    {
        _bubbleTween?.Kill();
        _bubbleTween = CreateTween();
        _bubbleTween.SetParallel(true);
        _bubbleTween.TweenProperty(_bubblePanel, "scale", new Vector2(0.94f, 0.94f), 0.18f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.In);
        _bubbleTween.TweenProperty(_bubblePanel, "modulate", new Color(1, 1, 1, 0), 0.16f)
            .SetTrans(Tween.TransitionType.Sine)
            .SetEase(Tween.EaseType.In);
        _bubbleTween.Finished += () => _bubblePanel.Visible = false;
    }

    /// <summary>为下一次状态切换启动计时器。</summary>
    private void StartNextStateTimer(bool initialRefresh)
    {
        var waitTime = initialRefresh
            ? _random.RandfRange(1.8f, 2.8f)
            : _random.RandfRange(4.4f, 6.8f);
        _stateTimer.Start(waitTime);
    }

    /// <summary>为下一次巡游目标切换启动计时器。</summary>
    private void StartNextWanderTimer(bool initialRefresh)
    {
        var waitTime = initialRefresh
            ? _random.RandfRange(0.8f, 1.2f)
            : _random.RandfRange(1.4f, 2.6f);
        _wanderTimer.Start(waitTime);
    }

    /// <summary>为下一次自动收起气泡启动计时器。</summary>
    private void StartNextBubbleTimer()
    {
        _bubbleTimer.Start(_random.RandfRange(2.0f, 3.4f));
    }

    /// <summary>解析当前应展示的宠物名字。</summary>
    private string ResolvePetName()
    {
        if (_ownedPet is null)
        {
            return _definition?.DisplayName ?? "宠物";
        }

        return string.IsNullOrWhiteSpace(_ownedPet.PetName)
            ? _definition?.DisplayName ?? "宠物"
            : _ownedPet.PetName;
    }

    /// <summary>解析宠物主体颜色，尽量做到同一宠物稳定且彼此有差异。</summary>
    private Color ResolvePetColor()
    {
        if (_ownedPet is null)
        {
            return Color.FromString(_definition?.ColorHex ?? "#D9C4A0", new Color(0.82f, 0.66f, 0.48f, 1.0f));
        }

        var seedText = $"{_ownedPet.PetId}|{_ownedPet.PetName}|{_ownedPet.AdoptedAtUtc}";
        var hash = (uint)seedText.GetHashCode();
        var hue = (hash % 360u) / 360.0f;
        var saturation = 0.38f + (((hash >> 9) % 26u) / 100.0f);
        var value = 0.82f + (((hash >> 17) % 12u) / 100.0f);
        return Color.FromHsv(hue, saturation, value, 1.0f);
    }

    /// <summary>根据存档文案和顺序索引推断初始生活状态。</summary>
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

        return parkIndex % 2 == 0 ? PetLifeState.Wandering : PetLifeState.Playing;
    }

    /// <summary>随机挑选一个与当前不同的下一状态。</summary>
    private PetLifeState PickNextLifeState(PetLifeState currentState)
    {
        var nextState = currentState;
        while (nextState == currentState)
        {
            nextState = (PetLifeState)_random.RandiRange(0, 3);
        }

        return nextState;
    }

    /// <summary>根据状态决定宠物允许活动的半径。</summary>
    private Vector2 ResolveRoamingRadius(PetLifeState state)
    {
        return state switch
        {
            PetLifeState.Resting => new Vector2(8.0f, 6.0f),
            PetLifeState.Watching => new Vector2(16.0f, 12.0f),
            PetLifeState.Playing => new Vector2(38.0f, 22.0f),
            _ => new Vector2(26.0f, 18.0f),
        };
    }

    /// <summary>根据状态决定移动速度。</summary>
    private float ResolveMoveSpeed(PetLifeState state)
    {
        return state switch
        {
            PetLifeState.Resting => 12.0f,
            PetLifeState.Watching => 22.0f,
            PetLifeState.Playing => 42.0f,
            _ => 30.0f,
        };
    }

    /// <summary>拼装状态气泡完整文案。</summary>
    private string BuildBubbleText(string petName, PetLifeState state)
    {
        var action = PickStateText(state);
        return $"{petName}{action}";
    }

    /// <summary>生成写回存档的简短状态文本。</summary>
    private string BuildPersistentStateText(PetLifeState state)
    {
        return state switch
        {
            PetLifeState.Resting => "休息中",
            PetLifeState.Wandering => "散步中",
            PetLifeState.Watching => "观察中",
            PetLifeState.Playing => "玩耍中",
            _ => "休息中",
        };
    }

    /// <summary>从状态对应的随机文案池中挑一句话。</summary>
    private string PickStateText(PetLifeState state)
    {
        var candidates = state switch
        {
            PetLifeState.Resting => RestingTexts,
            PetLifeState.Watching => WatchingTexts,
            PetLifeState.Playing => PlayingTexts,
            _ => WanderingTexts,
        };

        return candidates[_random.RandiRange(0, candidates.Length - 1)];
    }

    /// <summary>创建状态气泡的统一样式。</summary>
    private static StyleBoxFlat CreateBubbleStyle()
    {
        return new StyleBoxFlat
        {
            BgColor = new Color(1.0f, 0.99f, 0.95f, 0.97f),
            BorderColor = new Color(0.86f, 0.75f, 0.57f, 1.0f),
            CornerRadiusTopLeft = 16,
            CornerRadiusTopRight = 16,
            CornerRadiusBottomRight = 16,
            CornerRadiusBottomLeft = 16,
            BorderWidthLeft = 2,
            BorderWidthTop = 2,
            BorderWidthRight = 2,
            BorderWidthBottom = 2,
            ShadowColor = new Color(0, 0, 0, 0.12f),
            ShadowSize = 4,
        };
    }

    /// <summary>根据主题色创建宠物主体样式。</summary>
    private static StyleBoxFlat CreateBodyStyle(Color baseColor)
    {
        return new StyleBoxFlat
        {
            BgColor = baseColor,
            BorderColor = baseColor.Darkened(0.22f),
            CornerRadiusTopLeft = 24,
            CornerRadiusTopRight = 24,
            CornerRadiusBottomRight = 24,
            CornerRadiusBottomLeft = 24,
            BorderWidthLeft = 3,
            BorderWidthTop = 3,
            BorderWidthRight = 3,
            BorderWidthBottom = 3,
            ShadowColor = new Color(0, 0, 0, 0.16f),
            ShadowSize = 6,
            ContentMarginLeft = 12,
            ContentMarginTop = 12,
            ContentMarginRight = 12,
            ContentMarginBottom = 12,
        };
    }
}
