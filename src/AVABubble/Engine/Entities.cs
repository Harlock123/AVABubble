using AVABubble.Models;

namespace AVABubble.Engine;

/// <summary>Something that walks, jumps and falls: the hero, a monster, a fruit. Its position is its feet.</summary>
public class Body
{
    public Vec Pos { get; set; }
    public Vec Vel { get; set; }
    public bool OnGround { get; set; }
    public bool FacingLeft { get; set; }
    /// <summary>Half its width, and its height.</summary>
    public virtual double HalfW => 6;
    public virtual double Tall => 14;
    public Vec Centre => Pos + new Vec(0, -Tall / 2);
}

public sealed class Hero : Body
{
    /// <summary>Seconds of blowing left (for the drawing), and of not being catchable after starting.</summary>
    public double Blowing { get; set; }
    public double Safe { get; set; }
    public double Walk { get; set; }
}

public sealed class Monster : Body
{
    public required MonsterKind Kind { get; init; }
    public Vec Spawn { get; init; }
    public bool Angry { get; set; }
    public Bubble? Trapped { get; set; }
    public bool Dead { get; set; }
    public double Think { get; set; }
    public double Age { get; set; }
}

public sealed class Bubble
{
    public const double Radius = 8;
    public Vec Pos { get; set; }
    public Vec Vel { get; set; }
    /// <summary>Still shooting out of the hero's mouth (it traps a monster it meets).</summary>
    public double Shot { get; set; }
    public double Age { get; set; }
    public double Life { get; set; }
    public Monster? Monster { get; set; }
    /// <summary>A letter bubble: which of E X T E N D (0 to 5), or −1.</summary>
    public int Letter { get; set; } = -1;
    public bool Popped { get; set; }
}

public enum FruitKind { Cherry, Banana, Melon, Cake, Diamond }

public sealed class Fruit : Body
{
    public required FruitKind Kind { get; init; }
    public double Age { get; set; }
    public override double HalfW => 5;
    public override double Tall => 10;
}

/// <summary>The ghost that comes for a hero who takes too long: through walls, unstoppable.</summary>
public sealed class Shade
{
    public Vec Pos { get; set; }
    public double Age { get; set; }
}

public enum EffectKind { Score, Pop, Burst }

public sealed class Effect
{
    public required EffectKind Kind { get; init; }
    public Vec Pos { get; set; }
    public double Age { get; set; }
    public double Life { get; init; } = 1;
    public string Text { get; init; } = "";
    public bool Done => Age >= Life;
}

/// <summary>The controls for one step: left or right (−1 to 1), jump, and blow.</summary>
public record struct Controls(double Dx, bool Jump = false, bool Blow = false);
