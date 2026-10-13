using Avalonia;
using Avalonia.Media;
using AVABubble.Engine;
using AVABubble.Models;

namespace AVABubble.Views;

public sealed partial class GameCanvas
{
    private const double RightPanel = FieldLeft + Field.Width * Scale;

    /// <summary>
    /// Along the top: the score, the best score and the level's name. Down the sides: spare lives on
    /// the left; the level and the letters of EXTEND on the right. The keys along the bottom.
    /// </summary>
    private void DrawPanels(DrawingContext dc)
    {
        var g = Engine;
        bool playing = g.State is GameState.Playing or GameState.Paused or GameState.GameOver or GameState.EnterInitials or GameState.Attract;
        double lx = FieldLeft / 2, rx = RightPanel + (ScreenWidth - RightPanel) / 2;
        Text(dc, "1UP", new Point(lx, 24), 30, Orange, center: true);
        Text(dc, (playing ? g.Score : 0).ToString(), new Point(lx, 56), 34, White, center: true);
        Text(dc, "HI-SCORE", new Point(ScreenWidth / 2, 24), 30, Orange, center: true);
        Text(dc, Math.Max(g.HighScores.TopScore, g.Score).ToString(), new Point(ScreenWidth / 2, 56), 34, White, center: true);
        Text(dc, playing ? g.Level.Name.ToUpperInvariant() : "AVA BUBBLE", new Point(ScreenWidth / 2, 84), 26, Dim, center: true);

        if (playing)
        {
            Text(dc, "LIVES", new Point(lx, 150), 26, Dim, center: true);
            for (int i = 0; i < Math.Min(g.Lives - (g.Phase == Phase.Dying ? 0 : 1), 8); i++)
                DrawPanelSprite(dc, Sprites.Puff[0], lx - 40 + (i % 3) * 28, 172 + i / 3 * 30);
            Text(dc, "LEVEL", new Point(rx, 24), 30, Orange, center: true);
            Text(dc, g.LevelNumber.ToString(), new Point(rx, 56), 34, White, center: true);
        }
        // The letters of EXTEND, lit as they're collected.
        for (int i = 0; i < 6; i++)
        {
            bool got = playing && g.Letters[i];
            var c = new Point(rx, 160 + i * 52);
            dc.DrawEllipse(got ? Brush(0x603CD84A) : null, Pen(got ? 0xFF9AF0A0u : 0xFF3A3A5Au, 2), c, 22, 22);
            Text(dc, GameEngine.Extend[i].ToString(), c, 36, got ? LetterBrush(i) : Brush(0xFF3A3A5A), center: true);
        }
        Text(dc, "ARROWS MOVE   X OR SPACE JUMP (HOLD IT TO BOUNCE ON BUBBLES)   Z BLOW   P PAUSE   M MUTE   CTRL+S SCREENSHOT",
            new Point(ScreenWidth / 2, 748), 20, Dim, center: true);
    }

    private static void DrawPanelSprite(DrawingContext dc, Sprites.Sprite s, double left, double top)
    {
        const double px = 1.6;
        for (int r = 0; r < s.Height; r++)
            for (int c = 0; c < s.Width; c++)
            {
                char ch = s.Rows[r][c];
                if (ch == '.') continue;
                dc.FillRectangle(Brush(Sprites.Colours.GetValueOrDefault(ch, 0xFFFF00FF)), new Rect(left + c * px, top + r * px, px + 0.2, px + 0.2));
            }
    }

    private static void Panel(DrawingContext dc, double top, double bottom) =>
        dc.FillRectangle(Brush(0xD8000008), new Rect(P(18, top), P(Field.Width - 18, bottom)));

