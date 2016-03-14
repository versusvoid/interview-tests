// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace MinimalTowerDefence
{
    /// <summary>
    /// Renders game on WritableBitmap (unsafely)
    /// </summary>
    internal partial class Renderer
    {
        public struct Message
        {
            public enum Type
            {
                Resize,
                Render,

                LaserFired,
                MineBlow,
                MonsterVomit,

                NewMonster,
                MonsterMoved,
                MonsterDied,

                NewGun,
                GunDied,

                NewProjectile,
                ProjectileMoved,
                ProjectileHit,

                Stop,
            }

            public Type MessageType { get; private set; }

            public int NewWidth { get; private set; }

            public int NewHeight { get; private set; }

            public IntPtr pBackBuffer { get; private set; }

            public long FieldObjectID { get; private set; }

            public PolarCoordinates FieldObjectNewPosition { get; private set; }

            public int FieldObjectLevel { get; private set; }

            public int FieldObjectType { get; private set; }

            public static Message Resize(int newWidth, int newHeight)
            {
                return new Message() { MessageType = Type.Resize, NewWidth = newWidth, NewHeight = newHeight };
            }

            public static Message Render(IntPtr pBackBuffer)
            {
                return new Message() { MessageType = Type.Render, pBackBuffer = pBackBuffer };
            }

            internal static Message LaserFired(long gunID)
            {
                return new Message() { MessageType = Type.LaserFired, FieldObjectID = gunID };
            }

            internal static Message MineBlow(long gunID)
            {
                return new Message() { MessageType = Type.MineBlow, FieldObjectID = gunID };
            }

            internal static Message MonsterVomit(long monsterID)
            {
                return new Message() { MessageType = Type.MonsterVomit, FieldObjectID = monsterID };
            }

            internal static Message NewMonster(long ID, PolarCoordinates position, int type)
            {
                return new Message() { MessageType = Type.NewMonster, FieldObjectID = ID, FieldObjectType = type, FieldObjectNewPosition = position };
            }

            internal static Message MonsterMoved(long monsterID, PolarCoordinates position)
            {
                return new Message() { MessageType = Type.MonsterMoved, FieldObjectID = monsterID, FieldObjectNewPosition = position };
            }

            internal static Message MonsterDied(long monsterID)
            {
                return new Message() { MessageType = Type.MonsterDied, FieldObjectID = monsterID };
            }

            internal static Message NewGun(long ID, PolarCoordinates position, int type, int level)
            {
                return new Message() { MessageType = Type.NewGun, FieldObjectID = ID, FieldObjectType = type, FieldObjectLevel = level, FieldObjectNewPosition = position };
            }

            internal static Message GunDied(long gunID)
            {
                return new Message() { MessageType = Type.GunDied, FieldObjectID = gunID };
            }

            internal static Message NewProjectile(long ID, PolarCoordinates position, int level)
            {
                return new Message() { MessageType = Type.NewProjectile, FieldObjectID = ID, FieldObjectNewPosition = position, FieldObjectLevel = level };
            }

            internal static Message ProjectileMoved(long projectileID, PolarCoordinates position)
            {
                return new Message() { MessageType = Type.ProjectileMoved, FieldObjectID = projectileID, FieldObjectNewPosition = position };
            }

            internal static Message ProjectileHit(long projectileID)
            {
                return new Message() { MessageType = Type.ProjectileHit, FieldObjectID = projectileID };
            }

            internal static Message Stop()
            {
                return new Message() { MessageType = Type.Stop };
            }
        }

        /// <summary>
        /// Gun colors by type and level.
        /// </summary>
        public static readonly Color[,] GunColors;

        static Renderer()
        {
            GunColors = new Color[Enum.GetNames(typeof(Gun.Type)).Length, Gun.NumLevels];

            var intensityStep = 255 / Gun.NumLevels;
            for (int type = 0; type < Enum.GetNames(typeof(Gun.Type)).Length; ++type)
            {
                var r = type == 0 ? 1 : 0;
                var g = type == 1 ? 1 : 0;
                var b = type == 2 ? 1 : 0;
                for (byte level = 0; level < Gun.NumLevels; ++level)
                {
                    GunColors[type, level] = new Color()
                    {
                        A = 255,
                        R = (byte)(r * intensityStep * (level + 1)),
                        G = (byte)(g * intensityStep * (level + 1)),
                        B = (byte)(b * intensityStep * (level + 1))
                    };
                }
            }
        }

        /// <summary>
        /// Represents any object on game field. We separate monsters, guns and projectiles
        /// by keeping them in different places, and their parameters are common, so we can
        /// represent them by single class.
        /// </summary>
        private class FieldObject
        {
            public int ObjectType;

            /// <summary>
            /// Level for guns and projectiles.
            /// </summary>
            public int Level;

            /// <summary>
            /// Screen position in usual (X, Y) coordinates.
            /// </summary>
            public Point Position;

            /// <summary>
            /// Whether monster or gun died.
            /// </summary>
            public bool Dead;

            public int FramesSinceDeath;
        }

        /// <summary>
        /// Represent effects. This is render object only and has no counterpart in logic.
        /// </summary>
        private class Effect
        {
            public enum Type
            {
                Laser = 0,
                Exploison = 1,
                Vomit = 2
            }

            public Type EffectType;
            public int AgeFrames;

            /// <summary>
            /// Screen position in usual (X, Y) coordinates.
            /// </summary>
            public Point Position;

            /// <summary>
            /// Level for guns and projectiles.
            /// </summary>
            public int Level;
        }

        private static readonly Int32 s_backgroundColor = (85 << 16) | (49 << 8) | (3 << 0);
        private static readonly Int32 s_towerColor = (240 << 16) | (240 << 8) | (240 << 0);

        /// <summary>
        /// Number of frames for which corpses of dead guns and monsters will be drawn on screen.
        /// </summary>
        private static readonly int s_displayCorpsesFramesCount = 10;

        /// <summary>
        /// Number of frames for which effect of given type will appear on screen.
        /// </summary>
        private static readonly int[] s_effectFramesLength = new int[] { 3, 20, 3 };

        public BlockingCollection<Message> MessageBox { get; private set; }

        private GameField _gameFieldWindow;

        /// <summary>
        /// Size scale of pixel window size to game field size of game logic.
        /// </summary>
        private double _radialScale;

        private Dictionary<long, Renderer.FieldObject> _guns = new Dictionary<long, Renderer.FieldObject>();
        private Dictionary<long, Renderer.FieldObject> _monsters = new Dictionary<long, Renderer.FieldObject>();
        private Dictionary<long, Renderer.FieldObject> _projectiles = new Dictionary<long, Renderer.FieldObject>();
        private LinkedList<Effect> _effects = new LinkedList<Effect>();

        /// <summary>
        /// Frame bitmap width (in pixels)
        /// </summary>
        private int _width;

        /// <summary>
        /// Frame bitmap height (in pixels)
        /// </summary>
        private int _height;

        public Renderer(GameField gameFieldWindow)
        {
            MessageBox = new BlockingCollection<Message>();
            _gameFieldWindow = gameFieldWindow;
        }

        private void RenderFrame(IntPtr pBackBuffer)
        {
            var start = DateTime.Now;

            Clear(pBackBuffer);
            DrawTower(pBackBuffer);
            DrawObjects(pBackBuffer, _guns, DrawGun);
            DrawObjects(pBackBuffer, _projectiles, DrawProjectile);
            DrawObjects(pBackBuffer, _monsters, DrawMonster);
            DrawEffects(pBackBuffer);

            var time = (DateTime.Now - start).TotalMilliseconds;
            if (time >= (1000 / 24) / 2)
            {
                Console.Error.WriteLine("Can't render in time. Rendering took {0}ms", time);
            }
        }

        /// <summary>
        /// Draws effects filtering out old ones.
        /// </summary>
        /// <param name="pBackBuffer"></param>
        private void DrawEffects(IntPtr pBackBuffer)
        {
            var currentEffect = _effects.First;
            while (currentEffect != null)
            {
                if (currentEffect.Value.AgeFrames > s_effectFramesLength[(int)currentEffect.Value.EffectType])
                {
                    var tmp = currentEffect.Next;
                    _effects.Remove(currentEffect);
                    currentEffect = tmp;
                    continue;
                }

                currentEffect.Value.AgeFrames += 1;

                switch (currentEffect.Value.EffectType)
                {
                    case Effect.Type.Laser:
                        DrawLaserRay(pBackBuffer, currentEffect.Value);
                        break;

                    case Effect.Type.Exploison:
                        DrawExploison(pBackBuffer, currentEffect.Value);
                        break;

                    case Effect.Type.Vomit:
                        DrawVomit(pBackBuffer, currentEffect.Value);
                        break;

                    default:
                        throw new NotImplementedException();
                }

                currentEffect = currentEffect.Next;
            }
        }

        private unsafe void DrawVomit(IntPtr pBackBuffer, Effect effect)
        {
            var radius = Monster.VomitingRadius * _radialScale;
            var r2 = radius * radius;
            var invSqrt2 = 1 / Math.Sqrt(2);
            var xmin = Math.Max(0, (int)(effect.Position.X - radius));
            var xmax = Math.Min(_width, (int)(effect.Position.X + radius));
            var ymin = Math.Max(0, (int)(effect.Position.Y - radius));
            var ymax = Math.Min(_height, (int)(effect.Position.Y + radius));

            var directionToCenter = (new Point(_width / 2, _height / 2) - effect.Position);
            directionToCenter.Normalize();

            int color = (102 / (effect.AgeFrames + 1)) << 8 | (102 / (effect.AgeFrames + 1)) << 0;

            for (int x = xmin; x < xmax; ++x)
            {
                for (int y = ymin; y < ymax; ++y)
                {
                    var localX = x - xmin - radius;
                    var localY = y - ymin - radius;

                    // Within radius from monster position.
                    if (localX * localX + localY * localY < r2)
                    {
                        var direction = new Point(x, y) - effect.Position;
                        direction.Normalize();

                        // Equivalent to condition that angle difference is less than π/4.
                        if (directionToCenter * direction > invSqrt2)
                        {
                            *((int*)(pBackBuffer + y * _width * 4 + x * 4).ToPointer()) = color;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Draws explosion circle
        /// </summary>
        private unsafe void DrawExploison(IntPtr pBackBuffer, Effect effect)
        {
            var radius = (Gun.ExplosionBaseRadius + Gun.ExplosionRadiusStep * effect.Level) * _radialScale;
            var r2 = radius * radius;
            var xmin = Math.Max(0, (int)(effect.Position.X - radius));
            var xmax = Math.Min(_width, (int)(effect.Position.X + radius));
            var ymin = Math.Max(0, (int)(effect.Position.Y - radius));
            var ymax = Math.Min(_height, (int)(effect.Position.Y + radius));
            int color = (64 + (255 - 64) / (effect.AgeFrames + 1)) << 16;

            for (int x = xmin; x < xmax; ++x)
            {
                for (int y = ymin; y < ymax; ++y)
                {
                    var localX = x - xmin - radius;
                    var localY = y - ymin - radius;
                    if (localX * localX + localY * localY < r2)
                    {
                        *((int*)(pBackBuffer + y * _width * 4 + x * 4).ToPointer()) = color;
                    }
                }
            }
        }

        private void DrawLaserRay(IntPtr pBackBuffer, Effect effect)
        {
            var h = _height - 1;
            var w = _width - 1;
            var x0 = effect.Position.X;
            var y0 = effect.Position.Y;
            var dx = x0 - w / 2;
            var dy = y0 - h / 2;

            // Computing second laser ray edge point.
            var scale = Math.Min(Math.Min(Math.Abs(x0 / dx), Math.Abs(y0 / dy)), Math.Min(Math.Abs((w - x0) / dx), Math.Abs((h - y0) / dy)));
            var x1 = (int)(x0 + scale * dx);
            var y1 = (int)(y0 + scale * dy);

            Debug.Assert(x1 >= 0 && x1 < _width);
            Debug.Assert(y1 >= 0 && y1 < _height);

            var color = (255 / (effect.AgeFrames + 1)) << 8;

            // It's better to draw line with width depending on laser ray radius,
            // but algorithms for such line are too damn huge.
            DrawLine(pBackBuffer, (int)x0, (int)y0, x1, y1, color);
        }

        /// <summary>
        /// Bresenham line algorithm. (thanks to http://habrahabr.ru/post/248153/)
        /// </summary>
        private void DrawLine(IntPtr pBackBuffer, int x0, int y0, int x1, int y1, int color)
        {
            bool steep = false;
            if (Math.Abs(x0 - x1) < Math.Abs(y0 - y1))
            {
                Swap(ref x0, ref y0);
                Swap(ref x1, ref y1);
                steep = true;
            }

            if (x0 > x1)
            {
                Swap(ref x0, ref x1);
                Swap(ref y0, ref y1);
            }

            int dx = x1 - x0;
            int dy = y1 - y0;
            int derror2 = Math.Abs(dy) * 2;
            int error2 = 0;
            int y = y0;
            for (int x = x0; x <= x1; x++)
            {
                if (steep)
                {
                    SetPixel(pBackBuffer, y, x, color);
                }
                else
                {
                    SetPixel(pBackBuffer, x, y, color);
                }

                error2 += derror2;

                if (error2 > dx)
                {
                    y += (y1 > y0 ? 1 : -1);
                    error2 -= dx * 2;
                }
            }
        }

        private unsafe void SetPixel(IntPtr pBackBuffer, int x, int y, int color)
        {
            pBackBuffer += y * _width * 4 + x * 4;
            *((int*)pBackBuffer.ToPointer()) = color;
        }

        private static void Swap<T>(ref T a, ref T b)
        {
            T t = a;
            a = b;
            b = t;
        }

        private void DrawTower(IntPtr pBackBuffer)
        {
            int diameter = _towerSpriteDiameter;
            var radius = diameter / 2;
            var centerX = _width / 2;
            var centerY = _height / 2;
            WritePixels(pBackBuffer, _towerSprite, diameter, (int)centerX - radius, (int)centerY - radius);
        }

        /// <summary>
        /// Common method for drawing every field object.
        /// </summary>
        private void DrawObjects(IntPtr pBackBuffer, Dictionary<long, FieldObject> objects, Action<IntPtr, FieldObject> drawObject)
        {
            var deadObjects = new List<long>();
            foreach (var fieldObject in objects)
            {
                if (fieldObject.Value.Dead)
                {
                    if (fieldObject.Value.FramesSinceDeath > s_displayCorpsesFramesCount)
                    {
                        deadObjects.Add(fieldObject.Key);
                        continue;
                    }

                    fieldObject.Value.FramesSinceDeath += 1;
                }

                drawObject(pBackBuffer, fieldObject.Value);
            }

            foreach (var id in deadObjects)
            {
                objects.Remove(id);
            }
        }

        private void DrawGun(IntPtr pBackBuffer, FieldObject gun)
        {
            int diameter = _gunSpriteDiameter;
            var radius = _gunSpriteDiameter / 2;
            if (gun.Dead)
            {
                WritePixels(pBackBuffer, _deadGunSprite, diameter,
                    (int)gun.Position.X - radius, (int)gun.Position.Y - radius);
            }
            else
            {
                WritePixels(pBackBuffer, _gunSprites[gun.ObjectType, gun.Level], diameter,
                    (int)gun.Position.X - radius, (int)gun.Position.Y - radius);
            }
        }

        private void DrawProjectile(IntPtr pBackBuffer, FieldObject projectile)
        {
            int diameter = _projectileSpriteDiameters[projectile.Level];
            var radius = diameter / 2;
            var x = (int)projectile.Position.X - radius;
            var y = (int)projectile.Position.Y - radius;
            if (projectile.Dead)
            {
                throw new InvalidOperationException();
            }
            else
            {
                WritePixels(pBackBuffer, _projectileSprites[projectile.Level], diameter, x, y);
            }
        }

        private void DrawMonster(IntPtr pBackBuffer, FieldObject monster)
        {
            int diameter = _monsterSpriteDiameters[monster.ObjectType];
            var radius = diameter / 2;
            var x = (int)monster.Position.X - radius;
            var y = (int)monster.Position.Y - radius;
            if (monster.Dead)
            {
                WritePixels(pBackBuffer, _deadMonsterSprites[monster.ObjectType], diameter, x, y);
            }
            else
            {
                WritePixels(pBackBuffer, _monsterSprites[monster.ObjectType], diameter, x, y);
            }
        }

        /// <summary>
        /// Writes pixels from sprite on bitmap omitting ones that have background color.
        /// </summary>
        private unsafe void WritePixels(IntPtr pBackBuffer, Int32[] sprite, int diameter, int x, int y)
        {
            var xmin = Math.Max(0, x);
            var xmax = Math.Min(_width, x + diameter);

            var ymin = Math.Max(0, y);
            var ymax = Math.Min(_height, y + diameter);

            for (x = xmin; x < xmax; ++x)
            {
                for (y = ymin; y < ymax; ++y)
                {
                    var color = sprite[(y - ymin) * diameter + (x - xmin)];
                    if (color == s_backgroundColor) continue;
                    *((int*)(pBackBuffer + y * _width * 4 + x * 4).ToPointer()) = color;
                }
            }
        }

        /// <summary>
        /// Clears bitmap with background color
        /// </summary>
        private unsafe void Clear(IntPtr pBackBuffer)
        {
            for (int i = 0; i < _width * _height; ++i)
            {
                *((int*)pBackBuffer.ToPointer()) = s_backgroundColor;
                pBackBuffer += 4;
            }
        }
    }
}