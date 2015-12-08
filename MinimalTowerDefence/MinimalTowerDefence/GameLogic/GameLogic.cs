using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Windows;

namespace MinimalTowerDefence
{
    /// <summary>
    /// Handles the game logic processes. All computations are in polar coordinates.
    /// </summary>
    internal partial class GameLogic
    {
        /// <summary>
        /// Logical tower radius.
        /// </summary>
        public static readonly double TowerRadius = 7;

        /// <summary>
        /// Maximal logic(!) radius that will be visible on screen.
        /// </summary>
        public static readonly double MaxVisibleLogicRadius = 100.0;


        public BlockingCollection<Message> MessageBox { get; private set; }

        /// <summary>
        /// Represents messages sent by other threads to game logic thread.
        /// </summary>
        public struct Message
        {
            public enum Type
            {
                ContinueSimulation,
                NewGun,
            }

            public Type MessageType { get; private set; }

            public Gun.Type GunType { get; private set; }

            public int GunLevel { get; private set; }

            public PolarCoordinates GunPosition { get; private set; }

            public static Message ContinueSimulation()
            {
                return new Message() { MessageType = Type.ContinueSimulation };
            }

            public static Message NewGun(Gun.Type newGunType, int newGunLevel, PolarCoordinates newGunPosition)
            {
                return new Message() { MessageType = Type.NewGun, GunType = newGunType, GunLevel = newGunLevel, GunPosition = newGunPosition };
            }
        }

        /// <summary>
        /// Holds any game object and number of processing steps it should skip.
        /// </summary>
        /// <typeparam name="T">Monster, Gun or Projectile</typeparam>
        private class GameObject<T>
        {
            public GameObject(T gameObject)
            {
                this.InnerGameObject = gameObject;
            }

            /// <summary>
            /// Gets underlying Monster, Gun or Projectile.
            /// </summary>
            public T InnerGameObject { get; private set; }

            /// <summary>
            /// Gets or sets number of processing steps object should skip.
            /// </summary>
            public int SkipStepsCount { get; set; }
        }

        private static GameObject<T> MakeGameObject<T>(T gameObject)
        {
            return new GameObject<T>(gameObject);
        }

        private Dictionary<long, GameObject<Monster>> _monsters = new Dictionary<long, GameObject<Monster>>();
        private Dictionary<long, GameObject<Gun>> _guns = new Dictionary<long, GameObject<Gun>>();
        private Dictionary<long, GameObject<Projectile>> _projectiles = new Dictionary<long, GameObject<Projectile>>();

        private Random _rand = new Random();

        /// <summary>
        /// Life of tower.
        /// </summary>
        private double _towerLifu = 1000;

        /// <summary>
        /// Player account value.
        /// </summary>
        private long _playerMoney = 1000;

        /// <summary>
        /// Number of currently alive monsters.
        /// </summary>
        private int _monstersAlive = 0;

        #region ID counters for game objects

        private long _nextProjectileID = 0;
        private long _nextMonsterID = 0;
        private long _nextGunID = 0;

        #endregion ID counters for game objects

        private GameField _gameFieldWindow;
        private Renderer _renderer;

        public GameLogic(GameField gameFieldWindow, Renderer renderer)
        {
            this.MessageBox = new BlockingCollection<Message>();
            this._gameFieldWindow = gameFieldWindow;
            this._renderer = renderer;
        }

        /// <summary>
        /// Game logic thread entry point.
        /// </summary>
        internal void Run(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            TowerLifuChanged();
            for (int i = 1; i <= 5; ++i)
            {
                var previousWaveEnd = DateTime.Now;
                bool waveStarted = false;
                while (!waveStarted || _monstersAlive > 0)
                {
                    var newEvent = MessageBox.Take();
                    switch (newEvent.MessageType)
                    {
                        case Message.Type.ContinueSimulation:
                            _playerMoney += 10;
                            if (!waveStarted && (DateTime.Now - previousWaveEnd).TotalMilliseconds > 10000)
                            {
                                AddWaveMonsters(i);
                                waveStarted = true;
                            }

                            RunWave(i);
                            PlayerMoneyChanged();
                            break;

                        case Message.Type.NewGun:
                            var price = Gun.Price(newEvent.GunType, newEvent.GunLevel);
                            if (price <= _playerMoney)
                            {
                                _playerMoney -= price;
                                AddGun(newEvent);
                                PlayerMoneyChanged();
                            }

                            break;

                        default:
                            throw new InvalidOperationException();
                    }

                    if (this._towerLifu == 0)
                    {
                        Application.Current.Dispatcher.BeginInvoke(new Action<bool>(_gameFieldWindow.GameOver), false);
                        return;
                    }
                }
            }

            Application.Current.Dispatcher.BeginInvoke(new Action<bool>(_gameFieldWindow.GameOver), true);
        }

