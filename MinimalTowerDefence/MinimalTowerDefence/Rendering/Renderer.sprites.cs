using System;

namespace MinimalTowerDefence
{
    partial class Renderer
    {
        /// <summary>
        /// BGR32 data of tower sprite.
        /// </summary>
        private Int32[] towerSprite;
        private int towerSpriteDiameter;

        /// <summary>
        /// BGR32 data of gun sprites for every type and level.
        /// </summary>
        private Int32[,][] gunSprites;
        /// <summary>
        /// BGR32 data of dead gun sprite. The same for all gun types and levels.
        /// </summary>
        private Int32[] deadGunSprite;
        private int gunSpriteDiameter;

        /// <summary>
        /// BGR32 data of projectile sprites for every level.
        /// </summary>
        private Int32[][] projectileSprites;
        private int[] projectileSpriteDiameters;

        /// <summary>
        /// BGR32 data of monster sprites for every type.
        /// </summary>
        private Int32[][] monsterSprites;
        /// <summary>
        /// BGR32 data of dead monster sprites for every type.
        /// </summary>
        private Int32[][] deadMonsterSprites;
        private int[] monsterSpriteDiameters;

        private void InitializeSprites()
        {
            InitializeTowerSprite();
            InitializeGunSprites();
            InitializeProjectileSprites();
            InitializeMonsterSprites();
        }

        private void InitializeProjectileSprites()
        {
            projectileSprites = new Int32[Gun.NumLevels][];
            projectileSpriteDiameters = new int[Gun.NumLevels];

            var colorIntesityStep = 255 / Gun.NumLevels;

            for (int level = 0; level < Gun.NumLevels; ++level)
            {
                projectileSprites[level] = CirleSprite(Projectile.BaseRadius + Projectile.LevelRadiusStep * (level + 1),
                    ((level + 1) * colorIntesityStep) << (8 * ((int)Gun.Type.Machine)), out projectileSpriteDiameters[level]);
            }
        }

        private void InitializeMonsterSprites()
        {
            monsterSprites = new Int32[3][];
            deadMonsterSprites = new Int32[3][];
            monsterSpriteDiameters = new int[3];
            int[] colors = { (255 << 16) | (255 << 8), (255 << 16) | (255 << 0), (255 << 8) | (255 << 0) };
            for (int type = 0; type < 3; ++type)
            {
                int diameter = 2 * (int)(radialScale * Monster.Radius[type]);
                monsterSpriteDiameters[type] = diameter;
                monsterSprites[type] = new Int32[diameter * diameter];
                deadMonsterSprites[type] = new Int32[diameter * diameter];

                for (int i = 0; i < diameter * diameter; ++i)
                {
                    monsterSprites[type][i] = colors[type];
                    deadMonsterSprites[type][i] = (24 << 16) | (24 << 8) | (24 << 0);
                }
            }
        }

        private void InitializeGunSprites()
        {
            gunSprites = new Int32[3, Gun.NumLevels][];

            for (int type = 0; type < 3; ++type)
            {
                for (int level = 0; level < Gun.NumLevels; ++level)
                {
                    gunSprites[type, level] = CirleSprite(Gun.Radius,
                        (GunColors[type, level].R << 16) | (GunColors[type, level].G << 8) | (GunColors[type, level].B << 0),
                        out gunSpriteDiameter);
                }
            }

            deadGunSprite = CirleSprite(Gun.Radius, (24 << 16) | (24 << 8) | (24 << 0), out gunSpriteDiameter);
        }

        private void InitializeTowerSprite()
        {
            towerSprite = CirleSprite(GameLogic.TowerRadius, towerColor, out towerSpriteDiameter);
        }

        private Int32[] CirleSprite(double objectRadius, Int32 color, out int diameter)
        {
            int radius = (int)(radialScale * objectRadius);
            diameter = 2 * radius;
            var sprite = new Int32[diameter * diameter];
            int colored = 0;
            for (int x = 0; x < diameter; ++x)
            {
                for (int y = 0; y < diameter; ++y)
                {
                    if (Math.Sqrt((x - radius) * (x - radius) + (y - radius) * (y - radius)) < radius)
                    {
                        colored += 1;
                        sprite[y * diameter + x] = color;
                    }
                    else
                    {
                        sprite[y * diameter + x] = backgroundColor;
                    }
                }
            }

            return sprite;
        }

    }
}