using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace FoodRealms
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new GameForm());
        }
    }

    enum GameMode
    {
        Menu,
        Playing,
        Inventory,
        Shop,
        Journal,
        Paused,
        GameOver,
        Victory
    }

    class GameForm : Form
    {
        const int TileW = 86;
        const int TileH = 43;
        const float TileLift = 38f;

        readonly Timer timer;
        readonly bool[] keys = new bool[512];
        readonly Random rng = new Random(72);
        readonly List<LevelData> levels = new List<LevelData>();
        readonly List<Enemy> enemies = new List<Enemy>();
        readonly List<Prop> props = new List<Prop>();
        readonly List<Pickup> pickups = new List<Pickup>();
        readonly List<Projectile> projectiles = new List<Projectile>();
        readonly List<Particle> particles = new List<Particle>();
        readonly List<Toast> toasts = new List<Toast>();
        readonly Dictionary<string, Bitmap> textures = new Dictionary<string, Bitmap>();
        readonly Dictionary<string, TextureBrush> brushes = new Dictionary<string, TextureBrush>();

        Player player;
        GameMode mode = GameMode.Menu;
        DateTime lastTick;
        int levelIndex;
        float timeOfDay = 0.20f;
        float weatherClock;
        float portalPulse;
        bool raining;
        bool bossDefeated;
        bool introPulse;
        int killsThisLevel;
        int ingredientsThisLevel;
        int totalBosses;

        Font titleFont;
        Font h1Font;
        Font h2Font;
        Font uiFont;
        Font smallFont;

        public GameForm()
        {
            Text = "Food Realms - Nicolas Herguera";
            ClientSize = new Size(1280, 720);
            MinimumSize = new Size(1040, 620);
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            KeyPreview = true;

            titleFont = new Font("Segoe UI", 31, FontStyle.Bold);
            h1Font = new Font("Segoe UI", 18, FontStyle.Bold);
            h2Font = new Font("Segoe UI", 12, FontStyle.Bold);
            uiFont = new Font("Segoe UI", 10, FontStyle.Regular);
            smallFont = new Font("Segoe UI", 8, FontStyle.Regular);

            BuildTextures();
            BuildLevels();
            ResetCampaign();

            timer = new Timer();
            timer.Interval = 16;
            timer.Tick += Tick;
            lastTick = DateTime.Now;
            timer.Start();

            KeyDown += OnKeyDown;
            KeyUp += OnKeyUp;
            Resize += delegate { Invalidate(); };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (TextureBrush brush in brushes.Values)
                    brush.Dispose();
                foreach (Bitmap bmp in textures.Values)
                    bmp.Dispose();
                if (titleFont != null) titleFont.Dispose();
                if (h1Font != null) h1Font.Dispose();
                if (h2Font != null) h2Font.Dispose();
                if (uiFont != null) uiFont.Dispose();
                if (smallFont != null) smallFont.Dispose();
                if (timer != null) timer.Dispose();
            }
            base.Dispose(disposing);
        }

        void Tick(object sender, EventArgs e)
        {
            DateTime now = DateTime.Now;
            float dt = (float)(now - lastTick).TotalSeconds;
            lastTick = now;
            if (dt > 0.06f) dt = 0.06f;

            introPulse = !introPulse;
            portalPulse += dt * 2.6f;

            if (mode == GameMode.Playing)
            {
                UpdateGame(dt);
            }
            else
            {
                UpdateAmbient(dt);
            }

            Invalidate();
        }

        void OnKeyDown(object sender, KeyEventArgs e)
        {
            int code = (int)e.KeyCode;
            if (code >= 0 && code < keys.Length) keys[code] = true;

            if (mode == GameMode.Menu)
            {
                if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
                {
                    mode = GameMode.Playing;
                    AddToast("La aventura empieza: salva los Reinos de la Comida.");
                }
                return;
            }

            if (mode == GameMode.GameOver)
            {
                if (e.KeyCode == Keys.R) ResetCampaign();
                return;
            }

            if (mode == GameMode.Victory)
            {
                if (e.KeyCode == Keys.R) ResetCampaign();
                return;
            }

            if (mode == GameMode.Paused)
            {
                if (e.KeyCode == Keys.Escape || e.KeyCode == Keys.Enter) mode = GameMode.Playing;
                return;
            }

            if (mode == GameMode.Shop)
            {
                HandleShopKey(e.KeyCode);
                return;
            }

            if (mode == GameMode.Inventory)
            {
                if (e.KeyCode == Keys.I || e.KeyCode == Keys.Escape) mode = GameMode.Playing;
                if (e.KeyCode == Keys.H) UsePotion();
                return;
            }

            if (mode == GameMode.Journal)
            {
                if (e.KeyCode == Keys.J || e.KeyCode == Keys.Escape) mode = GameMode.Playing;
                return;
            }

            if (e.KeyCode == Keys.Escape) mode = GameMode.Paused;
            if (e.KeyCode == Keys.I) mode = GameMode.Inventory;
            if (e.KeyCode == Keys.J) mode = GameMode.Journal;
            if (e.KeyCode == Keys.E) TryOpenShop();
            if (e.KeyCode == Keys.Space) MeleeAttack();
            if (e.KeyCode == Keys.D1) CastWokFlame();
            if (e.KeyCode == Keys.D2) CastTeaMend();
            if (e.KeyCode == Keys.D3) CastFrostSorbet();
            if (e.KeyCode == Keys.D4) CastCurryNova();
            if (e.KeyCode == Keys.H) UsePotion();
            if (e.KeyCode == Keys.N) TryTravelPortal();
        }

        void OnKeyUp(object sender, KeyEventArgs e)
        {
            int code = (int)e.KeyCode;
            if (code >= 0 && code < keys.Length) keys[code] = false;
        }

        void BuildLevels()
        {
            levels.Add(new LevelData("Pradera del Desayuno", "Granjas, pan tostado, fruta y charcos de miel", "breakfast", "Harina antigua",
                "Chef Toston", Color.FromArgb(111, 177, 118), Color.FromArgb(32, 92, 142), "Derrota al guardian de la primera mesa.", false));
            levels.Add(new LevelData("Mercado Azul de Bento", "Faroles, vapor, arroz, dumplings y puestos de ramen", "market", "Arroz azul",
                "General Gyoza", Color.FromArgb(81, 168, 207), Color.FromArgb(24, 75, 129), "Consigue arroz azul y abre la ruta del este.", true));
            levels.Add(new LevelData("Bosque de Pizza y Pan", "Caminos de masa, queso fundido y hornos de piedra", "pizza", "Queso estelar",
                "Titan Mozzarella", Color.FromArgb(178, 135, 72), Color.FromArgb(71, 76, 118), "Rescata los hornos del bosque.", false));
            levels.Add(new LevelData("Templo de Sushi Lunar", "Arrecifes de alga nori, arroz brillante y lluvia fina", "sushi", "Perla nori",
                "Ronin Wasabi", Color.FromArgb(54, 151, 146), Color.FromArgb(21, 61, 99), "Purifica el templo de sushi.", true));
            levels.Add(new LevelData("Canon del Taco y Curry", "Especias, maiz, curry dorado y ruinas de sal", "curry", "Chile solar",
                "Reina Mole", Color.FromArgb(194, 133, 50), Color.FromArgb(85, 56, 104), "Domina la tormenta de especias.", false));
            levels.Add(new LevelData("Palacio del Ramen Eterno", "Rios de caldo, puentes de fideo y cielo azul nocturno", "ramen", "Caldo eterno",
                "Dragon Noodle", Color.FromArgb(91, 170, 214), Color.FromArgb(16, 47, 90), "Vence al dragon final y firma tu leyenda.", true));
        }

        void BuildTextures()
        {
            AddTexture("breakfast", Color.FromArgb(137, 190, 96), Color.FromArgb(250, 220, 132), Color.FromArgb(113, 93, 47), 1);
            AddTexture("market", Color.FromArgb(180, 224, 238), Color.FromArgb(82, 164, 214), Color.FromArgb(255, 238, 188), 2);
            AddTexture("pizza", Color.FromArgb(226, 183, 89), Color.FromArgb(202, 89, 55), Color.FromArgb(255, 238, 150), 3);
            AddTexture("sushi", Color.FromArgb(220, 238, 226), Color.FromArgb(50, 147, 139), Color.FromArgb(32, 68, 61), 4);
            AddTexture("curry", Color.FromArgb(229, 170, 57), Color.FromArgb(171, 78, 37), Color.FromArgb(255, 224, 129), 5);
            AddTexture("ramen", Color.FromArgb(213, 158, 75), Color.FromArgb(249, 214, 119), Color.FromArgb(148, 77, 35), 6);
            AddTexture("stone", Color.FromArgb(142, 151, 158), Color.FromArgb(81, 92, 106), Color.FromArgb(210, 224, 236), 7);
            AddTexture("water", Color.FromArgb(79, 171, 210), Color.FromArgb(33, 92, 151), Color.FromArgb(226, 250, 255), 8);
            AddTexture("wood", Color.FromArgb(161, 102, 48), Color.FromArgb(93, 54, 28), Color.FromArgb(220, 164, 92), 9);
            AddTexture("cloth", Color.FromArgb(213, 238, 255), Color.FromArgb(81, 165, 225), Color.FromArgb(255, 255, 255), 10);
        }

        void AddTexture(string key, Color a, Color b, Color accent, int style)
        {
            Bitmap bmp = new Bitmap(128, 128);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (LinearGradientBrush grad = new LinearGradientBrush(new Rectangle(0, 0, 128, 128), a, b, 45))
                    g.FillRectangle(grad, 0, 0, 128, 128);

                Random local = new Random(style * 1009);
                int i;
                for (i = 0; i < 500; i++)
                {
                    int x = local.Next(128);
                    int y = local.Next(128);
                    int alpha = local.Next(12, 45);
                    using (SolidBrush dot = new SolidBrush(Color.FromArgb(alpha, local.Next(2) == 0 ? Color.White : Color.Black)))
                        g.FillRectangle(dot, x, y, 1, 1);
                }

                if (style == 1)
                {
                    for (i = 0; i < 70; i++)
                    {
                        int x = local.Next(128);
                        int y = local.Next(128);
                        using (Pen p = new Pen(Color.FromArgb(80, accent), 1))
                            g.DrawLine(p, x, y + 5, x + local.Next(-3, 4), y);
                    }
                    using (Pen honey = new Pen(Color.FromArgb(105, 244, 197, 74), 4))
                        g.DrawBezier(honey, 5, 72, 35, 62, 70, 86, 124, 70);
                }
                else if (style == 2)
                {
                    using (Pen grid = new Pen(Color.FromArgb(70, Color.White), 2))
                    {
                        for (i = 0; i < 128; i += 24)
                        {
                            g.DrawLine(grid, i, 0, i - 58, 128);
                            g.DrawLine(grid, i, 0, i + 58, 128);
                        }
                    }
                    using (SolidBrush rice = new SolidBrush(Color.FromArgb(120, Color.White)))
                    {
                        for (i = 0; i < 70; i++)
                            g.FillEllipse(rice, local.Next(128), local.Next(128), 3, 2);
                    }
                }
                else if (style == 3)
                {
                    using (SolidBrush cheese = new SolidBrush(Color.FromArgb(90, Color.FromArgb(255, 238, 144))))
                        g.FillEllipse(cheese, 18, 20, 95, 76);
                    using (SolidBrush pepperoni = new SolidBrush(Color.FromArgb(135, 180, 44, 42)))
                    {
                        for (i = 0; i < 11; i++)
                            g.FillEllipse(pepperoni, local.Next(8, 108), local.Next(8, 108), 12, 12);
                    }
                }
                else if (style == 4)
                {
                    using (Pen seaweed = new Pen(Color.FromArgb(130, accent), 7))
                    {
                        for (i = 8; i < 128; i += 23)
                            g.DrawLine(seaweed, 0, i, 128, i + local.Next(-8, 9));
                    }
                    using (SolidBrush rice = new SolidBrush(Color.FromArgb(150, Color.White)))
                    {
                        for (i = 0; i < 110; i++)
                            g.FillEllipse(rice, local.Next(128), local.Next(128), 2, 2);
                    }
                }
                else if (style == 5)
                {
                    using (Pen spice = new Pen(Color.FromArgb(95, accent), 5))
                    {
                        for (i = 0; i < 7; i++)
                            g.DrawBezier(spice, -10, local.Next(128), 36, local.Next(128), 87, local.Next(128), 138, local.Next(128));
                    }
                    using (SolidBrush chile = new SolidBrush(Color.FromArgb(120, 156, 32, 24)))
                    {
                        for (i = 0; i < 16; i++)
                            g.FillEllipse(chile, local.Next(128), local.Next(128), 8, 4);
                    }
                }
                else if (style == 6)
                {
                    using (Pen noodle = new Pen(Color.FromArgb(145, accent), 4))
                    {
                        for (i = 0; i < 13; i++)
                            g.DrawBezier(noodle, -5, local.Next(128), 35, local.Next(128), 80, local.Next(128), 135, local.Next(128));
                    }
                    using (SolidBrush egg = new SolidBrush(Color.FromArgb(130, 255, 238, 154)))
                        g.FillEllipse(egg, 82, 20, 32, 24);
                }
                else if (style == 7)
                {
                    using (Pen cracks = new Pen(Color.FromArgb(120, 38, 48, 57), 2))
                    {
                        for (i = 0; i < 18; i++)
                        {
                            int x = local.Next(128);
                            int y = local.Next(128);
                            g.DrawLine(cracks, x, y, x + local.Next(-26, 27), y + local.Next(-18, 19));
                        }
                    }
                }
                else if (style == 8)
                {
                    using (Pen wave = new Pen(Color.FromArgb(105, Color.White), 3))
                    {
                        for (i = 8; i < 128; i += 16)
                            g.DrawBezier(wave, -8, i, 25, i - 12, 60, i + 12, 136, i - 3);
                    }
                }
                else if (style == 9)
                {
                    using (Pen grain = new Pen(Color.FromArgb(80, accent), 2))
                    {
                        for (i = 0; i < 18; i++)
                            g.DrawBezier(grain, 0, local.Next(128), 33, local.Next(128), 80, local.Next(128), 128, local.Next(128));
                    }
                }
                else
                {
                    using (Pen stripe = new Pen(Color.FromArgb(130, Color.White), 7))
                    {
                        for (i = -128; i < 256; i += 34)
                            g.DrawLine(stripe, i, 0, i + 80, 128);
                    }
                }
            }

            textures[key] = bmp;
            TextureBrush brush = new TextureBrush(bmp, WrapMode.Tile);
            brushes[key] = brush;
        }

        void ResetCampaign()
        {
            player = new Player();
            player.X = 5;
            player.Z = 5;
            player.MaxHealth = 120;
            player.Health = 120;
            player.MaxMana = 100;
            player.Mana = 100;
            player.Speed = 5.2f;
            player.Attack = 23;
            player.MagicPower = 20;
            player.Armor = 0;
            player.Level = 1;
            player.Xp = 0;
            player.Coins = 45;
            player.Potions = 2;
            player.Items["Harina antigua"] = 0;
            player.Items["Arroz azul"] = 0;
            player.Items["Queso estelar"] = 0;
            player.Items["Perla nori"] = 0;
            player.Items["Chile solar"] = 0;
            player.Items["Caldo eterno"] = 0;
            player.Items["Llave del Chef"] = 0;
            levelIndex = 0;
            totalBosses = 0;
            mode = GameMode.Menu;
            LoadLevel(0);
        }

        void LoadLevel(int index)
        {
            levelIndex = index;
            enemies.Clear();
            props.Clear();
            pickups.Clear();
            projectiles.Clear();
            particles.Clear();
            toasts.Clear();
            bossDefeated = false;
            killsThisLevel = 0;
            ingredientsThisLevel = 0;
            player.X = 4.5f;
            player.Z = 4.5f;
            player.LastDirX = 1;
            player.LastDirZ = 0;
            timeOfDay = 0.18f + (index * 0.11f);
            raining = index == 3 || index == 5;
            weatherClock = 0;

            GenerateProps();
            GenerateEnemies();
            AddToast("Nivel " + (levelIndex + 1).ToString() + ": " + CurrentLevel().Name);
        }

        LevelData CurrentLevel()
        {
            return levels[levelIndex];
        }

        void GenerateProps()
        {
            LevelData level = CurrentLevel();
            int size = level.Size;
            int i;

            props.Add(new Prop(4, 7, "shop", true));
            props.Add(new Prop(size - 4, size - 4, "portal", false));

            for (i = 0; i < size; i++)
            {
                props.Add(new Prop(i, 0, "wall", true));
                props.Add(new Prop(0, i, "wall", true));
                props.Add(new Prop(i, size - 1, "wall", true));
                props.Add(new Prop(size - 1, i, "wall", true));
            }

            Random local = new Random(2000 + levelIndex * 97);
            int propCount = 58 + levelIndex * 7;
            for (i = 0; i < propCount; i++)
            {
                float x = 2 + (float)local.NextDouble() * (size - 5);
                float z = 2 + (float)local.NextDouble() * (size - 5);
                if (Distance(x, z, player.X, player.Z) < 5) continue;
                if (Distance(x, z, size - 4, size - 4) < 3) continue;
                string kind = PickPropKind(level.TextureKey, local);
                bool solid = kind != "lamp" && kind != "steam" && kind != "spice";
                props.Add(new Prop(x, z, kind, solid));
            }

            for (i = 0; i < 12; i++)
            {
                float x2 = 6 + i * 2.1f;
                float z2 = 9 + (float)Math.Sin(i * 0.9f) * 2.5f;
                if (x2 < size - 5 && z2 < size - 5)
                    props.Add(new Prop(x2, z2, "lamp", false));
            }
        }

        string PickPropKind(string theme, Random local)
        {
            string[] generic = new string[] { "crate", "bread", "fruit", "cheese", "steam", "lamp" };
            string[] breakfast = new string[] { "toast", "honey", "fruit", "crate", "lamp" };
            string[] market = new string[] { "bowl", "dumpling", "lantern", "tea", "steam", "crate" };
            string[] pizza = new string[] { "pizza", "oven", "cheese", "tomato", "crate", "bread" };
            string[] sushi = new string[] { "sushi", "nori", "tea", "lantern", "steam", "stone" };
            string[] curry = new string[] { "spice", "pepper", "curry", "salt", "crate", "lamp" };
            string[] ramen = new string[] { "bowl", "noodle", "egg", "lantern", "steam", "tea" };
            string[] pool = generic;
            if (theme == "breakfast") pool = breakfast;
            if (theme == "market") pool = market;
            if (theme == "pizza") pool = pizza;
            if (theme == "sushi") pool = sushi;
            if (theme == "curry") pool = curry;
            if (theme == "ramen") pool = ramen;
            return pool[local.Next(pool.Length)];
        }

        void GenerateEnemies()
        {
            LevelData level = CurrentLevel();
            Random local = new Random(5000 + levelIndex * 211);
            int count = 12 + levelIndex * 4;
            int i;
            for (i = 0; i < count; i++)
            {
                Enemy e = CreateEnemy(false, local);
                e.X = 8 + (float)local.NextDouble() * (level.Size - 13);
                e.Z = 8 + (float)local.NextDouble() * (level.Size - 13);
                e.SpawnX = e.X;
                e.SpawnZ = e.Z;
                enemies.Add(e);
            }

            Enemy boss = CreateEnemy(true, local);
            boss.Name = level.BossName;
            boss.X = level.Size - 7;
            boss.Z = level.Size - 8;
            boss.SpawnX = boss.X;
            boss.SpawnZ = boss.Z;
            enemies.Add(boss);
        }

        Enemy CreateEnemy(bool boss, Random local)
        {
            LevelData level = CurrentLevel();
            Enemy e = new Enemy();
            e.Boss = boss;
            string theme = level.TextureKey;
            if (theme == "breakfast")
                e.Name = Pick(local, "Miga salvaje", "Abeja de miel", "Toston armado");
            else if (theme == "market")
                e.Name = Pick(local, "Slime de soja", "Dumpling ninja", "Seta shiitake");
            else if (theme == "pizza")
                e.Name = Pick(local, "Diablillo pepperoni", "Caballero de masa", "Golem de queso");
            else if (theme == "sushi")
                e.Name = Pick(local, "Sombra nori", "Wasabi vivo", "Ronin maki");
            else if (theme == "curry")
                e.Name = Pick(local, "Chile furioso", "Nube curry", "Totem de maiz");
            else
                e.Name = Pick(local, "Samurai fideo", "Huevo lunar", "Sopa espectral");

            e.Color = EnemyColor(theme, local);
            e.Radius = boss ? 1.05f : 0.55f;
            e.MaxHp = boss ? 210 + levelIndex * 55 : 45 + levelIndex * 15;
            e.Hp = e.MaxHp;
            e.Speed = boss ? 1.8f + levelIndex * 0.08f : 2.2f + (float)local.NextDouble() * 0.9f + levelIndex * 0.05f;
            e.Damage = boss ? 18 + levelIndex * 3 : 8 + levelIndex * 2;
            e.Wander = (float)local.NextDouble() * 6.28f;
            return e;
        }

        Color EnemyColor(string theme, Random local)
        {
            if (theme == "breakfast") return Color.FromArgb(196, 134 + local.Next(70), 68);
            if (theme == "market") return Color.FromArgb(74, 164 + local.Next(55), 201);
            if (theme == "pizza") return Color.FromArgb(207, 72 + local.Next(50), 51);
            if (theme == "sushi") return Color.FromArgb(34, 126 + local.Next(60), 102);
            if (theme == "curry") return Color.FromArgb(213, 121 + local.Next(40), 37);
            return Color.FromArgb(208, 176, 79);
        }

        string Pick(Random local, params string[] values)
        {
            return values[local.Next(values.Length)];
        }

        void UpdateAmbient(float dt)
        {
            timeOfDay += dt * 0.004f;
            if (timeOfDay > 1) timeOfDay -= 1;
            UpdateParticles(dt);
        }

        void UpdateGame(float dt)
        {
            timeOfDay += dt * 0.010f;
            if (timeOfDay > 1) timeOfDay -= 1;
            weatherClock += dt;
            if (weatherClock > 28)
            {
                weatherClock = 0;
                if (rng.NextDouble() < 0.45) raining = !raining;
                if (raining) AddToast("Empieza a llover: las texturas brillan sobre el suelo.");
            }

            player.MeleeCooldown -= dt;
            player.WokCooldown -= dt;
            player.TeaCooldown -= dt;
            player.FrostCooldown -= dt;
            player.CurryCooldown -= dt;
            if (player.Mana < player.MaxMana) player.Mana += dt * 8.0f;
            if (player.Mana > player.MaxMana) player.Mana = player.MaxMana;

            UpdateMovement(dt);
            UpdateEnemies(dt);
            UpdateProjectiles(dt);
            UpdatePickups();
            UpdateParticles(dt);
            UpdateToasts(dt);
            TryTravelPortalAuto();

            if (raining)
                SpawnRain(dt);

            if (player.Health <= 0)
            {
                player.Health = 0;
                mode = GameMode.GameOver;
            }
        }

        void UpdateMovement(float dt)
        {
            float dx = 0;
            float dz = 0;
            if (IsKey(Keys.W) || IsKey(Keys.Up)) dz -= 1;
            if (IsKey(Keys.S) || IsKey(Keys.Down)) dz += 1;
            if (IsKey(Keys.A) || IsKey(Keys.Left)) dx -= 1;
            if (IsKey(Keys.D) || IsKey(Keys.Right)) dx += 1;
            float len = (float)Math.Sqrt(dx * dx + dz * dz);
            if (len > 0.001f)
            {
                dx /= len;
                dz /= len;
                player.LastDirX = dx;
                player.LastDirZ = dz;
                TryMove(dx * player.Speed * dt, dz * player.Speed * dt);
                SpawnFootstepDust(dt);
            }
        }

        bool IsKey(Keys key)
        {
            int code = (int)key;
            return code >= 0 && code < keys.Length && keys[code];
        }

        void TryMove(float dx, float dz)
        {
            float oldX = player.X;
            float oldZ = player.Z;
            player.X += dx;
            if (Collides(player.X, player.Z, 0.48f)) player.X = oldX;
            player.Z += dz;
            if (Collides(player.X, player.Z, 0.48f)) player.Z = oldZ;
            LevelData level = CurrentLevel();
            if (player.X < 1.2f) player.X = 1.2f;
            if (player.Z < 1.2f) player.Z = 1.2f;
            if (player.X > level.Size - 2.2f) player.X = level.Size - 2.2f;
            if (player.Z > level.Size - 2.2f) player.Z = level.Size - 2.2f;
        }

        bool Collides(float x, float z, float radius)
        {
            int i;
            for (i = 0; i < props.Count; i++)
            {
                Prop p = props[i];
                if (!p.Solid) continue;
                if (Distance(x, z, p.X, p.Z) < radius + p.Radius) return true;
            }
            return false;
        }

        void SpawnFootstepDust(float dt)
        {
            if (rng.NextDouble() > dt * 14) return;
            Particle p = new Particle();
            p.X = player.X - player.LastDirX * 0.25f;
            p.Z = player.Z - player.LastDirZ * 0.25f;
            p.Y = 0.02f;
            p.VX = ((float)rng.NextDouble() - 0.5f) * 0.3f;
            p.VZ = ((float)rng.NextDouble() - 0.5f) * 0.3f;
            p.VY = 0.25f;
            p.Life = 0.45f;
            p.MaxLife = 0.45f;
            p.Color = Color.FromArgb(170, 233, 220, 178);
            p.Size = 6;
            particles.Add(p);
        }

        void UpdateEnemies(float dt)
        {
            int i;
            for (i = enemies.Count - 1; i >= 0; i--)
            {
                Enemy e = enemies[i];
                if (e.Hp <= 0) continue;
                e.AttackClock -= dt;
                if (e.SlowTimer > 0) e.SlowTimer -= dt;

                float dx = player.X - e.X;
                float dz = player.Z - e.Z;
                float dist = (float)Math.Sqrt(dx * dx + dz * dz);
                float speed = e.Speed * (e.SlowTimer > 0 ? 0.42f : 1.0f);

                if (dist < (e.Boss ? 15 : 9))
                {
                    if (dist > 0.1f)
                    {
                        e.X += dx / dist * speed * dt;
                        e.Z += dz / dist * speed * dt;
                    }
                }
                else
                {
                    e.Wander += dt * 0.7f;
                    e.X += (float)Math.Cos(e.Wander) * speed * 0.25f * dt;
                    e.Z += (float)Math.Sin(e.Wander * 0.8f) * speed * 0.25f * dt;
                    if (Distance(e.X, e.Z, e.SpawnX, e.SpawnZ) > 4)
                    {
                        e.X += (e.SpawnX - e.X) * dt * 0.4f;
                        e.Z += (e.SpawnZ - e.Z) * dt * 0.4f;
                    }
                }

                if (dist < e.Radius + 0.62f && e.AttackClock <= 0)
                {
                    float damage = Math.Max(1, e.Damage - player.Armor);
                    player.Health -= damage;
                    e.AttackClock = e.Boss ? 0.72f : 1.05f;
                    AddHitParticles(player.X, player.Z, Color.FromArgb(246, 86, 72), 9);
                    if (e.Boss && rng.NextDouble() < 0.45) BossBurst(e);
                }
            }
        }

        void UpdateProjectiles(float dt)
        {
            int i;
            for (i = projectiles.Count - 1; i >= 0; i--)
            {
                Projectile p = projectiles[i];
                p.X += p.VX * dt;
                p.Z += p.VZ * dt;
                p.Y += p.VY * dt;
                p.Life -= dt;
                SpawnProjectileTrail(p);
                int hit = HitEnemy(p.X, p.Z, p.Radius);
                if (hit >= 0)
                {
                    DamageEnemy(enemies[hit], p.Damage, p.Color);
                    projectiles.RemoveAt(i);
                }
                else if (p.Life <= 0)
                {
                    projectiles.RemoveAt(i);
                }
            }
        }

        int HitEnemy(float x, float z, float radius)
        {
            int i;
            for (i = 0; i < enemies.Count; i++)
            {
                Enemy e = enemies[i];
                if (e.Hp <= 0) continue;
                if (Distance(x, z, e.X, e.Z) < radius + e.Radius) return i;
            }
            return -1;
        }

        void UpdatePickups()
        {
            int i;
            for (i = pickups.Count - 1; i >= 0; i--)
            {
                Pickup p = pickups[i];
                p.FloatClock += 0.08f;
                if (Distance(player.X, player.Z, p.X, p.Z) < 0.85f)
                {
                    if (p.Kind == "coin")
                    {
                        player.Coins += p.Amount;
                        AddToast("+" + p.Amount.ToString() + " monedas");
                    }
                    else if (p.Kind == "potion")
                    {
                        player.Potions += p.Amount;
                        AddToast("Pocion conseguida");
                    }
                    else
                    {
                        if (!player.Items.ContainsKey(p.Kind)) player.Items[p.Kind] = 0;
                        player.Items[p.Kind] += p.Amount;
                        ingredientsThisLevel += p.Amount;
                        AddToast("Ingrediente: " + p.Kind);
                    }
                    pickups.RemoveAt(i);
                }
            }
        }

        void UpdateParticles(float dt)
        {
            int i;
            for (i = particles.Count - 1; i >= 0; i--)
            {
                Particle p = particles[i];
                p.X += p.VX * dt;
                p.Z += p.VZ * dt;
                p.Y += p.VY * dt;
                p.VY -= dt * 0.55f;
                p.Life -= dt;
                if (p.Life <= 0) particles.RemoveAt(i);
            }
        }

        void UpdateToasts(float dt)
        {
            int i;
            for (i = toasts.Count - 1; i >= 0; i--)
            {
                toasts[i].Life -= dt;
                if (toasts[i].Life <= 0) toasts.RemoveAt(i);
            }
        }

        void MeleeAttack()
        {
            if (player.MeleeCooldown > 0) return;
            player.MeleeCooldown = 0.36f;
            int i;
            bool hit = false;
            for (i = 0; i < enemies.Count; i++)
            {
                Enemy e = enemies[i];
                if (e.Hp <= 0) continue;
                float d = Distance(player.X, player.Z, e.X, e.Z);
                if (d < 1.75f + e.Radius)
                {
                    DamageEnemy(e, player.Attack, Color.FromArgb(255, 244, 205, 80));
                    hit = true;
                }
            }
            AddSlashParticles(hit ? Color.FromArgb(255, 247, 206, 71) : Color.FromArgb(190, 220, 240, 255));
        }

        void CastWokFlame()
        {
            if (player.WokCooldown > 0 || player.Mana < 16) return;
            player.Mana -= 16;
            player.WokCooldown = 0.62f;
            float dx = player.LastDirX;
            float dz = player.LastDirZ;
            Enemy nearest = NearestEnemy(8);
            if (nearest != null)
            {
                dx = nearest.X - player.X;
                dz = nearest.Z - player.Z;
                float l = (float)Math.Sqrt(dx * dx + dz * dz);
                if (l > 0.01f) { dx /= l; dz /= l; }
            }
            Projectile p = new Projectile();
            p.X = player.X + dx * 0.7f;
            p.Z = player.Z + dz * 0.7f;
            p.Y = 0.55f;
            p.VX = dx * 8.5f;
            p.VZ = dz * 8.5f;
            p.VY = 0;
            p.Life = 1.35f;
            p.Radius = 0.35f;
            p.Damage = 34 + player.MagicPower;
            p.Color = Color.FromArgb(255, 112, 42);
            p.Kind = "wok";
            projectiles.Add(p);
            AddToast("Magia wok: llama de especias");
        }

        void CastTeaMend()
        {
            if (player.TeaCooldown > 0 || player.Mana < 24) return;
            player.Mana -= 24;
            player.TeaCooldown = 5.5f;
            float heal = 38 + player.MagicPower * 0.8f;
            player.Health += heal;
            if (player.Health > player.MaxHealth) player.Health = player.MaxHealth;
            AddRingParticles(player.X, player.Z, Color.FromArgb(95, 220, 185), 34);
            AddToast("Te sanador: +" + ((int)heal).ToString() + " vida");
        }

        void CastFrostSorbet()
        {
            if (player.FrostCooldown > 0 || player.Mana < 32) return;
            player.Mana -= 32;
            player.FrostCooldown = 4.0f;
            int i;
            for (i = 0; i < enemies.Count; i++)
            {
                Enemy e = enemies[i];
                if (e.Hp <= 0) continue;
                if (Distance(player.X, player.Z, e.X, e.Z) < 4.3f + e.Radius)
                {
                    e.SlowTimer = 3.2f;
                    DamageEnemy(e, 18 + player.MagicPower * 0.55f, Color.FromArgb(139, 230, 255));
                }
            }
            AddRingParticles(player.X, player.Z, Color.FromArgb(139, 230, 255), 58);
            AddToast("Sorbete de hielo: enemigos ralentizados");
        }

        void CastCurryNova()
        {
            if (player.CurryCooldown > 0 || player.Mana < 48) return;
            player.Mana -= 48;
            player.CurryCooldown = 8.0f;
            int i;
            for (i = 0; i < enemies.Count; i++)
            {
                Enemy e = enemies[i];
                if (e.Hp <= 0) continue;
                if (Distance(player.X, player.Z, e.X, e.Z) < 5.4f + e.Radius)
                    DamageEnemy(e, 42 + player.MagicPower, Color.FromArgb(255, 190, 57));
            }
            AddRingParticles(player.X, player.Z, Color.FromArgb(255, 190, 57), 80);
            AddToast("Nova curry: explosion de especias");
        }

        Enemy NearestEnemy(float maxDistance)
        {
            Enemy best = null;
            float bestD = maxDistance;
            int i;
            for (i = 0; i < enemies.Count; i++)
            {
                Enemy e = enemies[i];
                if (e.Hp <= 0) continue;
                float d = Distance(player.X, player.Z, e.X, e.Z);
                if (d < bestD)
                {
                    bestD = d;
                    best = e;
                }
            }
            return best;
        }

        void DamageEnemy(Enemy e, float damage, Color flash)
        {
            e.Hp -= damage;
            e.Flash = 0.16f;
            AddHitParticles(e.X, e.Z, flash, e.Boss ? 18 : 9);
            if (e.Hp <= 0)
            {
                OnEnemyDefeated(e);
            }
        }

        void OnEnemyDefeated(Enemy e)
        {
            killsThisLevel++;
            player.Xp += e.Boss ? 85 : 20;
            while (player.Xp >= player.Level * 100)
            {
                player.Xp -= player.Level * 100;
                player.Level++;
                player.MaxHealth += 16;
                player.MaxMana += 8;
                player.Attack += 4;
                player.MagicPower += 3;
                player.Health = player.MaxHealth;
                player.Mana = player.MaxMana;
                AddToast("Subes al nivel " + player.Level.ToString());
                AddRingParticles(player.X, player.Z, Color.FromArgb(120, 200, 255), 44);
            }

            DropLoot(e);
            if (e.Boss)
            {
                bossDefeated = true;
                totalBosses++;
                player.Items[CurrentLevel().Ingredient] = player.Items.ContainsKey(CurrentLevel().Ingredient) ? player.Items[CurrentLevel().Ingredient] + 1 : 1;
                player.Items["Llave del Chef"] = totalBosses;
                AddToast("Jefe vencido: " + e.Name + ". Portal abierto.");
                AddRingParticles(e.X, e.Z, Color.FromArgb(97, 210, 255), 100);
            }
        }

        void DropLoot(Enemy e)
        {
            int coins = e.Boss ? 65 + levelIndex * 15 : 7 + rng.Next(13);
            Pickup coin = new Pickup();
            coin.Kind = "coin";
            coin.Amount = coins;
            coin.X = e.X;
            coin.Z = e.Z;
            pickups.Add(coin);

            if (rng.NextDouble() < (e.Boss ? 1.0 : 0.28))
            {
                Pickup ingredient = new Pickup();
                ingredient.Kind = CurrentLevel().Ingredient;
                ingredient.Amount = 1;
                ingredient.X = e.X + 0.42f;
                ingredient.Z = e.Z - 0.22f;
                pickups.Add(ingredient);
            }

            if (rng.NextDouble() < 0.17)
            {
                Pickup potion = new Pickup();
                potion.Kind = "potion";
                potion.Amount = 1;
                potion.X = e.X - 0.34f;
                potion.Z = e.Z + 0.36f;
                pickups.Add(potion);
            }
        }

        void UsePotion()
        {
            if (player.Potions <= 0 || player.Health >= player.MaxHealth) return;
            player.Potions--;
            player.Health += 52;
            if (player.Health > player.MaxHealth) player.Health = player.MaxHealth;
            AddToast("Pocion usada");
            AddRingParticles(player.X, player.Z, Color.FromArgb(140, 235, 194), 22);
        }

        void TryOpenShop()
        {
            int i;
            for (i = 0; i < props.Count; i++)
            {
                Prop p = props[i];
                if (p.Kind == "shop" && Distance(player.X, player.Z, p.X, p.Z) < 2.1f)
                {
                    mode = GameMode.Shop;
                    return;
                }
            }
            if (bossDefeated) TryTravelPortal();
        }

        void HandleShopKey(Keys key)
        {
            if (key == Keys.Escape || key == Keys.E)
            {
                mode = GameMode.Playing;
                return;
            }
            if (key == Keys.D1)
                Buy("Pocion de caldo", 25, delegate { player.Potions++; });
            if (key == Keys.D2)
                Buy("Sarten reforzada", 70, delegate { player.Attack += 8; });
            if (key == Keys.D3)
                Buy("Delantal blindado", 90, delegate { player.Armor += 3; player.MaxHealth += 18; player.Health += 18; });
            if (key == Keys.D4)
                Buy("Libro de especias", 110, delegate { player.MagicPower += 10; player.MaxMana += 16; player.Mana += 16; });
        }

        void Buy(string name, int price, SimpleAction action)
        {
            if (player.Coins < price)
            {
                AddToast("Faltan monedas para " + name);
                return;
            }
            player.Coins -= price;
            action();
            AddToast("Comprado: " + name);
        }

        delegate void SimpleAction();

        void TryTravelPortalAuto()
        {
            if (!bossDefeated) return;
            Prop portal = FindPortal();
            if (portal != null && Distance(player.X, player.Z, portal.X, portal.Z) < 1.0f)
                TryTravelPortal();
        }

        void TryTravelPortal()
        {
            if (!bossDefeated) return;
            Prop portal = FindPortal();
            if (portal == null) return;
            if (Distance(player.X, player.Z, portal.X, portal.Z) > 2.0f) return;
            if (levelIndex >= levels.Count - 1)
            {
                mode = GameMode.Victory;
                AddToast("Victoria total");
            }
            else
            {
                LoadLevel(levelIndex + 1);
            }
        }

        Prop FindPortal()
        {
            int i;
            for (i = 0; i < props.Count; i++)
                if (props[i].Kind == "portal") return props[i];
            return null;
        }

        void BossBurst(Enemy e)
        {
            int i;
            for (i = 0; i < 3 + levelIndex; i++)
            {
                float angle = (float)(i * Math.PI * 2 / (3 + levelIndex)) + portalPulse;
                Projectile p = new Projectile();
                p.X = e.X;
                p.Z = e.Z;
                p.Y = 0.9f;
                p.VX = (float)Math.Cos(angle) * 4.2f;
                p.VZ = (float)Math.Sin(angle) * 4.2f;
                p.VY = 0;
                p.Life = 1.2f;
                p.Radius = 0.28f;
                p.Damage = 0;
                p.Kind = "boss";
                p.Color = Color.FromArgb(255, 108, 65);
                projectiles.Add(p);
            }
        }

        void SpawnProjectileTrail(Projectile p)
        {
            Particle q = new Particle();
            q.X = p.X;
            q.Z = p.Z;
            q.Y = p.Y;
            q.VX = ((float)rng.NextDouble() - 0.5f) * 0.5f;
            q.VZ = ((float)rng.NextDouble() - 0.5f) * 0.5f;
            q.VY = ((float)rng.NextDouble()) * 0.35f;
            q.Life = 0.42f;
            q.MaxLife = 0.42f;
            q.Color = p.Color;
            q.Size = p.Kind == "wok" ? 9 : 6;
            particles.Add(q);
        }

        void AddSlashParticles(Color color)
        {
            int i;
            for (i = 0; i < 18; i++)
            {
                float a = (float)(rng.NextDouble() * Math.PI * 2);
                Particle p = new Particle();
                p.X = player.X + player.LastDirX * 0.7f;
                p.Z = player.Z + player.LastDirZ * 0.7f;
                p.Y = 0.55f;
                p.VX = (float)Math.Cos(a) * (1.1f + (float)rng.NextDouble() * 2.4f);
                p.VZ = (float)Math.Sin(a) * (1.1f + (float)rng.NextDouble() * 2.4f);
                p.VY = 1.1f + (float)rng.NextDouble() * 0.7f;
                p.Life = 0.34f;
                p.MaxLife = 0.34f;
                p.Color = color;
                p.Size = 7;
                particles.Add(p);
            }
        }

        void AddHitParticles(float x, float z, Color color, int count)
        {
            int i;
            for (i = 0; i < count; i++)
            {
                float a = (float)(rng.NextDouble() * Math.PI * 2);
                Particle p = new Particle();
                p.X = x;
                p.Z = z;
                p.Y = 0.7f;
                p.VX = (float)Math.Cos(a) * (0.8f + (float)rng.NextDouble() * 2.5f);
                p.VZ = (float)Math.Sin(a) * (0.8f + (float)rng.NextDouble() * 2.5f);
                p.VY = 0.8f + (float)rng.NextDouble() * 1.5f;
                p.Life = 0.45f + (float)rng.NextDouble() * 0.25f;
                p.MaxLife = p.Life;
                p.Color = color;
                p.Size = 5 + rng.Next(8);
                particles.Add(p);
            }
        }

        void AddRingParticles(float x, float z, Color color, int count)
        {
            int i;
            for (i = 0; i < count; i++)
            {
                float a = (float)(i * Math.PI * 2 / count);
                Particle p = new Particle();
                p.X = x;
                p.Z = z;
                p.Y = 0.15f + (float)rng.NextDouble() * 0.3f;
                p.VX = (float)Math.Cos(a) * (1.2f + (float)rng.NextDouble() * 2.7f);
                p.VZ = (float)Math.Sin(a) * (1.2f + (float)rng.NextDouble() * 2.7f);
                p.VY = 0.75f + (float)rng.NextDouble() * 0.4f;
                p.Life = 0.8f;
                p.MaxLife = 0.8f;
                p.Color = color;
                p.Size = 5 + rng.Next(8);
                particles.Add(p);
            }
        }

        void SpawnRain(float dt)
        {
            int drops = (int)(dt * 180);
            int i;
            for (i = 0; i < drops; i++)
            {
                Particle p = new Particle();
                p.X = player.X - 11 + (float)rng.NextDouble() * 22;
                p.Z = player.Z - 11 + (float)rng.NextDouble() * 22;
                p.Y = 7 + (float)rng.NextDouble() * 4;
                p.VX = -2.8f;
                p.VZ = 1.7f;
                p.VY = -9.5f;
                p.Life = 0.8f;
                p.MaxLife = 0.8f;
                p.Color = Color.FromArgb(120, 173, 225, 255);
                p.Size = 10;
                p.Rain = true;
                particles.Add(p);
            }
        }

        void AddToast(string text)
        {
            Toast t = new Toast();
            t.Text = text;
            t.Life = 3.2f;
            toasts.Add(t);
            if (toasts.Count > 5) toasts.RemoveAt(0);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            if (mode == GameMode.Menu)
            {
                DrawMenu(g);
                return;
            }

            DrawGame(g);

            if (mode == GameMode.Inventory) DrawInventory(g);
            else if (mode == GameMode.Shop) DrawShop(g);
            else if (mode == GameMode.Journal) DrawJournal(g);
            else if (mode == GameMode.Paused) DrawPaused(g);
            else if (mode == GameMode.GameOver) DrawGameOver(g);
            else if (mode == GameMode.Victory) DrawVictory(g);
        }

        void DrawMenu(Graphics g)
        {
            DrawSky(g);
            DrawMenuWorld(g);
            using (SolidBrush veil = new SolidBrush(Color.FromArgb(75, 255, 255, 255)))
                g.FillRectangle(veil, 0, 0, ClientSize.Width, ClientSize.Height);

            Rectangle titleBox = new Rectangle(ClientSize.Width / 2 - 390, ClientSize.Height / 2 - 185, 780, 310);
            using (GraphicsPath path = RoundedRect(titleBox, 8))
            using (SolidBrush panel = new SolidBrush(Color.FromArgb(225, 250, 253, 255)))
            using (Pen border = new Pen(Color.FromArgb(120, 92, 174, 225), 1))
            {
                g.FillPath(panel, path);
                g.DrawPath(border, path);
            }

            DrawCentered(g, "FOOD REALMS", titleFont, Color.FromArgb(23, 91, 145), titleBox.Top + 38);
            DrawCentered(g, "RPG 3D ejecutable creado para Nicolas Herguera", h2Font, Color.FromArgb(38, 116, 172), titleBox.Top + 96);
            DrawCentered(g, "Comida global, mazmorras asiaticas, jefes, magia, tienda, inventario y clima vivo.", uiFont, Color.FromArgb(46, 62, 74), titleBox.Top + 139);
            DrawCentered(g, "ENTER", h1Font, Color.FromArgb(19, 99, 168), titleBox.Top + 204);
            DrawCentered(g, "comenzar aventura", uiFont, Color.FromArgb(65, 89, 105), titleBox.Top + 236);
        }

        void DrawMenuWorld(Graphics g)
        {
            LevelData saved = CurrentLevel();
            DrawWorld(g, true);
        }

        void DrawGame(Graphics g)
        {
            DrawSky(g);
            DrawWorld(g, false);
            DrawWeatherOverlay(g);
            DrawHud(g);
            DrawToasts(g);
        }

        void DrawSky(Graphics g)
        {
            LevelData level = CurrentLevel();
            float night = NightAmount();
            Color top = Mix(Color.FromArgb(213, 244, 255), level.Night, night);
            Color bottom = Mix(Color.FromArgb(255, 250, 232), level.Day, 0.55f - night * 0.1f);
            using (LinearGradientBrush brush = new LinearGradientBrush(ClientRectangle, top, bottom, LinearGradientMode.Vertical))
                g.FillRectangle(brush, ClientRectangle);

            float sunX = (float)(ClientSize.Width * (0.1 + 0.8 * timeOfDay));
            float sunY = 70 + (float)Math.Sin(timeOfDay * Math.PI) * 82;
            Color orb = night > 0.45f ? Color.FromArgb(235, 242, 255) : Color.FromArgb(255, 236, 126);
            using (SolidBrush b = new SolidBrush(Color.FromArgb(150, orb)))
                g.FillEllipse(b, sunX - 28, sunY - 28, 56, 56);
        }

        void DrawWorld(Graphics g, bool menu)
        {
            LevelData level = CurrentLevel();
            int size = level.Size;
            int x, z;
            for (int sum = 0; sum <= size * 2; sum++)
            {
                for (x = 0; x < size; x++)
                {
                    z = sum - x;
                    if (z < 0 || z >= size) continue;
                    PointF center = Project(x + 0.5f, z + 0.5f, 0);
                    if (center.X < -160 || center.X > ClientSize.Width + 160 || center.Y < -120 || center.Y > ClientSize.Height + 180) continue;
                    DrawTile(g, x, z, level.TextureKey);
                }
            }

            List<RenderItem> items = new List<RenderItem>();
            int i;
            for (i = 0; i < props.Count; i++)
                items.Add(new RenderItem(props[i].X + props[i].Z, 0, props[i]));
            if (!menu)
            {
                for (i = 0; i < pickups.Count; i++)
                    items.Add(new RenderItem(pickups[i].X + pickups[i].Z + 0.03f, 1, pickups[i]));
                for (i = 0; i < enemies.Count; i++)
                    if (enemies[i].Hp > 0) items.Add(new RenderItem(enemies[i].X + enemies[i].Z + 0.07f, 2, enemies[i]));
                items.Add(new RenderItem(player.X + player.Z + 0.08f, 3, player));
                for (i = 0; i < projectiles.Count; i++)
                    items.Add(new RenderItem(projectiles[i].X + projectiles[i].Z + 0.09f, 4, projectiles[i]));
            }
            items.Sort(delegate(RenderItem a, RenderItem b) { return a.Order.CompareTo(b.Order); });

            for (i = 0; i < items.Count; i++)
            {
                RenderItem item = items[i];
                if (item.Kind == 0) DrawProp(g, (Prop)item.Ref);
                else if (item.Kind == 1) DrawPickup(g, (Pickup)item.Ref);
                else if (item.Kind == 2) DrawEnemy(g, (Enemy)item.Ref);
                else if (item.Kind == 3) DrawPlayer(g);
                else if (item.Kind == 4) DrawProjectile(g, (Projectile)item.Ref);
            }

            for (i = 0; i < particles.Count; i++)
                DrawParticle(g, particles[i]);
        }

        void DrawTile(Graphics g, int x, int z, string texture)
        {
            PointF c = Project(x + 0.5f, z + 0.5f, 0);
            PointF[] d = new PointF[]
            {
                new PointF(c.X, c.Y - TileH / 2),
                new PointF(c.X + TileW / 2, c.Y),
                new PointF(c.X, c.Y + TileH / 2),
                new PointF(c.X - TileW / 2, c.Y)
            };
            string key = texture;
            if ((x + z + levelIndex) % 17 == 0) key = "stone";
            if ((x * 13 + z * 11 + levelIndex) % 49 == 0) key = "water";
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddPolygon(d);
                g.FillPath(brushes[key], path);
            }
            int shade = ((x * 53 + z * 97) % 26) - 13;
            using (SolidBrush overlay = new SolidBrush(shade >= 0 ? Color.FromArgb(shade, Color.White) : Color.FromArgb(-shade, Color.Black)))
                g.FillPolygon(overlay, d);
            using (Pen edge = new Pen(Color.FromArgb(28, 26, 79, 109), 1))
                g.DrawPolygon(edge, d);

            if (raining && (x * 31 + z * 17) % 19 == 0)
            {
                using (Pen wet = new Pen(Color.FromArgb(60, 230, 250, 255), 2))
                    g.DrawLine(wet, c.X - 14, c.Y - 2, c.X + 18, c.Y - 8);
            }
        }

        void DrawProp(Graphics g, Prop p)
        {
            if (p.Kind == "wall")
            {
                DrawIsoBlock(g, p.X, p.Z, 0.95f, "stone", Color.FromArgb(84, 95, 108));
                return;
            }
            if (p.Kind == "shop")
            {
                DrawShopCart(g, p);
                return;
            }
            if (p.Kind == "portal")
            {
                DrawPortal(g, p);
                return;
            }

            PointF s = Project(p.X, p.Z, 0);
            DrawShadow(g, s, p.Solid ? 34 : 22, p.Solid ? 16 : 9, 65);
            if (p.Kind == "bowl" || p.Kind == "noodle" || p.Kind == "egg")
            {
                DrawBowl(g, s, p.Kind);
            }
            else if (p.Kind == "sushi" || p.Kind == "nori" || p.Kind == "dumpling")
            {
                DrawSushi(g, s, p.Kind);
            }
            else if (p.Kind == "pizza" || p.Kind == "cheese" || p.Kind == "tomato")
            {
                DrawPizzaProp(g, s, p.Kind);
            }
            else if (p.Kind == "lantern" || p.Kind == "lamp")
            {
                DrawLantern(g, s);
            }
            else if (p.Kind == "steam")
            {
                DrawSteam(g, s);
            }
            else if (p.Kind == "tea")
            {
                DrawTeaShrine(g, p, s);
            }
            else if (p.Kind == "spice" || p.Kind == "pepper" || p.Kind == "curry")
            {
                DrawSpice(g, s, p.Kind);
            }
            else if (p.Kind == "oven")
            {
                DrawIsoBlock(g, p.X, p.Z, 1.15f, "stone", Color.FromArgb(91, 89, 82));
            }
            else
            {
                DrawFoodCrate(g, s, p.Kind);
            }
        }

        void DrawShopCart(Graphics g, Prop p)
        {
            PointF s = Project(p.X, p.Z, 0);
            DrawShadow(g, s, 72, 28, 70);
            DrawIsoBlock(g, p.X, p.Z, 0.75f, "wood", Color.FromArgb(112, 67, 38));
            PointF top = Project(p.X, p.Z, 1.0f);
            RectangleF awning = new RectangleF(top.X - 58, top.Y - 28, 116, 34);
            using (GraphicsPath path = RoundedRect(Rectangle.Round(awning), 6))
            using (LinearGradientBrush b = new LinearGradientBrush(awning, Color.FromArgb(235, 250, 255), Color.FromArgb(93, 178, 230), LinearGradientMode.Horizontal))
            {
                g.FillPath(b, path);
                using (Pen pen = new Pen(Color.FromArgb(55, 122, 174), 2))
                    g.DrawPath(pen, path);
            }
            using (SolidBrush blue = new SolidBrush(Color.FromArgb(50, 145, 212)))
            {
                g.FillRectangle(blue, top.X - 45, top.Y - 28, 15, 34);
                g.FillRectangle(blue, top.X - 5, top.Y - 28, 15, 34);
                g.FillRectangle(blue, top.X + 35, top.Y - 28, 15, 34);
            }
            using (SolidBrush sign = new SolidBrush(Color.FromArgb(250, 255, 255, 255)))
                g.FillEllipse(sign, top.X - 17, top.Y - 4, 34, 22);
            DrawCenteredAt(g, "T", h2Font, Color.FromArgb(38, 103, 157), top.X, top.Y + 0);
        }

        void DrawPortal(Graphics g, Prop p)
        {
            PointF s = Project(p.X, p.Z, 0);
            float pulse = (float)Math.Sin(portalPulse) * 0.5f + 0.5f;
            DrawShadow(g, s, 92 + pulse * 20, 32 + pulse * 8, bossDefeated ? 100 : 40);
            using (Pen ring = new Pen(bossDefeated ? Color.FromArgb(120, 213, 246, 255) : Color.FromArgb(70, 160, 170, 180), 5))
                g.DrawEllipse(ring, s.X - 45 - pulse * 8, s.Y - 58 - pulse * 6, 90 + pulse * 16, 56 + pulse * 12);
            using (Pen ring2 = new Pen(bossDefeated ? Color.FromArgb(205, 60, 154, 224) : Color.FromArgb(90, 94, 103, 112), 3))
                g.DrawArc(ring2, s.X - 36, s.Y - 52, 72, 52, portalPulse * 35, 250);
            using (SolidBrush core = new SolidBrush(bossDefeated ? Color.FromArgb(85, 90, 180, 255) : Color.FromArgb(35, 90, 90, 90)))
                g.FillEllipse(core, s.X - 24, s.Y - 40, 48, 31);
        }

        void DrawFoodCrate(Graphics g, PointF s, string kind)
        {
            using (SolidBrush b = new SolidBrush(Color.FromArgb(141, 90, 42)))
            using (Pen pen = new Pen(Color.FromArgb(92, 54, 27), 2))
            {
                RectangleF r = new RectangleF(s.X - 24, s.Y - 38, 48, 34);
                g.FillRectangle(b, r);
                g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height);
                g.DrawLine(pen, r.X, r.Y, r.Right, r.Bottom);
                g.DrawLine(pen, r.Right, r.Y, r.X, r.Bottom);
            }
            Color food = Color.FromArgb(233, 204, 108);
            if (kind == "fruit") food = Color.FromArgb(226, 71, 72);
            if (kind == "bread" || kind == "toast") food = Color.FromArgb(219, 153, 78);
            if (kind == "honey") food = Color.FromArgb(244, 190, 53);
            using (SolidBrush f = new SolidBrush(food))
            {
                g.FillEllipse(f, s.X - 16, s.Y - 52, 16, 12);
                g.FillEllipse(f, s.X + 1, s.Y - 50, 19, 13);
                g.FillEllipse(f, s.X - 2, s.Y - 61, 14, 15);
            }
        }

        void DrawBowl(Graphics g, PointF s, string kind)
        {
            using (SolidBrush bowl = new SolidBrush(Color.FromArgb(232, 245, 255)))
            using (Pen rim = new Pen(Color.FromArgb(54, 120, 173), 2))
            {
                g.FillEllipse(bowl, s.X - 33, s.Y - 33, 66, 30);
                g.DrawEllipse(rim, s.X - 33, s.Y - 33, 66, 30);
            }
            using (SolidBrush soup = new SolidBrush(kind == "egg" ? Color.FromArgb(255, 230, 120) : Color.FromArgb(197, 121, 58)))
                g.FillEllipse(soup, s.X - 25, s.Y - 29, 50, 21);
            using (Pen noodle = new Pen(Color.FromArgb(255, 228, 126), 3))
            {
                g.DrawBezier(noodle, s.X - 22, s.Y - 21, s.X - 10, s.Y - 33, s.X + 7, s.Y - 10, s.X + 22, s.Y - 22);
                g.DrawBezier(noodle, s.X - 18, s.Y - 16, s.X - 5, s.Y - 27, s.X + 8, s.Y - 7, s.X + 18, s.Y - 18);
            }
            if (kind == "egg")
            {
                using (SolidBrush egg = new SolidBrush(Color.White))
                    g.FillEllipse(egg, s.X + 2, s.Y - 29, 19, 15);
                using (SolidBrush yolk = new SolidBrush(Color.FromArgb(238, 181, 37)))
                    g.FillEllipse(yolk, s.X + 8, s.Y - 25, 8, 8);
            }
        }

        void DrawSushi(Graphics g, PointF s, string kind)
        {
            int i;
            for (i = 0; i < 2; i++)
            {
                float x = s.X - 24 + i * 30;
                using (SolidBrush nori = new SolidBrush(Color.FromArgb(35, 70, 61)))
                    g.FillEllipse(nori, x, s.Y - 41, 30, 26);
                using (SolidBrush rice = new SolidBrush(Color.FromArgb(245, 250, 245)))
                    g.FillEllipse(rice, x + 4, s.Y - 38, 22, 19);
                using (SolidBrush fish = new SolidBrush(kind == "dumpling" ? Color.FromArgb(226, 210, 165) : Color.FromArgb(230, 89, 76)))
                    g.FillEllipse(fish, x + 10, s.Y - 33, 10, 9);
            }
        }

        void DrawPizzaProp(Graphics g, PointF s, string kind)
        {
            PointF[] tri = new PointF[]
            {
                new PointF(s.X - 30, s.Y - 14),
                new PointF(s.X + 32, s.Y - 24),
                new PointF(s.X - 4, s.Y - 58)
            };
            using (SolidBrush crust = new SolidBrush(Color.FromArgb(217, 141, 69)))
                g.FillPolygon(crust, tri);
            using (SolidBrush cheese = new SolidBrush(Color.FromArgb(255, 224, 111)))
                g.FillPolygon(cheese, new PointF[] { new PointF(s.X - 20, s.Y - 18), new PointF(s.X + 22, s.Y - 25), new PointF(s.X - 4, s.Y - 50) });
            using (SolidBrush pep = new SolidBrush(kind == "tomato" ? Color.FromArgb(217, 48, 47) : Color.FromArgb(172, 43, 45)))
            {
                g.FillEllipse(pep, s.X - 6, s.Y - 35, 9, 9);
                g.FillEllipse(pep, s.X + 8, s.Y - 29, 8, 8);
            }
        }

        void DrawLantern(Graphics g, PointF s)
        {
            using (Pen pole = new Pen(Color.FromArgb(80, 71, 59), 3))
                g.DrawLine(pole, s.X, s.Y - 62, s.X, s.Y - 8);
            using (SolidBrush glow = new SolidBrush(Color.FromArgb(70, 255, 221, 113)))
                g.FillEllipse(glow, s.X - 28, s.Y - 75, 56, 40);
            using (SolidBrush lantern = new SolidBrush(Color.FromArgb(238, 78, 69)))
                g.FillEllipse(lantern, s.X - 13, s.Y - 67, 26, 25);
            using (Pen ribs = new Pen(Color.FromArgb(124, 35, 35), 1))
            {
                g.DrawLine(ribs, s.X, s.Y - 67, s.X, s.Y - 43);
                g.DrawEllipse(ribs, s.X - 13, s.Y - 67, 26, 25);
            }
        }

        void DrawSteam(Graphics g, PointF s)
        {
            using (Pen steam = new Pen(Color.FromArgb(95, 245, 250, 255), 3))
            {
                g.DrawBezier(steam, s.X - 13, s.Y - 12, s.X - 35, s.Y - 38, s.X + 10, s.Y - 48, s.X - 8, s.Y - 72);
                g.DrawBezier(steam, s.X + 12, s.Y - 10, s.X - 3, s.Y - 33, s.X + 30, s.Y - 44, s.X + 14, s.Y - 67);
            }
        }

        void DrawTeaShrine(Graphics g, Prop p, PointF s)
        {
            DrawIsoBlock(g, p.X, p.Z, 0.35f, "wood", Color.FromArgb(127, 73, 44));
            using (SolidBrush cup = new SolidBrush(Color.FromArgb(232, 250, 255)))
                g.FillEllipse(cup, s.X - 17, s.Y - 48, 34, 21);
            using (SolidBrush tea = new SolidBrush(Color.FromArgb(87, 180, 143)))
                g.FillEllipse(tea, s.X - 12, s.Y - 45, 24, 13);
            DrawSteam(g, new PointF(s.X, s.Y - 20));
        }

        void DrawSpice(Graphics g, PointF s, string kind)
        {
            using (SolidBrush mound = new SolidBrush(kind == "pepper" ? Color.FromArgb(198, 34, 32) : Color.FromArgb(234, 151, 38)))
                g.FillEllipse(mound, s.X - 25, s.Y - 28, 50, 22);
            using (Pen sparkle = new Pen(Color.FromArgb(145, 255, 229, 123), 2))
            {
                g.DrawLine(sparkle, s.X - 4, s.Y - 47, s.X + 5, s.Y - 36);
                g.DrawLine(sparkle, s.X + 5, s.Y - 47, s.X - 4, s.Y - 36);
            }
        }

        void DrawIsoBlock(Graphics g, float wx, float wz, float height, string texture, Color sideBase)
        {
            PointF a = Project(wx - 0.48f, wz, height);
            PointF b = Project(wx, wz - 0.48f, height);
            PointF c = Project(wx + 0.48f, wz, height);
            PointF d = Project(wx, wz + 0.48f, height);
            PointF ab = Project(wx - 0.48f, wz, 0);
            PointF cb = Project(wx + 0.48f, wz, 0);
            PointF db = Project(wx, wz + 0.48f, 0);
            PointF bb = Project(wx, wz - 0.48f, 0);

            using (SolidBrush left = new SolidBrush(Darken(sideBase, 0.72f)))
                g.FillPolygon(left, new PointF[] { d, c, cb, db });
            using (SolidBrush right = new SolidBrush(Darken(sideBase, 0.55f)))
                g.FillPolygon(right, new PointF[] { b, c, cb, bb });
            using (GraphicsPath path = new GraphicsPath())
            {
                path.AddPolygon(new PointF[] { a, b, c, d });
                g.FillPath(brushes.ContainsKey(texture) ? brushes[texture] : brushes["stone"], path);
            }
            using (Pen edge = new Pen(Color.FromArgb(58, 30, 44, 55), 1))
            {
                g.DrawPolygon(edge, new PointF[] { a, b, c, d });
                g.DrawPolygon(edge, new PointF[] { d, c, cb, db });
                g.DrawPolygon(edge, new PointF[] { b, c, cb, bb });
            }
        }

        void DrawPickup(Graphics g, Pickup p)
        {
            PointF s = Project(p.X, p.Z, 0.35f + (float)Math.Sin(p.FloatClock) * 0.08f);
            DrawShadow(g, Project(p.X, p.Z, 0), 22, 8, 55);
            if (p.Kind == "coin")
            {
                using (SolidBrush coin = new SolidBrush(Color.FromArgb(255, 204, 55)))
                    g.FillEllipse(coin, s.X - 10, s.Y - 10, 20, 20);
                using (Pen edge = new Pen(Color.FromArgb(168, 103, 24), 2))
                    g.DrawEllipse(edge, s.X - 10, s.Y - 10, 20, 20);
            }
            else if (p.Kind == "potion")
            {
                using (SolidBrush glass = new SolidBrush(Color.FromArgb(214, 245, 255)))
                    g.FillRectangle(glass, s.X - 8, s.Y - 18, 16, 24);
                using (SolidBrush liquid = new SolidBrush(Color.FromArgb(86, 220, 168)))
                    g.FillRectangle(liquid, s.X - 7, s.Y - 4, 14, 10);
                using (Pen outline = new Pen(Color.FromArgb(51, 114, 153), 2))
                    g.DrawRectangle(outline, s.X - 8, s.Y - 18, 16, 24);
            }
            else
            {
                using (SolidBrush ing = new SolidBrush(Color.FromArgb(246, 242, 224)))
                    g.FillEllipse(ing, s.X - 13, s.Y - 11, 26, 22);
                using (Pen edge = new Pen(Color.FromArgb(71, 138, 176), 2))
                    g.DrawEllipse(edge, s.X - 13, s.Y - 11, 26, 22);
                using (SolidBrush dot = new SolidBrush(CurrentLevel().Accent))
                    g.FillEllipse(dot, s.X - 5, s.Y - 4, 10, 9);
            }
        }

        void DrawEnemy(Graphics g, Enemy e)
        {
            PointF basePt = Project(e.X, e.Z, 0);
            DrawShadow(g, basePt, e.Boss ? 76 : 40, e.Boss ? 24 : 13, 80);
            float scale = e.Boss ? 1.7f : 1.0f;
            PointF s = Project(e.X, e.Z, 0.35f);
            Color body = e.Flash > 0 ? Color.White : e.Color;
            e.Flash -= 0.016f;

            using (SolidBrush b = new SolidBrush(body))
                g.FillEllipse(b, s.X - 22 * scale, s.Y - 43 * scale, 44 * scale, 44 * scale);
            using (SolidBrush belly = new SolidBrush(Color.FromArgb(130, Color.White)))
                g.FillEllipse(belly, s.X - 10 * scale, s.Y - 34 * scale, 20 * scale, 18 * scale);
            using (Pen edge = new Pen(Color.FromArgb(84, 42, 53), 2))
                g.DrawEllipse(edge, s.X - 22 * scale, s.Y - 43 * scale, 44 * scale, 44 * scale);
            using (SolidBrush eye = new SolidBrush(Color.FromArgb(34, 38, 42)))
            {
                g.FillEllipse(eye, s.X - 9 * scale, s.Y - 30 * scale, 5 * scale, 6 * scale);
                g.FillEllipse(eye, s.X + 5 * scale, s.Y - 30 * scale, 5 * scale, 6 * scale);
            }
            if (e.Boss)
            {
                using (Pen horn = new Pen(Color.FromArgb(255, 224, 132), 5))
                {
                    g.DrawLine(horn, s.X - 19 * scale, s.Y - 37 * scale, s.X - 34 * scale, s.Y - 61 * scale);
                    g.DrawLine(horn, s.X + 19 * scale, s.Y - 37 * scale, s.X + 34 * scale, s.Y - 61 * scale);
                }
                using (Pen aura = new Pen(Color.FromArgb(120, CurrentLevel().Accent), 3))
                    g.DrawEllipse(aura, s.X - 38 * scale, s.Y - 54 * scale, 76 * scale, 63 * scale);
            }
            DrawHealthBar(g, s.X - 30 * scale, s.Y - 58 * scale, 60 * scale, 7, e.Hp, e.MaxHp, e.Boss);
        }

        void DrawPlayer(Graphics g)
        {
            PointF basePt = Project(player.X, player.Z, 0);
            DrawShadow(g, basePt, 43, 14, 90);
            PointF s = Project(player.X, player.Z, 0.45f);

            using (SolidBrush cape = new SolidBrush(Color.FromArgb(50, 146, 220)))
            {
                PointF[] capePts = new PointF[]
                {
                    new PointF(s.X - player.LastDirX * 8 - 16, s.Y - 30 - player.LastDirZ * 2),
                    new PointF(s.X - player.LastDirX * 22, s.Y - 2),
                    new PointF(s.X + 18, s.Y - 10)
                };
                g.FillPolygon(cape, capePts);
            }
            using (SolidBrush apron = new SolidBrush(Color.FromArgb(242, 250, 255)))
                g.FillEllipse(apron, s.X - 18, s.Y - 38, 36, 42);
            using (Pen trim = new Pen(Color.FromArgb(39, 117, 177), 2))
                g.DrawEllipse(trim, s.X - 18, s.Y - 38, 36, 42);
            using (SolidBrush face = new SolidBrush(Color.FromArgb(237, 183, 137)))
                g.FillEllipse(face, s.X - 13, s.Y - 55, 26, 26);
            using (SolidBrush hat = new SolidBrush(Color.White))
            {
                g.FillEllipse(hat, s.X - 17, s.Y - 69, 34, 18);
                g.FillRectangle(hat, s.X - 12, s.Y - 60, 24, 12);
            }
            using (Pen pan = new Pen(Color.FromArgb(44, 56, 67), 5))
                g.DrawLine(pan, s.X + 12, s.Y - 33, s.X + 34, s.Y - 45);
            using (SolidBrush wok = new SolidBrush(Color.FromArgb(50, 60, 70)))
                g.FillEllipse(wok, s.X + 25, s.Y - 55, 22, 14);
            using (Pen wand = new Pen(Color.FromArgb(129, 82, 43), 4))
                g.DrawLine(wand, s.X - 12, s.Y - 31, s.X - 32, s.Y - 50);
        }

        void DrawProjectile(Graphics g, Projectile p)
        {
            PointF s = Project(p.X, p.Z, p.Y);
            using (SolidBrush glow = new SolidBrush(Color.FromArgb(90, p.Color)))
                g.FillEllipse(glow, s.X - 20, s.Y - 20, 40, 40);
            using (SolidBrush core = new SolidBrush(p.Color))
                g.FillEllipse(core, s.X - 8, s.Y - 8, 16, 16);
        }

        void DrawParticle(Graphics g, Particle p)
        {
            PointF s = Project(p.X, p.Z, p.Y);
            int alpha = (int)(255 * Math.Max(0, p.Life / Math.Max(0.001f, p.MaxLife)));
            if (alpha > 255) alpha = 255;
            if (alpha < 0) alpha = 0;
            if (p.Rain)
            {
                using (Pen rain = new Pen(Color.FromArgb(alpha / 2, p.Color), 1))
                    g.DrawLine(rain, s.X, s.Y, s.X + 12, s.Y + 25);
            }
            else
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(alpha, p.Color)))
                    g.FillEllipse(b, s.X - p.Size / 2, s.Y - p.Size / 2, p.Size, p.Size);
            }
        }

        void DrawHud(Graphics g)
        {
            Rectangle hud = new Rectangle(18, 16, 338, 124);
            DrawPanel(g, hud, 235);
            using (SolidBrush text = new SolidBrush(Color.FromArgb(31, 69, 96)))
            {
                g.DrawString("Food Realms", h2Font, text, 36, 28);
                g.DrawString(CurrentLevel().Name, uiFont, text, 36, 51);
                g.DrawString("Nivel " + player.Level.ToString() + "   Monedas " + player.Coins.ToString() + "   Pociones " + player.Potions.ToString(), smallFont, text, 36, 113);
            }
            DrawBar(g, 36, 77, 205, 14, player.Health, player.MaxHealth, Color.FromArgb(230, 70, 80), "Vida");
            DrawBar(g, 36, 96, 205, 14, player.Mana, player.MaxMana, Color.FromArgb(54, 142, 232), "Mana");

            Rectangle quest = new Rectangle(ClientSize.Width - 372, 16, 350, 104);
            DrawPanel(g, quest, 225);
            using (SolidBrush text = new SolidBrush(Color.FromArgb(31, 69, 96)))
            {
                g.DrawString("Mision activa", h2Font, text, quest.X + 18, quest.Y + 14);
                g.DrawString(CurrentLevel().Quest, uiFont, text, new RectangleF(quest.X + 18, quest.Y + 40, quest.Width - 36, 38));
                string status = "Enemigos " + killsThisLevel.ToString() + " | Ingredientes " + ingredientsThisLevel.ToString() + " | Jefe " + (bossDefeated ? "vencido" : "pendiente");
                g.DrawString(status, smallFont, text, quest.X + 18, quest.Y + 78);
            }

            DrawSpellBar(g);
            DrawMinimap(g);
        }

        void DrawSpellBar(Graphics g)
        {
            int y = ClientSize.Height - 82;
            int x = ClientSize.Width / 2 - 194;
            DrawPanel(g, new Rectangle(x - 18, y - 12, 388, 67), 218);
            DrawSpell(g, x, y, "1", "Wok", player.WokCooldown, 0.62f, Color.FromArgb(255, 112, 42));
            DrawSpell(g, x + 92, y, "2", "Te", player.TeaCooldown, 5.5f, Color.FromArgb(95, 220, 185));
            DrawSpell(g, x + 184, y, "3", "Hielo", player.FrostCooldown, 4.0f, Color.FromArgb(139, 230, 255));
            DrawSpell(g, x + 276, y, "4", "Curry", player.CurryCooldown, 8.0f, Color.FromArgb(255, 190, 57));
        }

        void DrawSpell(Graphics g, int x, int y, string key, string name, float cooldown, float maxCooldown, Color color)
        {
            Rectangle box = new Rectangle(x, y, 74, 44);
            using (GraphicsPath path = RoundedRect(box, 8))
            using (SolidBrush back = new SolidBrush(Color.FromArgb(245, 255, 255, 255)))
            using (Pen border = new Pen(Color.FromArgb(95, 115, 181, 225), 1))
            {
                g.FillPath(back, path);
                g.DrawPath(border, path);
            }
            using (SolidBrush c = new SolidBrush(color))
                g.FillEllipse(c, x + 9, y + 10, 24, 24);
            using (SolidBrush t = new SolidBrush(Color.FromArgb(34, 76, 105)))
            {
                g.DrawString(key, h2Font, t, x + 39, y + 5);
                g.DrawString(name, smallFont, t, x + 38, y + 26);
            }
            if (cooldown > 0)
            {
                float ratio = Math.Min(1, cooldown / maxCooldown);
                using (SolidBrush shade = new SolidBrush(Color.FromArgb(150, 20, 40, 60)))
                    g.FillRectangle(shade, x, y, 74, 44 * ratio);
            }
        }

        void DrawMinimap(Graphics g)
        {
            int w = 128;
            int h = 128;
            Rectangle map = new Rectangle(ClientSize.Width - w - 24, ClientSize.Height - h - 24, w, h);
            DrawPanel(g, map, 205);
            LevelData level = CurrentLevel();
            float sx = (w - 22) / (float)level.Size;
            float sy = (h - 22) / (float)level.Size;
            using (SolidBrush prop = new SolidBrush(Color.FromArgb(92, 124, 142)))
            {
                int i;
                for (i = 0; i < props.Count; i++)
                {
                    Prop p = props[i];
                    if (!p.Solid && p.Kind != "portal" && p.Kind != "shop") continue;
                    Color cc = p.Kind == "portal" ? Color.FromArgb(84, 169, 230) : p.Kind == "shop" ? Color.FromArgb(245, 190, 73) : Color.FromArgb(96, 118, 132);
                    using (SolidBrush b = new SolidBrush(cc))
                        g.FillRectangle(b, map.X + 11 + p.X * sx, map.Y + 11 + p.Z * sy, 3, 3);
                }
            }
            using (SolidBrush enemy = new SolidBrush(Color.FromArgb(215, 80, 80)))
            {
                int i;
                for (i = 0; i < enemies.Count; i++)
                    if (enemies[i].Hp > 0) g.FillEllipse(enemy, map.X + 11 + enemies[i].X * sx, map.Y + 11 + enemies[i].Z * sy, enemies[i].Boss ? 6 : 3, enemies[i].Boss ? 6 : 3);
            }
            using (SolidBrush hero = new SolidBrush(Color.FromArgb(44, 136, 220)))
                g.FillEllipse(hero, map.X + 8 + player.X * sx, map.Y + 8 + player.Z * sy, 8, 8);
        }

        void DrawInventory(Graphics g)
        {
            Rectangle panel = CenterRect(600, 430);
            DrawModalShade(g);
            DrawPanel(g, panel, 246);
            using (SolidBrush text = new SolidBrush(Color.FromArgb(28, 67, 96)))
            {
                g.DrawString("Inventario del Chef", h1Font, text, panel.X + 28, panel.Y + 25);
                g.DrawString("Arma: sarten heroica  |  Armadura: delantal azul  |  Magia: libro de especias", uiFont, text, panel.X + 30, panel.Y + 70);
                g.DrawString("Ataque " + player.Attack.ToString() + "   Poder " + player.MagicPower.ToString() + "   Armadura " + player.Armor.ToString(), uiFont, text, panel.X + 30, panel.Y + 98);
                g.DrawString("Pociones: " + player.Potions.ToString() + "        Monedas: " + player.Coins.ToString(), h2Font, text, panel.X + 30, panel.Y + 132);
                int y = panel.Y + 174;
                foreach (string key in player.Items.Keys)
                {
                    g.DrawString(key + ": " + player.Items[key].ToString(), uiFont, text, panel.X + 44, y);
                    y += 25;
                }
                g.DrawString("H usa una pocion si falta vida. I vuelve al juego.", smallFont, text, panel.X + 30, panel.Bottom - 38);
            }
        }

        void DrawShop(Graphics g)
        {
            Rectangle panel = CenterRect(650, 430);
            DrawModalShade(g);
            DrawPanel(g, panel, 248);
            using (SolidBrush text = new SolidBrush(Color.FromArgb(28, 67, 96)))
            {
                g.DrawString("Tienda ambulante", h1Font, text, panel.X + 28, panel.Y + 25);
                g.DrawString("Monedas disponibles: " + player.Coins.ToString(), h2Font, text, panel.X + 30, panel.Y + 72);
                DrawOffer(g, panel.X + 35, panel.Y + 122, "1", "Pocion de caldo", "Cura vida durante la aventura.", 25, Color.FromArgb(86, 220, 168));
                DrawOffer(g, panel.X + 35, panel.Y + 185, "2", "Sarten reforzada", "Sube el ataque cuerpo a cuerpo.", 70, Color.FromArgb(70, 90, 104));
                DrawOffer(g, panel.X + 35, panel.Y + 248, "3", "Delantal blindado", "Mas salud y armadura.", 90, Color.FromArgb(86, 159, 222));
                DrawOffer(g, panel.X + 35, panel.Y + 311, "4", "Libro de especias", "Aumenta mana y poder magico.", 110, Color.FromArgb(255, 190, 57));
                g.DrawString("E o ESC cierra la tienda.", smallFont, text, panel.X + 30, panel.Bottom - 34);
            }
        }

        void DrawOffer(Graphics g, int x, int y, string key, string title, string desc, int price, Color color)
        {
            Rectangle r = new Rectangle(x, y, 580, 48);
            using (GraphicsPath path = RoundedRect(r, 8))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(238, 255, 255, 255)))
            using (Pen pen = new Pen(Color.FromArgb(95, 118, 178, 220), 1))
            {
                g.FillPath(b, path);
                g.DrawPath(pen, path);
            }
            using (SolidBrush icon = new SolidBrush(color))
                g.FillEllipse(icon, x + 13, y + 11, 26, 26);
            using (SolidBrush text = new SolidBrush(Color.FromArgb(31, 70, 98)))
            {
                g.DrawString(key, h2Font, text, x + 49, y + 7);
                g.DrawString(title, h2Font, text, x + 82, y + 6);
                g.DrawString(desc, smallFont, text, x + 84, y + 28);
                g.DrawString(price.ToString() + " monedas", uiFont, text, x + 464, y + 15);
            }
        }

        void DrawJournal(Graphics g)
        {
            Rectangle panel = CenterRect(610, 390);
            DrawModalShade(g);
            DrawPanel(g, panel, 246);
            using (SolidBrush text = new SolidBrush(Color.FromArgb(28, 67, 96)))
            {
                g.DrawString("Diario de misiones", h1Font, text, panel.X + 28, panel.Y + 25);
                g.DrawString(CurrentLevel().Name, h2Font, text, panel.X + 30, panel.Y + 76);
                g.DrawString(CurrentLevel().Quest, uiFont, text, new RectangleF(panel.X + 30, panel.Y + 105, panel.Width - 60, 54));
                g.DrawString("Objetivo principal: vencer a " + CurrentLevel().BossName + ".", uiFont, text, panel.X + 30, panel.Y + 168);
                g.DrawString("Ingrediente legendario: " + CurrentLevel().Ingredient + ".", uiFont, text, panel.X + 30, panel.Y + 198);
                g.DrawString("Progreso: enemigos derrotados " + killsThisLevel.ToString() + ", ingredientes recogidos " + ingredientsThisLevel.ToString() + ", jefes totales " + totalBosses.ToString() + ".", uiFont, text, panel.X + 30, panel.Y + 228);
                g.DrawString("J o ESC vuelve al juego.", smallFont, text, panel.X + 30, panel.Bottom - 36);
            }
        }

        void DrawPaused(Graphics g)
        {
            DrawModalShade(g);
            Rectangle panel = CenterRect(430, 180);
            DrawPanel(g, panel, 246);
            DrawCentered(g, "Pausa", h1Font, Color.FromArgb(28, 67, 96), panel.Y + 38);
            DrawCentered(g, "ENTER o ESC para volver", uiFont, Color.FromArgb(56, 92, 116), panel.Y + 88);
        }

        void DrawGameOver(Graphics g)
        {
            DrawModalShade(g);
            Rectangle panel = CenterRect(520, 220);
            DrawPanel(g, panel, 246);
            DrawCentered(g, "Has caido en la mazmorra", h1Font, Color.FromArgb(133, 49, 58), panel.Y + 45);
            DrawCentered(g, "R reinicia la aventura", uiFont, Color.FromArgb(56, 92, 116), panel.Y + 100);
        }

        void DrawVictory(Graphics g)
        {
            DrawModalShade(g);
            Rectangle panel = CenterRect(620, 280);
            DrawPanel(g, panel, 248);
            DrawCentered(g, "Victoria de Nicolas Herguera", h1Font, Color.FromArgb(26, 104, 166), panel.Y + 42);
            DrawCentered(g, "El Dragon Noodle ha caido y los Reinos de la Comida vuelven a brillar.", uiFont, Color.FromArgb(45, 79, 105), panel.Y + 94);
            DrawCentered(g, "Jefes vencidos: " + totalBosses.ToString() + "   Nivel del chef: " + player.Level.ToString(), h2Font, Color.FromArgb(33, 86, 130), panel.Y + 145);
            DrawCentered(g, "R reinicia la campana", uiFont, Color.FromArgb(56, 92, 116), panel.Y + 202);
        }

        void DrawToasts(Graphics g)
        {
            int y = 154;
            int i;
            for (i = toasts.Count - 1; i >= 0; i--)
            {
                Toast t = toasts[i];
                int alpha = (int)(Math.Min(1, t.Life) * 220);
                Rectangle r = new Rectangle(20, y, 390, 34);
                using (GraphicsPath path = RoundedRect(r, 8))
                using (SolidBrush b = new SolidBrush(Color.FromArgb(alpha, 250, 253, 255)))
                    g.FillPath(b, path);
                using (SolidBrush text = new SolidBrush(Color.FromArgb(alpha, 35, 79, 108)))
                    g.DrawString(t.Text, uiFont, text, r.X + 13, r.Y + 8);
                y += 40;
            }
        }

        void DrawWeatherOverlay(Graphics g)
        {
            float night = NightAmount();
            if (night > 0.05f)
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb((int)(night * 105), 9, 24, 55)))
                    g.FillRectangle(b, ClientRectangle);
            }
            if (raining)
            {
                using (SolidBrush b = new SolidBrush(Color.FromArgb(24, 155, 195, 230)))
                    g.FillRectangle(b, ClientRectangle);
            }
        }

        void DrawPanel(Graphics g, Rectangle r, int alpha)
        {
            using (GraphicsPath path = RoundedRect(r, 8))
            using (SolidBrush b = new SolidBrush(Color.FromArgb(alpha, 250, 253, 255)))
            using (Pen pen = new Pen(Color.FromArgb(130, 116, 179, 222), 1))
            {
                g.FillPath(b, path);
                g.DrawPath(pen, path);
            }
        }

        void DrawModalShade(Graphics g)
        {
            using (SolidBrush shade = new SolidBrush(Color.FromArgb(108, 20, 44, 70)))
                g.FillRectangle(shade, ClientRectangle);
        }

        void DrawBar(Graphics g, int x, int y, int w, int h, float value, float max, Color color, string label)
        {
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(220, 230, 240, 247)))
                g.FillRectangle(bg, x, y, w, h);
            float ratio = max <= 0 ? 0 : Math.Max(0, Math.Min(1, value / max));
            using (SolidBrush fill = new SolidBrush(color))
                g.FillRectangle(fill, x, y, (int)(w * ratio), h);
            using (Pen pen = new Pen(Color.FromArgb(110, 52, 94, 127), 1))
                g.DrawRectangle(pen, x, y, w, h);
            using (SolidBrush text = new SolidBrush(Color.FromArgb(240, 255, 255, 255)))
                g.DrawString(label + " " + ((int)value).ToString() + "/" + ((int)max).ToString(), smallFont, text, x + 6, y - 1);
        }

        void DrawHealthBar(Graphics g, float x, float y, float w, float h, float value, float max, bool boss)
        {
            using (SolidBrush bg = new SolidBrush(Color.FromArgb(190, 45, 35, 42)))
                g.FillRectangle(bg, x, y, w, h);
            float ratio = max <= 0 ? 0 : Math.Max(0, Math.Min(1, value / max));
            using (SolidBrush fill = new SolidBrush(boss ? Color.FromArgb(197, 46, 64) : Color.FromArgb(238, 91, 85)))
                g.FillRectangle(fill, x, y, w * ratio, h);
            using (Pen pen = new Pen(Color.FromArgb(180, 255, 255, 255), 1))
                g.DrawRectangle(pen, x, y, w, h);
        }

        void DrawShadow(Graphics g, PointF p, float w, float h, int alpha)
        {
            using (SolidBrush b = new SolidBrush(Color.FromArgb(alpha, 22, 31, 38)))
                g.FillEllipse(b, p.X - w / 2, p.Y - h / 2, w, h);
        }

        void DrawCentered(Graphics g, string text, Font font, Color color, float y)
        {
            SizeF size = g.MeasureString(text, font);
            using (SolidBrush b = new SolidBrush(color))
                g.DrawString(text, font, b, ClientSize.Width / 2 - size.Width / 2, y);
        }

        void DrawCenteredAt(Graphics g, string text, Font font, Color color, float x, float y)
        {
            SizeF size = g.MeasureString(text, font);
            using (SolidBrush b = new SolidBrush(color))
                g.DrawString(text, font, b, x - size.Width / 2, y - size.Height / 2);
        }

        PointF Project(float x, float z, float y)
        {
            float camX = (player.X - player.Z) * TileW / 2f;
            float camY = (player.X + player.Z) * TileH / 2f;
            float sx = (x - z) * TileW / 2f - camX + ClientSize.Width / 2f;
            float sy = (x + z) * TileH / 2f - camY - y * TileLift + ClientSize.Height / 2f + 80;
            return new PointF(sx, sy);
        }

        float Distance(float ax, float az, float bx, float bz)
        {
            float dx = ax - bx;
            float dz = az - bz;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        float NightAmount()
        {
            float n = (float)Math.Cos((timeOfDay - 0.5f) * Math.PI * 2);
            n = (n + 1) / 2f;
            return Math.Max(0, Math.Min(1, n * 0.75f));
        }

        Color Mix(Color a, Color b, float t)
        {
            if (t < 0) t = 0;
            if (t > 1) t = 1;
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        Color Darken(Color c, float amount)
        {
            return Color.FromArgb((int)(c.R * amount), (int)(c.G * amount), (int)(c.B * amount));
        }

        Rectangle CenterRect(int w, int h)
        {
            return new Rectangle(ClientSize.Width / 2 - w / 2, ClientSize.Height / 2 - h / 2, w, h);
        }

        GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    class LevelData
    {
        public string Name;
        public string Description;
        public string TextureKey;
        public string Ingredient;
        public string BossName;
        public string Quest;
        public Color Day;
        public Color Night;
        public Color Accent;
        public bool AsianFocus;
        public int Size;

        public LevelData(string name, string description, string textureKey, string ingredient, string bossName, Color day, Color night, string quest, bool asianFocus)
        {
            Name = name;
            Description = description;
            TextureKey = textureKey;
            Ingredient = ingredient;
            BossName = bossName;
            Day = day;
            Night = night;
            Quest = quest;
            AsianFocus = asianFocus;
            Size = 32;
            Accent = Color.FromArgb((day.R + 255) / 2, (day.G + 255) / 2, (day.B + 255) / 2);
        }
    }

    class Player
    {
        public float X;
        public float Z;
        public float LastDirX = 1;
        public float LastDirZ;
        public float Health;
        public float MaxHealth;
        public float Mana;
        public float MaxMana;
        public float Speed;
        public float Attack;
        public float MagicPower;
        public float Armor;
        public int Level;
        public int Xp;
        public int Coins;
        public int Potions;
        public float MeleeCooldown;
        public float WokCooldown;
        public float TeaCooldown;
        public float FrostCooldown;
        public float CurryCooldown;
        public readonly Dictionary<string, int> Items = new Dictionary<string, int>();
    }

    class Enemy
    {
        public string Name;
        public float X;
        public float Z;
        public float SpawnX;
        public float SpawnZ;
        public float Hp;
        public float MaxHp;
        public float Speed;
        public float Damage;
        public float Radius;
        public float Wander;
        public float AttackClock;
        public float SlowTimer;
        public float Flash;
        public bool Boss;
        public Color Color;
    }

    class Prop
    {
        public float X;
        public float Z;
        public string Kind;
        public bool Solid;
        public float Radius;

        public Prop(float x, float z, string kind, bool solid)
        {
            X = x;
            Z = z;
            Kind = kind;
            Solid = solid;
            Radius = solid ? 0.72f : 0.42f;
            if (kind == "wall") Radius = 0.62f;
            if (kind == "shop") Radius = 1.05f;
        }
    }

    class Pickup
    {
        public float X;
        public float Z;
        public string Kind;
        public int Amount;
        public float FloatClock;
    }

    class Projectile
    {
        public float X;
        public float Z;
        public float Y;
        public float VX;
        public float VZ;
        public float VY;
        public float Life;
        public float Radius;
        public float Damage;
        public string Kind;
        public Color Color;
    }

    class Particle
    {
        public float X;
        public float Z;
        public float Y;
        public float VX;
        public float VZ;
        public float VY;
        public float Life;
        public float MaxLife;
        public int Size;
        public bool Rain;
        public Color Color;
    }

    class Toast
    {
        public string Text;
        public float Life;
    }

    class RenderItem
    {
        public float Order;
        public int Kind;
        public object Ref;

        public RenderItem(float order, int kind, object obj)
        {
            Order = order;
            Kind = kind;
            Ref = obj;
        }
    }
}
