// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace MinimalTowerDefence
{
    /// <summary>
    /// Interaction logic for GameField.xaml
    /// </summary>
    public partial class GameField : Window
    {
        /// <summary>
        /// An item for ComboBox representing gun that can be purchased.
        /// </summary>
        private class GunShopItem : INotifyPropertyChanged
        {
            public Gun.Type Type { get; set; }

            public int Level { get; set; }

            public int Price { get; set; }

            private bool _isSelectable;

            /// <summary>
            /// Whether gun can be purchased now.
            /// </summary>
            public bool IsSelectable
            {
                get
                {
                    return _isSelectable;
                }

                set
                {
                    _isSelectable = value;
                    OnPropertyChanged("IsSelectable");
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            protected virtual void OnPropertyChanged(string propertyName)
            {
                if (this.PropertyChanged != null)
                {
                    this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
                }
            }
        }

        private readonly BackgroundWorker _renderWorker = new BackgroundWorker();
        private Renderer _renderer;

        private readonly BackgroundWorker _logicWorker = new BackgroundWorker();
        private GameLogic _logic;

        /// <summary>
        /// Timer indicating, that new game frame should be rendered.
        /// </summary>
        private readonly DispatcherTimer _renderFrameTimer = new DispatcherTimer();

        /// <summary>
        /// Whether player had won.
        /// </summary>
        private bool _win;

        /// <summary>
        /// Whether player currently selecting place for new gun.
        /// </summary>
        private bool _selectingGunPlace;

        /// <summary>
        /// Gun, that player is placing now.
        /// </summary>
        private GunShopItem _selectedGun;

        /// <summary>
        /// Gun selectors for every gun type.
        /// </summary>
        private ComboBox[] _gunShop;

        /// <summary>
        /// Size scale of pixel window size to game field size of game logic.
        /// </summary>
        private double _radialScale;

        /// <summary>
        /// Bitmap for rendering frame.
        /// </summary>
        private WriteableBitmap _writeableBitmap;

        public GameField()
        {
            InitializeComponent();
            _gunShop = new ComboBox[3];
            _gunShop[0] = mineSelector;
            _gunShop[1] = machineGunSelector;
            _gunShop[2] = lazerSelector;

            _renderFrameTimer.Tick += RequestNewFrame;
            _renderFrameTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000 / 24);

            InitGunShop();
            Init();
        }

        /// <summary>
        /// Called (remotely) by game logic thread when new gun had been successfully added.
        /// </summary>
        internal void GunAdded()
        {
            Debug.Assert(_selectingGunPlace);
            _selectingGunPlace = false;

            mineSelector.SelectedIndex = -1;
            lazerSelector.SelectedIndex = -1;
            machineGunSelector.SelectedIndex = -1;

            canvas.Children[0].Visibility = System.Windows.Visibility.Collapsed;
        }

        /// <summary>
        /// Called (remotely) by game logic when game ends.
        /// </summary>
        /// <param name="win">Whether player has won or lost.</param>
        internal void GameOver(bool win)
        {
            _win = win;
            Console.WriteLine("Finish: {0}", win);
            _renderFrameTimer.Stop();
            _renderer.MessageBox.Add(Renderer.Message.Stop());
        }

        /// <summary>
        /// Called (remotely) by game logic, when player spends or earns money.
        /// </summary>
        /// <param name="playerMoney">New value of player account</param>
        internal void SetPlayerMoney(long playerMoney)
        {
            moneyLabel.Content = playerMoney.ToString();

            UpdateGunShopSelector(mineSelector, playerMoney);
            UpdateGunShopSelector(machineGunSelector, playerMoney);
            UpdateGunShopSelector(lazerSelector, playerMoney);
        }

        /// <summary>
        /// Called (remotely) by game logic, when life value of tower changes.
        /// </summary>
        /// <param name="playerMoney">New value of tower's life.</param>
        internal void SetTowerLifu(long towerLifu)
        {
            lifuLabel.Content = towerLifu.ToString();
        }

        /// <summary>
        /// Called (remotely) by rendering thread, when it finishes new frame.
        /// </summary>
        internal void FrameRendered()
        {
            _logic.MessageBox.Add(GameLogic.Message.ContinueSimulation());

            _writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, (int)_writeableBitmap.Width, (int)_writeableBitmap.Height));
            _writeableBitmap.Unlock();
        }

        private void InitGunShop()
        {
            mineSelector.ItemsSource = InitSelector(Gun.Type.Mine);
            lazerSelector.ItemsSource = InitSelector(Gun.Type.Lazer);
            machineGunSelector.ItemsSource = InitSelector(Gun.Type.Machine);
        }

        private System.Collections.IEnumerable InitSelector(Gun.Type type)
        {
            var guns = new ObservableCollection<GunShopItem>();
            foreach (var level in Enumerable.Range(0, Gun.NumLevels))
            {
                guns.Add(new GunShopItem()
                {
                    Type = type,
                    Level = level,
                    Price = Gun.Price(type, level),
                    IsSelectable = false
                });
            }

            return guns;
        }

        /// <summary>
        /// Starts new game. Runs game logic and rendering threads.
        /// </summary>
        private void Init()
        {
            _renderer = new Renderer(this);
            _logic = new GameLogic(this, _renderer);

            if (contentGrid.ActualWidth > 0)
                ReinitFieldBitmap((int)contentGrid.ActualWidth, (int)contentGrid.ActualHeight);

            _logicWorker.DoWork += _logic.Run;
            _logicWorker.RunWorkerAsync();

            _renderWorker.DoWork += _renderer.Run;
            _renderWorker.RunWorkerCompleted += RendererStopped;
            _renderWorker.WorkerSupportsCancellation = true;
            _renderWorker.RunWorkerAsync();

            _renderFrameTimer.Start();
        }

        /// <summary>
        /// Sends request for new frame to rendering thread.
        /// </summary>
        private void RequestNewFrame(object sender, EventArgs e)
        {
            _writeableBitmap.Lock();
            _renderer.MessageBox.Add(Renderer.Message.Render(_writeableBitmap.BackBuffer));
        }

        /// <summary>
        /// Called when rendering thread has stopped. Meaning that game has ended.
        /// </summary>
        private void RendererStopped(object sender, RunWorkerCompletedEventArgs e)
        {
            _logicWorker.DoWork -= _logic.Run;
            _renderWorker.DoWork -= _renderer.Run;
            _renderer = null;
            _logic = null;

            var done = false;
            if (_win)
            {
                MessageBox.Show("Hey, cheater, you won!", "Holy cow!");
                done = true;
            }
            else
            {
                var replay = MessageBox.Show("Hey, loooser. Retry?", "Looooooooseeeeer", MessageBoxButton.YesNo);
                done = replay == MessageBoxResult.No;
            }

            if (done)
            {
                var mainWindow = new MainWindow();
                App.Current.MainWindow = mainWindow;
                this.Close();
                mainWindow.Show();
            }
            else
            {
                Init();
            }
        }

        /// <summary>
        /// Event handler for window resizing.
        /// </summary>
        private void GameFieldSizeChanged(object sender, SizeChangedEventArgs e)
        {
            ReinitFieldBitmap((int)e.NewSize.Width, (int)e.NewSize.Height);
        }

        /// <summary>
        /// Creates new bitmap for rendering, notifies rendering thread of resize, updates depended variables.
        /// </summary>
        /// <param name="w">New width</param>
        /// <param name="h">New height</param>
        private void ReinitFieldBitmap(int w, int h)
        {
            _writeableBitmap = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgr32, null);
            fieldImage.Source = _writeableBitmap;
            _renderer.MessageBox.Add(Renderer.Message.Resize(w, h));
            _radialScale = Math.Sqrt(w * w + h * h) / (GameLogic.MaxVisibleLogicRadius * 2.0);

            var gunImage = canvas.Children[0] as Ellipse;

            gunImage.Width = 2 * Gun.Radius * _radialScale;
            gunImage.Height = 2 * Gun.Radius * _radialScale;
        }

        /// <summary>
        /// Enables or disables gun items for purchase.
        /// </summary>
        /// <param name="selector">Gun selector</param>
        /// <param name="playerMoney">New value of player account</param>
        private void UpdateGunShopSelector(ComboBox selector, long playerMoney)
        {
            foreach (GunShopItem item in selector.ItemsSource)
            {
                var newValue = playerMoney >= item.Price;
                if (newValue != item.IsSelectable)
                    item.IsSelectable = newValue;
            }
        }

        /// <summary>
        /// Handler for player selecting gun from shop.
        /// </summary>
        /// <param name="sender">ComboBox, one of three for every gun type.</param>
        private void gunSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0) return;

            // Disabling all other gun selectors.
            foreach (var comboBox in _gunShop)
            {
                if (comboBox != sender) comboBox.SelectedIndex = -1;
            }

            canvas.Children[0].Visibility = System.Windows.Visibility.Collapsed;

            _selectingGunPlace = true;
            _selectedGun = (GunShopItem)e.AddedItems[0];
        }

        /// <summary>
        /// Handler for mouse moving.
        /// Updates gun pre-image when user selects place for new gun.
        /// </summary>
        private void window_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_selectingGunPlace) return;

            var gunImage = canvas.Children[0] as Ellipse;
            if (!gunImage.IsVisible)
            {
                gunImage.Visibility = System.Windows.Visibility.Visible;
                gunImage.Fill = new SolidColorBrush(Renderer.GunColors[(int)_selectedGun.Type, _selectedGun.Level]);
            }

            var pos = e.GetPosition(canvas);
            Canvas.SetLeft(gunImage, pos.X - gunImage.Width);
            Canvas.SetTop(gunImage, pos.Y - gunImage.Height);
        }

        /// <summary>
        /// Handler for mouse click.
        /// Sends request for new gun to game logic thread.
        /// </summary>
        private void window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!_selectingGunPlace) return;

            var screenPosition = e.GetPosition(contentGrid);
            screenPosition.X -= Gun.Radius * _radialScale;
            screenPosition.Y -= Gun.Radius * _radialScale;

            var p = (screenPosition - new Point(contentGrid.ActualWidth / 2, contentGrid.ActualHeight / 2)) / _radialScale;
            var polarCoordinates = new PolarCoordinates(Math.Sqrt(p.X * p.X + p.Y * p.Y), Math.Atan2(p.Y, p.X));
            if (polarCoordinates.φ < 0)
                polarCoordinates.φ += 2 * Math.PI;

            _logic.MessageBox.Add(GameLogic.Message.NewGun(_selectedGun.Type, _selectedGun.Level, polarCoordinates));
        }
    }
}