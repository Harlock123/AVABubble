using System.Globalization;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using AVABubble.Engine;
using AVABubble.Models;

namespace AVABubble.Views;

public sealed partial class GameCanvas
{
    private static readonly Dictionary<uint, IBrush> BrushCache = [];
    private static readonly Dictionary<(uint, double), IPen> PenCache = [];
    private static readonly Typeface Retro = new(new FontFamily("avares://AVABubble/Assets/Fonts#VT323"));

    public static IBrush Brush(uint argb)
    {
        if (!BrushCache.TryGetValue(argb, out var b))
        {
            b = new SolidColorBrush(Color.FromUInt32(argb)).ToImmutable();
            BrushCache[argb] = b;
        }
        return b;
    }

    private static IPen Pen(uint argb, double thickness)
    {
        if (!PenCache.TryGetValue((argb, thickness), out var p))
        {
            p = new ImmutablePen(argb, thickness);
            PenCache[(argb, thickness)] = p;
        }
        return p;
    }

    private static readonly IBrush White = Brush(0xFFF2F2F2);
    private static readonly IBrush Yellow = Brush(0xFFFFE14A);
    private static readonly IBrush Red = Brush(0xFFFF3D3D);
    private static readonly IBrush Orange = Brush(0xFFFF8A1E);
    private static readonly IBrush Cyan = Brush(0xFF4FD7FF);
    private static readonly IBrush Dim = Brush(0xFF8A8AA6);

    /// <summary>Each level's blocks: face, light edge, dark edge.</summary>
    private static readonly (uint Face, uint Light, uint Dark)[] Blocks =
    [
        (0xFFE84AC0, 0xFFFFA8E8, 0xFF821A66), (0xFF3A9AE8, 0xFF9AD0FF, 0xFF16508A), (0xFFFF8A1E, 0xFFFFC080, 0xFFA04E08),
        (0xFF3CC84A, 0xFF9AF0A0, 0xFF16702A), (0xFFE0382E, 0xFFFF9A90, 0xFF821810), (0xFFF0D23A, 0xFFFFF4A0, 0xFF8A7210),
        (0xFFB050F0, 0xFFE0B0FF, 0xFF5A2A8A), (0xFF3CC8C8, 0xFFA0F0F0, 0xFF167070), (0xFFA8ACB8, 0xFFE8ECF4, 0xFF5A5E6A),
        (0xFFFF6A6A, 0xFFFFB0B0, 0xFF8A2A2A), (0xFF6A8AFF, 0xFFB0C4FF, 0xFF2A3A8A), (0xFFC8A040, 0xFFF0D890, 0xFF6A5010),
    ];

    /// <summary>Game pixels to the 1024×768 screen.</summary>
    private static Point P(double x, double y) => new(FieldLeft + x * Scale, FieldTop + y * Scale);
    private static Point P(Vec v) => P(v.X, v.Y);

    private static void Fill(DrawingContext dc, uint argb, double x, double y, double w, double h) =>
        dc.FillRectangle(Brush(argb), new Rect(FieldLeft + x * Scale, FieldTop + y * Scale, w * Scale, h * Scale));

    private void DrawScreen(DrawingContext dc)
    {
        var g = Engine;
        using (dc.PushClip(new Rect(P(0, 0), P(Field.Width, Field.Height))))
        {
            dc.FillRectangle(Brush(0xFF04040C), new Rect(P(0, 0), P(Field.Width, Field.Height)));
            DrawLevel(dc, g.State is GameState.Title or GameState.HighScores ? 0 : g.LevelIndex);
            switch (g.State)
            {
                case GameState.Title: DrawTitle(dc); break;
                case GameState.HighScores: DrawHighScores(dc); break;
                default: DrawPlay(dc); break;
            }
        }
        DrawPanels(dc);
    }

    private static void DrawLevel(DrawingContext dc, int index)
    {
        var level = Levels.All[index];
        var (face, light, dark) = Blocks[index % Blocks.Length];
        for (int r = 0; r < Field.Rows; r++)
            for (int c = 0; c < Field.Cols; c++)
            {
                var t = level.Tiles[c, r];
                if (t == Tile.Empty) continue;
                double x = c * Field.Tile, y = r * Field.Tile;
                Fill(dc, face, x, y, Field.Tile, Field.Tile);
                Fill(dc, light, x, y, Field.Tile, 1);
                Fill(dc, light, x, y, 1, Field.Tile);
                Fill(dc, dark, x + 1, y + Field.Tile - 1, Field.Tile - 1, 1);
                Fill(dc, dark, x + Field.Tile - 1, y + 1, 1, Field.Tile - 1);
                if (t == Tile.Wall) Fill(dc, dark, x + 3, y + 3, 2, 2);
            }
    }