    private void DrawTitle(DrawingContext dc)
    {
        double cx = Field.Width / 2.0;
        Panel(dc, 6, 202);
        Text(dc, "AVA BUBBLE", P(cx, 22), 100, Orange, center: true);
        Text(dc, "AFTER THE 1986 ARCADE CLASSIC", P(cx, 38), 28, Dim, center: true);
        Text(dc, "BLOW BUBBLES TO TRAP THE MONSTERS, THEN POP THEM.", P(cx, 50), 28, White, center: true);
        Text(dc, "POP A CROWD AT ONCE FOR DOUBLE, DOUBLE, DOUBLE...", P(cx, 59), 28, Yellow, center: true);

        double y = 76;
        void Row(Sprites.Sprite art, string text, string points, bool bubble = false)
        {
            DrawSprite(dc, art, 46, y - 8);
            if (bubble) dc.DrawEllipse(Brush(0x303CD84A), Pen(0xFF9AF0A0, 2), P(54, y - 1), 9 * Scale, 9 * Scale);
            Text(dc, text, P(70, y), 26, White);
            Text(dc, points, P(212, y), 26, Yellow, right: true);
            y += 15;
        }
        Row(Sprites.Puff[(int)(_clock * 4) % 2], "PUFF: THAT'S YOU", "");
        Row(Sprites.Walker[(int)(_clock * 4) % 2], "CLANK WALKS THE PLATFORMS", "");
        Row(Sprites.Jumper[(int)(_clock * 3) % 2], "BOING NEVER STOPS JUMPING", "");
        Row(Sprites.Flyer[(int)(_clock * 6) % 2], "FLIT FLIES AND BOUNCES", "");
        Row(Sprites.Walker[0], "POP ONE IN A BUBBLE", "1000+", bubble: true);
        Row(Sprites.Cherry, "FRUIT, FOR PICKING UP", "500-5000");
        // EXTEND.
        for (int i = 0; i < 6; i++)
        {
            dc.DrawEllipse(Brush(0x303CD84A), Pen(0xFFFFE14A, 2), P(54 + i * 22, y + 2), 8 * Scale, 8 * Scale);
            Text(dc, GameEngine.Extend[i].ToString(), P(54 + i * 22, y + 2), 30, LetterBrush(i), center: true);
        }
        Text(dc, "= A LIFE", P(186, y + 2), 26, White);
        Text(dc, "TAKE TOO LONG AND THE SHADE COMES FOR YOU", P(cx, 182), 26, Red, center: true);
        if (_clock % 1 < 0.65) Text(dc, "PRESS ENTER OR SPACE TO START", P(cx, 192), 30, Yellow, center: true);
        Text(dc, "© 2026 LONNIE WATSON", P(cx, 199), 20, Dim, center: true);
    }

    private void DrawHighScores(DrawingContext dc)
    {
        double cx = Field.Width / 2.0;
        Panel(dc, 10, 198);
        Text(dc, "TOP BUBBLERS", P(cx, 26), 64, Orange, center: true);
        Text(dc, "RANK      SCORE   NAME  LEVEL", P(cx, 42), 26, Dim, center: true);
        var table = Engine.HighScores.Entries;
        for (int i = 0; i < table.Count; i++)
        {
            var e = table[i];
            bool fresh = i == Engine.NewEntryIndex;
            if (fresh && _clock % 0.5 > 0.35) continue;
            var brush = fresh ? Yellow : i == 0 ? Orange : White;
            Text(dc, $"{i + 1,2}.  {e.Score,9}   {e.Initials}   {e.Stage,3}", P(cx, 56 + i * 12.5), 32, brush, center: true);
        }
        if (_clock % 1 < 0.65) Text(dc, "PRESS ENTER OR SPACE TO PLAY", P(cx, 188), 30, White, center: true);
    }

    private void DrawInitials(DrawingContext dc)
    {
        var entry = Engine.Initials;
        if (entry is null) return;
        dc.FillRectangle(Brush(0xE8000000), new Rect(P(40, 50), P(216, 150)));
        double cx = Field.Width / 2.0;
        Text(dc, "A NEW HIGH SCORE!", P(cx, 64), 48, Yellow, center: true);
        Text(dc, $"{Engine.Score}", P(cx, 80), 42, White, center: true);
        for (int i = 0; i < HighScoreTable.InitialsLength; i++)
        {
            var at = P(cx - 16 + i * 16, 108);
            bool cursor = i == entry.Cursor;
            Text(dc, entry.Initials[i].ToString(), at, 80, cursor ? Yellow : White, center: true);
            if (cursor && _clock % 0.6 < 0.4) dc.FillRectangle(Yellow, new Rect(at.X - 18, at.Y + 36, 36, 6));
        }
        Text(dc, "TYPE, OR UP/DOWN AND SPACE.  ENTER WHEN DONE", P(cx, 140), 24, Dim, center: true);
    }
}
