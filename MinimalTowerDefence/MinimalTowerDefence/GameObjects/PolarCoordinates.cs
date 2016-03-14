using System;

namespace MinimalTowerDefence
{
    /// <summary>
    /// Logical polar coordinates of any game object.
    /// </summary>
    internal struct PolarCoordinates
    {
        /// <summary>
        /// Polar radius.
        /// </summary>
        public double R { get; set; }

        /// <summary>
        /// Angle in radians.
        /// </summary>
        public double φ { get; set; }

        public PolarCoordinates(double r, double φ) : this()
        {
            this.R = r;
            if (φ > 2 * Math.PI)
                this.φ = φ - 2 * Math.PI;
            else
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
}