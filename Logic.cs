/*
Zapoctovy program pro Programovani 2 2019/2020
Michal Tichy
Vlastni verze hry "Storm-Fighters" pro ZX Spectrum
*/
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.DirectX.AudioVideoPlayback;

/* Sound-effect sources:
    https://www.youtube.com/watch?v=Lo11dnWTyyQ
    https://www.youtube.com/watch?v=fO9tao41HcE
    https://www.youtube.com/watch?v=EXzoh6uJO1w
*/
namespace Storm_Fighters
{
    enum State { Game, End }
    enum Sound { Explosion, Firing, PlayerExplosion }
    enum DestroyState { Destroyed, NotDestroyed }
    abstract class MovingObject
    {
        protected Rectangle sprite; // its sprite on canvas
        protected Canvas myCanvas; // parent canvas
        protected Game game; // parent game
        protected int speedX, speedY;
        static readonly protected int speed = 4; // base speed
        // Intervals for Clocks
        static readonly protected uint shortWait = 2;
        static readonly protected uint mediumWait = 5;
        static readonly protected uint longWait = 50;
        public virtual Rect RectSprite // Rect object for hit detection
        {
            get { return new Rect(Canvas.GetLeft(sprite), Canvas.GetTop(sprite), sprite.Width, sprite.Height); }
        }
        public MovingObject(Canvas myCanvas, Game game)
        {
            this.myCanvas = myCanvas;
            this.game = game;
            this.game.MovingObjects.Add(this);
        }
        public virtual void Remove()
        {
            myCanvas.Children.Remove(sprite);
            game.MovingObjects.Remove(this);
        }
        public virtual void TextureSprite(string source)
        {
            // Fill the shape with an image from source
            ImageBrush skin = new ImageBrush
            {
                ImageSource = new BitmapImage(new Uri(source))
            };
            sprite.Fill = skin;
        }
        public abstract void Move();
    }
    abstract class FiringObject : MovingObject
    {
        // fireClock is used to limit how quickly an object can fire
        protected ulong fireClock;
        public FiringObject(Canvas myCanvas, Game game) : base(myCanvas, game)
        {
            this.game.FiringObjects.Add(this);
            fireClock = 0;
        }
        public override void Remove()
        {
            base.Remove();
            game.FiringObjects.Remove(this);
        }
        public abstract void Fire();
    }
    interface IDestructible
    {
        // Objects that are able to be destroyed by bullets (Everything except Asteroids)
        void Destroy();
    }
    interface IHitable
    {
        // Objects which change their behaviour when hit
        void IsHit(List<Hitbox> Hitboxes);
    }
    readonly struct Hitbox
    {
        // Pairing of an object and its hitbox
        public readonly Rect box;
        public readonly MovingObject obj;
        public Hitbox(Rect hitbox, MovingObject obj)
        {
            this.box = hitbox;
            this.obj = obj;
        }
    }

