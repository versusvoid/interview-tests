// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace MinimalTowerDefence
{
    internal partial class Renderer
    {
        /// <summary>
        /// BGR32 data of tower sprite.
        /// </summary>
        private Int32[] _towerSprite;

        private int _towerSpriteDiameter;

        /// <summary>
        /// BGR32 data of gun sprites for every type and level.
        /// </summary>
        private Int32[,][] _gunSprites;

        /// <summary>
        /// BGR32 data of dead gun sprite. The same for all gun types and levels.
        /// </summary>
        private Int32[] _deadGunSprite;

        private int _gunSpriteDiameter;

        /// <summary>
        /// BGR32 data of projectile sprites for every level.
        /// </summary>
        private Int32[][] _projectileSprites;

        private int[] _projectileSpriteDiameters;

        /// <summary>
        /// BGR32 data of monster sprites for every type.
        /// </summary>
        private Int32[][] _monsterSprites;

        /// <summary>
        /// BGR32 data of dead monster sprites for every type.
        /// </summary>
        private Int32[][] _deadMonsterSprites;

        private int[] _monsterSpriteDiameters;

        private void InitializeSprites()
        {
            InitializeTowerSprite();
            InitializeGunSprites();
            InitializeProjectileSprites();
            InitializeMonsterSprites();
        }

        private void InitializeProjectileSprites()
        {
            _projectileSprites = new Int32[Gun.NumLevels][];
            _projectileSpriteDiameters = new int[Gun.NumLevels];

            var colorIntesityStep = 255 / Gun.NumLevels;

            for (int level = 0; level < Gun.NumLevels; ++level)
            {
                _projectileSprites[level] = CirleSprite(Projectile.BaseRadius + Projectile.LevelRadiusStep * (level + 1),
                    ((level + 1) * colorIntesityStep) << (8 * ((int)Gun.Type.Machine)), out _projectileSpriteDiameters[level]);
            }
        }

        private void InitializeMonsterSprites()
        {
            _monsterSprites = new Int32[3][];
            _deadMonsterSprites = new Int32[3][];
            _monsterSpriteDiameters = new int[3];
            int[] colors = { (255 << 16) | (255 << 8), (255 << 16) | (255 << 0), (255 << 8) | (255 << 0) };
            for (int type = 0; type < 3; ++type)
            {
                int diameter = 2 * (int)(_radialScale * Monster.Radius[type]);
                _monsterSpriteDiameters[type] = diameter;
                _monsterSprites[type] = new Int32[diameter * diameter];
                _deadMonsterSprites[type] = new Int32[diameter * diameter];

                for (int i = 0; i < diameter * diameter; ++i)
                {
                    _monsterSprites[type][i] = colors[type];
                    _deadMonsterSprites[type][i] = (24 << 16) | (24 << 8) | (24 << 0);
                }
            }
        }

        private void InitializeGunSprites()
        {
            _gunSprites = new Int32[3, Gun.NumLevels][];

            for (int type = 0; type < 3; ++type)
            {
                for (int level = 0; level < Gun.NumLevels; ++level)
                {
                    _gunSprites[type, level] = CirleSprite(Gun.Radius,
                        (GunColors[type, level].R << 16) | (GunColors[type, level].G << 8) | (GunColors[type, level].B << 0),
                        out _gunSpriteDiameter);
                }
            }

            _deadGunSprite = CirleSprite(Gun.Radius, (24 << 16) | (24 << 8) | (24 << 0), out _gunSpriteDiameter);
        }

        private void InitializeTowerSprite()
        {
            _towerSprite = CirleSprite(GameLogic.TowerRadius, s_towerColor, out _towerSpriteDiameter);
        }

        private Int32[] CirleSprite(double objectRadius, Int32 color, out int diameter)
        {
            int radius = (int)(_radialScale * objectRadius);
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
                        sprite[y * diameter + x] = s_backgroundColor;
                    }
                }
            }

            return sprite;
        }
    }
}