    private void DrawPlay(DrawingContext dc)
    {
        var g = Engine;
        foreach (var f in g.Fruits)
            if (f.Age < 7 || (int)(_clock * 8) % 2 == 0)
                DrawSprite(dc, Sprites.FruitSprite((int)f.Kind), f.Pos.X - 6, f.Pos.Y - 12);

        foreach (var m in g.Monsters)
        {
            if (m.Dead || m.Trapped is not null) continue;
            DrawMonster(dc, m, m.Pos);
        }

        var h = g.Hero;
        bool showHero = g.Phase != Phase.Dying && !(h.Safe > 0 && (int)(_clock * 12) % 2 == 0);
        if (showHero)
        {
            var art = h.Blowing > 0 ? Sprites.PuffBlow : Sprites.Puff[h.OnGround ? (int)(h.Walk * 8) % 2 : 1];
            DrawSprite(dc, art, h.Pos.X - 8, h.Pos.Y - 16, flip: h.FacingLeft);
        }

        foreach (var b in g.Bubbles) DrawBubble(dc, b);

        if (g.Shade is { } s)
        {
            // The Shade: a dark shape with burning eyes, wavering.
            var c = P(s.Pos);
            double wob = Math.Sin(s.Age * 6) * Scale;
            dc.DrawEllipse(Brush(0xC02A2A3A), null, c + new Point(0, wob), 9 * Scale, 10 * Scale);
            dc.DrawRectangle(Brush(0xC02A2A3A), null, new Rect(c.X - 9 * Scale, c.Y + wob, 18 * Scale, 8 * Scale));
            for (int i = 0; i < 4; i++)
                dc.DrawEllipse(Brush(0xFF04040C), null, c + new Point((-6.75 + i * 4.5) * Scale, 8 * Scale + wob), 2.2 * Scale, 2.2 * Scale);
            uint eye = (int)(_clock * 4) % 2 == 0 ? 0xFFFF3D3Du : 0xFFFFE14Au;
            dc.DrawEllipse(Brush(eye), null, c + new Point(-3.5 * Scale, -1 * Scale + wob), 1.6 * Scale, 2 * Scale);
            dc.DrawEllipse(Brush(eye), null, c + new Point(3.5 * Scale, -1 * Scale + wob), 1.6 * Scale, 2 * Scale);
        }

        foreach (var fx in g.Effects)
        {
            double t = fx.Age / fx.Life;
            switch (fx.Kind)
            {
                case EffectKind.Pop:
                    for (int i = 0; i < 8; i++)
                    {
                        double a = i * Math.PI / 4;
                        var at = fx.Pos + new Vec(Math.Cos(a), Math.Sin(a)) * (6 + t * 8);
                        Fill(dc, 0xFFB8FFC8, at.X - 1, at.Y - 1, 2, 2);
                    }
                    break;
                case EffectKind.Burst:
                    // The hero goes up in a puff of stars.
                    for (int i = 0; i < 10; i++)
                    {
                        double a = i * Math.PI / 5 + t * 4;
                        var at = fx.Pos + new Vec(Math.Cos(a) * (4 + t * 20), Math.Sin(a) * (4 + t * 20) - t * 30);
                        uint c = (i % 3) switch { 0 => 0xFFFFE14Au, 1 => 0xFF3CD84Au, _ => 0xFFFFFFFFu };
                        Fill(dc, c, at.X - 1.5, at.Y - 1.5, 3, 3);
                    }
                    break;
                case EffectKind.Score:
                    Text(dc, fx.Text, P(fx.Pos), fx.Text.Length > 6 ? 44 : 30, fx.Text.StartsWith('H') || fx.Text.StartsWith('E') ? Red : Yellow, center: true);
                    break;
            }
        }

        switch (g.State)
        {
            case GameState.Paused: Banner(dc, "PAUSED", "ESC TO QUIT, ANY OTHER KEY TO RESUME"); break;
            case GameState.GameOver: Banner(dc, "GAME  OVER", null); break;
            case GameState.EnterInitials: DrawInitials(dc); break;
            default:
                if (g.Phase == Phase.Ready)
                    Banner(dc, $"LEVEL {g.LevelNumber}", g.State == GameState.Attract ? "DEMONSTRATION" : g.Level.Name.ToUpperInvariant());
                else if (g.Phase == Phase.Clear && g.PhaseTime > 1) Banner(dc, "CLEAR!", null);
                else if (g.State == GameState.Attract && (int)(_clock * 2) % 2 == 0)
                    Text(dc, "DEMONSTRATION", P(Field.Width / 2.0, 14), 32, Red, center: true);
                break;
        }
    }

