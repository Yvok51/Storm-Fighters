/*
Zapoctovy program pro Programovani 2 2019/2020
Michal Tichy
Vlastni verze hry "Storm-Fighters" pro ZX Spectrum
*/
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Storm_Fighters
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        readonly DispatcherTimer timer;
        Game game;
        bool spaceIsPressed;
        public MainWindow()
        {
            InitializeComponent();
            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(40)
            };
            timer.Tick += new EventHandler(GameEngine); // every tick GameLoop will be called

            ScoreText.Visibility = LivesText.Visibility = TryButton.Visibility = ExitButton.Visibility =
                GameOverText.Visibility = EndScoreText.Visibility = Visibility.Collapsed;

            // Background for the canvas
            ImageBrush ib = new ImageBrush
            {
                ImageSource = new BitmapImage(new Uri("pack://application:,,,/images/background2.png")),
                TileMode = TileMode.Tile,
                Viewport = new Rect(0, 0, 63, 47),
                ViewportUnits = BrushMappingMode.Absolute
            };
            MyCanvas.Background = ib;

            spaceIsPressed = false;
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            // Starts a new game
            this.game = new Game(MyCanvas);
            timer.Start();

            StartButton.Visibility = Visibility.Collapsed;
            ScoreText.Visibility = LivesText.Visibility = Visibility.Visible;
        }
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Left)
            {
                game.player.GoLeft = true;
            }
            if (e.Key == Key.Right)
            {
                game.player.GoRight = true;
            }
            // (!spaceIsPressed) -> Player fires only once per a press of the spacebar
            if (e.Key == Key.Space && !spaceIsPressed)
            {
                spaceIsPressed = true;
                game.player.FireBullet = true;
            }
        }
        private void OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Left)
            {
                game.player.GoLeft = false;
            }
            if (e.Key == Key.Right)
            {
                game.player.GoRight = false;
            }
            if (e.Key == Key.Space)
            {
                spaceIsPressed = false;
            }
        }
        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
        private void TryButton_Click(object sender, RoutedEventArgs e)
        {
            // Previous game is discarded and a new game begins
            game = null;
            TryButton.Visibility = ExitButton.Visibility = GameOverText.Visibility = EndScoreText.Visibility = Visibility.Collapsed;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            StartButton_Click(sender, e);
        }
        private void GameEngine(object sender, EventArgs e)
        {
            switch (game.State)
            {
                case State.Game:
                    game.GameLoop();
                    break;
                case State.End:
                    timer.Stop();
                    LivesText.Visibility = ScoreText.Visibility = Visibility.Collapsed;
                    TryButton.Visibility = ExitButton.Visibility = GameOverText.Visibility = EndScoreText.Visibility = Visibility.Visible;
                    break;
            }
        }

        private void MyCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Handler for when the size of MyCanvas is changed
            // Try to keep the placement same
            double ratioX, ratioY;
            if (e.PreviousSize.Width != 0)
            {
                ratioX = e.NewSize.Width / e.PreviousSize.Width;
            }
            else
            {
                ratioX = 1;
            }
            if (e.PreviousSize.Height != 0)
            {
                ratioY = e.NewSize.Height / e.PreviousSize.Height;
            }
            else
            {
                ratioY = 1;
            }
            foreach (UIElement obj in MyCanvas.Children)
            {
                Canvas.SetLeft(obj, Canvas.GetLeft(obj) * ratioX);
                Canvas.SetTop(obj, Canvas.GetTop(obj) * ratioY);
            }
        }
    }
}
