namespace AVABubble.Models;

/// <summary>A 2D point or vector in game pixels (y grows downwards).</summary>
public readonly record struct Vec(double X, double Y)
{
    public static Vec operator +(Vec a, Vec b) => new(a.X + b.X, a.Y + b.Y);
    public static Vec operator -(Vec a, Vec b) => new(a.X - b.X, a.Y - b.Y);
    public static Vec operator *(Vec a, double k) => new(a.X * k, a.Y * k);
    public double Length => Math.Sqrt(X * X + Y * Y);
    public Vec Normal => Length < 1e-9 ? new Vec(0, 0) : this * (1 / Length);
    public override string ToString() => $"({X:0.#}, {Y:0.#})";
}

/// <summary>
/// The screen in game pixels: a level of 32 by 26 tiles, 8 pixels each, so 256 by 208. Walls down
/// the sides; a floor and a ceiling with a gap in the middle, so falling through the floor brings you
/// back in at the top.
/// </summary>
public static class Field
{
    public const int Cols = 32;
    public const int Rows = 26;
    public const int Tile = 8;
    public const int Width = Cols * Tile;
    public const int Height = Rows * Tile;
    public const double Step = 1.0 / 60;
}

/// <summary>What a tile is.</summary>
public enum Tile : byte
{
    Empty,
    /// <summary>Solid from every side.</summary>
    Wall,
    /// <summary>A platform: stood on from above, jumped up through from below.</summary>
    Platform,
}

public enum MonsterKind
{
    /// <summary>Walks the platforms and hops up after you.</summary>
    Walker,
    /// <summary>Bounds about, jumping all the time.</summary>
    Jumper,
    /// <summary>Floats about, bouncing off everything.</summary>
    Flyer,
}

/// <summary>
/// A level, written as text: <c>W</c> wall, <c>#</c> platform, <c>P</c> where the hero starts,
/// <c>m</c> <c>j</c> <c>f</c> a walker, a jumper and a flyer (each marks the lower-left tile of the
/// two-by-two space it takes; its feet rest on the tile below).
/// </summary>
public sealed class LevelMap
{
    public required string Name { get; init; }
    public Tile[,] Tiles { get; } = new Tile[Field.Cols, Field.Rows];
    public Vec Start { get; private set; }
    public List<(MonsterKind Kind, Vec At)> Monsters { get; } = [];

    /// <summary>Where something two tiles wide whose lower-left tile is (col, row) has its feet.</summary>
    public static Vec FeetAt(int col, int row) => new((col + 1) * Field.Tile, (row + 1) * Field.Tile);

    public static LevelMap Parse(string name, IReadOnlyList<string> rows)
    {
        var m = new LevelMap { Name = name };
        for (int r = 0; r < Field.Rows; r++)
            for (int c = 0; c < Field.Cols; c++)
            {
                char ch = rows[r][c];
                m.Tiles[c, r] = ch switch { 'W' => Tile.Wall, '#' => Tile.Platform, _ => Tile.Empty };
                switch (ch)
                {
                    case 'P': m.Start = FeetAt(c, r); break;
                    case 'm': m.Monsters.Add((MonsterKind.Walker, FeetAt(c, r))); break;
                    case 'j': m.Monsters.Add((MonsterKind.Jumper, FeetAt(c, r))); break;
                    case 'f': m.Monsters.Add((MonsterKind.Flyer, FeetAt(c, r))); break;
                }
            }
        return m;
    }
}

