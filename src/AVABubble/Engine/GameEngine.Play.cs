using AVABubble.Models;

namespace AVABubble.Engine;

public sealed partial class GameEngine
{
    public const double WalkSpeed = 64;
    public const double JumpSpeed = 248;
    public const double Gravity = 560;
    /// <summary>Things fall slowly here, as in the arcade game.</summary>
    public const double MaxFall = 105;
    public const double BubbleSpeed = 190;
    public const double ShotSeconds = 0.32;
    public const double BubbleLife = 10;
    public const double TrappedSeconds = 7;
    public const double BlowEvery = 0.24;
    public const int MaxBubbles = 12;
    public const double HurrySeconds = 30;
    public const double ShadeSeconds = 40;
    public const double ReadySeconds = 1.6;
    public const double DyingSeconds = 2.0;
    public const double ClearSeconds = 3.5;
    public const int EmptyPopPoints = 10;
    public static readonly string Extend = "EXTEND";

    public int LevelIndex { get; private set; }
    public int Lap { get; private set; }
    public int LevelNumber => Lap * Levels.All.Length + LevelIndex + 1;
    public LevelMap Level => Levels.All[LevelIndex];
    public Hero Hero { get; } = new();
    public List<Monster> Monsters { get; } = [];
    public List<Bubble> Bubbles { get; } = [];
    public List<Fruit> Fruits { get; } = [];
    public Shade? Shade { get; private set; }
    /// <summary>The letters of EXTEND collected so far.</summary>
    public bool[] Letters { get; } = new bool[6];
    /// <summary>Seconds into the level (the hurry-up and the Shade go by it).</summary>
    public double LevelTime { get; private set; }
    public bool Hurried { get; private set; }
    public string? LastDeath { get; private set; }
    internal List<string> LossLog { get; } = [];

    private double _blowClock, _letterClock = 12;
    private bool _blowPrev, _jumpPrev;
    private bool _calm;

    public double MonsterSpeed(Monster m) => (m.Kind == MonsterKind.Jumper ? 34 : 40) * (m.Angry ? 1.6 : 1) * (1 + 0.12 * Lap);

    // ----- tiles ----------------------------------------------------------------------------------

    /// <summary>The tile at a column and row: walls beyond the sides; above and below wraps round.</summary>
    public Tile TileAt(int col, int row)
    {
        if (col < 0 || col >= Field.Cols) return Tile.Wall;
        row = ((row % Field.Rows) + Field.Rows) % Field.Rows;
        return Level.Tiles[col, row];
    }

    private Tile TileAtPoint(double x, double y) => TileAt((int)Math.Floor(x / Field.Tile), (int)Math.Floor(y / Field.Tile));

    // ----- levels ---------------------------------------------------------------------------------

    private void StartLevel(int index)
    {
        LevelIndex = index;
        Monsters.Clear();
        if (!_calm)
            foreach (var (kind, at) in Level.Monsters)
                Monsters.Add(new Monster { Kind = kind, Pos = at, Spawn = at, FacingLeft = at.X > Field.Width / 2.0 });
        Fruits.Clear();
        NewLife();
    }

    /// <summary>The hero back at the start, bubbles gone, the monsters where they began.</summary>
    private void NewLife()
    {
        Hero.Pos = Level.Start;
        Hero.Vel = new Vec(0, 0);
        Hero.FacingLeft = Hero.Pos.X > Field.Width / 2.0;
        Hero.Safe = 2;
        Bubbles.Clear();
        foreach (var m in Monsters.Where(m => !m.Dead))
        {
            m.Pos = m.Spawn;
            m.Vel = new Vec(m.Kind == MonsterKind.Flyer ? 40 : 0, m.Kind == MonsterKind.Flyer ? 40 : 0);
            m.Angry = false;
            m.Trapped = null;
        }
        Shade = null;
        LevelTime = 0;
        Hurried = false;
        SetPhase(Phase.Ready);
        Music(false);
    }

