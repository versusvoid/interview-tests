namespace MinimalTowerDefence
{
    /// <summary>
    /// Class representing game projectile fired by Machine type gun.
    /// </summary>
    internal class Projectile
    {
        /// <summary>
        /// Unique (for period of existence at least) identificator.
        /// </summary>
        public long ID { get; private set; }

        /// <summary>
        /// Level of projectile inherited from firing gun.
        /// </summary>
        public int Level { get; private set; }

        /// <summary>
        /// Logical (off-screen) position.
        /// </summary>
        public PolarCoordinates Position { get; set; }

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