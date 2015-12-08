using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace MinimalTowerDefence
{
    /// <summary>
    /// Handles the game logic processes. All computations are in polar coordinates.
    /// </summary>
    partial class GameLogic
    {
        /// <summary>
        /// Logical tower radius.
        /// </summary>
        public static readonly double TowerRadius = 7;

        /// <summary>
        /// Representes messeges sended by other threads to game logic thread.
        /// </summary>
        public struct Message
        {
            public enum Type
            {
                ContinueSimulation,
                NewGun,
            }

            public Type type { get; private set; }

            public Gun.Type GunType { get; private set; }
            public int GunLevel { get; private set; }
            public PolarCoordinates GunPosition { get; private set; }

            public static Message ContinueSimulation()
            {
                return new Message() { type = Type.ContinueSimulation };
            }

            public static Message NewGun(Gun.Type newGunType, int newGunLevel, PolarCoordinates newGunPosition)
            {
                return new Message() { type = Type.NewGun, GunType = newGunType, GunLevel = newGunLevel, GunPosition = newGunPosition };
            }
        }

        public BlockingCollection<Message> MessageBox { get; private set; }

        /// <summary>
        /// Holds any game object and number of processing steps it should skip.
        /// </summary>
        /// <typeparam name="T">Monster, Gun or Projectile</typeparam>
        private class GameObject<T>
        {
            /// <summary>
            /// Monster, Gun or Projectile.
            /// </summary>
            public T gameObject { get; set; }
            /// <summary>
            /// Number of processing steps object should skip.
            /// </summary>
            public int skipStepsCount { get; set; }

            public GameObject(T gameObject) 
            {
                this.gameObject = gameObject;
            }
        }
        private static GameObject<T> makeGameObject<T>(T gameObject)
        {
            return new GameObject<T>(gameObject);
        }

        private Dictionary<long, GameObject<Monster>> monsters = new Dictionary<long, GameObject<Monster>>();
        private Dictionary<long, GameObject<Gun>> guns = new Dictionary<long, GameObject<Gun>>();
        private Dictionary<long, GameObject<Projectile>> projectiles = new Dictionary<long, GameObject<Projectile>>();

        private Random rand = new Random();
        /// <summary>
        /// Life of tower.
        /// </summary>
        private double towerLifu = 1000;
        /// <summary>
        /// Player account value.
        /// </summary>
        private long playerMoney = 1000;
        /// <summary>
        /// Number of currently alive monsters.
        /// </summary>
        private int monstersAlive = 0;

        #region ID counters for game objects
        private long nextProjectileID = 0;
        private long nextMonsterID = 0;
        private long nextGunID = 0;
        #endregion

        private GameField gameFieldWindow;
        private Renderer renderer;

        public GameLogic(GameField gameFieldWindow, Renderer renderer)
        {
            this.MessageBox = new BlockingCollection<Message>();
            this.gameFieldWindow = gameFieldWindow;
            this.renderer = renderer;
        }

        /// <summary>
        /// Game logic thread entry point.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        internal void run(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            towerLifuChanged();
            for (int i = 1; i <= 5; ++i)
            {
                var previousWaveEnd = DateTime.Now;
                bool waveStarted = false;
                while (!waveStarted || monstersAlive > 0)
                {
                    var newEvent = MessageBox.Take();
                    switch (newEvent.type)
                    {
                        case Message.Type.ContinueSimulation:
                            playerMoney += 10;
                            if (!waveStarted && (DateTime.Now - previousWaveEnd).TotalMilliseconds > 10000)
                            {
                                addWaveMonsters(i);
                                waveStarted = true;
                            }
                            runWave(i);
                            playerMoneyChanged();
                            break;
                        case Message.Type.NewGun:
                            var price = Gun.price(newEvent.GunType, newEvent.GunLevel);
                            if (price <= playerMoney)
                            {
                                playerMoney -= price;
                                addGun(newEvent);
                                playerMoneyChanged();
                            }
                            break;
                        default:
                            throw new InvalidOperationException();
                    }

                    if (towerLifu == 0)
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action<bool>(gameFieldWindow.gameOver), false);
                        return;
                    }
                }
            }

            Application.Current.Dispatcher.BeginInvoke(new Action<bool>(gameFieldWindow.gameOver), true);
        }

        private void addGun(Message newEvent)
        {
            var gun = new Gun(nextGunID++, newEvent.GunType, newEvent.GunLevel, newEvent.GunPosition, 80 + 20 * newEvent.GunLevel);
            newGun(gun);
            guns.Add(gun.ID, makeGameObject(gun));
        }
        
        private void addWaveMonsters(int wave)
        {
            for (int i = 0; i < 100 * wave; ++i)
            {
                // New monster types appear with new waves (3 - number of monster types, 5 - number of waves).
                var type = rand.Next(0, (int)Math.Round((double)wave * 3.0 / 5.0));
                var monster = new Monster(nextMonsterID++, (Monster.Type)type,
                    new PolarCoordinates(100 + rand.NextDouble() * 20 * wave, rand.NextDouble() * 2 * Math.PI),
                    100 + 20 * type);
                newMonster(monster);
                monsters.Add(monster.ID, makeGameObject(monster));
            }
            monstersAlive = monsters.Count;
        }

        private void runWave(int wave)
        {
            var start = DateTime.Now;

            updateGuns();
            updateProjectiles();
            updateMonster();

            var time = (DateTime.Now - start).TotalMilliseconds;
            if (time >= (1000 / 24) / 2)
            {
                Console.Error.WriteLine("Can't simulate in time. Simulation took {0}ms", time);
            }
        }

        private void updateGuns()
        {
            updateObjects(guns, () => true, fireGun);
        }

        private void updateProjectiles()
        {
            updateObjects(projectiles, () => true, moveProjectile);
        }

        private void updateMonster()
        {
            updateObjects(monsters, () => towerLifu > 0, moveMonster);
        }

        private delegate bool ObjectUpdater<T>(T gameObject, ref int skipStepsCount);

        /// <summary>
        /// Generic method for updating game objects. Updates skip steps counts and removes dead objects.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="objects"></param>
        /// <param name="predicate">condition whether to continue update</param>
        /// <param name="updateObject">method that handles update of single object</param>
        private void updateObjects<T>(Dictionary<long, GameObject<T>> objects, Func<bool> predicate, ObjectUpdater<T> updateObject)
        {
            var deadObjects = new List<long>();
            foreach (var fieldObject in objects)
            {
                if (fieldObject.Value.skipStepsCount > 0)
                {
                    fieldObject.Value.skipStepsCount -= 1;
                    continue;
                }

                int newSkipStepsCount = 0;

                if (!updateObject(fieldObject.Value.gameObject, ref newSkipStepsCount))
                    deadObjects.Add(fieldObject.Key);

                fieldObject.Value.skipStepsCount = newSkipStepsCount;

                if (!predicate()) break;
            }

            foreach (var id in deadObjects)
            {
                objects.Remove(id);
            }
        }
        
        /// <summary>
        /// Moves projectile along radius, checks for collisions with monsters.
        /// </summary>
        /// <returns>
        /// true if projectile still active,
        /// false if it have hit something or got out of view
        /// </returns>
        private bool moveProjectile(Projectile projectile, ref int newSkipStepsCount)
        {
            newSkipStepsCount = 0;

            var radialSpeed = Projectile.BaseRadialSpeed + projectile.Level;
            var r0 = projectile.Position.R;
            var r1 = r0 + radialSpeed;

            var radius = Projectile.BaseRadius + Projectile.LevelRadiusStep * projectile.Level;

            foreach (var monsterAndDelay in monsters.Values)
            {
                var monster = monsterAndDelay.gameObject;
                if (monster.Life == 0) continue;
                if (monster.Position.R < projectile.Position.R) continue;
                if (monster.Position.R - Monster.Radius[(int)monster.type] > r1 + radius) continue;

                var cos = Math.Cos(monster.Position.φ - projectile.Position.φ);

                var t = (monster.Position.R * cos - r0) / (r1 - r0);
                t = Math.Max(0, Math.Min(1, t)); // interpolation parameter between previous projectile position
                                                 // and new one, where distance to monster is minimal

                var r1t = (r0*(1 - t) + r1*t);
                var r1t_2 = r1t*r1t;

                var r2 = monster.Position.R;
                var r2_2 = r2*r2;
                var d = Math.Sqrt(r1t_2 + r2_2 - 2 * r1t * r2 * cos); // computing distance by hand because already have computed Cos
                if (d < radius + Monster.Radius[(int)monster.type]) // if actual distance suppose hit
                {
                    monster.Life = Math.Max(0, monster.Life - Projectile.BasePower - 10 * projectile.Level);
                    if (monster.Life == 0)
                    {
                        monsterDied(monster.ID, monster.type, true);
                    }

                    projectileHit(projectile.ID);
                    return false;
                }
            }

            // Moving projectile along radius
            projectile.Position.R = r1;
            projectileMoved(projectile.ID, projectile.Position);

            if (r1 < Renderer.MaxVisibleLogicRadius * 1.2) // if projectile is still visible on (or not far from) screen
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Fires gun depending on it's type.
        /// </summary>
        /// <returns>true if gun still alive</returns>
        private bool fireGun(Gun gun, ref int newSkipStepsCount)
        {
            if (gun.Life == 0) return false;

            switch (gun.type)
            {
                case Gun.Type.Machine:
                    newSkipStepsCount = fireMachineGun(gun);
                    break;
                case Gun.Type.Lazer:
                    newSkipStepsCount = fireLaserGun(gun);
                    break;
                case Gun.Type.Mine:
                    break;
                default:
                    throw new InvalidOperationException();
            }

            return true;
        }

        /// <summary>
        /// Fires laser gun. Searches for laser ray intersections with monsters.
        /// </summary>
        /// <returns>Number of iterations till next volley</returns>
        private int fireLaserGun(Gun gun)
        {
            laserFired(gun.ID);
            var lazerRayWidth = Gun.BaseLaserRayWidth + gun.Level * Gun.LaserRayWidthStep;
            foreach (var monsterObject in monsters.Values)
            {
                var monster = monsterObject.gameObject;
                if (monster.Life == 0) continue;
                if (monster.Position.R <= gun.Position.R) continue;

                var minφ = Math.Min(monster.Position.φ, gun.Position.φ);
                var maxφ = Math.Max(monster.Position.φ, gun.Position.φ);
                var dφ = Math.Min(maxφ - minφ, minφ + 2 * Math.PI - maxφ);

                if (dφ > Math.PI / 4) continue; // sanity check)

                var distanceToRay = monster.Position.R * Math.Sin(dφ);
                if (distanceToRay <= lazerRayWidth)
                {
                    monster.Life = Math.Max(0, monster.Life - Gun.BaseLaserPower - 5 * gun.Level);
                    if (monster.Life == 0)
                    {
                        monsterDied(monster.ID, monster.type, true);
                    }
                }
            }

            return (Gun.NumLevels - gun.Level)*7;
        }

        /// <summary>
        /// Fires machine gun - creates new projectile.
        /// </summary>
        /// <returns>Number of iterations till next volley</returns>
        private int fireMachineGun(Gun gun)
        {
            var projectile = new Projectile(nextProjectileID++, gun.Level, new PolarCoordinates(gun.Position.R + 0.1, gun.Position.φ));
            newProjectile(projectile);
            projectiles.Add(projectile.ID, makeGameObject(projectile));

            return (Gun.NumLevels - gun.Level)*5;
        }

        /// <summary>
        /// Blows mine. Searches for affected monsters.
        /// </summary>
        private void blowMine(Gun mine)
        {
            mineBlown(mine.ID);
            var explosionRadius = Gun.ExplosionBaseRadius + Gun.ExplosionRadiusStep * mine.Level;
            foreach (var monsterObject in monsters.Values)
            {
                var monster = monsterObject.gameObject;
                if (monster.Life == 0) continue;
                if (Math.Abs(monster.Position.R - mine.Position.R) > explosionRadius + Monster.Radius[(int)monster.type]) continue;

                var distance = PolarCoordinates.PolarDistance(monster.Position, mine.Position);
                distance = Math.Min(explosionRadius, Math.Max(0, distance - Gun.Radius - Monster.Radius[(int)monster.type]));

                monster.Life = Math.Max(0, monster.Life - (1.0 - distance / explosionRadius) * (mine.Level + 1) * Gun.ExplosionPower);
                if (monster.Life == 0)
                {
                    monsterDied(monster.ID, monster.type, true);
                }
            }
            mine.Life = 0;
        }

        /// <summary>
        /// Moves monster around. Searches for interactions.
        /// </summary>
        /// <returns>true if monster still alive</returns>
        private bool moveMonster(Monster monster, ref int newSkipStepsCount)
        {
            if (monster.Life == 0) return false;

            if (monster.Position.R - TowerRadius < 0.5) // if monster reaches tower, it dies taking part of tower life
            {
                towerLifu = Math.Max(0, towerLifu - monster.Life / 10);
                towerLifuChanged();
                monster.Life = 0;
                monsterDied(monster.ID, monster.type, false);
                return false;
            }

            var vomitedGuns = new List<Gun>();
            Lazy<double> deltaPhi = new Lazy<double>(() => // max difference between angles when gun can still be affected by vomiting monster
            {
                return Math.Atan(Monster.VomitingRadiusOverSqrt2 / (monster.Position.R - Monster.VomitingRadiusOverSqrt2));
            });
            foreach (var gunObject in guns.Values)
            {
                var gun = gunObject.gameObject;
                if (gun.Life == 0) continue;
                
                if (PolarCoordinates.PolarDistanceLessThan(monster.Position, gun.Position, Gun.Radius * 1.1))
                {
                    if (gun.type == Gun.Type.Mine)
                    {
                        blowMine(gun);
                        if (monster.Life > 0)
                        {
                            // if monster survived the blast, it stops for some time
                            newSkipStepsCount = (gun.Level + 1) * 7;
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else if (monster.type != Monster.Type.Vomiting)
                    {
                        gun.Life = Math.Max(0, gun.Life - Monster.Power[(int)monster.type]);
                        if (gun.Life == 0)
                        {
                            gunDied(gun.ID);
                        }
                    }
                }

                var minφ = Math.Min(monster.Position.φ, gun.Position.φ);
                var maxφ = Math.Max(monster.Position.φ, gun.Position.φ);
                var dφ = Math.Min(maxφ - minφ, minφ + 2 * Math.PI - maxφ);
                if (monster.type == Monster.Type.Vomiting
                    && dφ <= deltaPhi.Value
                    && PolarCoordinates.PolarDistanceLessThan(monster.Position, gun.Position, Monster.VomitingRadius))
                {
                    if (gun.Position.R <= monster.Position.R / Math.Cos(dφ) / (1 + Math.Atan(dφ))) // if gun fall into vomiting area 
                                                                                                   // (π/4 to left and right of direction from monster to tower)
                    {
                        vomitedGuns.Add(gun);
                    }
                }
            }

            if (vomitedGuns.Count > 0)
            {
                vomit(monster.ID);
                foreach (var gun in vomitedGuns)
                {
                    gun.Life = Math.Max(0, gun.Life - 10);
                    if (gun.Life == 0)
                    {
                        gunDied(gun.ID);
                    }
                }
                newSkipStepsCount = 6; // When monster vomits, it waits some time
                return true;
            }
            else
            {
                var dr = Monster.Speed[(int)monster.type] * (1 - rand.NextDouble() * 0.01);
                var r0 = monster.Position.R;
                var r0_2 = r0*r0;
                var r1 = monster.Position.R - dr;
                var r1_2 = r1*r1;
                var R_2 = Monster.Speed[(int)monster.type]*Monster.Speed[(int)monster.type];
                var dφ = (1 - 2 * (rand.Next() % 2)) * Math.Sqrt(1 - (r0_2 + r1_2 - R_2) / (2 * r0 * r1));

                monster.Position.R = r1;
                monster.Position.φ += dφ;
                if (monster.Position.φ > Math.PI * 2) monster.Position.φ -= Math.PI * 2;
                monsterMoved(monster.ID, monster.Position);
            }

            newSkipStepsCount = 0;
            return true;
        }

    }


}