    private void Simulate(double dt, Controls c)
    {
        PhaseTime += dt;
        foreach (var e in Effects.Where(e => e.Kind == EffectKind.Score)) e.Pos += new Vec(0, -10 * dt);
        switch (Phase)
        {
            case Phase.Ready:
                if (PhaseTime < ReadySeconds) return;
                SetPhase(Phase.Playing);
                Music(true);
                return;
            case Phase.Dying:
                if (PhaseTime < DyingSeconds) return;
                Lives--;
                if (Lives <= 0) { GameOver(); return; }
                NewLife();
                return;
            case Phase.Clear:
                MoveHero(dt, c);
                MoveFruits(dt);
                MoveBubbles(dt);
                if (PhaseTime < ClearSeconds) return;
                if (LevelIndex + 1 < Levels.All.Length) StartLevel(LevelIndex + 1);
                else { Lap++; StartLevel(0); }
                return;
        }

        LevelTime += dt;
        if (Hero.Safe > 0) Hero.Safe -= dt;
        MoveHero(dt, c);
        Blow(dt, c);
        MoveBubbles(dt);
        if (Phase != Phase.Playing) return;
        MoveMonsters(dt);
        MoveFruits(dt);
        Hurry(dt);
        Letters_(dt);
        Touch();
        if (Phase == Phase.Playing && Monsters.Count > 0 && Monsters.All(m => m.Dead))
        {
            Music(false);
            Play(Sfx.Clear);
            Shade = null;
            SetPhase(Phase.Clear);
        }
    }

    private void Die(string why)
    {
        if (Hero.Safe > 0 || Phase != Phase.Playing) return;
        LastDeath = why;
        LossLog.Add($"level {LevelNumber}: {why}");
        Effects.Add(new Effect { Kind = EffectKind.Burst, Pos = Hero.Centre, Life = DyingSeconds });
        Music(false);
        Play(Sfx.Death);
        SetPhase(Phase.Dying);
    }

    // ----- walking, jumping, falling --------------------------------------------------------------

    /// <summary>
    /// Moves a body: across, stopped by walls; then down, landing on top of a wall or platform it
    /// comes down on (platforms let it up through them from below). Falling out of the bottom brings it
    /// back in at the top.
    /// </summary>
    private void MoveBody(Body b, double dt, bool gravity = true)
    {
        var p = b.Pos;
        var v = b.Vel;
        // Across.
        double nx = p.X + v.X * dt;
        double side = nx + (v.X > 0 ? b.HalfW : -b.HalfW);
        bool blocked = false;
        for (double y = p.Y - b.Tall + 1; y <= p.Y - 1; y += 4)
            if (TileAtPoint(side, y) == Tile.Wall) { blocked = true; break; }
        if (!blocked) p = p with { X = Math.Clamp(nx, b.HalfW + 1, Field.Width - b.HalfW - 1) };
        else v = v with { X = 0 };
        // Down or up.
        if (gravity) v = v with { Y = Math.Min(MaxFall, v.Y + Gravity * dt) };
        double ny = p.Y + v.Y * dt;
        b.OnGround = false;
        if (v.Y >= 0)
        {
            int row = (int)Math.Floor(p.Y / Field.Tile), nrow = (int)Math.Floor(ny / Field.Tile);
            for (int r = row; r <= nrow; r++)
            {
                double top = r * Field.Tile;
                if (top < p.Y - 0.01 || top > ny) continue;
                if (Standing(p.X, b.HalfW, r)) { ny = top; v = v with { Y = 0 }; b.OnGround = true; break; }
            }
        }
        else
        {
            double head = ny - b.Tall;
            if (TileAtPoint(p.X - b.HalfW + 1, head) == Tile.Wall || TileAtPoint(p.X + b.HalfW - 1, head) == Tile.Wall)
            {
                ny = p.Y;
                v = v with { Y = 0 };
            }
        }
        p = p with { Y = ny };
        // Through the floor's gap and in at the top.
        if (p.Y > Field.Height + b.Tall) p = p with { Y = p.Y - Field.Height - b.Tall };
        b.Pos = p;
        b.Vel = v;
    }

    /// <summary>Something to stand on in this row under feet at x.</summary>
    private bool Standing(double x, double halfW, int row)
    {
        if (row < 0 || row >= Field.Rows) return false;
        for (double xx = x - halfW + 1; xx <= x + halfW - 1; xx += 3)
            if (TileAt((int)Math.Floor(xx / Field.Tile), row) != Tile.Empty) return true;
        return TileAt((int)Math.Floor((x + halfW - 1) / Field.Tile), row) != Tile.Empty;
    }

    private void MoveHero(double dt, Controls c)
    {
        var h = Hero;
        double dx = Math.Clamp(c.Dx, -1, 1);
        if (Math.Abs(dx) > 0.2) h.FacingLeft = dx < 0;
        h.Vel = h.Vel with { X = dx * WalkSpeed };
        bool jumpPressed = c.Jump && !_jumpPrev;
        _jumpPrev = c.Jump;
        if (jumpPressed && h.OnGround)
        {
            h.Vel = h.Vel with { Y = -JumpSpeed };
            Play(Sfx.Jump);
        }
        MoveBody(h, dt);
        if (Math.Abs(dx) > 0.2 && h.OnGround) h.Walk += dt;
        if (h.Blowing > 0) h.Blowing -= dt;
        // Bouncing off the tops of bubbles with jump held, else popping them.
        foreach (var b in Bubbles.ToList())
        {
            if (b.Popped || (b.Pos - h.Centre).Length > Bubble.Radius + 6) continue;
            if (b.Shot > 0 && b.Age < 0.12) continue;
            if (c.Jump && h.Vel.Y > 0 && h.Pos.Y < b.Pos.Y + 2)
            {
                h.Vel = h.Vel with { Y = -JumpSpeed };
                Play(Sfx.Bounce);
                continue;
            }
            PopChain(b);
        }
    }

