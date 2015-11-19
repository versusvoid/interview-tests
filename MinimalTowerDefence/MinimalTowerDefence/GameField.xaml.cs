using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
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
        class GunShopItem : INotifyPropertyChanged
        {
            public Gun.Type Type { get; set; }
            public int Level { get; set; }
            public int Price { get; set; }

            private bool isSelectable;
            /// <summary>
            /// Whether gun can be purchased now.
            /// </summary>
            public bool IsSelectable
            {
                get
                {
                    return isSelectable;
                }
                set
                {
                    isSelectable = value;
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

        private readonly BackgroundWorker renderWorker = new BackgroundWorker();
        private Renderer renderer;

        private readonly BackgroundWorker logicWorker = new BackgroundWorker();
        private GameLogic logic;

        /// <summary>
        /// Timer indicating, that new game frame should be rendered.
        /// </summary>
        private readonly DispatcherTimer renderFrameTimer = new DispatcherTimer();

        /// <summary>
        /// Whether player had won.
        /// </summary>
        private bool win;
        /// <summary>
        /// Whether player currently selecting place for new gun.
        /// </summary>
        private bool selectingGunPlace;
        /// <summary>
        /// Gun, that player is placing now.
        /// </summary>
        private GunShopItem selectedGun;
        /// <summary>
        /// Gun selectors for every gun type.
        /// </summary>
        private ComboBox[] gunShop;
        /// <summary>
        /// Size scale of pixel window size to game field size of game logic.
        /// </summary>
        private double radialScale;
        /// <summary>
        /// Bitmap for rendering frame.
        /// </summary>
        private WriteableBitmap writeableBitmap;

        public GameField()
        {
            InitializeComponent();
            gunShop = new ComboBox[3];
            gunShop[0] = mineSelector;
            gunShop[1] = machineGunSelector;
            gunShop[2] = lazerSelector;

            renderFrameTimer.Tick += requestNewFrame;
            renderFrameTimer.Interval = new TimeSpan(0, 0, 0, 0, 1000 / 24);

            initGunShop();
            init();
        }

        private void initGunShop()
        {
            mineSelector.ItemsSource = initSelector(Gun.Type.Mine);
            lazerSelector.ItemsSource = initSelector(Gun.Type.Lazer);
            machineGunSelector.ItemsSource = initSelector(Gun.Type.Machine);
        }

        private System.Collections.IEnumerable initSelector(Gun.Type type)
        {
 
            var guns = new ObservableCollection<GunShopItem>();
            foreach (var level in Enumerable.Range(0, Gun.NumLevels))
            {
                guns.Add(new GunShopItem()
                {
                    Type = type,
                    Level = level,
                    Price = Gun.price(type, level),
                    IsSelectable = false
                });
            }
            return guns;
        }

        /// <summary>
        /// Starts new game. Runs game logic and rendering threads.
        /// </summary>
        private void init()
        {
            renderer = new Renderer(this);
            logic = new GameLogic(this, renderer);
            renderer.gameLogic = logic;

            if (contentGrid.ActualWidth > 0)
                reinitFieldBitmap((int)contentGrid.ActualWidth, (int)contentGrid.ActualHeight);

            logicWorker.DoWork += logic.run;
            logicWorker.RunWorkerAsync();

            renderWorker.DoWork += renderer.run;
            renderWorker.RunWorkerCompleted += rendererStopped;
            renderWorker.WorkerSupportsCancellation = true;
            renderWorker.RunWorkerAsync();
            renderFrameTimer.Start();
        }

        /// <summary>
        /// Sends request for new frame to rendering thread.
        /// </summary>
        private void requestNewFrame(object sender, EventArgs e)
        {
            writeableBitmap.Lock();
            renderer.messageBox.Add(Renderer.Message.Render(writeableBitmap.BackBuffer));
        }

        /// <summary>
        /// Called (remotely) by rendering thread, when it finishs new frame.
        /// </summary>
        internal void frameRendered()
        {
            writeableBitmap.AddDirtyRect(new Int32Rect(0, 0, (int)writeableBitmap.Width, (int)writeableBitmap.Height));          
            writeableBitmap.Unlock();
        }

        /// <summary>
        /// Called when rendering thread has stopped. Meaning that game has ended.
        /// </summary>
        private void rendererStopped(object sender, RunWorkerCompletedEventArgs e)
        {
            logicWorker.DoWork -= logic.run;
            renderWorker.DoWork -= renderer.run;
            renderer = null;
            logic = null;

            var done = false;
            if (win)
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
                init();
            }
        }

        /// <summary>
        /// Event handler for window resizing.
        /// </summary>
        private void gameFieldSizeChanged(object sender, SizeChangedEventArgs e)
        {
            reinitFieldBitmap((int)e.NewSize.Width, (int)e.NewSize.Height);
        }

        /// <summary>
        /// Creates new bitmap for rendering, notifies rendering thread of resize, updates depended variables.
        /// </summary>
        /// <param name="w">New width</param>
        /// <param name="h">New height</param>
        private void reinitFieldBitmap(int w, int h) 
        {
            writeableBitmap = new WriteableBitmap(w, h, 96, 96, PixelFormats.Bgr32, null);
            fieldImage.Source = writeableBitmap;
            renderer.messageBox.Add(Renderer.Message.Resize(w, h));
            radialScale = Math.Sqrt(w * w + h * h) / (Renderer.MaxVisibleLogicRadius * 2.0);

            var gunImage = canvas.Children[0] as Ellipse;
        
            gunImage.Width = 2 * Gun.Radius * radialScale;
            gunImage.Height = 2 * Gun.Radius * radialScale;
        }

        /// <summary>
        /// Called (remotely) by game logic when game ends.
        /// </summary>
        /// <param name="win">Whether player has won or lost.</param>
        internal void gameOver(bool win)
        {
            this.win = win;
            Console.WriteLine("Finish: {0}", win);
            renderFrameTimer.Stop();
            renderer.messageBox.Add(Renderer.Message.Stop());
        }

        /// <summary>
        /// Called (remotely) by game logic, when player spends or earns money.
        /// </summary>
        /// <param name="playerMoney">New value of player account</param>
        internal void setPlayerMoney(long playerMoney)
        {
            moneyLabel.Content = playerMoney.ToString();

            updateGunShopSelector(mineSelector, playerMoney);
            updateGunShopSelector(machineGunSelector, playerMoney);
            updateGunShopSelector(lazerSelector, playerMoney);
        }

        /// <summary>
        /// Enables or disables gun items for purchase.
        /// </summary>
        /// <param name="selector">Gun selector</param>
        /// <param name="playerMoney">New value of player account</param>
        private void updateGunShopSelector(ComboBox selector, long playerMoney) 
        {
            foreach (GunShopItem item in selector.ItemsSource)
            {
                var newValue = playerMoney >= item.Price;
                if (newValue != item.IsSelectable)
                    item.IsSelectable = newValue;
            }
        }

        /// <summary>
        /// Called (remotely) by game logic, when life value of tower changes.
        /// </summary>
        /// <param name="playerMoney">New value of tower's life.</param>
        internal void setTowerLifu(long towerLifu)
        {
            lifuLabel.Content = towerLifu.ToString();
        }

        /// <summary>
        /// Handler for player selecting gun from shop.
        /// </summary>
        /// <param name="sender">ComboBox, one of three for every gun type.</param>
        private void gunSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0) return;
            // Disabling all other gun selectors.
            foreach (var comboBox in gunShop)
            {
                if (comboBox != sender) comboBox.SelectedIndex = -1;
            }
            canvas.Children[0].Visibility = System.Windows.Visibility.Collapsed;

            selectingGunPlace = true;
            selectedGun = (GunShopItem)e.AddedItems[0];
        }

        /// <summary>
        /// Handler for mouse moving. 
        /// Updates gun pre-image when user selects place for new gun.
        /// </summary>
        private void window_MouseMove(object sender, MouseEventArgs e)
        {
            if (!selectingGunPlace) return;

            var gunImage = canvas.Children[0] as Ellipse;
            if (!gunImage.IsVisible) {
                gunImage.Visibility = System.Windows.Visibility.Visible;
                gunImage.Fill = new SolidColorBrush(Renderer.GunColors[(int)selectedGun.Type, selectedGun.Level]);
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
            if (!selectingGunPlace) return;

            var screenPosition = e.GetPosition(contentGrid);
            screenPosition.X -= Gun.Radius * radialScale;
            screenPosition.Y -= Gun.Radius * radialScale;

            var p = (screenPosition - new Point(contentGrid.ActualWidth / 2, contentGrid.ActualHeight / 2)) / radialScale;
            var polarCoordinates = new PolarCoordinates(Math.Sqrt(p.X * p.X + p.Y * p.Y), Math.Atan2(p.Y, p.X));
            if (polarCoordinates.φ < 0)
                polarCoordinates.φ += 2 * Math.PI;

            logic.MessageBox.Add(GameLogic.Message.NewGun(selectedGun.Type, selectedGun.Level, polarCoordinates));
        }

        /// <summary>
        /// Called (remotely) by game logic thread when new gun had been successfully added.
        /// </summary>
        internal void gunAdded()
        {
            Debug.Assert(selectingGunPlace);
            selectingGunPlace = false;

            mineSelector.SelectedIndex = -1;
            lazerSelector.SelectedIndex = -1;
            machineGunSelector.SelectedIndex = -1;

            canvas.Children[0].Visibility = System.Windows.Visibility.Collapsed;
        }
    }
}
