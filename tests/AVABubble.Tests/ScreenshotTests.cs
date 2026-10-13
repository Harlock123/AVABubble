using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AVABubble.Engine;
using AVABubble.Models;
using AVABubble.Views;
using Controls = AVABubble.Engine.Controls;

namespace AVABubble.Tests;

/// <summary>
/// Draws the game in a real (headless) window at the moments worth showing. Set BUBBLE_SHOTS to a
/// folder to save them as PNGs: the README's screenshots come from here.
/// </summary>
public sealed class ScreenshotTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "bubble-ui-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private (Window W, GameCanvas Canvas, GameEngine G) Open(int seed = 11)
    {
        var g = new GameEngine(seed, HighScoreTable.Load(_dir));
        var canvas = new GameCanvas(g, new SoundManager(silent: true), startTimer: false);
        var w = new Window { Width = 1024, Height = 768, Content = canvas };
        w.Show();
        Pump();
        return (w, canvas, g);
    }

    private static void Pump()
    {
        for (int i = 0; i < 10; i++) Dispatcher.UIThread.RunJobs();
    }

    private static void Shot(Window w, GameCanvas c, string name)
    {
        c.InvalidateVisual();
        Pump();
        var frame = w.CaptureRenderedFrame();
        Assert.NotNull(frame);
        var dir = Environment.GetEnvironmentVariable("BUBBLE_SHOTS");
        if (string.IsNullOrEmpty(dir)) return;
        Directory.CreateDirectory(dir);
        frame.Save(Path.Combine(dir, $"AVABubble-{name}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
    }

    [AvaloniaFact]
    public void The_title_and_a_level_under_way()
    {
        var (w, c, g) = Open();
        g.Idle(60 * 2);
        Shot(w, c, "title");
        g.StartGame();
        g.Go();
        var pilot = new AttractPilot(2);
        for (int i = 0; i < 60 * 60 && g.State == GameState.Playing; i++)
        {
            g.Tick(pilot.Decide(g));
            if (i > 60 * 4 && g.Phase == Phase.Playing && g.Bubbles.Count >= 2 && g.Bubbles.Any(b => b.Monster is not null)) break;
        }
        Shot(w, c, "play");
        w.Close();
    }

    [AvaloniaFact]
    public void A_crowd_in_bubbles_and_the_fruit()
    {
        var (w, c, g) = Open(seed: 3);
        g.StartGame();
        g.JumpTo(9);
        int n = 0;
        foreach (var m in g.Monsters.Take(4))
        {
            var b = g.PutBubble(new Vec(80 + n * 16, 40 + (n % 2) * 8));
            b.Monster = m;
            m.Trapped = b;
            b.Pos = b.Pos;
            n++;
        }
        g.Letters[0] = g.Letters[2] = g.Letters[3] = true;
        g.PutBubble(new Vec(200, 60), letter: 1);
        for (int i = 0; i < 20; i++) g.Tick(default);
        Shot(w, c, "bubbles");
        // Pop them all at once.
        g.PutHero(g.Bubbles.First(b => b.Monster is not null).Pos + new Vec(0, 8));
        g.Tick(default);
        for (int i = 0; i < 50; i++) g.Tick(default);
        Shot(w, c, "fruit");
        w.Close();
    }

    [AvaloniaFact]
    public void Hurry_up_and_the_shade()
    {
        var (w, c, g) = Open(seed: 5);
        g.StartGame();
        g.JumpTo(4);
        g.SkipTime(GameEngine.ShadeSeconds - 1);
        var pilot = new AttractPilot(5);
        for (int i = 0; i < 60 * 3 && g.Phase == Phase.Playing; i++) g.Tick(pilot.Decide(g));
        Shot(w, c, "shade");
        w.Close();
    }

    [AvaloniaFact]
    public void Game_over_and_the_high_scores()
    {
        var (w, c, g) = Open(seed: 6);
        g.StartGame();
        g.Go();
        while (g.State == GameState.Playing) g.Tick(new Controls(1));
        Assert.Equal(GameState.GameOver, g.State);
        g.Idle(20);
        g.Idle(60 * 4);
        if (g.State == GameState.EnterInitials) { g.Initials!.Finish(); g.Idle(5); }
        Assert.Equal(GameState.HighScores, g.State);
        g.Idle(20);
        Shot(w, c, "high-scores");
        w.Close();
    }
}