    private void Blow(double dt, Controls c)
    {
        _blowClock -= dt;
        bool pressed = c.Blow && !_blowPrev;
        _blowPrev = c.Blow;
        if (!pressed || _blowClock > 0 || Bubbles.Count >= MaxBubbles) return;
        _blowClock = BlowEvery;
        var h = Hero;
        int dir = h.FacingLeft ? -1 : 1;
        Bubbles.Add(new Bubble { Pos = h.Centre + new Vec(dir * 8, 0), Vel = new Vec(dir * BubbleSpeed, 0), Shot = ShotSeconds, Life = BubbleLife });
        h.Blowing = 0.18;
        Play(Sfx.Blow);
    }

    // ----- bubbles --------------------------------------------------------------------------------

    /// <summary>
    /// Shot out fast, then slowing to a float: up to the top, then along it towards the middle, as on
    /// air currents. They jostle apart, and burst when their time's up (letting out a monster in a
    /// temper).
    /// </summary>
    private void MoveBubbles(double dt)
    {
        foreach (var b in Bubbles)
        {
            if (b.Popped) continue;
            b.Age += dt;
            if (b.Shot > 0)
            {
                b.Shot -= dt;
                // Traps the first free monster it meets.
                foreach (var m in Monsters)
                {
                    if (m.Dead || m.Trapped is not null || b.Monster is not null || (m.Centre - b.Pos).Length > Bubble.Radius + 6) continue;
                    m.Trapped = b;
                    b.Monster = m;
                    b.Shot = 0;
                    b.Life = TrappedSeconds * (m.Angry ? 0.7 : 1);
                    b.Age = 0;
                    Play(Sfx.Trap);
                }
                if (b.Shot <= 0) b.Vel = new Vec(0, 0);
            }
            else
            {
                double up = b.Pos.Y > 18 ? -26 : 0;
                double across = b.Pos.Y <= 26 ? Math.Sign(Field.Width / 2.0 - b.Pos.X) * 14 : 0;
                b.Vel += (new Vec(across, up) - b.Vel) * Math.Min(1, 3 * dt);
            }
            var np = b.Pos + b.Vel * dt;
            if (TileAtPoint(np.X + Math.Sign(b.Vel.X) * Bubble.Radius, np.Y) == Tile.Wall && b.Pos.Y > Field.Tile)
            {
                np = b.Pos;
                b.Shot = 0;
                b.Vel = b.Vel with { X = 0 };
            }
            b.Pos = new Vec(Math.Clamp(np.X, 16 + Bubble.Radius, Field.Width - 16 - Bubble.Radius), Math.Max(10, np.Y));
            if (b.Monster is { } m2) m2.Pos = b.Pos + new Vec(0, 7);
            if (b.Age >= b.Life) Burst(b);
        }
        // Jostling.
        for (int i = 0; i < Bubbles.Count; i++)
            for (int j = i + 1; j < Bubbles.Count; j++)
            {
                var a = Bubbles[i];
                var b = Bubbles[j];
                if (a.Shot > 0 || b.Shot > 0) continue;
                var d = b.Pos - a.Pos;
                double len = d.Length;
                if (len >= Bubble.Radius * 2 || len < 1e-6) continue;
                var push = d.Normal * ((Bubble.Radius * 2 - len) * 0.5);
                a.Pos -= push * 0.5;
                b.Pos += push * 0.5;
            }
        Bubbles.RemoveAll(b => b.Popped);
    }

    /// <summary>A bubble's time is up: it bursts by itself, and any monster in it gets out, angry.</summary>
    private void Burst(Bubble b)
    {
        b.Popped = true;
        if (b.Monster is { } m)
        {
            m.Trapped = null;
            m.Angry = true;
            m.Vel = new Vec(0, 0);
        }
        Effects.Add(new Effect { Kind = EffectKind.Pop, Pos = b.Pos, Life = 0.3 });
    }

