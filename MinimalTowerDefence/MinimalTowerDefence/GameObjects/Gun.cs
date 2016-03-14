namespace MinimalTowerDefence
{
    /// <summary>
    /// Class representing game gun.
    /// </summary>
    internal class Gun
{
    public enum Type
    {
        Machine = 0,
        Lazer = 1,
        Mine = 2,
    }

    /// <summary>
    /// Unique (for period of existence at least) identificator.
    /// </summary>
    public long ID { get; private set; }

    public Type GunType { get; private set; }

    public int Level { get; private set; }

    /// <summary>
    /// Logical (off-screen) position.
    /// </summary>
    public PolarCoordinates Position { get; set; }

    public double Life { get; set; }

    public Gun(long ID, Type type, int level, PolarCoordinates position, double life)
    {
        this.ID = ID;
        this.GunType = type;
        this.Level = level;
        this.Position = position;
        this.Life = life;
    }

    /// <summary>
    /// Number of possible levels.
    /// </summary>
    public static readonly int NumLevels = 5;

    /// <summary>
    /// Radius (size) of gun. Every gun logically is a circle.
    /// </summary>
    public static readonly double Radius = 1.5;

    /// <summary>
    /// Radius of Mine explosion on level 0.
    /// </summary>
    public static readonly double ExplosionBaseRadius = 1.7;

    /// <summary>
    /// Step of explosion radius enlargement on every level.
    /// </summary>
    public static readonly double ExplosionRadiusStep = 1;

    /// <summary>
    /// Attack power of explosion.
    /// </summary>
    public static readonly double ExplosionPower = 130;

    /// <summary>
    /// Lazer ray width on level 0.
    /// </summary>
    public static readonly double BaseLaserRayWidth = 2;

    /// <summary>
    /// Lazer ray width enlargement on every level.
    /// </summary>
    public static readonly double LaserRayWidthStep = 0.2;

    /// <summary>
    /// Attack power of laser ray.
    /// </summary>
    public static readonly double BaseLaserPower = 40;

    /// <summary>
    /// Computes price of gun for given type and level.
    /// </summary>
    /// <param name="type">Gun type</param>
    /// <param name="level">Gun level</param>
    /// <returns></returns>
    public static int Price(Type type, int level)
    {
        return (type == Type.Lazer ? 400 : 200) * (level + 1);
    }
}
}