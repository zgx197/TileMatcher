using Godot;
using TileMatcher.Config;
using TileMatcher.Game;
using TileMatcher.Home;
using TileMatcher.Result;

namespace TileMatcher.App;

/// <summary>
/// 外围流程主路由。
/// 当前版本负责三件事：
/// 1. 在主页、游戏页、结算页之间切换
/// 2. 读取关卡目录和规则目录，拼装页面所需的展示文案
/// 3. 维护“当前关卡号”与“下一关预告”的最小流程状态
/// </summary>
public partial class AppRoot : Node
{
    private const string DefaultLevelCatalogPath = "res://configs/levels/default_levels.tres";
    private const string DefaultProfileCatalogPath = "res://configs/layout_profiles/default_catalog.tres";

    [Export]
    public PackedScene HomePageScene { get; set; } = null!;

    [Export]
    public PackedScene GamePageScene { get; set; } = null!;

    [Export]
    public PackedScene LevelCompletePageScene { get; set; } = null!;

    [Export]
    public LevelCatalog LevelCatalog { get; set; } = null!;

    [Export]
    public LayoutProfileCatalog ProfileCatalog { get; set; } = null!;

    private Node _currentPage = null!;
    private int _currentLevelNumber = 1;

    public override void _Ready()
    {
        HomePageScene ??= GD.Load<PackedScene>("res://scenes/home/HomePage.tscn");
        GamePageScene ??= GD.Load<PackedScene>("res://scenes/game/GameScene.tscn");
        LevelCompletePageScene ??= GD.Load<PackedScene>("res://scenes/result/LevelCompletePage.tscn");

        // AppRoot 统一兜底加载目录资源，避免页面层各自维护同一份入口数据。
        EnsureCatalogsLoaded();
        ShowHomePage();
    }

    /// <summary>显示主页，并把当前关卡标题与规则摘要显示出来。</summary>
    private void ShowHomePage()
    {
        var homePage = HomePageScene.Instantiate<HomePage>();
        var level = GetLevelOrFallback(_currentLevelNumber);

        // 主页只接收轻量展示数据，不直接依赖目录资源或牌桌运行时对象。

        homePage.Configure(
            _currentLevelNumber,
            "青雀旅人",
            1,
            BuildLevelTitle(level, _currentLevelNumber),
            BuildLevelSummary(level));
        homePage.StartGameRequested += OnStartGameRequested;

        SwitchToPage(homePage);
    }

    /// <summary>进入游戏页。</summary>
    private void ShowGamePage(int levelNumber)
    {
        var gamePage = GamePageScene.Instantiate<GameScene>();
        gamePage.AutoStartPrototype = false;
        gamePage.LevelCompleted += OnLevelCompleted;
        gamePage.BackToHomeRequested += OnBackToHomeRequested;

        SwitchToPage(gamePage);
        // 页面切换完成后再显式启动关卡，避免旧页面还在释放时就访问新页面节点。
        gamePage.StartLevel(levelNumber);
    }

    /// <summary>显示结算页，并同时传入下一关预告。</summary>
    private void ShowLevelCompletePage(LevelCompleteResult result)
    {
        var completePage = LevelCompletePageScene.Instantiate<LevelCompletePage>();
        completePage.Configure(result);
        completePage.ContinueRequested += OnContinueRequested;
        completePage.ReturnHomeRequested += OnBackToHomeRequested;

        SwitchToPage(completePage);
    }

    /// <summary>切换当前页面实例。</summary>
    private void SwitchToPage(Node nextPage)
    {
        if (_currentPage is not null)
        {
            RemoveChild(_currentPage);
            _currentPage.QueueFree();
        }

        _currentPage = nextPage;
        AddChild(_currentPage);
    }

    private void OnStartGameRequested(int levelNumber)
    {
        _currentLevelNumber = levelNumber;
        ShowGamePage(levelNumber);
    }