    /// <summary>
    /// The hero pops a bubble: and every bubble touching it pops too, and so on. Each monster popped
    /// in one go is worth double the one before; out comes a fruit.
    /// </summary>
    private void PopChain(Bubble first)
    {
        var queue = new Queue<Bubble>();
        queue.Enqueue(first);
        first.Popped = true;
        int caught = 0;
        while (queue.Count > 0)
        {
            var b = queue.Dequeue();
            Effects.Add(new Effect { Kind = EffectKind.Pop, Pos = b.Pos, Life = 0.3 });
            if (b.Monster is { } m)
            {
                m.Dead = true;
                m.Trapped = null;
                int points = 1000 << Math.Min(caught, 5);
                caught++;
                AddScore(points);
                Popup(b.Pos, points.ToString(), 1.4);
                var kind = (FruitKind)Math.Min(4, caught - 1 + (_rng.NextDouble() < 0.15 ? 1 : 0));
                Fruits.Add(new Fruit { Kind = kind, Pos = b.Pos, Vel = new Vec((_rng.NextDouble() - 0.5) * 120, -170) });
            }
            else if (b.Letter >= 0)
            {
                Letters[b.Letter] = true;
                Play(Sfx.Letter);
                if (Letters.All(x => x)) Extended();
            }
            else AddScore(EmptyPopPoints);
            foreach (var o in Bubbles)
            {
                if (o.Popped || o.Shot > 0 || (o.Pos - b.Pos).Length > Bubble.Radius * 2 + 8) continue;
                o.Popped = true;
                queue.Enqueue(o);
            }
        }
        Play(caught > 0 ? Sfx.Trap : Sfx.Pop);
        if (caught > 0) Play(Sfx.Pop);
    }

    /// <summary>All six letters: an extra life, and straight on to the next level.</summary>
    private void Extended()
    {
        Array.Clear(Letters);
        Lives++;
        Play(Sfx.Extend);
        Popup(Hero.Centre + new Vec(0, -10), "EXTEND!", 2.5);
        foreach (var m in Monsters) m.Dead = true;
    }

    /// <summary>Now and then a letter of EXTEND floats up out of the gap in the floor.</summary>
    private void Letters_(double dt)
    {
        if (_calm) return;
        _letterClock -= dt;
        if (_letterClock > 0 || Bubbles.Count(b => b.Letter >= 0) >= 1) return;
        _letterClock = 12 + _rng.NextDouble() * 8;
        var missing = Enumerable.Range(0, 6).Where(i => !Letters[i]).ToList();
        if (missing.Count == 0) return;
        int letter = missing[_rng.Next(missing.Count)];
        Bubbles.Add(new Bubble { Pos = new Vec(Field.Width / 2.0, Field.Height - 4), Vel = new Vec(0, -30), Life = 14, Letter = letter });
    }

    // ----- monsters -------------------------------------------------------------------------------

    private void MoveMonsters(double dt)
    {
        foreach (var m in Monsters)
        {
            if (m.Dead || m.Trapped is not null) continue;
            m.Age += dt;
            double speed = MonsterSpeed(m);
            if (m.Kind == MonsterKind.Flyer)
            {
                // Bouncing off anything solid, both ways.
                var v = m.Vel.Normal * speed * 1.1;
                var nx = m.Pos with { X = m.Pos.X + v.X * dt };
                if (Solid(nx, m)) v = v with { X = -v.X };
                else m.Pos = nx;
                var ny = m.Pos with { Y = m.Pos.Y + v.Y * dt };
                if (Solid(ny, m) || ny.Y - m.Tall < 10 || ny.Y > Field.Height - 9) v = v with { Y = -v.Y };
                else m.Pos = ny;
                m.Vel = v;
                m.FacingLeft = v.X < 0;
                continue;
            }
            var hero = Hero.Pos;
            m.Think -= dt;
            if (m.OnGround && m.Think <= 0)
            {
                m.Think = m.Kind == MonsterKind.Jumper ? 0.8 + _rng.NextDouble() : 0.5 + _rng.NextDouble() * 0.8;
                // Up after the hero, or just for the sake of it.
                bool heroAbove = hero.Y < m.Pos.Y - 12;
                double chance = m.Kind == MonsterKind.Jumper ? 0.7 : heroAbove ? 0.55 : 0.08;
                if (_rng.NextDouble() < chance) m.Vel = m.Vel with { Y = -JumpSpeed * 0.98 };
                else if (Math.Abs(hero.Y - m.Pos.Y) < 10 && _rng.NextDouble() < 0.6) m.FacingLeft = hero.X < m.Pos.X;
            }
            // At a ledge: over it if the hero is below, else turn back.
            if (m.OnGround)
            {
                double ahead = m.Pos.X + (m.FacingLeft ? -m.HalfW - 2 : m.HalfW + 2);
                int row = (int)Math.Floor(m.Pos.Y / Field.Tile);
                bool ledge = TileAt((int)Math.Floor(ahead / Field.Tile), row) == Tile.Empty;
                if (ledge && !(hero.Y > m.Pos.Y + 12 && _rng.NextDouble() < 0.8)) m.FacingLeft = !m.FacingLeft;
            }
            double before = m.Pos.X;
            m.Vel = m.Vel with { X = (m.FacingLeft ? -1 : 1) * speed };
            MoveBody(m, dt);
            if (Math.Abs(m.Pos.X - before) < 1e-6 && m.OnGround) m.FacingLeft = !m.FacingLeft;
        }
    }

