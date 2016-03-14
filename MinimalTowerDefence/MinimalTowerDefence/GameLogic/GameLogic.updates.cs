using System;
using System.Windows;

namespace MinimalTowerDefence
{
    internal partial class GameLogic
    {
        private void LaserFired(long gunID)
        {
            _renderer.MessageBox.Add(Renderer.Message.LaserFired(gunID));
        }

        private void MineBlown(long gunID)
        {
            _renderer.MessageBox.Add(Renderer.Message.MineBlow(gunID));
        }

        private void Vomit(long monsterID)
        {
            _renderer.MessageBox.Add(Renderer.Message.MonsterVomit(monsterID));
        }

        private void NewMonster(Monster monster)
        {
            _renderer.MessageBox.Add(Renderer.Message.NewMonster(monster.ID, monster.Position, (int)monster.MonsterType));
        }

        private void MonsterMoved(long monsterID, PolarCoordinates position)
        {
            _renderer.MessageBox.Add(Renderer.Message.MonsterMoved(monsterID, position));
        }

        private void MonsterDied(long monsterID, Monster.Type monsterType, bool addMoney)
        {
            _monstersAlive -= 1;
            if (addMoney) _playerMoney += 200 * (int)(monsterType + 1);
            _renderer.MessageBox.Add(Renderer.Message.MonsterDied(monsterID));
        }

        private void NewGun(Gun gun)
        {
            _renderer.MessageBox.Add(Renderer.Message.NewGun(gun.ID, gun.Position, (int)gun.GunType, gun.Level));
            Application.Current.Dispatcher.BeginInvoke(new Action(_gameFieldWindow.GunAdded));
        }

        private void GunDied(long gunID)
        {
            _renderer.MessageBox.Add(Renderer.Message.GunDied(gunID));
        }

        private void NewProjectile(Projectile projectile)
        {
            _renderer.MessageBox.Add(Renderer.Message.NewProjectile(projectile.ID, projectile.Position, projectile.Level));
        }

        private void ProjectileMoved(long projectileID, PolarCoordinates position)
        {
            _renderer.MessageBox.Add(Renderer.Message.ProjectileMoved(projectileID, position));
        }

        private void ProjectileHit(long projectileID)
        {
            _renderer.MessageBox.Add(Renderer.Message.ProjectileHit(projectileID));
        }

        private void PlayerMoneyChanged()
        {
            // Try-catch should be everywhere, but this is the most frequent where game crashes on exit.
            try
            {
                Application.Current.Dispatcher.BeginInvoke(new Action<long>(_gameFieldWindow.SetPlayerMoney), _playerMoney);
            }
            catch (NullReferenceException) { }
        }

        private void TowerLifuChanged()
        {
            Application.Current.Dispatcher.BeginInvoke(new Action<long>(_gameFieldWindow.SetTowerLifu), (long)_towerLifu);
        }
    }
}