        private void AddGun(Message newEvent)
        {
            var gun = new Gun(_nextGunID++, newEvent.GunType, newEvent.GunLevel, newEvent.GunPosition, 80 + 20 * newEvent.GunLevel);
            NewGun(gun);
            _guns.Add(gun.ID, GameLogic.MakeGameObject(gun));
        }

        private void AddWaveMonsters(int wave)
        {
            for (int i = 0; i < 100 * wave; ++i)
            {
                // New monster types appear with new waves (3 - number of monster types, 5 - number of waves).
                var type = _rand.Next(0, (int)Math.Round((double)wave * 3.0 / 5.0));
                var monster = new Monster(_nextMonsterID++, (Monster.Type)type,
                    new PolarCoordinates(100 + _rand.NextDouble() * 20 * wave, _rand.NextDouble() * 2 * Math.PI),
                    100 + 20 * type);
                NewMonster(monster);
                _monsters.Add(monster.ID, GameLogic.MakeGameObject(monster));
            }

            _monstersAlive = _monsters.Count;
        }

        private void RunWave(int wave)
        {
            var start = DateTime.Now;

            UpdateGuns();
            UpdateProjectiles();
            UpdateMonster();

            var time = (DateTime.Now - start).TotalMilliseconds;
            if (time >= (1000 / 24) / 2)
            {
                Console.Error.WriteLine("Can't simulate in time. Simulation took {0}ms", time);
            }
        }

        private void UpdateGuns()
        {
            UpdateObjects(_guns, () => true, new ObjectUpdater<Gun>(ObjectUpdaterFireGun));
        }

        private void UpdateProjectiles()
        {
            UpdateObjects(_projectiles, () => true, new ObjectUpdater<Projectile>(ObjectUpdaterMoveProjectile));
        }

        private void UpdateMonster()
        {
            UpdateObjects(_monsters, () => _towerLifu > 0, new ObjectUpdater<Monster>(ObjectUpdaterMoveMonster));
        }

        private delegate bool ObjectUpdater<T>(T gameObject, ref int skipStepsCount);

