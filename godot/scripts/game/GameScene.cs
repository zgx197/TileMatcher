using Godot;

namespace TileMatcher.Game;

public partial class GameScene : Node2D
{
    public override void _Ready()
    {
        RenderingServer.SetDefaultClearColor(new Color(0.07f, 0.42f, 0.29f, 1.0f));

        var levelValue = GetNodeOrNull<Label>("UI/Root/TopBar/Stats/LevelBox/Value");
        var scoreValue = GetNodeOrNull<Label>("UI/Root/TopBar/Stats/ScoreBox/Value");
        var matchValue = GetNodeOrNull<Label>("UI/Root/TopBar/Stats/MatchBox/Value");

        if (levelValue is not null)
        {
            levelValue.Text = "1";
        }

        if (scoreValue is not null)
        {
            scoreValue.Text = "0";
        }

        if (matchValue is not null)
        {
            matchValue.Text = "0";
        }
    }
}
