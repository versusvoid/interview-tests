using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;

namespace MinimalTowerDefence
{
    partial class Renderer
    {

        /// <summary>
        /// Render thread entry point.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        internal void run(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            while (true)
            {
                var newEvent = messageBox.Take();
                switch (newEvent.type)
                {
                    // Global events
                    case Message.Type.Resize:
                        resizeWritableBuffer(newEvent.NewWidth, newEvent.NewHeight);
                        break;
                    case Message.Type.Render:
                        render(newEvent.pBackBuffer);
                        break;

                    // Effects-related events
                    case Message.Type.LaserFired:
                        laserFired(newEvent.FieldObjectID);
                        break;
                    case Message.Type.MineBlow:
                        mineBlow(newEvent.FieldObjectID);
                        break;
                    case Message.Type.MonsterVomit:
                        monsterVomit(newEvent.FieldObjectID);
                        break;

                    // Field objects-related events
                    case Message.Type.NewMonster:
                        newMonster(newEvent.FieldObjectID, newEvent.FieldObjectType, newEvent.FieldObjectNewPosition);
                        break;
                    case Message.Type.MonsterMoved:
                        monsterMoved(newEvent.FieldObjectID, newEvent.FieldObjectNewPosition);
                        break;
                    case Message.Type.MonsterDied:
                        monsterDied(newEvent.FieldObjectID);
                        break;

                    case Message.Type.NewGun:
                        newGun(newEvent.FieldObjectID, newEvent.FieldObjectType, newEvent.FieldObjectLevel, newEvent.FieldObjectNewPosition);
                        break;
                    case Message.Type.GunDied:
                        gunDied(newEvent.FieldObjectID);
                        break;

                    case Message.Type.NewProjectile:
                        newProjectile(newEvent.FieldObjectID, newEvent.FieldObjectLevel, newEvent.FieldObjectNewPosition);
                        break;
                    case Message.Type.ProjectileMoved:
                        projectileMoved(newEvent.FieldObjectID, newEvent.FieldObjectNewPosition);
                        break;
                    case Message.Type.ProjectileHit:
                        projectileHit(newEvent.FieldObjectID);
                        break;

                    case Message.Type.Stop:
                        return;

                    default:
                        throw new NotImplementedException();
                }
            }
        }

        private Point coordinateTransformation(PolarCoordinates point)
        {
            var r = point.R * radialScale;

            return new Point(r * Math.Cos(point.φ) + width / 2, r * Math.Sin(point.φ) + height / 2);
        }


        /// <summary>
        /// Handles frame bitmap resize
        /// </summary>
        private void resizeWritableBuffer(int width, int height)
        {
            updateCoordinates((double)width / (double)this.width, (double)height / (double)this.height);

            this.width = width;
            this.height = height;
            radialScale = Math.Sqrt(width * width + height * height) / (MaxVisibleLogicRadius * 2.0);
            InitializeSprites();
        }
        
        private void updateCoordinates(double widthScale, double heightScale)
        {
            updateCoordinates(guns, widthScale, heightScale);
            updateCoordinates(monsters, widthScale, heightScale);
            updateCoordinates(projectiles, widthScale, heightScale);
        }

        /// <summary>
        /// Updates screen coordinates of every fieldObject
        /// </summary>
        private void updateCoordinates(Dictionary<long, FieldObject> objects, double widthScale, double heightScale)
        {
            foreach (var fieldObject in objects)
            {
                fieldObject.Value.position.X *= widthScale;
                fieldObject.Value.position.Y *= heightScale;
            }
        }


        private void render(IntPtr pBackBuffer)
        {
            renderFrame(pBackBuffer);
            Application.Current.Dispatcher.BeginInvoke(new Action(gameFieldWindow.frameRendered));
        }



        private void laserFired(long fieldObjectID)
        {
            Debug.Assert(guns.ContainsKey(fieldObjectID));
            var gun = guns[fieldObjectID];
            effects.AddLast(new Effect() { type = Effect.Type.Laser, ageFrames = 0, level = gun.level, position = gun.position });
        }

        private void mineBlow(long fieldObjectID)
        {
            Debug.Assert(guns.ContainsKey(fieldObjectID));
            var gun = guns[fieldObjectID];
            guns.Remove(fieldObjectID);
            effects.AddLast(new Effect() { type = Effect.Type.Exploison, ageFrames = 0, level = gun.level, position = gun.position });
        }

        private void monsterVomit(long fieldObjectID)
        {
            Debug.Assert(monsters.ContainsKey(fieldObjectID));
            var monster = monsters[fieldObjectID];
            effects.AddLast(new Effect() { type = Effect.Type.Vomit, ageFrames = 0, position = monster.position });
        }

        private void newMonster(long fieldObjectID, int fieldObjectType, PolarCoordinates fieldObjectNewPosition)
        {
            monsters.Add(fieldObjectID, new FieldObject() { type = fieldObjectType, position = coordinateTransformation(fieldObjectNewPosition) });
        }

        private void monsterMoved(long fieldObjectID, PolarCoordinates fieldObjectNewPosition)
        {
            monsters[fieldObjectID].position = coordinateTransformation(fieldObjectNewPosition);
        }

        private void monsterDied(long fieldObjectID)
        {
            monsters[fieldObjectID].dead = true;
        }

        private void newGun(long fieldObjectID, int fieldObjectType, int fieldObjectLevel, PolarCoordinates fieldObjectNewPosition)
        {
            guns.Add(fieldObjectID, new FieldObject() { type = fieldObjectType, level = fieldObjectLevel, position = coordinateTransformation(fieldObjectNewPosition) });
        }

        private void gunDied(long fieldObjectID)
        {
            guns[fieldObjectID].dead = true;
        }

        private void newProjectile(long fieldObjectID, int fieldObjectLevel, PolarCoordinates fieldObjectNewPosition)
        {
            projectiles.Add(fieldObjectID, new FieldObject() { level = fieldObjectLevel, position = coordinateTransformation(fieldObjectNewPosition) });
        }

        private void projectileMoved(long fieldObjectID, PolarCoordinates fieldObjectNewPosition)
        {
            projectiles[fieldObjectID].position = coordinateTransformation(fieldObjectNewPosition);
        }

        private void projectileHit(long fieldObjectID)
        {
            projectiles.Remove(fieldObjectID); // projectiles die immediately
        }
    }
}