        /// <summary>
        /// Generic method for updating game objects. Updates skip steps counts and removes dead objects.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="objects"></param>
        /// <param name="predicate">condition whether to continue update</param>
        /// <param name="updateObject">method that handles update of single object</param>
        private void UpdateObjects<T>(Dictionary<long, GameObject<T>> objects, Func<bool> predicate, ObjectUpdater<T> updateObject)
        {
            var deadObjects = new List<long>();
            foreach (var fieldObject in objects)
            {
                if (fieldObject.Value.SkipStepsCount > 0)
                {
                    fieldObject.Value.SkipStepsCount -= 1;
                    continue;
                }

                int newSkipStepsCount = 0;

                if (!updateObject(fieldObject.Value.InnerGameObject, ref newSkipStepsCount))
                {
                    deadObjects.Add(fieldObject.Key);
                }

                fieldObject.Value.SkipStepsCount = newSkipStepsCount;

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
        private bool ObjectUpdaterMoveProjectile(Projectile projectile, ref int newSkipStepsCount)
        {
            newSkipStepsCount = 0;

            var radialSpeed = Projectile.BaseRadialSpeed + projectile.Level;
            var r0 = projectile.Position.R;
            var r1 = r0 + radialSpeed;

            var radius = Projectile.BaseRadius + Projectile.LevelRadiusStep * projectile.Level;

            foreach (var monsterAndDelay in _monsters.Values)
            {
                var monster = monsterAndDelay.InnerGameObject;
                if (monster.Life == 0) continue;
                if (monster.Position.R < projectile.Position.R) continue;
                if (monster.Position.R - Monster.Radius[(int)monster.MonsterType] > r1 + radius) continue;

                var cos = Math.Cos(monster.Position.φ - projectile.Position.φ);

                var t = (monster.Position.R * cos - r0) / (r1 - r0);

                // Interpolation parameter between previous projectile position
                // and new one, where distance to monster is minimal.
                t = Math.Max(0, Math.Min(1, t));

                var r1t = (r0 * (1 - t) + r1 * t);
                var r1t_2 = r1t * r1t;

                var r2 = monster.Position.R;
                var r2_2 = r2 * r2;

                // Computing distance by hand because already have computed Cos.
                var d = Math.Sqrt(r1t_2 + r2_2 - 2 * r1t * r2 * cos);

                // If actual distance suppose hit.
                if (d < radius + Monster.Radius[(int)monster.MonsterType])
                {
                    monster.Life = Math.Max(0, monster.Life - Projectile.BasePower - 10 * projectile.Level);
                    if (monster.Life == 0)
                    {
                        MonsterDied(monster.ID, monster.MonsterType, true);
                    }

                    ProjectileHit(projectile.ID);
                    return false;
                }
            }

            // Moving projectile along radius.
            projectile.Position = new PolarCoordinates(r1, projectile.Position.φ);
            ProjectileMoved(projectile.ID, projectile.Position);

            // If projectile is still visible on (or not far from) screen.
            if (r1 < MaxVisibleLogicRadius * 1.2)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Fires gun depending on it's type.
        /// </summary>
        /// <returns>true if gun still alive</returns>
        private bool ObjectUpdaterFireGun(Gun gun, ref int newSkipStepsCount)
        {
            if (gun.Life == 0) return false;

            switch (gun.GunType)
            {
                case Gun.Type.Machine:
                    newSkipStepsCount = FireMachineGun(gun);
                    break;

                case Gun.Type.Lazer:
                    newSkipStepsCount = FireLaserGun(gun);
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
        private int FireLaserGun(Gun gun)
        {
            LaserFired(gun.ID);
            var lazerRayWidth = Gun.BaseLaserRayWidth + gun.Level * Gun.LaserRayWidthStep;
            foreach (var monsterObject in _monsters.Values)
            {
                var monster = monsterObject.InnerGameObject;
                if (monster.Life == 0) continue;
                if (monster.Position.R <= gun.Position.R) continue;

                var minφ = Math.Min(monster.Position.φ, gun.Position.φ);
                var maxφ = Math.Max(monster.Position.φ, gun.Position.φ);
                var dφ = Math.Min(maxφ - minφ, minφ + 2 * Math.PI - maxφ);

                // Sanity check.
                if (dφ > Math.PI / 4) continue;

                var distanceToRay = monster.Position.R * Math.Sin(dφ);
                if (distanceToRay <= lazerRayWidth)
                {
                    monster.Life = Math.Max(0, monster.Life - Gun.BaseLaserPower - 5 * gun.Level);
                    if (monster.Life == 0)
                    {
                        MonsterDied(monster.ID, monster.MonsterType, true);
                    }
                }
            }

            return (Gun.NumLevels - gun.Level) * 7;
        }

        /// <summary>
        /// Fires machine gun - creates new projectile.
        /// </summary>
        /// <returns>Number of iterations till next volley</returns>
        private int FireMachineGun(Gun gun)
        {
            var projectile = new Projectile(_nextProjectileID++, gun.Level, new PolarCoordinates(gun.Position.R + 0.1, gun.Position.φ));
            NewProjectile(projectile);
            _projectiles.Add(projectile.ID, GameLogic.MakeGameObject(projectile));

            return (Gun.NumLevels - gun.Level) * 5;
        }

        /// <summary>
        /// Blows mine. Searches for affected monsters.
        /// </summary>
        private void BlowMine(Gun mine)
        {
            MineBlown(mine.ID);
            var explosionRadius = Gun.ExplosionBaseRadius + Gun.ExplosionRadiusStep * mine.Level;
            foreach (var monsterObject in _monsters.Values)
            {
                var monster = monsterObject.InnerGameObject;
                if (monster.Life == 0) continue;
                if (Math.Abs(monster.Position.R - mine.Position.R) > explosionRadius + Monster.Radius[(int)monster.MonsterType]) continue;

                var distance = PolarCoordinates.PolarDistance(monster.Position, mine.Position);
                distance = Math.Min(explosionRadius, Math.Max(0, distance - Gun.Radius - Monster.Radius[(int)monster.MonsterType]));

                monster.Life = Math.Max(0, monster.Life - (1.0 - distance / explosionRadius) * (mine.Level + 1) * Gun.ExplosionPower);
                if (monster.Life == 0)
                {
                    MonsterDied(monster.ID, monster.MonsterType, true);
                }
            }

            mine.Life = 0;
        }

        /// <summary>
        /// Moves monster around. Searches for interactions.
        /// </summary>
        /// <returns>true if monster still alive</returns>
        private bool ObjectUpdaterMoveMonster(Monster monster, ref int newSkipStepsCount)
        {
            if (monster.Life == 0) return false;

            // If monster reaches tower, it dies taking part of tower life.
            if (monster.Position.R - TowerRadius < 0.5)
            {
                _towerLifu = Math.Max(0, _towerLifu - monster.Life / 10);
                TowerLifuChanged();
                monster.Life = 0;
                MonsterDied(monster.ID, monster.MonsterType, false);
                return false;
            }

            var vomitedGuns = new List<Gun>();

            // Max difference between angles when gun can still be affected by vomiting monster.
            Lazy<double> deltaPhi = new Lazy<double>(() =>
            {
                return Math.Atan(Monster.VomitingRadiusOverSqrt2 / (monster.Position.R - Monster.VomitingRadiusOverSqrt2));
            });

            foreach (var gunObject in _guns.Values)
            {
                var gun = gunObject.InnerGameObject;
                if (gun.Life == 0) continue;

                if (PolarCoordinates.PolarDistanceLessThan(monster.Position, gun.Position, Gun.Radius * 1.1))
                {
                    if (gun.GunType == Gun.Type.Mine)
                    {
                        BlowMine(gun);
                        if (monster.Life > 0)
                        {
                            // If monster survived the blast, it stops for some time.
                            newSkipStepsCount = (gun.Level + 1) * 7;
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else if (monster.MonsterType != Monster.Type.Vomiting)
                    {
                        gun.Life = Math.Max(0, gun.Life - Monster.Power[(int)monster.MonsterType]);
                        if (gun.Life == 0)
                        {
                            GunDied(gun.ID);
                        }
                    }
                }

                var minφ = Math.Min(monster.Position.φ, gun.Position.φ);
                var maxφ = Math.Max(monster.Position.φ, gun.Position.φ);
                var dφ = Math.Min(maxφ - minφ, minφ + 2 * Math.PI - maxφ);
                if (monster.MonsterType == Monster.Type.Vomiting
                    && dφ <= deltaPhi.Value
                    && PolarCoordinates.PolarDistanceLessThan(monster.Position, gun.Position, Monster.VomitingRadius))
                {
                    // If gun fall into vomiting area
                    // (π/4 to left and right of direction from monster to tower).
                    if (gun.Position.R <= monster.Position.R / Math.Cos(dφ) / (1 + Math.Atan(dφ)))
                    {
                        vomitedGuns.Add(gun);
                    }
                }
            }

            if (vomitedGuns.Count > 0)
            {
                Vomit(monster.ID);
                foreach (var gun in vomitedGuns)
                {
                    gun.Life = Math.Max(0, gun.Life - 10);
                    if (gun.Life == 0)
                    {
                        GunDied(gun.ID);
                    }
                }

                // When monster vomits, it waits some time.
                newSkipStepsCount = 6;

                return true;
            }
            else
            {
                var dr = Monster.Speed[(int)monster.MonsterType] * (1 - _rand.NextDouble() * 0.01);
                var r0 = monster.Position.R;
                var r0_2 = r0 * r0;
                var r1 = monster.Position.R - dr;
                var r1_2 = r1 * r1;
                var R_2 = Monster.Speed[(int)monster.MonsterType] * Monster.Speed[(int)monster.MonsterType];
                var dφ = (1 - 2 * (_rand.Next() % 2)) * Math.Sqrt(1 - (r0_2 + r1_2 - R_2) / (2 * r0 * r1));

                monster.Position = new PolarCoordinates(r1, monster.Position.φ + dφ);
                MonsterMoved(monster.ID, monster.Position);
            }

            newSkipStepsCount = 0;
            return true;
        }
    }
}