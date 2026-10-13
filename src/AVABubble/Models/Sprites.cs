namespace AVABubble.Models;

/// <summary>
/// The game's pixel art, this game's own drawings (the hero and the monsters are this game's own
/// characters), painted from ellipses and boxes into a pixel grid. Each letter is a colour (see <see cref="Colours"/>); '.' is clear.
/// </summary>
public static class Sprites
{
    public sealed record Sprite(string[] Rows)
    {
        public int Width => Rows[0].Length;
        public int Height => Rows.Length;
    }

    private static Sprite S(params string[] rows)
    {
        if (rows.Any(r => r.Length != rows[0].Length))
            throw new ArgumentException("Sprite rows must be the same width: " + string.Join("|", rows));
        return new Sprite(rows);
    }

    /// <summary>A pixel grid to paint shapes into.</summary>
    private sealed class Canvas(int width, int height)
    {
        private readonly char[,] _px = Fill(width, height);

        private static char[,] Fill(int w, int h)
        {
            var g = new char[w, h];
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++) g[x, y] = '.';
            return g;
        }

        public Canvas Ellipse(double cx, double cy, double rx, double ry, char c)
        {
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    double dx = (x + 0.5 - cx) / rx, dy = (y + 0.5 - cy) / ry;
                    if (dx * dx + dy * dy <= 1) _px[x, y] = c;
                }
            return this;
        }

        public Canvas Box(int x0, int y0, int x1, int y1, char c)
        {
            for (int x = Math.Max(0, x0); x <= Math.Min(width - 1, x1); x++)
                for (int y = Math.Max(0, y0); y <= Math.Min(height - 1, y1); y++) _px[x, y] = c;
            return this;
        }

        public Canvas Dot(int x, int y, char c) => Box(x, y, x, y, c);

        public Sprite Done()
        {
            var rows = new string[height];
            for (int y = 0; y < height; y++)
            {
                var chars = new char[width];
                for (int x = 0; x < width; x++) chars[x] = _px[x, y];
                rows[y] = new string(chars);
            }
            return new Sprite(rows);
        }
    }

    public static readonly IReadOnlyDictionary<char, uint> Colours = new Dictionary<char, uint>
    {
        ['w'] = 0xFFF4F4F8, // white
        ['k'] = 0xFF14141C, // black
        ['g'] = 0xFF3CD84A, // green
        ['G'] = 0xFF1E8A2A, // dark green
        ['y'] = 0xFFFFE14A, // yellow
        ['o'] = 0xFFFF8A1E, // orange
        ['p'] = 0xFFFF8AC8, // pink
        ['r'] = 0xFFE83A3A, // red
        ['R'] = 0xFF9A1E1E, // dark red
        ['m'] = 0xFFA8ACB8, // silver
        ['d'] = 0xFF5C6070, // dark grey
        ['v'] = 0xFFB050F0, // violet
        ['V'] = 0xFF6A2A9A, // dark violet
        ['c'] = 0xFF4FD7FF, // cyan
        ['b'] = 0xFF3A7AE8, // blue
        ['n'] = 0xFF9A6A30, // brown
    };

    // ----- the hero: Puff, a round little creature with a crest, facing right ------------------------

    private static Canvas PuffBody(int stride, bool open) =>
        new Canvas(16, 16)
            .Ellipse(8, 9, 6.6, 6.2, 'g').Ellipse(9, 11, 3.6, 3, 'y')        // body, belly
            .Dot(5, 2, 'G').Dot(6, 2, 'G').Dot(4, 3, 'G').Dot(7, 3, 'g')       // crest
            .Ellipse(10.5, 6.5, 2.4, 2.6, 'w').Dot(11, 6, 'k').Dot(11, 7, 'k') // eye
            .Dot(13, 8, 'p')                                                   // cheek
            .Box(open ? 13 : 14, 9, 15, open ? 11 : 9, open ? 'R' : 'G')        // mouth
            .Box(stride == 1 ? 3 : 4, 14, stride == 1 ? 5 : 6, 15, 'o')
            .Box(stride == 1 ? 10 : 9, 14, stride == 1 ? 12 : 11, 15, 'o');

    public static readonly Sprite[] Puff = [PuffBody(0, false).Done(), PuffBody(1, false).Done()];
    public static readonly Sprite PuffBlow = PuffBody(0, true).Done();

    // ----- monsters -----------------------------------------------------------------------------------

    /// <summary>Clank: a wind-up robot with a key in its back. Two steps; red when angry.</summary>
    private static Sprite Clank(int step, char body, char dark) => new Canvas(16, 16)
        .Box(3, 3, 12, 12, body).Box(3, 12, 12, 12, dark).Box(4, 5, 11, 8, 'w').Dot(6, 6, 'k').Dot(9, 6, 'k').Box(6, 10, 9, 10, 'k')
        .Box(0, 6, 2, 7, 'y').Box(1, 4, 1, 9, 'y')                          // the key
        .Box(7, 0, 8, 2, dark).Dot(7, 0, 'r')                                // aerial
        .Box(step == 0 ? 4 : 5, 13, step == 0 ? 6 : 7, 15, dark).Box(step == 0 ? 9 : 8, 13, step == 0 ? 11 : 10, 15, dark).Done();

    public static readonly Sprite[] Walker = [Clank(0, 'm', 'd'), Clank(1, 'm', 'd')];
    public static readonly Sprite[] WalkerAngry = [Clank(0, 'r', 'R'), Clank(1, 'r', 'R')];

    /// <summary>Boing: a blob on a spring.</summary>
    private static Sprite Boing(bool squash, char body, char dark) => new Canvas(16, 16)
        .Ellipse(8, squash ? 8 : 6, 6, squash ? 4 : 5, body).Ellipse(8, squash ? 9 : 7.5, 4, 2, dark)
        .Ellipse(6, squash ? 7 : 5, 1.6, 1.8, 'w').Ellipse(10, squash ? 7 : 5, 1.6, 1.8, 'w').Dot(6, squash ? 7 : 5, 'k').Dot(10, squash ? 7 : 5, 'k')
        .Box(7, squash ? 12 : 11, 8, 15, 'm').Dot(6, 13, 'm').Dot(9, 14, 'm').Box(5, 15, 10, 15, 'd').Done();

    public static readonly Sprite[] Jumper = [Boing(false, 'v', 'V'), Boing(true, 'v', 'V')];
    public static readonly Sprite[] JumperAngry = [Boing(false, 'r', 'R'), Boing(true, 'r', 'R')];

    /// <summary>Flit: a little bat, wings up and down.</summary>
    private static Sprite Flit(bool up, char body, char dark) => new Canvas(16, 16)
        .Ellipse(8, 9, 4, 4, body).Ellipse(6.5, 8, 1.3, 1.5, 'w').Ellipse(9.5, 8, 1.3, 1.5, 'w').Dot(6, 8, 'k').Dot(9, 8, 'k')
        .Box(up ? 0 : 1, up ? 4 : 9, 3, up ? 6 : 11, dark).Box(12, up ? 4 : 9, up ? 15 : 14, up ? 6 : 11, dark)
        .Dot(7, 11, 'w').Dot(9, 11, 'w').Done();

    public static readonly Sprite[] Flyer = [Flit(true, 'p', 'v'), Flit(false, 'p', 'v')];
    public static readonly Sprite[] FlyerAngry = [Flit(true, 'r', 'R'), Flit(false, 'r', 'R')];

    // ----- fruit --------------------------------------------------------------------------------------

    public static readonly Sprite Cherry = new Canvas(12, 12).Ellipse(3.5, 9, 2.6, 2.6, 'r').Ellipse(8.5, 9.5, 2.6, 2.6, 'r')
        .Dot(3, 8, 'w').Dot(8, 8, 'w').Dot(4, 5, 'G').Dot(5, 3, 'G').Dot(6, 2, 'G').Dot(8, 5, 'G').Dot(7, 3, 'G').Box(6, 1, 9, 1, 'g').Done();
    public static readonly Sprite Banana = new Canvas(12, 12).Ellipse(6, 7, 5.5, 3.5, 'y').Ellipse(6, 5, 4.5, 2.5, '.').Dot(1, 6, 'n').Dot(11, 5, 'n').Done();
    public static readonly Sprite Melon = new Canvas(12, 12).Ellipse(6, 4, 5.8, 6, 'g').Ellipse(6, 4, 4.6, 4.8, 'r').Box(0, 0, 11, 4, '.')
        .Dot(4, 6, 'k').Dot(6, 7, 'k').Dot(8, 6, 'k').Done();
    public static readonly Sprite Cake = new Canvas(12, 12).Box(1, 6, 10, 11, 'p').Box(1, 6, 10, 7, 'w').Box(1, 9, 10, 9, 'o').Box(5, 2, 6, 5, 'w').Dot(5, 1, 'o').Dot(6, 1, 'y').Done();
    public static readonly Sprite Diamond = new Canvas(12, 12).Box(3, 2, 8, 3, 'c').Box(2, 4, 9, 4, 'c').Box(3, 5, 8, 5, 'b').Box(4, 6, 7, 7, 'b').Box(5, 8, 6, 9, 'b').Dot(4, 2, 'w').Dot(3, 4, 'w').Done();

    public static Sprite FruitSprite(int kind) => kind switch { 0 => Cherry, 1 => Banana, 2 => Melon, 3 => Cake, _ => Diamond };
}
