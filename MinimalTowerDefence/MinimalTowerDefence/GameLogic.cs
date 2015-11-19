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
    /// Handles the game logic processes.
    /// </summary>
    class GameLogic
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
            public int GunLevel  { get; private set; }
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
        /// Represents logical update that should be made on game field.
        /// </summary>
        private class UpdateEvent
        {
            public enum Type
            {
                MonsterMove,
                ProjectileMove,
                GunFire
            }

            public Type type;
            /// <summary>
            /// Monster or gun index in corresponding List.
            /// </summary>
            public int index;
            /// <summary>
            /// Projectile that should be moved, if this is a ProjectileMove event.
            /// </summary>
            public Projectile projectile;
            /// <summary>
            /// Number of iterations this event should be postponed on.
            /// </summary>
            public int skipStepsCount;
        }

        /// <summary>
        /// List of all monsters of current wave.
        /// </summary>
        private List<Monster> monsters = new List<Monster>();
        /// <summary>
        /// List of all guns.
        /// </summary>
        private List<Gun> guns = new List<Gun>();
        private Random rand = new Random();
        /// <summary>
        /// Queue for game field updates.
        /// </summary>
        private LinkedList<UpdateEvent> updateQueue = new LinkedList<UpdateEvent>();
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

                cleanupAfterWave();
            }

            Application.Current.Dispatcher.BeginInvoke(new Action<bool>(gameFieldWindow.gameOver), true);
        }

        private void addGun(Message newEvent)
        {
            var gun = new Gun(nextGunID++, newEvent.GunType, newEvent.GunLevel, newEvent.GunPosition, 80 + 20 * newEvent.GunLevel);
            newGun(gun);
            guns.Add(gun);
            if (gun.type != Gun.Type.Mine)
                updateQueue.AddLast(new UpdateEvent() { type = UpdateEvent.Type.GunFire, index = guns.Count - 1 });
        }

        /// <summary>
        /// Removes dead monsters and guns from lists when wave wave is ended.
        /// </summary>
        private void cleanupAfterWave()
        {   
            monsters.Clear();

            var currentUpdateEvent = updateQueue.First;
            while (currentUpdateEvent != null)
            {
                if (currentUpdateEvent.Value.type == UpdateEvent.Type.GunFire
                    || currentUpdateEvent.Value.type == UpdateEvent.Type.MonsterMove)
                {
                    var tmp = currentUpdateEvent.Next;
                    updateQueue.Remove(currentUpdateEvent);
                    currentUpdateEvent = tmp;
                    continue;
                }
                else
                {
                    currentUpdateEvent = currentUpdateEvent.Next;
                }
            }


            var newGuns = new List<Gun>();
            foreach (var gun in guns)
            {
                if (gun.Life > 0 && gun.type != Gun.Type.Mine)
                {
                    newGuns.Add(gun);
                    updateQueue.AddLast(new UpdateEvent() { index = newGuns.Count - 1, type = UpdateEvent.Type.GunFire });
                }
            }
            guns = newGuns;
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
                monsters.Add(monster);
                updateQueue.AddLast(new UpdateEvent() { type = UpdateEvent.Type.MonsterMove, index = i });
            }
            monstersAlive = monsters.Count;
        }

        private void runWave(int wave)
        {
            var start = DateTime.Now;

            var eventsProcessed = 0;
            var totalObjects = updateQueue.Count;

            while (towerLifu > 0 && eventsProcessed < totalObjects)
            {
                eventsProcessed += 1;
                processUpdateEvent(updateQueue.First.Value);
                updateQueue.RemoveFirst();
            }

            var time = (DateTime.Now - start).TotalMilliseconds;
            if (time >= (1000 / 24) / 2)
            {
                Console.Error.WriteLine("Can't simulate in time. Simulation took {0}ms", time);
            }
        }

        private void processUpdateEvent(UpdateEvent p)
        {
            switch (p.type)
            {
                case UpdateEvent.Type.MonsterMove:
                    moveMonster(p.index, p.skipStepsCount);
                    break;
                case UpdateEvent.Type.GunFire:
                    fireGun(p.index, p.skipStepsCount);
                    break;
                case UpdateEvent.Type.ProjectileMove:
                    moveProjectile(p.projectile);
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Moves projectile along radius, checks for collisions with monsters.
        /// </summary>
        private void moveProjectile(Projectile projectile)
        {
            var radialSpeed = Projectile.BaseRadialSpeed + projectile.Level;
            var r0 = projectile.Position.R;
            var r1 = r0 + radialSpeed;

            var radius = Projectile.BaseRadius + Projectile.LevelRadiusStep * projectile.Level;

            for (int i = 0; i < monsters.Count; ++i)
            {
                var monster = monsters[i];
                if (monster.Life == 0) continue;
                if (monster.Position.R < projectile.Position.R) continue;
                if (monster.Position.R - Monster.Radius[(int)monster.type] > r1 + radius) continue;

                var cos = Math.Cos(monsters[i].Position.φ - projectile.Position.φ);

                var t = (monsters[i].Position.R * cos - r0) / (r1 - r0);
                t = Math.Max(0, Math.Min(1, t)); // interpolation parameter between previous projectile position
                                                 // and new one, where distance to monster is minimal

                var r1t = (r0*(1 - t) + r1*t);
                var r1t_2 = r1t*r1t;

                var r2 = monster.Position.R;
                var r2_2 = r2*r2;
                var d = Math.Sqrt(r1t_2 + r2_2 - 2 * r1t * r2 * cos); // computing distance by hand because already have computed Cos
                if (d < radius + Monster.Radius[(int)monster.type]) // if actual distance suppose hit
                {
                    monsters[i].Life = Math.Max(0, monsters[i].Life - Projectile.BasePower - 10 * projectile.Level);
                    if (monsters[i].Life == 0)
                    {
                        monsterDied(monsters[i].ID, monsters[i].type, true);
                    }

                    projectileHit(projectile.ID);
                    return;
                }
            }

            // Moving projectile along radius
            projectile.Position.R = r1;
            projectileMoved(projectile.ID, projectile.Position);

            if (r1 < Renderer.MaxVisibleLogicRadius * 1.2) // if projectile is still visible (or not far from) on screen
            {
                updateQueue.AddLast(new UpdateEvent() { projectile = projectile, type = UpdateEvent.Type.ProjectileMove });
            }
        }

        private void fireGun(int gunIndex, int skipSteps)
        {
            var gun = guns[gunIndex];
            if (gun.Life == 0) return;
            Debug.Assert(gun.type != Gun.Type.Mine);

            int newSkipSteps = Math.Max(0, skipSteps - 1);
            if (skipSteps == 0)
            {
                if (gun.type == Gun.Type.Lazer)
                {
                    newSkipSteps = fireLaserGun(gun);
                }
                else
                {
                    newSkipSteps = fireMachineGun(gun);
                }
            }

            updateQueue.AddLast(new UpdateEvent() { index = gunIndex, type = UpdateEvent.Type.GunFire, skipStepsCount = newSkipSteps });
        }

        /// <summary>
        /// Fires lazer gun. Searches for lazer ray intersections with monsters.
        /// </summary>
        /// <returns>Number of iterations till next volley</returns>
        private int fireLaserGun(Gun gun)
        {
            laserFired(gun.ID);
            var lazerRayWidth = Gun.BaseLaserRayWidth + gun.Level * Gun.LaserRayWidthStep;
            for (int i = 0; i < monsters.Count; ++i)
            {
                var monster = monsters[i];
                if (monster.Life == 0) continue;
                if (monster.Position.R <= gun.Position.R) continue;

                var minφ = Math.Min(monster.Position.φ, gun.Position.φ);
                var maxφ = Math.Max(monster.Position.φ, gun.Position.φ);
                var dφ = Math.Min(maxφ - minφ, minφ + 2 * Math.PI - maxφ);

                if (dφ > Math.PI / 4) continue; // sanity check)

                var distanceToRay = monster.Position.R * Math.Sin(dφ);
                if (distanceToRay <= lazerRayWidth)
                {
                    monsters[i].Life = Math.Max(0, monsters[i].Life - Gun.BaseLaserPower - 5 * gun.Level);
                    if (monsters[i].Life == 0)
                    {
                        monsterDied(monsters[i].ID, monsters[i].type, true);
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
            updateQueue.AddLast(new UpdateEvent() { type = UpdateEvent.Type.ProjectileMove, projectile = projectile });

            return (Gun.NumLevels - gun.Level)*5;
        }

        /// <summary>
        /// Blows mine. Searches for affected monsters.
        /// </summary>
        private void blowMine(Gun mine)
        {
            mineBlown(mine.ID);
            var explosionRadius = Gun.ExplosionBaseRadius + Gun.ExplosionRadiusStep * mine.Level;
            for (int i = 0; i < monsters.Count; ++i)
            {
                var monster = monsters[i];
                if (monster.Life == 0) continue;
                if (Math.Abs(monsters[i].Position.R - mine.Position.R) > explosionRadius + Monster.Radius[(int)monster.type]) continue;

                var distance = PolarCoordinates.PolarDistance(monster.Position, mine.Position);
                distance = Math.Min(explosionRadius, Math.Max(0, distance - Gun.Radius - Monster.Radius[(int)monster.type]));

                monsters[i].Life = Math.Max(0, monsters[i].Life - (1.0 - distance / explosionRadius) * (mine.Level + 1) * Gun.ExplosionPower);
                if (monsters[i].Life == 0)
                {
                    monsterDied(monsters[i].ID, monsters[i].type, true);
                }
            }
            mine.Life = 0;
        }

        /// <summary>
        /// Moves monster around. Searches for interactions.
        /// </summary>
        private void moveMonster(int monsterIndex, int skipStepsCount)
        {
            Monster monster = monsters[monsterIndex];
            if (monster.Life == 0) return;
            if (skipStepsCount > 0)
            {
                updateQueue.AddLast(new UpdateEvent() { type = UpdateEvent.Type.MonsterMove, index = monsterIndex, skipStepsCount = skipStepsCount - 1 });
                return;
            }

            if (monster.Position.R - TowerRadius < 0.5) // if monster reaches tower, it dies taking part of tower life
            {
                towerLifu = Math.Max(0, towerLifu - monster.Life / 10);
                towerLifuChanged();
                monster.Life = 0;
                monsterDied(monster.ID, monster.type, false);
                return;
            }

            var vomitedGuns = new List<int>();
            Lazy<double> deltaPhi = new Lazy<double>(() => // max difference between angles when gun can still be affected by vomiting monster
            {
                return Math.Atan(Monster.VomitingRadiusOverSqrt2 / (monster.Position.R - Monster.VomitingRadiusOverSqrt2));
            });
            for (int i = 0; i < guns.Count; ++i)
            {
                var gun = guns[i];
                if (gun.Life == 0) continue;
                
                if (PolarCoordinates.PolarDistanceLessThan(monster.Position, guns[i].Position, Gun.Radius * 1.1))
                {
                    if (gun.type == Gun.Type.Mine)
                    {
                        blowMine(gun);
                        if (monster.Life > 0)
                        {
                            // if monster survived the blast, it stops for some time
                            updateQueue.AddLast(new UpdateEvent() { type = UpdateEvent.Type.MonsterMove, index = monsterIndex, skipStepsCount = (gun.Level + 1)*7 });
                        }
                        return;
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
                        vomitedGuns.Add(i);
                    }
                }
            }

            if (vomitedGuns.Count > 0)
            {
                vomit(monster.ID);
                for (int i = 0; i < vomitedGuns.Count; ++i)
                {
                    var gun = guns[vomitedGuns[i]];
                    gun.Life = Math.Max(0, gun.Life - 10);
                    if (gun.Life == 0)
                    {
                        gunDied(gun.ID);
                    }
                }
                updateQueue.AddLast(new UpdateEvent() { type = UpdateEvent.Type.MonsterMove, index = monsterIndex, skipStepsCount = 6 }); // When monster vomits, it waits some time
                return;
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

            updateQueue.AddLast(new UpdateEvent() { type = UpdateEvent.Type.MonsterMove, index = monsterIndex });
        }




        private void laserFired(long gunID)
        {
            renderer.messageBox.Add(Renderer.Message.LaserFired(gunID));
        }

        private void mineBlown(long gunID)
        {
            renderer.messageBox.Add(Renderer.Message.MineBlow(gunID));
        }

        private void vomit(long monsterID)
        {
            renderer.messageBox.Add(Renderer.Message.MonsterVomit(monsterID));
        }

        private void newMonster(Monster monster)
        {
            renderer.messageBox.Add(Renderer.Message.NewMonster(monster.ID, monster.Position, (int)monster.type));
        }

        private void monsterMoved(long monsterID, PolarCoordinates position)
        {
            renderer.messageBox.Add(Renderer.Message.MonsterMoved(monsterID, position));
        }

        private void monsterDied(long monsterID, Monster.Type monsterType, bool addMoney)
        {
            monstersAlive -= 1;
            if (addMoney) playerMoney += 200 * (int)(monsterType + 1);
            renderer.messageBox.Add(Renderer.Message.MonsterDied(monsterID));
        }

        private void newGun(Gun gun)
        {
            renderer.messageBox.Add(Renderer.Message.NewGun(gun.ID, gun.Position, (int)gun.type, gun.Level));
            Application.Current.Dispatcher.BeginInvoke(new Action(gameFieldWindow.gunAdded));
        }

        private void gunDied(long gunID)
        {
            renderer.messageBox.Add(Renderer.Message.GunDied(gunID));
        }

        private void newProjectile(Projectile projectile)
        {
            renderer.messageBox.Add(Renderer.Message.NewProjectile(projectile.ID, projectile.Position, projectile.Level));
        }

        private void projectileMoved(long projectileID, PolarCoordinates position)
        {
            renderer.messageBox.Add(Renderer.Message.ProjectileMoved(projectileID, position));
        }

        private void projectileHit(long projectileID)
        {
            renderer.messageBox.Add(Renderer.Message.ProjectileHit(projectileID));
        }

        private void playerMoneyChanged()
        {
            try // try-catch should be everywhere, but this is the most frequent where game crashes on exit
            {
                Application.Current.Dispatcher.BeginInvoke(new Action<long>(gameFieldWindow.setPlayerMoney), playerMoney);
            }
            catch (NullReferenceException) { }
        }

        private void towerLifuChanged()
        {
            Application.Current.Dispatcher.BeginInvoke(new Action<long>(gameFieldWindow.setTowerLifu), (long)towerLifu);
        }
    }


}
