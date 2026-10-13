using AVABubble.Engine;
using AVABubble.Models;

namespace AVABubble.Tests;

public sealed class GameplayTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "bubble-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private const double Floor = 25 * Field.Tile;

    /// <summary>A level under way with no monsters unless put there.</summary>
    private GameEngine Calm(int level = 0, int seed = 1)
    {
        var g = new GameEngine(seed, new HighScoreTable(_dir));
        g.StartGame();
        g.JumpTo(level);
        g.Calm();
        return g;
    }

    private static void Steps(GameEngine g, int n, Controls c = default)
    {
        for (int i = 0; i < n; i++) g.Tick(c);
    }

    private static readonly Controls Right = new(1), Left = new(-1);

    /// <summary>Taps blow: one bubble a press.</summary>
    private static void BlowOnce(GameEngine g)
    {
        g.Tick(new Controls(0, Blow: true));
        g.Tick(default);
    }

    [Fact]
    public void The_levels_are_all_there()
    {
        Assert.Equal(12, Levels.All.Length);
        foreach (var l in Levels.All)
        {
            Assert.InRange(l.Monsters.Count, 3, 7);
            Assert.Equal(Floor, l.Start.Y);
            for (int c = 14; c < 18; c++) { Assert.Equal(Tile.Empty, l.Tiles[c, 0]); Assert.Equal(Tile.Empty, l.Tiles[c, Field.Rows - 1]); }
        }
    }

    [Fact]
    public void The_hero_walks_jumps_up_through_platforms_and_lands_on_them()
    {
        var g = Calm();
        g.PutHero(new Vec(40, Floor));
        Steps(g, 30, Right);
        Assert.True(g.Hero.Pos.X > 50);
        Assert.True(g.Hero.OnGround);
        // Level 1 has a platform at row 19, cols 4 to 11: jump up through it from below and land on top.
        g.PutHero(new Vec(60, Floor));
        g.Tick(new Controls(0, Jump: true));
        Steps(g, 90);
        Assert.Equal(19 * Field.Tile, g.Hero.Pos.Y);
        Assert.True(g.Hero.OnGround);
        // Off the end: down it goes.
        Steps(g, 40, Right);
        Steps(g, 90);
        Assert.Equal(Floor, g.Hero.Pos.Y);
    }

    [Fact]
    public void Walls_stop_the_hero_and_the_gap_in_the_floor_wraps_to_the_top()
    {
        var g = Calm();
        g.PutHero(new Vec(40, Floor));
        Steps(g, 120, Left);
        Assert.True(g.Hero.Pos.X >= 2 * Field.Tile + g.Hero.HalfW - 0.01);
        g.PutHero(new Vec(128, Floor));
        bool wrapped = false;
        for (int i = 0; i < 120 && !wrapped; i++) { g.Tick(default); wrapped = g.Hero.Pos.Y < 60; }
        Assert.True(wrapped);
    }

    [Fact]
    public void A_blown_bubble_traps_a_monster_and_popping_it_scores_and_leaves_fruit()
    {
        var g = Calm();
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.PutHero(new Vec(40, Floor));
        g.Hero.FacingLeft = false;
        var m = g.PutMonster(MonsterKind.Walker, new Vec(80, Floor));
        g.PutMonster(MonsterKind.Walker, new Vec(200, 7 * Field.Tile)); // keeps the level going
        BlowOnce(g);
        for (int i = 0; i < 30 && m.Trapped is null; i++) g.Tick(default);
        Assert.NotNull(m.Trapped);
        Assert.Contains(Sfx.Trap, heard);
        // Up it floats; go after it and pop it.
        var b = m.Trapped!;
        g.PutHero(b.Pos + new Vec(0, 8));
        g.Tick(default);
        Assert.True(m.Dead);
        Assert.Equal(1000, g.Score);
        Assert.Single(g.Fruits);
        Steps(g, 150);
        var fruit = g.Fruits[0];
        Assert.True(fruit.OnGround);
        g.PutHero(fruit.Pos);
        g.Tick(default);
        Assert.Empty(g.Fruits);
        Assert.True(g.Score > 1000);
        Assert.Contains(Sfx.Fruit, heard);
    }

    [Fact]
    public void Popping_a_crowd_of_trapped_monsters_doubles_each_time()
    {
        var g = Calm();
        g.PutMonster(MonsterKind.Walker, new Vec(200, 7 * Field.Tile));
        var bubbles = new List<Bubble>();
        for (int i = 0; i < 3; i++)
        {
            var m = g.PutMonster(MonsterKind.Walker, new Vec(60 + i * 15, 60));
            var b = g.PutBubble(new Vec(60 + i * 15, 50));
            b.Monster = m;
            m.Trapped = b;
            bubbles.Add(b);
        }
        g.PutHero(new Vec(60, 60));
        g.Tick(default);
        Assert.All(bubbles, b => Assert.True(b.Popped));
        Assert.Equal(1000 + 2000 + 4000, g.Score);
    }

    [Fact]
    public void Holding_jump_bounces_off_the_top_of_a_bubble()
    {
        var g = Calm();
        var b = g.PutBubble(new Vec(232, 120));
        b.Life = 100;
        g.PutHero(new Vec(232, 100));
        g.Hero.Vel = new Vec(0, 80);
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        for (int i = 0; i < 40 && !heard.Contains(Sfx.Bounce); i++) { b.Pos = new Vec(232, 120); g.Tick(new Controls(0, Jump: true)); }
        Assert.Contains(Sfx.Bounce, heard);
        Assert.False(b.Popped);
        Assert.True(g.Hero.Vel.Y < 0);
    }

    [Fact]
    public void A_trapped_monster_left_too_long_gets_out_angry()
    {
        var g = Calm();
        g.PutHero(new Vec(40, Floor));
        var m = g.PutMonster(MonsterKind.Walker, new Vec(80, Floor));
        BlowOnce(g);
        for (int i = 0; i < 30 && m.Trapped is null; i++) g.Tick(default);
        Assert.NotNull(m.Trapped);
        g.PutHero(new Vec(220, Floor));
        for (int i = 0; i < 60 * 9 && m.Trapped is not null; i++) g.Tick(default);
        Assert.Null(m.Trapped);
        Assert.True(m.Angry);
        Assert.True(g.MonsterSpeed(m) > 40);
    }

    [Fact]
    public void A_free_monster_catches_the_hero()
    {
        var g = Calm();
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.PutHero(new Vec(40, Floor));
        g.PutMonster(MonsterKind.Walker, new Vec(44, Floor));
        g.Tick(default);
        Assert.Equal(Phase.Dying, g.Phase);
        Assert.Equal("caught by a walker", g.LastDeath);
        Assert.Contains(Sfx.Death, heard);
        Steps(g, (int)(60 * GameEngine.DyingSeconds) + 2);
        Assert.Equal(GameEngine.StartingLives - 1, g.Lives);
        Assert.Equal(g.Level.Start, g.Hero.Pos);
    }

    [Fact]
    public void Flyers_bounce_about_the_level()
    {
        var g = Calm(level: 7);
        var f = g.PutMonster(MonsterKind.Flyer, new Vec(120, 100));
        g.PutHero(new Vec(40, Floor));
        var seen = new HashSet<(int, int)>();
        for (int i = 0; i < 60 * 8; i++) { g.Tick(default); seen.Add(((int)f.Pos.X / 32, (int)f.Pos.Y / 32)); if (g.Phase != Phase.Playing) break; }
        Assert.True(seen.Count > 5, "it got about");
        Assert.InRange(f.Pos.X, 0, Field.Width);
    }

    [Fact]
    public void Taking_too_long_makes_the_monsters_angry_and_brings_the_shade()
    {
        var g = new GameEngine(1, new HighScoreTable(_dir));
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.StartGame();
        g.JumpTo(0);
        g.SkipTime(GameEngine.HurrySeconds);
        g.Tick(default);
        Assert.True(g.Hurried);
        Assert.All(g.Monsters, m => Assert.True(m.Angry));
        Assert.Contains(Sfx.Hurry, heard);
        g.SkipTime(GameEngine.ShadeSeconds - GameEngine.HurrySeconds);
        g.Tick(default);
        Assert.NotNull(g.Shade);
        var s = g.Shade!;
        double d = (s.Pos - g.Hero.Centre).Length;
        Steps(g, 30);
        Assert.True((s.Pos - g.Hero.Centre).Length < d, "it comes for the hero");
    }

    [Fact]
    public void Collecting_extend_gives_a_life_and_ends_the_level()
    {
        var g = Calm();
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.PutMonster(MonsterKind.Walker, new Vec(200, 7 * Field.Tile));
        for (int i = 0; i < 5; i++) g.Letters[i] = true;
        var b = g.PutBubble(new Vec(60, 120), letter: 5);
        g.PutHero(new Vec(60, 128));
        g.Tick(default);
        Assert.True(b.Popped);
        Assert.Equal(GameEngine.StartingLives + 1, g.Lives);
        Assert.All(g.Letters, l => Assert.False(l));
        Assert.Contains(Sfx.Extend, heard);
        g.Tick(default);
        Assert.Equal(Phase.Clear, g.Phase);
    }

    [Fact]
    public void Letter_bubbles_float_up_now_and_then()
    {
        var g = new GameEngine(1, new HighScoreTable(_dir));
        g.StartGame();
        g.JumpTo(0);
        g.PutHero(new Vec(40, Floor));
        bool seen = false;
        for (int i = 0; i < 60 * 25 && !seen && g.Phase == Phase.Playing; i++)
        {
            g.Tick(default);
            seen = g.Bubbles.Any(b => b.Letter >= 0);
            foreach (var m in g.Monsters) m.Pos = m.Spawn;
        }
        Assert.True(seen);
    }

    [Fact]
    public void Clearing_a_level_moves_on_to_the_next()
    {
        var g = Calm();
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        var m = g.PutMonster(MonsterKind.Walker, new Vec(200, 7 * Field.Tile));
        var b = g.PutBubble(new Vec(60, 120));
        b.Monster = m;
        m.Trapped = b;
        g.PutHero(new Vec(60, 128));
        g.Tick(default);
        g.Tick(default);
        Assert.Equal(Phase.Clear, g.Phase);
        Assert.Contains(Sfx.Clear, heard);
        Steps(g, (int)(60 * GameEngine.ClearSeconds) + 2);
        Assert.Equal(2, g.LevelNumber);
    }

    [Fact]
    public void Extra_lives_at_30000_and_every_70000_after()
    {
        var g = Calm();
        Assert.Equal(3, g.Lives);
        g.AddScore(29999);
        Assert.Equal(3, g.Lives);
        g.AddScore(1);
        Assert.Equal(4, g.Lives);
        g.AddScore(70000);
        Assert.Equal(5, g.Lives);
    }

    [Fact]
    public void The_demonstration_pilot_clears_a_level_and_the_game_ends_in_the_table()
    {
        var g = new GameEngine(1, new HighScoreTable(_dir));
        g.StartGame();
        var pilot = new AttractPilot(1);
        for (int i = 0; i < 60 * 60 * 4 && g.State == GameState.Playing && g.LevelNumber < 2; i++) g.Tick(pilot.Decide(g));
        Assert.True(g.LevelNumber >= 2, $"{g.Monsters.Count(m => !m.Dead)} monsters left; lost: {string.Join("; ", g.LossLog)}");
        for (int i = 0; i < 60 * 60 * 10 && g.State == GameState.Playing; i++) g.Tick(default);
        Assert.Equal(GameState.GameOver, g.State);
        g.Idle(60 * 4);
        if (g.HighScores.Qualifies(g.Score))
        {
            Assert.Equal(GameState.EnterInitials, g.State);
            g.Initials!.Finish();
            g.Idle(2);
        }
        Assert.Equal(GameState.HighScores, g.State);
    }

    [Fact]
    public void The_same_seed_plays_the_same_game()
    {
        string Run()
        {
            var g = new GameEngine(21, new HighScoreTable(_dir));
            g.StartGame();
            var pilot = new AttractPilot(5);
            for (int i = 0; i < 60 * 60; i++) g.Tick(pilot.Decide(g));
            return $"{g.LevelNumber} {g.Score} {g.Lives} {g.Hero.Pos} {g.Bubbles.Count} {string.Join(",", g.Monsters.Select(m => m.Pos.ToString()))}";
        }
        Assert.Equal(Run(), Run());
    }

    [Fact]
    public void The_title_plays_its_tune_and_the_demonstration_has_sound()
    {
        var g = new GameEngine(5, new HighScoreTable(_dir));
        var heard = new List<Sfx>();
        g.Sound += heard.Add;
        g.Idle(1);
        Assert.Equal([Sfx.TitleTune], heard);
        g.Idle((int)(60 * (GameEngine.TitleIdleSeconds + 20)));
        Assert.Equal(GameState.Attract, g.State);
        Assert.Contains(Sfx.StartTune, heard);
        Assert.Contains(Sfx.Music, heard);
        Assert.Contains(Sfx.Blow, heard);
        // The demonstration ends after its time (or sooner, if the computer player runs out of lives).
        for (int i = 0; i < 60 * GameEngine.AttractSeconds && g.State == GameState.Attract; i++) g.Tick(default);
        Assert.Equal(GameState.HighScores, g.State);
        Assert.Equal(Sfx.MusicStop, heard.Last(s => s is Sfx.Music or Sfx.MusicStop));
        g.Idle((int)(60 * (GameEngine.HighScoreSeconds + 1)));
        Assert.Equal(GameState.Title, g.State);
    }

    [Fact]
    public void Pausing_holds_everything()
    {
        var g = Calm();
        g.PutHero(new Vec(40, Floor));
        g.Pause();
        Steps(g, 30, Right);
        Assert.Equal(40, g.Hero.Pos.X);
        g.Resume();
        Steps(g, 30, Right);
        Assert.True(g.Hero.Pos.X > 40);
    }

    [Fact]
    public void Every_sound_and_sprite_is_there()
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "Sounds");
        Assert.All(Enum.GetValues<Sfx>().Where(s => s != Sfx.MusicStop).Select(s => s.ToString()), f => Assert.True(File.Exists(Path.Combine(folder, f + ".wav")), f));
        foreach (var s in new[] { Sprites.Puff[0], Sprites.PuffBlow, Sprites.Walker[0], Sprites.Jumper[1], Sprites.Flyer[0], Sprites.Cherry, Sprites.Diamond })
            Assert.All(s.Rows.SelectMany(r => r).Where(ch => ch != '.'), ch => Assert.True(Sprites.Colours.ContainsKey(ch), $"colour {ch}"));
    }
}
