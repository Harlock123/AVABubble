using AVABubble.Models;

namespace AVABubble.Engine;

/// <summary>
/// The computer player for attract mode and the tests. It picks a goal: a bubble with a monster in it
/// to pop, a letter, a fruit, or else a free monster to hunt (sticking with one until it's caught).
/// To get up to a goal it goes under a platform within jumping reach and jumps (up through it); to get
/// down it walks off an edge. On the same level as a monster it turns to face it and blows from a safe
/// distance. Anything free and close it blows at or backs away from, and it keeps clear of the Shade.
/// </summary>
public sealed class AttractPilot(int seed)
{
    private readonly Random _rng = new(seed);
    private bool _blow, _jump;
    private Monster? _prey;
    private double _stuck, _lastX;
    private int _wander = 1;

    private const int Size = Field.Tile;
    /// <summary>How many rows a jump can climb.</summary>
    private const int Reach = 6;

    public Controls Decide(GameEngine g)
    {
        if (g.Phase is not (Phase.Playing or Phase.Clear)) return default;
        var h = g.Hero;
        var me = h.Centre;
        double dx = 0;
        bool jump = false, blow = false;

        _stuck = Math.Abs(h.Pos.X - _lastX) < 0.2 ? _stuck + Field.Step : 0;
        _lastX = h.Pos.X;
        if (_stuck > 1.5) { _wander = -_wander; _stuck = 0; }

        var free = g.Monsters.Where(m => !m.Dead && m.Trapped is null).ToList();
        var close = free.Where(m => Math.Abs(m.Centre.X - me.X) < 34 && Math.Abs(m.Centre.Y - me.Y) < 20)
            .OrderBy(m => Math.Abs(m.Centre.X - me.X)).FirstOrDefault();

        if (close is not null)
        {
            // Too close: blow if it's in front and level with us, else get away from it.
            bool level = Math.Abs(close.Centre.Y - me.Y) < 10;
            bool facing = close.Centre.X < me.X == h.FacingLeft;
            if (level && facing) blow = true;
            else if (level) dx = Math.Sign(close.Centre.X - me.X);
            else dx = close.Centre.X < me.X ? 1 : -1;
        }
        else
        {
            Vec? goal = g.Bubbles.Where(b => b.Monster is not null || b.Letter >= 0).OrderBy(b => (b.Pos - me).Length).Select(b => (Vec?)(b.Pos + new Vec(0, 10))).FirstOrDefault()
                ?? g.Fruits.Where(f => f.OnGround).OrderBy(f => (f.Centre - me).Length).Select(f => (Vec?)f.Pos).FirstOrDefault();
            if (goal is null)
            {
                if (_prey is null || _prey.Dead || _prey.Trapped is not null)
                    _prey = free.OrderBy(m => Math.Abs(m.Centre.Y - me.Y) * 2 + Math.Abs(m.Centre.X - me.X)).FirstOrDefault();
                if (_prey is { } prey)
                {
                    double ddx = prey.Centre.X - me.X;
                    if (Math.Abs(prey.Centre.Y - me.Y) < 12 && Math.Abs(ddx) < 100)
                    {
                        // On its level: face it and blow, from a safe way off.
                        bool facing = ddx < 0 == h.FacingLeft;
                        if (!facing) dx = Math.Sign(ddx);
                        else if (Math.Abs(ddx) < 70) blow = true;
                        else dx = Math.Sign(ddx);
                    }
                    else goal = prey.Pos;
                }
            }
            if (goal is { } gpos)
            {
                var (mx, mj) = Head(g, gpos);
                dx = mx;
                jump = mj;
            }
            else if (dx == 0 && !blow) dx = _wander;
        }

        // Something dropping on us from above: step out from under it.
        var falling = free.FirstOrDefault(m => !m.OnGround && m.Kind != MonsterKind.Flyer && m.Centre.Y < me.Y - 6 && m.Centre.Y > me.Y - 48 && Math.Abs(m.Centre.X - me.X) < 18);
        if (falling is not null) { dx = falling.Centre.X < me.X ? 1 : -1; blow = false; }

        // The Shade: away from it.
        if (g.Shade is { } s && (s.Pos - me).Length < 64)
        {
            dx = s.Pos.X < me.X ? 1 : -1;
            jump |= h.OnGround && s.Pos.Y < me.Y + 20;
        }
        if (_stuck > 0.8 && h.OnGround) jump = true;

        // Buttons only count on the press: let go in between.
        _blow = blow && !_blow;
        _jump = jump && !_jump;
        return new Controls(dx, _jump, _blow);
    }

    /// <summary>The way towards a goal (given by where feet would be): across, and whether to jump.</summary>
    private (double Dx, bool Jump) Head(GameEngine g, Vec goal)
    {
        var h = g.Hero;
        int heroRow = (int)Math.Round(h.Pos.Y / Size), goalRow = (int)Math.Round(goal.Y / Size);
        if (!h.OnGround) return (Math.Abs(goal.X - h.Pos.X) > 3 ? Math.Sign(goal.X - h.Pos.X) : 0, false);
        if (goalRow >= heroRow - 1 && goalRow <= heroRow + 1)
            return (Math.Abs(goal.X - h.Pos.X) > 3 ? Math.Sign(goal.X - h.Pos.X) : 0, goal.Y < h.Pos.Y - 4);
        if (goalRow < heroRow)
        {
            // Up: under a platform within reach (the nearer the goal's level and the hero, the better).
            double bestX = double.NaN, bestScore = double.MaxValue;
            for (int r = Math.Max(1, heroRow - Reach); r <= heroRow - 2; r++)
                for (int c = 2; c < Field.Cols - 2; c++)
                {
                    if (g.TileAt(c, r) == Tile.Empty || g.TileAt(c, r - 1) != Tile.Empty) continue;
                    double x = c * Size + Size / 2.0;
                    double score = Math.Abs(x - h.Pos.X) + Math.Abs(x - goal.X) * 0.4 + Math.Max(0, r - goalRow) * 6;
                    if (score < bestScore) { bestScore = score; bestX = x; }
                }
            // Or straight up, if the goal's within reach and there's nothing better.
            if (double.IsNaN(bestX) || (heroRow - goalRow <= Reach && Math.Abs(goal.X - h.Pos.X) < 10)) bestX = goal.X;
            bool under = Math.Abs(bestX - h.Pos.X) < 4;
            return (under ? 0 : Math.Sign(bestX - h.Pos.X), under);
        }
        // Down: off the nearer edge of this platform, the goal's way if possible.
        return (Math.Abs(goal.X - h.Pos.X) > 6 ? Math.Sign(goal.X - h.Pos.X) : _wander, false);
    }
}