    private bool Solid(Vec feet, Body b)
    {
        foreach (double x in new[] { feet.X - b.HalfW, feet.X + b.HalfW })
            foreach (double y in new[] { feet.Y - b.Tall, feet.Y - 1 })
                if (TileAtPoint(x, y) != Tile.Empty) return true;
        return false;
    }

    private void MoveFruits(double dt)
    {
        foreach (var f in Fruits.ToList())
        {
            f.Age += dt;
            if (!f.OnGround) MoveBody(f, dt);
            else f.Vel = new Vec(0, 0);
            if (f.Age > 9) { Fruits.Remove(f); continue; }
            // A moment in the air first, so it isn't snapped up the instant it appears.
            if (f.Age > 0.5 && (f.Centre - Hero.Centre).Length < 12)
            {
                Fruits.Remove(f);
                int points = FruitPoints(f.Kind);
                AddScore(points);
                Popup(f.Centre, points.ToString());
                Play(Sfx.Fruit);
            }
        }
    }

    public static int FruitPoints(FruitKind k) => k switch
    {
        FruitKind.Cherry => 500, FruitKind.Banana => 700, FruitKind.Melon => 1000, FruitKind.Cake => 2000, _ => 5000,
    };

    /// <summary>Taking too long: everything gets angry, then the Shade comes.</summary>
    private void Hurry(double dt)
    {
        if (!Hurried && LevelTime >= HurrySeconds)
        {
            Hurried = true;
            foreach (var m in Monsters) m.Angry = true;
            Popup(new Vec(Field.Width / 2.0, Field.Height / 2.0), "HURRY UP!", 2);
            Play(Sfx.Hurry);
        }
        if (Shade is null && LevelTime >= ShadeSeconds && !_calm)
        {
            Shade = new Shade { Pos = new Vec(Field.Width / 2.0, 12) };
            Play(Sfx.ShadeComes);
        }
        if (Shade is { } s)
        {
            s.Age += dt;
            s.Pos += (Hero.Centre - s.Pos).Normal * (36 + 6 * Lap) * dt;
        }
    }

    private void Touch()
    {
        var c = Hero.Centre;
        foreach (var m in Monsters)
            if (!m.Dead && m.Trapped is null && Math.Abs(m.Centre.X - c.X) < 10 && Math.Abs(m.Centre.Y - c.Y) < 11)
            {
                Die($"caught by a {m.Kind.ToString().ToLowerInvariant()}");
                return;
            }
        if (Shade is { } s && (s.Pos - c).Length < 10) Die("caught by the Shade");
    }

    // ----- for the tests and screenshots ----------------------------------------------------------

    internal void Idle(int steps)
    {
        for (int i = 0; i < steps; i++) Tick(default);
    }

    internal void Go()
    {
        if (Phase != Phase.Ready) return;
        SetPhase(Phase.Playing);
        Music(true);
    }

    internal void JumpTo(int index)
    {
        StartLevel(index);
        Go();
    }

    /// <summary>No monsters (unless put there), and no letters.</summary>
    internal void Calm()
    {
        _calm = true;
        Monsters.Clear();
    }

    internal Monster PutMonster(MonsterKind kind, Vec feet)
    {
        var m = new Monster { Kind = kind, Pos = feet, Spawn = feet, Vel = kind == MonsterKind.Flyer ? new Vec(40, 40) : new Vec(0, 0) };
        Monsters.Add(m);
        return m;
    }

    internal void PutHero(Vec feet)
    {
        Hero.Pos = feet;
        Hero.Vel = new Vec(0, 0);
        Hero.Safe = 0;
    }

    internal Bubble PutBubble(Vec at, int letter = -1)
    {
        var b = new Bubble { Pos = at, Life = BubbleLife, Letter = letter };
        Bubbles.Add(b);
        return b;
    }

    internal void SkipTime(double seconds) => LevelTime += seconds;
}