    class Asteroid : MovingObject
    {
        public Asteroid(double posX, double posY, Canvas myCanvas, Game game) : base(myCanvas, game)
        {
            // create an appropriate rectangle on canvas 
            sprite = new Rectangle
            {
                Tag = "Asteroid",
                Height = 15,
                Width = 18
            };
            // set the rectangle on the desired location
            Canvas.SetTop(sprite, posY);
            Canvas.SetLeft(sprite, posX);

            TextureSprite("pack://application:,,,/images/Asteroid.png");

            myCanvas.Children.Add(sprite);

            speedX = 0;
            speedY = 3 * speed;
        }
        public override void Move()
        {
            Canvas.SetTop(sprite, Canvas.GetTop(sprite) + speedY);
            // If the asteroid goes off screen
            if (Canvas.GetTop(sprite) > myCanvas.ActualHeight)
            {
                game.ToRemove.Add(this);
            }
        }

    }
    class Bullet : MovingObject, IDestructible
    {
        public Bullet(double posX, double posY, int direction, Canvas myCanvas, Game game) : base(myCanvas, game)
        {
            sprite = new Rectangle()
            {
                Tag = "Bullet",
                Height = 16,
                Width = 6,
                Fill = Brushes.White
            };
            // direction: -1 -> bullet moves towards the top, 1 -> bullet moves towards the bottom
            Canvas.SetTop(sprite, posY + (direction * (sprite.Height + 1))); // +1 so that bullet does not intersect with the object that fired it
            Canvas.SetLeft(sprite, posX);

            this.myCanvas.Children.Add(sprite);

            speedX = 0;
            speedY = 6 * speed * direction;

            game.PlaySound(Sound.Firing);

            this.game.Bullets.Add(this);
        }
        public override void Move()
        {
            Canvas.SetTop(sprite, Canvas.GetTop(sprite) + speedY);
            // If object gets out of rendered portion of canvas then we delete it
            if (Canvas.GetTop(sprite) < 0 || Canvas.GetTop(sprite) > myCanvas.ActualHeight)
            {
                game.ToRemove.Add(this);
            }
        }
        public void Hits(List<Hitbox> Hitboxes)
        {
            // Checks whether bullet hit any object and if so, then destroys bullet and potentionaly object
            // check if bullet is from the player (wether the player was hit by an enemy bullet is handled by player.IsHit()) -> No friendly fire among enemies 
            if (speedY < 0)
            {
                // Make Rect object with the same properties to use IntersectsWith method
                Rect bullet = RectSprite;
                foreach (Hitbox hitbox in Hitboxes)
                {
                    // if they intersect and the two refernces do not point to the same object
                    if (bullet.IntersectsWith(hitbox.box) && !Object.Equals(this, hitbox.obj))
                    {
                        Destroy();
                        // Check wether obj is destructible (only one cast compared to if(obj is IDestructible) and ((IDestructible)obj).Destroy() https://secondboyet.com/Articles/DoubleCastingAntiPattern.html)
                        IDestructible test = hitbox.obj as IDestructible;
                        if (test != null)
                        {
                            test.Destroy();
                            return;
                        }
                    }
                }
            }
        }
        public override void Remove()
        {
            base.Remove();
            game.Bullets.Remove(this);
        }
        public void Destroy()
        {
            game.ToRemove.Add(this);
        }
    }
    class TieFighter : FiringObject, IDestructible, IHitable
    {
        ulong destroyClock;
        DestroyState state;
        public TieFighter(double posX, double posY, int direction, Canvas myCanvas, Game game) : base(myCanvas, game)
        {
            sprite = new Rectangle
            {
                Tag = "TieFighter",
                Height = 24,
                Width = 47
            };
            Canvas.SetTop(this.sprite, posY);
            Canvas.SetLeft(this.sprite, posX);

            TextureSprite("pack://application:,,,/images/TieFighter.png");
            this.myCanvas.Children.Add(sprite);
            this.game.Hitables.Add(this);
            // direction determines wether the Fighter goes left or right
            speedX = 3 * speed * direction;
            speedY = 3 * speed;
            destroyClock = 0;
            state = DestroyState.NotDestroyed;
        }
        void ChangeDirection()
        {
            speedX *= -1;
        }
        public void Move(int scalarX, int scalarY)
        {
            // Shorlty wait while in a destroyed state and then remove
            if (game.GameClock == destroyClock + shortWait)
            {
                game.ToRemove.Add(this);
            }
            Canvas.SetLeft(sprite, Canvas.GetLeft(sprite) + scalarX * speedX);
            Canvas.SetTop(sprite, Canvas.GetTop(sprite) + scalarY * speedY);
            if (Canvas.GetTop(sprite) > myCanvas.ActualHeight)
            {
                game.ToRemove.Add(this);
            }
            // Warp from one side of the canvas to the other
            if (Canvas.GetLeft(sprite) > myCanvas.ActualWidth)
            {
                Canvas.SetLeft(sprite, 0);
            }
            else if (Canvas.GetLeft(sprite) < 0)
            {
                Canvas.SetLeft(sprite, myCanvas.ActualWidth);
            }
        }
        public override void Move()
        {
            Move(1, 1);
        }
        public void IsHit(List<Hitbox> Hitboxes)
        {
            // Change direction if it intersecs with another object
            Rect fighter = RectSprite;
            foreach (Hitbox hitbox in Hitboxes)
            {
                // If they intersect and the two refernces do not point to the same object
                if (fighter.IntersectsWith(hitbox.box) && !Object.ReferenceEquals(this, hitbox.obj))
                {
                    // Resolve collision
                    while (fighter.IntersectsWith(hitbox.box) && speedX != 0)
                    {
                        Move(-1, 0);
                        fighter = RectSprite;
                    }
                    ChangeDirection();
                    break;
                }
            }
        }
        public override void Fire()
        {
            // If the player is under him then Fire a bullet
            double playerPosX = game.player.PosX;
            double fighterPosX = Canvas.GetLeft(sprite) + (sprite.Width / 2);
            if (game.GameClock > fireClock + mediumWait && playerPosX < fighterPosX && fighterPosX < playerPosX + game.player.Width)
            {
                _ = new Bullet(fighterPosX, Canvas.GetTop(sprite) + sprite.Height, 1, myCanvas, game);
                fireClock = game.GameClock;
            }
        }
        public override void Remove()
        {
            base.Remove();
            game.Hitables.Remove(this);
        }
        public void Destroy()
        {
            if (state == DestroyState.NotDestroyed)
            {
                // Destroy Tie-Fighter
                TextureSprite("pack://application:,,,/images/TieFighterDestroy.png");
                // Add points and play sound if this is the first hit
                game.AddPoints = true;
                game.PlaySound(Sound.Explosion);
                destroyClock = game.GameClock;
                state = DestroyState.Destroyed;
                // Stop moving
                speedX = speedY = 0;
            }
        }
    }
    class Saucer : FiringObject, IDestructible
    {
        ulong destroyClock;
        DestroyState state;
        public Saucer(double posY, Canvas myCanvas, Game game) : base(myCanvas, game)
        {
            sprite = new Rectangle()
            {
                Tag = "Saucer",
                Height = 27,
                Width = 54
            };
            Canvas.SetTop(sprite, posY);
            Canvas.SetLeft(sprite, 0);

            TextureSprite("pack://application:,,,/images/Saucer.png");
            myCanvas.Children.Add(sprite);

            speedX = 3 * speed;
            speedY = 0;
            destroyClock = 0;
            state = DestroyState.NotDestroyed;
        }
        public Saucer(double posX, double posY, Canvas myCanvas, Game game) : this(posY, myCanvas, game)
        {
            Canvas.SetLeft(sprite, posX);
        }
        public override void Move()
        {
            if (game.GameClock == destroyClock + shortWait)
            {
                game.ToRemove.Add(this);
            }
            Canvas.SetLeft(sprite, Canvas.GetLeft(sprite) + speedX);
            if (Canvas.GetLeft(sprite) > myCanvas.ActualWidth)
            {
                game.ToRemove.Add(this);
            }
        }
        public override void Fire()
        {
            // Fire bullet in the center of the sprite
            if (game.GameClock > fireClock + 2 * mediumWait)
            {
                _ = new Bullet(Canvas.GetLeft(sprite) + sprite.Width / 2, Canvas.GetTop(sprite) + sprite.Height, 1, myCanvas, game);
                fireClock = game.GameClock;
            }
        }
        public void Destroy()
        {
            if (state == DestroyState.NotDestroyed)
            {
                // Destroy Saucer (change sprite to destroyed texture, play explosion sound etc.)
                state = DestroyState.Destroyed;
                TextureSprite("pack://application:,,,/images/SaucerDestroy.png");
                // Add points and play sound if this is the first hit
                game.AddPoints = true;
                game.PlaySound(Sound.Explosion);
                destroyClock = game.GameClock;
                // Stop moving
                speedX = speedY = 0;
            }
        }
    }
    class Player : FiringObject, IDestructible, IHitable
    {
        bool goLeft, goRight, fireBullet;
        public bool GoLeft
        {
            set { goLeft = value; }
        }
        public bool GoRight
        {
            set { goRight = value; }
        }
        public bool FireBullet
        {
            set { fireBullet = value; }
        }
        public double PosX
        {
            get { return Canvas.GetLeft(sprite); }
        }
        public double PosY
        {
            get { return Canvas.GetTop(sprite); }
        }
        public double Width
        {
            get { return sprite.Width; }
        }
        public override Rect RectSprite
        {
            // Make player hitbox smaller than it is to make the game easier
            get { return new Rect(Canvas.GetLeft(sprite) + 10, Canvas.GetTop(sprite) + 10, sprite.Width - 20, sprite.Height - 25); }
        }
        public Player(double posY, Canvas myCanvas, Game game) : base(myCanvas, game)
        {
            // add player box to canvas
            sprite = new Rectangle
            {
                Tag = "Player",
                Height = 45,
                Width = 48,
            };
            Canvas.SetTop(sprite, myCanvas.ActualHeight - posY);
            Canvas.SetLeft(sprite, (myCanvas.ActualWidth - sprite.Width) / 2); // player is centred at the beginning
            TextureSprite("pack://application:,,,/images/Player.png");
            // add player to canvas
            this.myCanvas.Children.Add(sprite);

            speedX = 5 * speed;
            speedY = 0;
            goLeft = goRight = fireBullet = false;
        }
        public Player(double posX, double posY, Canvas myCanvas, Game game) : this(posY, myCanvas, game)
        {
            Canvas.SetLeft(sprite, posX);
        }
        public override void Move()
        {
            // Move player ship if it is within bounds
            if (goLeft && Canvas.GetLeft(sprite) > 30)
            {
                Canvas.SetLeft(sprite, Canvas.GetLeft(sprite) - speedX);
            }
            if (goRight && Canvas.GetLeft(sprite) + sprite.Width < myCanvas.ActualWidth - 30)
            {
                Canvas.SetLeft(sprite, Canvas.GetLeft(sprite) + speedX);
            }
        }
        public override void Fire()
        {
            // create two bullets at either end of sprite that travel bottom -> up (-1)
            if (fireBullet && game.GameClock > fireClock + shortWait)
            {
                _ = new Bullet(Canvas.GetLeft(sprite), Canvas.GetTop(sprite), -1, myCanvas, game);
                _ = new Bullet(Canvas.GetLeft(sprite) + sprite.Width - 6, Canvas.GetTop(sprite), -1, myCanvas, game);
                fireBullet = false;
                fireClock = game.GameClock;
            }
        }
        public void IsHit(List<Hitbox> Hitboxes)
        {
            // Make Rect object with the same properties to use IntersectsWith method
            Rect player = RectSprite;
            foreach (Hitbox hitbox in Hitboxes)
            {
                // if they intersect and the two refernces do not point to the same object
                if (player.IntersectsWith(hitbox.box) && !Object.ReferenceEquals(this, hitbox.obj))
                {
                    Destroy();
                    return;
                }
            }
        }
        public void Destroy()
        {
            // Destroy player, start a new life and return false (don't add points to score)
            game.PlaySound(Sound.PlayerExplosion);
            game.NewLife();
        }
    }
    class Game
    {
        readonly MainWindow myWindow;
        readonly Canvas myCanvas;
        // Dimensions of the canvas used
        readonly double top, bottom;
        readonly List<Hitbox> Hitboxes;
        readonly Random rand;
        readonly Audio explosionSE, firingSE, playerExplosionSE;
        State state;
        ulong gameClock, saucerSpawnClock, fighterSpawnClock;
        int score, lives, totalScore;
        bool addPoints;