/// <summary>The game's own twelve levels (none is copied from the arcade's).</summary>
public static class Levels
{
    public static readonly LevelMap[] All =
    [
        LevelMap.Parse("First Bubbles",
        [
            "WWWWWWWWWWWWWW....WWWWWWWWWWWWWW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW....m.................m.....WW",
            "WW..########........########..WW",
            "WW............................WW",
            "WW.............m..............WW",
            "WW..........########..........WW",
            "WW............................WW",
            "WW............................WW",
            "WW..########........########..WW",
            "WW............................WW",
            "WW............................WW",
            "WW..........########..........WW",
            "WW............................WW",
            "WW............................WW",
            "WW..########........########..WW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW.P..........................WW",
            "WWWWWWWWWWWWWW....WWWWWWWWWWWWWW",
        ]),
        LevelMap.Parse("Staircase",
        [
            "WWWWWWWWWWWWWW....WWWWWWWWWWWWWW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW.......................m....WW",
            "WW......................#####.WW",
            "WW............................WW",
            "WW...m...............m........WW",
            "WW.#######..........#####.....WW",
            "WW............................WW",
            "WW............................WW",
            "WW..............#####.........WW",
            "WW............................WW",
            "WW...........j................WW",
            "WW.#####....#####.............WW",
            "WW............................WW",
            "WW............................WW",
            "WW......#####......#####......WW",
            "WW............................WW",
            "WW............................WW",
            "WW..#####..............#####..WW",
            "WW............................WW",
            "WW............................WW",
            "WW.P..........................WW",
            "WWWWWWWWWWWWWW....WWWWWWWWWWWWWW",
        ]),
        LevelMap.Parse("Columns",
        [
            "WWWWWWWWWWWWWW....WWWWWWWWWWWWWW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW.......WW..........WW.......WW",
            "WW.......WW....j.....WW.......WW",
            "WW.......WW##########WW.......WW",
            "WW..m....WW..........WW...m...WW",
            "WW#######WW..........WW#######WW",
            "WW.......WW..........WW.......WW",
            "WW.......WW..........WW.......WW",
            "WW.......WW##########WW.......WW",
            "WW.......WW..........WW.......WW",
            "WW#######WW..........WW#######WW",
            "WW.......WW..........WW.......WW",
            "WW.......WW....m.....WW.......WW",
            "WW.......WW##########WW.......WW",
            "WW.......WW..........WW.......WW",
            "WW#######WW..........WW#######WW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW.P..........................WW",
            "WWWWWWWWWWWWWW....WWWWWWWWWWWWWW",
        ]),
        LevelMap.Parse("Boxes",
        [
            "WWWWWWWWWWWWWW....WWWWWWWWWWWWWW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW..#########......#########..WW",
            "WW..#.......#......#.......#..WW",
            "WW..#.......#......#.......#..WW",
            "WW..#.......#......#.......#..WW",
            "WW..#.......#......#.......#..WW",
            "WW..#..m....#......#..m....#..WW",
            "WW..#########..f...#########..WW",
            "WW...........######...........WW",
            "WW............................WW",
            "WW..#########......#########..WW",
            "WW..#.......#......#.......#..WW",
            "WW..#.......#......#.......#..WW",
            "WW..#.......#......#.......#..WW",
            "WW..#.......#......#.......#..WW",
            "WW..#..j....#......#..j....#..WW",
            "WW..#########......#########..WW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW.P..........................WW",
            "WWWWWWWWWWWWWW....WWWWWWWWWWWWWW",
        ]),
        LevelMap.Parse("Heart",
        [
            "WWWWWWWWWWWWWW....WWWWWWWWWWWWWW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW........m........m..........WW",
            "WW........###.....###.........WW",
            "WW............................WW",
            "WW............................WW",
            "WW.............j..............WW",
            "WW......###############.......WW",
            "WW............................WW",
            "WW............................WW",
            "WW...........m................WW",
            "WW........###########.........WW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW............###.............WW",
            "WW............................WW",
            "WW...f........................WW",
            "WW.#######............#######.WW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW.P..........................WW",
            "WWWWWWWWWWWWWW....WWWWWWWWWWWWWW",
        ]),
        LevelMap.Parse("Zigzag",
        [
            "WWWWWWWWWWWWWW....WWWWWWWWWWWWWW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW........m...................WW",
            "WW#####################.......WW",
            "WW............................WW",
            "WW............................WW",
            "WW..................j.........WW",
            "WW.......#####################WW",
            "WW............................WW",
            "WW............................WW",
            "WW......m.....................WW",
            "WW#####################.......WW",
            "WW............................WW",
            "WW............................WW",
            "WW....................j.......WW",
            "WW.......#####################WW",
            "WW............................WW",
            "WW............................WW",
            "WW........m...................WW",
            "WW#####################.......WW",
            "WW............................WW",
            "WW............................WW",
            "WW........................P...WW",
            "WWWWWWWWWWWWWW....WWWWWWWWWWWWWW",
        ]),
        LevelMap.Parse("Shelves",
        [
            "WWWWWWWWWWWWWW....WWWWWWWWWWWWWW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW..m....................m....WW",
            "WW.####...####...####...####..WW",
            "WW............................WW",
            "WW............................WW",
            "WW.........j......f...........WW",
            "WW.####...####...####...####..WW",
            "WW............................WW",
            "WW............................WW",
            "WW................j...........WW",
            "WW.####...####...####...####..WW",
            "WW............................WW",
            "WW............................WW",
            "WW..m.........................WW",
            "WW.####...####...####...####..WW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW.####...####...####...####..WW",
            "WW............................WW",
            "WW.P..........................WW",
            "WWWWWWWWWWWWWW....WWWWWWWWWWWWWW",
        ]),
        LevelMap.Parse("Open Sky",
        [
            "WWWWWWWWWWWWWW....WWWWWWWWWWWWWW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW......f..............f......WW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW..f.........................WW",
            "WW..........########..........WW",
            "WW............................WW",
            "WW............................WW",
            "WW.............f..............WW",
            "WW............................WW",
            "WW............................WW",
            "WW.......m....................WW",
            "WW....#######......#######....WW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW.P..........................WW",
            "WWWWWWWWWWWWWW....WWWWWWWWWWWWWW",
        ]),
        LevelMap.Parse("A.V.A.",
        [
            "WWWWWWWWWWWWWW....WWWWWWWWWWWWWW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW....j.................j.....WW",
            "WW....#......#...#......#.....WW",
            "WW............................WW",
            "WW............................WW",
            "WW...#.#.....#...#.....#.#....WW",
            "WW............................WW",
            "WW.............f..............WW",
            "WW..#...#.....#.#.....#...#...WW",
            "WW............................WW",
            "WW....m........m........m.....WW",
            "WW..#####.....#.#.....#####...WW",
            "WW............................WW",
            "WW............................WW",
            "WW..#...#......#......#...#...WW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW.#########........#########.WW",
            "WW............................WW",
            "WW............................WW",
            "WW.P..........................WW",
            "WWWWWWWWWWWWWW....WWWWWWWWWWWWWW",
        ]),
        LevelMap.Parse("Pyramid",
        [
            "WWWWWWWWWWWWWW....WWWWWWWWWWWWWW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW.............j..............WW",
            "WW............####............WW",
            "WW............................WW",
            "WW...........m...m............WW",
            "WW..........########..........WW",
            "WW............................WW",
            "WW.............f..............WW",
            "WW........############........WW",
            "WW............................WW",
            "WW.......j...........j........WW",
            "WW......################......WW",
            "WW............................WW",
            "WW............................WW",
            "WW....####################....WW",
            "WW............................WW",
            "WW............................WW",
            "WW..########################..WW",
            "WW............................WW",
            "WW............................WW",
            "WWP...........................WW",
            "WWWWWWWWWWWWWW....WWWWWWWWWWWWWW",
        ]),
        LevelMap.Parse("Tunnels",
        [
            "WWWWWWWWWWWWWW....WWWWWWWWWWWWWW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW...m...................m....WW",
            "WWWWWWWWWWWW........WWWWWWWWWWWW",
            "WW............................WW",
            "WW.............j..............WW",
            "WW......################......WW",
            "WW............................WW",
            "WW...j...................j....WW",
            "WWWWWWWWWWWW........WWWWWWWWWWWW",
            "WW............................WW",
            "WW.............f..............WW",
            "WW......################......WW",
            "WW............................WW",
            "WW.......................m....WW",
            "WWWWWWWWWWWW........WWWWWWWWWWWW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW.P..........................WW",
            "WWWWWWWWWWWWWW....WWWWWWWWWWWWWW",
        ]),
        LevelMap.Parse("Fortress",
        [
            "WWWWWWWWWWWWWW....WWWWWWWWWWWWWW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW..........f.......f.........WW",
            "WW.......WWWWWWWWWWWWWW.......WW",
            "WW.......W............W.......WW",
            "WW.......W............W.......WW",
            "WW.......W..m......j..W.......WW",
            "WW.......W############W.......WW",
            "WW.j.....W............W....j..WW",
            "WW#####..W............W..#####WW",
            "WW.......W.....m......W.......WW",
            "WW.......W############W.......WW",
            "WW............................WW",
            "WW............................WW",
            "WW............................WW",
            "WW.......##############.......WW",
            "WW............................WW",
            "WW............................WW",
            "WW.########..........########.WW",
            "WW............................WW",
            "WW............................WW",
            "WW.P..........................WW",
            "WWWWWWWWWWWWWW....WWWWWWWWWWWWWW",
        ]),
    ];
}