    private void DrawMonster(DrawingContext dc, Monster m, Vec feet, bool inBubble = false)
    {
        int frame = (int)(m.Age * 6) % 2;
        var set = m.Kind switch
        {
            MonsterKind.Walker => m.Angry ? Sprites.WalkerAngry : Sprites.Walker,
            MonsterKind.Jumper => m.Angry ? Sprites.JumperAngry : Sprites.Jumper,
            _ => m.Angry ? Sprites.FlyerAngry : Sprites.Flyer,
        };
        var art = set[inBubble ? 0 : m.Kind == MonsterKind.Jumper ? (m.OnGround ? 1 : 0) : frame];
        DrawSprite(dc, art, feet.X - 8, feet.Y - 16, flip: m.FacingLeft);
    }

    private void DrawBubble(DrawingContext dc, Bubble b)
    {
        // Blinking near the end of its time.
        if (b.Life - b.Age < 2 && (int)(_clock * 10) % 2 == 0) return;
        double grow = b.Shot > 0 ? 0.6 + 0.4 * (1 - b.Shot / GameEngine.ShotSeconds) : 1;
        double r = Bubble.Radius * grow;
        if (b.Monster is { } m)
        {
            DrawMonster(dc, m, b.Pos + new Vec(0, 7), inBubble: true);
            // Struggling: it shakes as it nears getting out.
        }
        uint ring = b.Letter >= 0 ? 0xFFFFE14A : b.Monster is not null ? (b.Monster.Angry ? 0xFFFF6A6Au : 0xFF9AF0A0u) : 0xFF9AF0A0;
        dc.DrawEllipse(Brush(b.Monster is null ? 0x403CD84Au : 0x283CD84Au), Pen(ring, 2), P(b.Pos), r * Scale, r * Scale);
        dc.DrawEllipse(Brush(0xC0FFFFFF), null, P(b.Pos.X - r * 0.4, b.Pos.Y - r * 0.4), 1.4 * Scale, 1.2 * Scale);
        if (b.Letter >= 0)
            Text(dc, GameEngine.Extend[b.Letter].ToString(), P(b.Pos), 34, LetterBrush(b.Letter), center: true);
    }

    private static readonly uint[] LetterColours = [0xFFFF3D3D, 0xFFFF8A1E, 0xFFFFE14A, 0xFF3CD84A, 0xFF4FD7FF, 0xFFB050F0];
    private static IBrush LetterBrush(int i) => Brush(LetterColours[i]);

    private void Banner(DrawingContext dc, string title, string? sub)
    {
        Fill(dc, 0xC8000000, 24, 82, Field.Width - 48, sub is null ? 24 : 36);
        Text(dc, title, P(Field.Width / 2.0, 94), 52, Yellow, center: true);
        if (sub is not null) Text(dc, sub, P(Field.Width / 2.0, 109), 28, White, center: true);
    }

    /// <summary>Draws a sprite with its top-left at a game point, optionally mirrored.</summary>
    private static void DrawSprite(DrawingContext dc, Sprites.Sprite s, double x, double y, bool flip = false, double px = 1)
    {
        for (int r = 0; r < s.Height; r++)
        {
            var row = s.Rows[r];
            for (int c = 0; c < s.Width; c++)
            {
                char ch = row[flip ? s.Width - 1 - c : c];
                if (ch == '.') continue;
                var a = P(x + c * px, y + r * px);
                dc.FillRectangle(Brush(Sprites.Colours.GetValueOrDefault(ch, 0xFFFF00FF)), new Rect(a.X, a.Y, Scale * px + 0.3, Scale * px + 0.3));
            }
        }
    }

    private static void Text(DrawingContext dc, string text, Point at, double size, IBrush brush, bool center = false, bool right = false)
    {
        var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Retro, size, brush);
        double x = center ? at.X - ft.Width / 2 : right ? at.X - ft.Width : at.X;
        dc.DrawText(ft, new Point(x, at.Y - ft.Height / 2));
    }
}
