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
    partial class Renderer
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

            public Type type { get; private set; }

            public int NewWidth { get; private set; }
            public int NewHeight { get; private set; }
            public IntPtr pBackBuffer { get; private set; }

            public long FieldObjectID { get; private set; }
            public PolarCoordinates FieldObjectNewPosition { get; private set; }
            public int FieldObjectLevel { get; private set; }
            public int FieldObjectType { get; private set; }
            
            public static Message Resize(int newWidth, int newHeight)
            {
                return new Message() { type = Type.Resize, NewWidth = newWidth, NewHeight = newHeight };
            }
            
            public static Message Render(IntPtr pBackBuffer)
            {
                return new Message() { type = Type.Render, pBackBuffer = pBackBuffer };
            }

            internal static Message LaserFired(long gunID)
            {
                return new Message() { type = Type.LaserFired, FieldObjectID = gunID };
            }

            internal static Message MineBlow(long gunID)
            {
                return new Message() { type = Type.MineBlow, FieldObjectID = gunID };
            }

            internal static Message MonsterVomit(long monsterID)
            {
                return new Message() { type = Type.MonsterVomit, FieldObjectID = monsterID };
            }

            internal static Message NewMonster(long ID, PolarCoordinates position, int type)
            {
                return new Message() { type = Type.NewMonster, FieldObjectID = ID, FieldObjectType = type, FieldObjectNewPosition = position };
            }

            internal static Message MonsterMoved(long monsterID, PolarCoordinates position)
            {
                return new Message() { type = Type.MonsterMoved, FieldObjectID = monsterID, FieldObjectNewPosition = position };
            }

            internal static Message MonsterDied(long monsterID)
            {
                return new Message() { type = Type.MonsterDied, FieldObjectID = monsterID };
            }

            internal static Message NewGun(long ID, PolarCoordinates position, int type, int level)
            {
                return new Message() { type = Type.NewGun, FieldObjectID = ID, FieldObjectType = type, FieldObjectLevel = level, FieldObjectNewPosition = position };
            }

            internal static Message GunDied(long gunID)
            {
                return new Message() { type = Type.GunDied, FieldObjectID = gunID };
            }

            internal static Message NewProjectile(long ID, PolarCoordinates position, int level)
            {
                return new Message() { type = Type.NewProjectile, FieldObjectID = ID, FieldObjectNewPosition = position, FieldObjectLevel = level };
            }

            internal static Message ProjectileMoved(long projectileID, PolarCoordinates position)
            {
                return new Message() { type = Type.ProjectileMoved, FieldObjectID = projectileID, FieldObjectNewPosition = position };
            }

            internal static Message ProjectileHit(long projectileID)
            {
                return new Message() { type = Type.ProjectileHit, FieldObjectID = projectileID };
            }
            
            internal static Message Stop()
            {
                return new Message() { type = Type.Stop };
            }
        }

        /// <summary>
        /// Gun colors by type and level.
        /// </summary>
        public static readonly Color[,] GunColors;
        /// <summary>
        /// Maximal logic(!) radius that will be visible on screen.
        /// </summary>
        public static readonly double MaxVisibleLogicRadius = 100.0;

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
            public int type;
            /// <summary>
            /// Level for guns and projectiles.
            /// </summary>
            public int level;
            /// <summary>
            /// Screen position in usual (X, Y) coordinates.
            /// </summary>
            public Point position;

            /// <summary>
            /// Whether monster or gun died.
            /// </summary>
            public bool dead;
            public int framesSinceDeath;
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

            public Type type;
            public int ageFrames;
            /// <summary>
            /// Screen position in usual (X, Y) coordinates.
            /// </summary>
            public Point position;
            /// <summary>
            /// Level for guns and projectiles.
            /// </summary>
            public int level;
        }


        private static readonly Int32 backgroundColor = (85 << 16) | (49 << 8) | (3 << 0);
        private static readonly Int32 towerColor = (240 << 16) | (240 << 8) | (240 << 0);

        /// <summary>
        /// Number of frames for which corpses of dead guns and monsters will be drawn on screen.
        /// </summary>
        private static readonly int DisplayCorpsesFramesCount = 10;
        /// <summary>
        /// Number of frames for which effect of given type will appear on screen.
        /// </summary>
        private static readonly int[] EffectFramesLength = new int[] { 3, 20, 3 };


        public BlockingCollection<Message> messageBox { get; private set; }


        private GameField gameFieldWindow;
        /// <summary>
        /// Size scale of pixel window size to game field size of game logic.
        /// </summary>
        private double radialScale;
        
       

        private Dictionary<long, Renderer.FieldObject> guns = new Dictionary<long, Renderer.FieldObject>();
        private Dictionary<long, Renderer.FieldObject> monsters = new Dictionary<long, Renderer.FieldObject>();
        private Dictionary<long, Renderer.FieldObject> projectiles = new Dictionary<long, Renderer.FieldObject>();
        private LinkedList<Effect> effects = new LinkedList<Effect>();

        /// <summary>
        /// Frame bitmap width (in pixels)
        /// </summary>
        private int width;
        /// <summary>
        /// Frame bitmap height (in pixels)
        /// </summary>
        private int height;

        public Renderer(GameField gameFieldWindow)
        {
            messageBox = new BlockingCollection<Message>();
            this.gameFieldWindow = gameFieldWindow;
        }

        private void renderFrame(IntPtr pBackBuffer)
        {
            var start = DateTime.Now;

            clear(pBackBuffer);
            drawTower(pBackBuffer);
            drawObjects(pBackBuffer, guns, drawGun);
            drawObjects(pBackBuffer, projectiles, drawProjectile);
            drawObjects(pBackBuffer, monsters, drawMonster);
            drawEffects(pBackBuffer);  

            var time = (DateTime.Now - start).TotalMilliseconds;
            if (time >= (1000 / 24) / 2) 
            {
                Console.Error.WriteLine("Can't render in time. Rendering took {0}ms", time);
                //Debug.Assert(false);
            }
        }

        /// <summary>
        /// Draws effects filtering out old ones.
        /// </summary>
        /// <param name="pBackBuffer"></param>
        private void drawEffects(IntPtr pBackBuffer)
        {
            var currentEffect = effects.First;
            while (currentEffect != null)
            {
                if (currentEffect.Value.ageFrames > EffectFramesLength[(int)currentEffect.Value.type])
                {
                    var tmp = currentEffect.Next;
                    effects.Remove(currentEffect);
                    currentEffect = tmp;
                    continue;
                }
                currentEffect.Value.ageFrames += 1;

                switch (currentEffect.Value.type)
                {
                    case Effect.Type.Laser:
                        drawLaserRay(pBackBuffer, currentEffect.Value);
                        break;
                    case Effect.Type.Exploison:
                        drawExploison(pBackBuffer, currentEffect.Value);
                        break;
                    case Effect.Type.Vomit:
                        drawVomit(pBackBuffer, currentEffect.Value);
                        break;
                    default:
                        throw new NotImplementedException();
                }

                currentEffect = currentEffect.Next;
            }
        }

        private unsafe void drawVomit(IntPtr pBackBuffer, Effect effect)
        {
            var radius = Monster.VomitingRadius * radialScale;
            var r2 = radius * radius;
            var invSqrt2 = 1 / Math.Sqrt(2);
            var xmin = Math.Max(0, (int)(effect.position.X - radius));
            var xmax = Math.Min(width, (int)(effect.position.X + radius));
            var ymin = Math.Max(0, (int)(effect.position.Y - radius));
            var ymax = Math.Min(height, (int)(effect.position.Y + radius));

            var directionToCenter = (new Point(width / 2, height / 2) - effect.position);
            directionToCenter.Normalize();

            int color = (102 / (effect.ageFrames + 1)) << 8 | (102 / (effect.ageFrames + 1)) << 0;

            for (int x = xmin; x < xmax; ++x)
            {
                for (int y = ymin; y < ymax; ++y)
                {
                    var localX = x - xmin - radius;
                    var localY = y - ymin - radius;
                    if (localX * localX + localY * localY < r2) // within radius from monster position
                    {
                        var direction = new Point(x, y) - effect.position;
                        direction.Normalize();
                        if (directionToCenter * direction > invSqrt2) // equivalent to condition that angle difference is less than π/4
                        {
                            *((int*)(pBackBuffer + y * width * 4 + x * 4).ToPointer()) = color;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Draws explosion circle
        /// </summary>
        private unsafe void drawExploison(IntPtr pBackBuffer, Effect effect)
        {
            var radius = (Gun.ExplosionBaseRadius + Gun.ExplosionRadiusStep * effect.level) * radialScale;
            var r2 = radius*radius;
            var xmin = Math.Max(0, (int)(effect.position.X - radius));
            var xmax = Math.Min(width, (int)(effect.position.X + radius));
            var ymin = Math.Max(0, (int)(effect.position.Y - radius));
            var ymax = Math.Min(height, (int)(effect.position.Y + radius));
            int color = (64 + (255 - 64) / (effect.ageFrames + 1)) << 16;

            for (int x = xmin; x < xmax; ++x)
            {
                for (int y = ymin; y < ymax; ++y)
                {
                    var localX = x - xmin - radius;
                    var localY = y - ymin - radius;
                    if (localX * localX + localY * localY < r2)
                    {
                        *((int*)(pBackBuffer + y * width * 4 + x * 4).ToPointer()) = color;
                    }
                }
            }
        }

        private void drawLaserRay(IntPtr pBackBuffer, Effect effect)
        {
            var h = height - 1;
            var w = width - 1;
            var x0 = effect.position.X;
            var y0 = effect.position.Y;
            var dx = x0 - w/2;
            var dy = y0 - h/2;

            // Computing second lazer ray edge point
            var scale = Math.Min(Math.Min(Math.Abs(x0 / dx), Math.Abs(y0 / dy)), Math.Min(Math.Abs((w - x0)/dx), Math.Abs((h - y0)/dy)));
            var x1 = (int)(x0 + scale * dx);
            var y1 = (int)(y0 + scale * dy);

            Debug.Assert(x1 >= 0 && x1 < width);
            Debug.Assert(y1 >= 0 && y1 < height);

            var color = (255 / (effect.ageFrames + 1)) << 8;
            drawLine(pBackBuffer, (int)x0, (int)y0, x1, y1, color); // It's better to draw line with width depending on lazer ray radius,
                                                                    // but algorithms for such line are too damn huge
        }

        /// <summary>
        /// Bresenham line algorithm. (thanks to http://habrahabr.ru/post/248153/)
        /// </summary>
        void drawLine(IntPtr pBackBuffer, int x0, int y0, int x1, int y1, int color)
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
                    setPixel(pBackBuffer, y, x, color);
                }
                else
                {
                    setPixel(pBackBuffer, x, y, color);
                }
                error2 += derror2;

                if (error2 > dx)
                {
                    y += (y1 > y0 ? 1 : -1);
                    error2 -= dx * 2;
                }
            }
        }

        private unsafe void setPixel(IntPtr pBackBuffer, int x, int y, int color)
        {
            pBackBuffer += y * width * 4 + x * 4;
            *((int*)pBackBuffer.ToPointer()) = color;
        }

        private static void Swap<T>(ref T a, ref T b)
        {
            T t = a;
            a = b;
            b = t;
        }

        private void drawTower(IntPtr pBackBuffer)
        {
            int diameter = towerSpriteDiameter;
            var radius = diameter / 2;
            var centerX = width / 2;
            var centerY = height / 2;
            writePixels(pBackBuffer, towerSprite, diameter, (int)centerX - radius, (int)centerY - radius);
        }

        /// <summary>
        /// Common method for drawing every field object.
        /// </summary>
        private void drawObjects(IntPtr pBackBuffer, Dictionary<long, FieldObject> objects, Action<IntPtr, FieldObject> drawObject)
        {
            var deadObjects = new List<long>();
            foreach (var fieldObject in objects) 
            {
                if (fieldObject.Value.dead)
                {
                    if (fieldObject.Value.framesSinceDeath > DisplayCorpsesFramesCount)
                    {
                        deadObjects.Add(fieldObject.Key);
                        continue;
                    }
                    fieldObject.Value.framesSinceDeath += 1;
                }
                drawObject(pBackBuffer, fieldObject.Value);
            }

            foreach (var id in deadObjects)
            {
                objects.Remove(id);
            }
        }

        private void drawGun(IntPtr pBackBuffer, FieldObject gun)
        {
            int diameter = gunSpriteDiameter;
            var radius = gunSpriteDiameter / 2;
            if (gun.dead)
            {
                writePixels(pBackBuffer, deadGunSprite, diameter,
                    (int)gun.position.X - radius, (int)gun.position.Y - radius);
            }
            else
            {
                writePixels(pBackBuffer, gunSprites[gun.type, gun.level], diameter,
                    (int)gun.position.X - radius, (int)gun.position.Y - radius);
            }
        }

        private void drawProjectile(IntPtr pBackBuffer, FieldObject projectile)
        {
            int diameter = projectileSpriteDiameters[projectile.level];
            var radius = diameter / 2;
            var x = (int)projectile.position.X - radius; 
            var y = (int)projectile.position.Y - radius;
            if (projectile.dead)
            {
                throw new InvalidOperationException();
            }
            else
            {
                writePixels(pBackBuffer, projectileSprites[projectile.level], diameter, x, y);
            }
        }

        private void drawMonster(IntPtr pBackBuffer, FieldObject monster)
        {
            int diameter = monsterSpriteDiameters[monster.type];
            var radius = diameter / 2;
            var x = (int)monster.position.X - radius;
            var y = (int)monster.position.Y - radius;
            if (monster.dead) 
            {
                writePixels(pBackBuffer, deadMonsterSprites[monster.type], diameter, x, y);
            }
            else
            {
                writePixels(pBackBuffer, monsterSprites[monster.type], diameter, x, y);
            }
        }

        /// <summary>
        /// Writes pixels from sprite on bitmap omitting ones that have background color.
        /// </summary>
        private unsafe void writePixels(IntPtr pBackBuffer, Int32[] sprite, int diameter, int x, int y)
        {
            var xmin = Math.Max(0, x);
            var xmax = Math.Min(width, x + diameter);

            var ymin = Math.Max(0, y);
            var ymax = Math.Min(height, y + diameter);

            for (x = xmin; x < xmax; ++x)
            {
                for (y = ymin; y < ymax; ++y)
                {
                    var color = sprite[(y - ymin)*diameter + (x - xmin)];
                    if (color == backgroundColor) continue;
                    *((int*)(pBackBuffer + y * width * 4 + x * 4).ToPointer()) = color;
                }
            }
        }

        /// <summary>
        /// Clears bitmap with background color
        /// </summary>
        private unsafe void clear(IntPtr pBackBuffer)
        {
            for (int i = 0; i < width * height; ++i)
            {
                *((int*)pBackBuffer.ToPointer()) = backgroundColor;
                pBackBuffer += 4;
            }

        }

    }
}
