
using System;
using System.Windows;
namespace MinimalTowerDefence
{
    struct PolarCoordinates
    {
        public double R { get; set; }
        /// <summary>
        /// Angle in radians.
        /// </summary>
        public double φ { get; set; }

        public PolarCoordinates(double R, double φ) : this()
        {
            this.R = R;
            this.φ = φ;
        }

        public static double PolarDistance(PolarCoordinates a, PolarCoordinates b)
        {
            return Math.Sqrt(a.R * a.R + b.R * b.R - 2 * a.R * b.R * Math.Cos(a.φ - b.φ));
        }

        public static bool PolarDistanceLessThan(PolarCoordinates a, PolarCoordinates b, double ε)
        {
            if (Math.Abs(a.R - b.R) >= ε) return false;
            return PolarDistance(a, b) < ε;
        }
    }

    /// <summary>
    /// Class representing game monster.
    /// </summary>
    class Monster
    {
        public enum Type
        {
            Slow = 0,
            Fast = 1,
            Vomiting = 2
        }

        /// <summary>
        /// Uniqe (for period of existence at least) identificator.
        /// </summary>
        public readonly long ID;
        public readonly Type type;
        /// <summary>
        /// Logical (off-screen) position.
        /// </summary>
        public PolarCoordinates Position;
        public double Life;

        public Monster(long ID, Type type, PolarCoordinates position, double life)
        {
            this.ID = ID;
            this.type = type;
            this.Position = position;
            this.Life = life;
        }

        /// <summary>
        /// Attack power for every monster type.
        /// </summary>
        public static readonly int[] Power = new int[] { 7, 5, 10 };
        /// <summary>
        /// Movement speed for every monster type.
        /// </summary>
        public static readonly double[] Speed = new double[] { 0.2, 0.6, 0.15 };
        /// <summary>
        /// Radius of monsters for every type. Logically every monster is a circle.
        /// </summary>
        public static readonly double[] Radius = new double[] { 1, 0.7, 1.4 };

        /// <summary>
        /// Radius of distant attack of vomiting monster type.
        /// </summary>
        public static readonly double VomitingRadius = 6;
        /// <summary>
        /// Radius of distant attack of vomiting monster type divided by √2.
        /// Used for interaction computation.
        /// </summary>
        public static readonly double VomitingRadiusOverSqrt2 = VomitingRadius / Math.Sqrt(2);
    }

    /// <summary>
    /// Class representing game gun.
    /// </summary>
    class Gun
    {
        public enum Type
        {
            Machine = 0,
            Lazer = 1,
            Mine = 2,
        }

        /// <summary>
        /// Uniqe (for period of existence at least) identificator.
        /// </summary>
        public readonly long ID;
        public readonly Type type;
        public readonly int Level;
        /// <summary>
        /// Logical (off-screen) position.
        /// </summary>
        public PolarCoordinates Position;
        public double Life;

        public Gun(long ID, Type type, int level, PolarCoordinates position, double life)
        {
            this.ID = ID;
            this.type = type;
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
        /// Radius of Mine exploision on level 0.
        /// </summary>
        public static readonly double ExplosionBaseRadius = 1.7;
        /// <summary>
        /// Step of exploision radius enlargement on every level.
        /// </summary>
        public static readonly double ExplosionRadiusStep = 1;
        /// <summary>
        /// Attack power of exploision.
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
        /// Attack power of lazer ray.
        /// </summary>
        public static readonly double BaseLaserPower = 40;

        /// <summary>
        /// Computes price of gun for given type and level.
        /// </summary>
        /// <param name="type">Gun type</param>
        /// <param name="level">Gun level</param>
        /// <returns></returns>
        public static int price(Type type, int level)
        {
            return (type == Type.Lazer? 400 : 200) * (level + 1);
        }
    }

    /// <summary>
    /// Class representing game projectile fired by Machine type gun.
    /// </summary>
    struct Projectile
    {
        /// <summary>
        /// Uniqe (for period of existence at least) identificator.
        /// </summary>
        public readonly long ID;
        /// <summary>
        /// Level of projectile inherited from firing gun.
        /// </summary>
        public readonly int Level;
        /// <summary>
        /// Logical (off-screen) position.
        /// </summary>
        public PolarCoordinates Position;

        public Projectile(long id, int level, PolarCoordinates position)
        {
            this.ID = id;
            this.Level = level;
            this.Position = position;
        }

        /// <summary>
        /// Radial (off-screen) speed on level 0.
        /// </summary>
        public static readonly double BaseRadialSpeed = 0.1;
        /// <summary>
        /// Radius (size) of projectile. Every projectile logically is a circle.
        /// </summary>
        public static readonly double BaseRadius = 0.6;
        /// <summary>
        /// Radius enlargement on every level.
        /// </summary>
        public static readonly double LevelRadiusStep = 0.1;
        /// <summary>
        /// Attack power of projectile.
        /// </summary>
        public static readonly double BasePower = 20;
    }
}