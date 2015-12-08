using System;
using System.Windows;

namespace MinimalTowerDefence
{
    partial class GameLogic
    {
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