// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;

namespace MinimalTowerDefence
{
    internal partial class Renderer
    {
        /// <summary>
        /// Render thread entry point.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        internal void Run(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            while (true)
            {
                var newEvent = MessageBox.Take();
                switch (newEvent.MessageType)
                {
                    // Global events
                    case Message.Type.Resize:
                        ResizeWritableBuffer(newEvent.NewWidth, newEvent.NewHeight);
                        break;

                    case Message.Type.Render:
                        Render(newEvent.pBackBuffer);
                        break;

                    // Effects-related events
                    case Message.Type.LaserFired:
                        LaserFired(newEvent.FieldObjectID);
                        break;

                    case Message.Type.MineBlow:
                        MineBlow(newEvent.FieldObjectID);
                        break;

                    case Message.Type.MonsterVomit:
                        MonsterVomit(newEvent.FieldObjectID);
                        break;

                    // Field objects-related events
                    case Message.Type.NewMonster:
                        NewMonster(newEvent.FieldObjectID, newEvent.FieldObjectType, newEvent.FieldObjectNewPosition);
                        break;

                    case Message.Type.MonsterMoved:
                        MonsterMoved(newEvent.FieldObjectID, newEvent.FieldObjectNewPosition);
                        break;

                    case Message.Type.MonsterDied:
                        MonsterDied(newEvent.FieldObjectID);
                        break;

                    case Message.Type.NewGun:
                        newGun(newEvent.FieldObjectID, newEvent.FieldObjectType, newEvent.FieldObjectLevel, newEvent.FieldObjectNewPosition);
                        break;

                    case Message.Type.GunDied:
                        GunDied(newEvent.FieldObjectID);
                        break;

                    case Message.Type.NewProjectile:
                        newProjectile(newEvent.FieldObjectID, newEvent.FieldObjectLevel, newEvent.FieldObjectNewPosition);
                        break;

                    case Message.Type.ProjectileMoved:
                        ProjectileMoved(newEvent.FieldObjectID, newEvent.FieldObjectNewPosition);
                        break;

                    case Message.Type.ProjectileHit:
                        ProjectileHit(newEvent.FieldObjectID);
                        break;

                    case Message.Type.Stop:
                        return;

                    default:
                        throw new NotImplementedException();
                }
            }
        }

        private Point CoordinateTransformation(PolarCoordinates point)
        {
            var r = point.R * _radialScale;

            return new Point(r * Math.Cos(point.φ) + _width / 2, r * Math.Sin(point.φ) + _height / 2);
        }

        /// <summary>
        /// Handles frame bitmap resize
        /// </summary>
        private void ResizeWritableBuffer(int width, int height)
        {
            UpdateCoordinates((double)width / (double)_width, (double)height / (double)_height);

            _width = width;
            _height = height;
            _radialScale = Math.Sqrt(width * width + height * height) / (GameLogic.MaxVisibleLogicRadius * 2.0);
            InitializeSprites();
        }

        private void UpdateCoordinates(double widthScale, double heightScale)
        {
            UpdateCoordinates(_guns, widthScale, heightScale);
            UpdateCoordinates(_monsters, widthScale, heightScale);
            UpdateCoordinates(_projectiles, widthScale, heightScale);
        }

        /// <summary>
        /// Updates screen coordinates of every fieldObject
        /// </summary>
        private void UpdateCoordinates(Dictionary<long, FieldObject> objects, double widthScale, double heightScale)
        {
            foreach (var fieldObject in objects)
            {
                fieldObject.Value.Position.X *= widthScale;
                fieldObject.Value.Position.Y *= heightScale;
            }
        }

        private void Render(IntPtr pBackBuffer)
        {
            RenderFrame(pBackBuffer);
            Application.Current.Dispatcher.BeginInvoke(new Action(_gameFieldWindow.FrameRendered));
        }

        private void LaserFired(long fieldObjectID)
        {
            Debug.Assert(_guns.ContainsKey(fieldObjectID));
            var gun = _guns[fieldObjectID];
            _effects.AddLast(new Effect() { EffectType = Effect.Type.Laser, AgeFrames = 0, Level = gun.Level, Position = gun.Position });
        }

        private void MineBlow(long fieldObjectID)
        {
            Debug.Assert(_guns.ContainsKey(fieldObjectID));
            var gun = _guns[fieldObjectID];
            _guns.Remove(fieldObjectID);
            _effects.AddLast(new Effect() { EffectType = Effect.Type.Exploison, AgeFrames = 0, Level = gun.Level, Position = gun.Position });
        }

        private void MonsterVomit(long fieldObjectID)
        {
            Debug.Assert(_monsters.ContainsKey(fieldObjectID));
            var monster = _monsters[fieldObjectID];
            _effects.AddLast(new Effect() { EffectType = Effect.Type.Vomit, AgeFrames = 0, Position = monster.Position });
        }

        private void NewMonster(long fieldObjectID, int fieldObjectType, PolarCoordinates fieldObjectNewPosition)
        {
            _monsters.Add(fieldObjectID, new FieldObject() { ObjectType = fieldObjectType, Position = CoordinateTransformation(fieldObjectNewPosition) });
        }

        private void MonsterMoved(long fieldObjectID, PolarCoordinates fieldObjectNewPosition)
        {
            _monsters[fieldObjectID].Position = CoordinateTransformation(fieldObjectNewPosition);
        }

        private void MonsterDied(long fieldObjectID)
        {
            _monsters[fieldObjectID].Dead = true;
        }

        private void newGun(long fieldObjectID, int fieldObjectType, int fieldObjectLevel, PolarCoordinates fieldObjectNewPosition)
        {
            _guns.Add(fieldObjectID, new FieldObject() { ObjectType = fieldObjectType, Level = fieldObjectLevel, Position = CoordinateTransformation(fieldObjectNewPosition) });
        }

        private void GunDied(long fieldObjectID)
        {
            _guns[fieldObjectID].Dead = true;
        }

        private void newProjectile(long fieldObjectID, int fieldObjectLevel, PolarCoordinates fieldObjectNewPosition)
        {
            _projectiles.Add(fieldObjectID, new FieldObject() { Level = fieldObjectLevel, Position = CoordinateTransformation(fieldObjectNewPosition) });
        }

        private void ProjectileMoved(long fieldObjectID, PolarCoordinates fieldObjectNewPosition)
        {
            _projectiles[fieldObjectID].Position = CoordinateTransformation(fieldObjectNewPosition);
        }

        private void ProjectileHit(long fieldObjectID)
        {
            // Projectiles die immediately.
            _projectiles.Remove(fieldObjectID);
        }
    }
}