    private void OnLevelCompleted(LevelCompleteResult result)
    {
        var nextLevelNumber = result.LevelNumber + 1;
        var nextLevel = GetLevelOrFallback(nextLevelNumber);

        // 这里把下一关预告一次性组装好，结果页只负责展示和转发按钮事件。

        _currentLevelNumber = nextLevelNumber;
        ShowLevelCompletePage(new LevelCompleteResult
        {
            LevelNumber = result.LevelNumber,
            Score = result.Score,
            MatchCount = result.MatchCount,
            ElapsedText = result.ElapsedText,
            NextLevelNumber = nextLevelNumber,
            NextLevelName = BuildLevelTitle(nextLevel, nextLevelNumber),
            NextLevelSummary = BuildLevelSummary(nextLevel),
        });
    }

    private void OnContinueRequested(int nextLevelNumber)
    {
        _currentLevelNumber = nextLevelNumber;
        ShowGamePage(nextLevelNumber);
    }

    private void OnBackToHomeRequested()
    {
        ShowHomePage();
    }

    /// <summary>若未在 Inspector 中绑定目录资源，则从默认路径回退加载。</summary>
    private void EnsureCatalogsLoaded()
    {
        LevelCatalog ??= GD.Load<LevelCatalog>(DefaultLevelCatalogPath);
        ProfileCatalog ??= GD.Load<LayoutProfileCatalog>(DefaultProfileCatalogPath);

        if (LevelCatalog is null)
        {
            GD.PushError($"[AppRoot] 无法加载关卡目录: {DefaultLevelCatalogPath}");
        }

        if (ProfileCatalog is null)
        {
            GD.PushError($"[AppRoot] 无法加载规则目录: {DefaultProfileCatalogPath}");
        }
    }

    /// <summary>按关卡号读取配置，找不到时按目录默认关卡回退。</summary>
    private LevelConfig? GetLevelOrFallback(int levelNumber)
    {
        if (LevelCatalog is null)
        {
            return null;
        }

        foreach (var level in LevelCatalog.Levels)
        {
            if (level is not null && level.LevelNumber == levelNumber)
            {
                return level;
            }
        }

        foreach (var level in LevelCatalog.Levels)
        {
            if (level is not null && level.LevelNumber == LevelCatalog.DefaultLevelNumber)
            {
                return level;
            }
        }

        return null;
    }

    /// <summary>构造页面上展示的关卡标题。</summary>
    private static string BuildLevelTitle(LevelConfig? level, int fallbackLevelNumber)
    {
        if (level is null)
        {
            return $"关卡 {fallbackLevelNumber}";
        }

        return string.IsNullOrWhiteSpace(level.DisplayName)
            ? $"关卡 {level.LevelNumber}"
            : level.DisplayName;
    }

    /// <summary>根据关卡配置和规则档案拼出主页/结算页的短摘要。</summary>
    private string BuildLevelSummary(LevelConfig? level)
    {
        if (level is null)
        {
            return "未找到关卡配置，进入后将回退到默认布局。";
        }

        var profileName = ResolveProfileDisplayName(level.LayoutProfileId);
        // 主页和结算页都只需要简短摘要，不复用调试面板里的长规则说明。
        var sourceText = level.LayoutSourceMode switch
        {
            LevelLayoutSourceMode.Prototype => "固定原型牌桌",
            LevelLayoutSourceMode.RandomGenerated => level.UseFixedSeed
                ? $"固定种子随机布局 #{level.RandomSeed}"
                : "动态随机布局",
            _ => "未知布局模式",
        };

        return $"规则档案：{profileName} | 布局：{sourceText}";
    }

    /// <summary>把规则档案 id 转成更适合页面展示的名字。</summary>
    private string ResolveProfileDisplayName(string profileId)
    {
        if (ProfileCatalog is null || string.IsNullOrWhiteSpace(profileId))
        {
            return "默认规则";
        }

        foreach (var profile in ProfileCatalog.Profiles)
        {
            if (profile is not null && profile.ProfileId == profileId)
            {
                return string.IsNullOrWhiteSpace(profile.DisplayName)
                    ? profile.ProfileId
                    : profile.DisplayName;
            }
        }

        return profileId;
    }
}