        static readonly uint mediumWait = 5;
        static readonly uint longWait = 50;

        public List<MovingObject> MovingObjects;
        public List<FiringObject> FiringObjects;
        public List<Bullet> Bullets;
        public List<IHitable> Hitables;
        public List<MovingObject> ToRemove;

        public Player player;

        public ulong GameClock
        {
            get { return gameClock; }
        }
        public State State
        {
            get { return state; }
        }
        public bool AddPoints
        {
            set { addPoints = value; }
        }
        public Game(Canvas myCanvas)
        {
            this.myCanvas = myCanvas;
            myWindow = Application.Current.Windows[0] as MainWindow;
            // Sound
            string basePath = AppDomain.CurrentDomain.BaseDirectory;
            explosionSE = new Audio(basePath + @"soundEffects\Explosion.wav");
            firingSE = new Audio(basePath + @"soundEffects\BulletSound.wav");
            playerExplosionSE = new Audio(basePath + @"soundEffects\PlayerExplosion.wav");
            playerExplosionSE.Play();

            score = totalScore = 0;
            lives = 3;
            bottom = 60;
            top = 25;
            gameClock = saucerSpawnClock = fighterSpawnClock = 0;
            addPoints = false;
            myWindow.LivesText.Content = "Lives: " + lives.ToString();
            myWindow.ScoreText.Content = "Score: " + score.ToString();
            state = State.Game;
            MovingObjects = new List<MovingObject>();
            FiringObjects = new List<FiringObject>();
            ToRemove = new List<MovingObject>();
            Bullets = new List<Bullet>(); // To check wether they intersect with a destructable object
            Hitboxes = new List<Hitbox>(); // Generate a list of hitboxes for all moving objects
            Hitables = new List<IHitable>();
            rand = new Random();

            player = new Player(bottom, this.myCanvas, this);
        }
        void SpawnEnemy()
        {   // Whether to spawn an enemy
            int num = rand.Next(100);
            if (num < (10 + (score + 10) / 20))
            {   // Force spawn Tie-Fighter
                if (fighterSpawnClock + longWait > gameClock)
                {
                    num = rand.Next(100);
                }
                else
                {
                    num = 10;
                }
                // Which enemy to spawn
                switch (num)
                {
                    case int n when (n > 18):
                        _ = new Asteroid(rand.Next(20, Convert.ToInt32(myCanvas.ActualWidth - 20)), top, myCanvas, this);
                        break;
                    case int n when (n <= 18 && n > 1):
                        if (gameClock > fighterSpawnClock + mediumWait)
                        {
                            fighterSpawnClock = gameClock;
                            //randomly choose direction
                            num = rand.Next(2) == 0 ? 1 : -1;
                            _ = new TieFighter(rand.Next(20, Convert.ToInt32(myCanvas.ActualWidth - 20)), top, num, myCanvas, this);
                        }
                        break;
                    case int n when (n <= 1):
                        if (gameClock > saucerSpawnClock + longWait)
                        {
                            saucerSpawnClock = gameClock;
                            _ = new Saucer(top, myCanvas, this);
                        }
                        break;
                }
            }
        }
        public void PlaySound(Sound tag)
        {
            // Play appropriate sound
            switch (tag)
            {
                case Sound.Explosion:
                    explosionSE.Stop();
                    explosionSE.Play();
                    break;
                case Sound.Firing:
                    firingSE.Stop();
                    firingSE.Play();
                    break;
                case Sound.PlayerExplosion:
                    playerExplosionSE.Stop();
                    playerExplosionSE.Play();
                    break;
            }
        }
        public void DisposeSound()
        {
            explosionSE.Dispose();
            playerExplosionSE.Dispose();
            firingSE.Dispose();
        }
        public void NewLife()
        {
            // Start a new life
            // Clean-up all objects on the canvas
            foreach (MovingObject obj in MovingObjects)
            {
                ToRemove.Add(obj);
            }
            gameClock = saucerSpawnClock = fighterSpawnClock = 0;
            // change fields
            lives--;
            myWindow.LivesText.Content = "Lives: " + lives.ToString();

            totalScore += score;
            score = 0;
            myWindow.ScoreText.Content = "Score: 0";
            myWindow.EndScoreText.Content = "Score: " + totalScore.ToString();

            if (lives == 0)
            {
                state = State.End;
            }
            else
            {
                player = new Player(bottom, myCanvas, this);
            }
        }
        public void GameLoop()
        {
            gameClock++;
            // Action
            foreach (MovingObject obj in MovingObjects)
            {
                obj.Move();
            }
            foreach (FiringObject obj in FiringObjects)
            {
                obj.Fire();
            }
            // Hit detection
            // Create all the Rect objects only once
            foreach (MovingObject obj in MovingObjects)
            {
                Hitboxes.Add(new Hitbox(obj.RectSprite, obj));
            }
            foreach (Bullet obj in Bullets)
            {
                // Determine whether bullet hits something and whether to add points for this hit  
                obj.Hits(Hitboxes);
                if (addPoints)
                {
                    score += 10;
                    myWindow.ScoreText.Content = "Score: " + score.ToString();
                    addPoints = false;
                }
            }
            // Loop checking if a bullet hits something (previous loop) must be before this one. (This is because of the hit resolution of the Tie-Fighter)
            foreach (IHitable obj in Hitables)
            {
                obj.IsHit(Hitboxes);
            }
            player.IsHit(Hitboxes);

            if (gameClock > longWait) // wait before spawning enemies
            {
                SpawnEnemy();
            }
            // Clean-up
            foreach (MovingObject obj in ToRemove)
            {
                obj.Remove();
            }
            ToRemove.Clear();
            Hitboxes.Clear();
        }
    }
}