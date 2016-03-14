using System;

namespace MinimalTowerDefence
{
    /// <summary>
    /// Class representing game monster.
    /// </summary>
    internal class Monster
    {
        public enum Type
        {
            Slow = 0,
            Fast = 1,
            Vomiting = 2
        }

        /// <summary>
        /// Unique (for period of existence at least) identificator.
        /// </summary>
        public long ID { get; private set; }

        public Type MonsterType { get; private set; }

        /// <summary>
        /// Logical (off-screen) position.
        /// </summary>
        public PolarCoordinates Position { get; set; }

        public double Life { get; set; }

        public Monster(long ID, Type type, PolarCoordinates position, double life)
        {
            this.ID = ID;
            this.MonsterType = type;
            this.Position = position;
            this.Life = life;
        }

        /// <summary>
        /// Attack power for every monster type.
        /// </summary>
        public static readonly int[] Power = { 7, 5, 10 };

        /// <summary>
        /// Movement speed for every monster type.
        /// </summary>
        public static readonly double[] Speed = { 0.2, 0.6, 0.15 };

        /// <summary>
        /// Radius of monsters for every type. Logically every monster is a circle.
        /// </summary>
        public static readonly double[] Radius = { 1, 0.7, 1.4 };

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
}