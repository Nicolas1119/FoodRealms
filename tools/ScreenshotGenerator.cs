using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;

class ScreenshotGenerator
{
    const int W = 1280;
    const int H = 720;
    const int TileW = 94;
    const int TileH = 47;
    static Random rng = new Random(44);

    static void Main()
    {
        string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "assets");
        Directory.CreateDirectory(root);
        CreateScene(Path.Combine(root, "capture-overworld.png"), "Pradera del Desayuno", "Exploracion, inventario y tienda", 0);
        CreateScene(Path.Combine(root, "capture-sushi-temple.png"), "Templo de Sushi Lunar", "Nivel asiatico con lluvia nocturna", 1);
        CreateScene(Path.Combine(root, "capture-ramen-boss.png"), "Palacio del Ramen Eterno", "Jefe final: Dragon Noodle", 2);
        CreatePoster(Path.Combine(root, "hero-poster.png"));
    }

    static void CreateScene(string path, string title, string subtitle, int style)
    {
        using (Bitmap bmp = new Bitmap(W, H))
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            DrawSky(g, style);
            DrawIsoField(g, style);
            DrawDecor(g, style);
            DrawHero(g, 650, 395, 1.0f);
            if (style == 2) DrawBoss(g, 820, 320);
            else DrawEnemies(g, style);
            if (style == 1) DrawRain(g);
            DrawFakeHud(g, title, subtitle, style);
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
    }

    static void CreatePoster(string path)
    {
        using (Bitmap bmp = new Bitmap(W, H))
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (LinearGradientBrush sky = new LinearGradientBrush(new Rectangle(0, 0, W, H), Color.FromArgb(239, 250, 255), Color.FromArgb(207, 236, 251), LinearGradientMode.Vertical))
                g.FillRectangle(sky, 0, 0, W, H);
            DrawIsoField(g, 2);
            DrawDecor(g, 2);
            DrawHero(g, 575, 390, 1.25f);
            DrawBoss(g, 795, 300);
            using (SolidBrush veil = new SolidBrush(Color.FromArgb(95, 255, 255, 255)))
                g.FillRectangle(veil, 0, 0, W, H);
            using (Font title = new Font("Segoe UI", 74, FontStyle.Bold))
            using (Font sub = new Font("Segoe UI", 22, FontStyle.Regular))
            using (SolidBrush blue = new SolidBrush(Color.FromArgb(20, 92, 154)))
            using (SolidBrush grey = new SolidBrush(Color.FromArgb(50, 72, 86)))
            {
                g.DrawString("Food Realms", title, blue, 78, 72);
                g.DrawString("Una obra jugable de cocina, magia y aventura.", sub, grey, 88, 178);
            }
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        }
    }

    static void DrawSky(Graphics g, int style)
    {
        Color a = style == 1 ? Color.FromArgb(29, 61, 105) : Color.FromArgb(228, 248, 255);
        Color b = style == 1 ? Color.FromArgb(96, 151, 181) : Color.FromArgb(255, 252, 235);
        using (LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(0, 0, W, H), a, b, LinearGradientMode.Vertical))
            g.FillRectangle(brush, 0, 0, W, H);
        using (SolidBrush sun = new SolidBrush(style == 1 ? Color.FromArgb(220, 237, 255) : Color.FromArgb(255, 223, 111)))
            g.FillEllipse(sun, style == 1 ? 1040 : 940, style == 1 ? 72 : 58, 76, 76);
    }

    static void DrawIsoField(Graphics g, int style)
    {
        int ox = W / 2;
        int oy = 210;
        for (int sum = 0; sum < 26; sum++)
        {
            for (int x = 0; x < 14; x++)
            {
                int z = sum - x;
                if (z < 0 || z >= 14) continue;
                PointF p = Iso(x, z, ox, oy);
                PointF[] d = new PointF[]
                {
                    new PointF(p.X, p.Y - TileH / 2),
                    new PointF(p.X + TileW / 2, p.Y),
                    new PointF(p.X, p.Y + TileH / 2),
                    new PointF(p.X - TileW / 2, p.Y)
                };
                Color baseColor = TerrainColor(style, x, z);
                using (LinearGradientBrush tile = new LinearGradientBrush(Bounds(d), Light(baseColor, 1.18f), Dark(baseColor, 0.86f), 45))
                    g.FillPolygon(tile, d);
                AddTextureMarks(g, d, style, x, z);
                using (Pen edge = new Pen(Color.FromArgb(36, 40, 91, 124), 1))
                    g.DrawPolygon(edge, d);
            }
        }
    }

    static Color TerrainColor(int style, int x, int z)
    {
        if (style == 1) return (x + z) % 5 == 0 ? Color.FromArgb(214, 235, 226) : Color.FromArgb(68, 154, 148);
        if (style == 2) return (x + z) % 4 == 0 ? Color.FromArgb(210, 151, 75) : Color.FromArgb(226, 188, 97);
        return (x + z) % 6 == 0 ? Color.FromArgb(248, 217, 136) : Color.FromArgb(135, 189, 111);
    }

    static void AddTextureMarks(Graphics g, PointF[] d, int style, int x, int z)
    {
        GraphicsState state = g.Save();
        using (GraphicsPath path = new GraphicsPath())
        {
            path.AddPolygon(d);
            g.SetClip(path);
            RectangleF r = Bounds(d);
            if (style == 1)
            {
                using (Pen nori = new Pen(Color.FromArgb(70, 26, 74, 68), 5))
                    g.DrawLine(nori, r.Left, r.Top + 14, r.Right, r.Bottom - 9);
                using (SolidBrush rice = new SolidBrush(Color.FromArgb(110, Color.White)))
                    g.FillEllipse(rice, r.Left + 20 + (x * 7) % 28, r.Top + 12 + (z * 5) % 18, 7, 4);
            }
            else if (style == 2)
            {
                using (Pen noodle = new Pen(Color.FromArgb(92, 255, 230, 132), 4))
                    g.DrawBezier(noodle, r.Left - 8, r.Top + 18, r.Left + 18, r.Bottom, r.Right - 20, r.Top, r.Right + 8, r.Bottom - 10);
            }
            else
            {
                using (Pen grass = new Pen(Color.FromArgb(80, 74, 139, 72), 1))
                    g.DrawLine(grass, r.Left + 24, r.Top + 18, r.Left + 31, r.Top + 8);
                using (SolidBrush honey = new SolidBrush(Color.FromArgb(70, 255, 210, 80)))
                    g.FillEllipse(honey, r.Left + 48, r.Top + 16, 20, 8);
            }
        }
        g.Restore(state);
    }

    static void DrawDecor(Graphics g, int style)
    {
        if (style == 0)
        {
            DrawShop(g, 420, 355);
            DrawFoodCrate(g, 765, 410, Color.FromArgb(228, 86, 70));
            DrawFoodCrate(g, 845, 455, Color.FromArgb(245, 189, 74));
            DrawLantern(g, 545, 318);
        }
        else if (style == 1)
        {
            DrawSushi(g, 420, 365);
            DrawSushi(g, 510, 330);
            DrawLantern(g, 760, 305);
            DrawLantern(g, 900, 385);
            DrawPortal(g, 820, 435);
        }
        else
        {
            DrawBowl(g, 435, 355);
            DrawBowl(g, 535, 415);
            DrawLantern(g, 920, 360);
            DrawPortal(g, 990, 435);
        }
    }

    static void DrawHero(Graphics g, float x, float y, float scale)
    {
        using (SolidBrush shadow = new SolidBrush(Color.FromArgb(80, 30, 38, 46)))
            g.FillEllipse(shadow, x - 33 * scale, y - 4 * scale, 66 * scale, 20 * scale);
        using (SolidBrush cape = new SolidBrush(Color.FromArgb(63, 157, 225)))
            g.FillPolygon(cape, new PointF[] { new PointF(x - 22 * scale, y - 52 * scale), new PointF(x - 44 * scale, y - 2 * scale), new PointF(x + 18 * scale, y - 16 * scale) });
        using (SolidBrush apron = new SolidBrush(Color.White))
            g.FillEllipse(apron, x - 24 * scale, y - 64 * scale, 48 * scale, 58 * scale);
        using (Pen trim = new Pen(Color.FromArgb(32, 111, 176), 3 * scale))
            g.DrawEllipse(trim, x - 24 * scale, y - 64 * scale, 48 * scale, 58 * scale);
        using (SolidBrush face = new SolidBrush(Color.FromArgb(235, 185, 138)))
            g.FillEllipse(face, x - 17 * scale, y - 88 * scale, 34 * scale, 34 * scale);
        using (SolidBrush hat = new SolidBrush(Color.White))
        {
            g.FillEllipse(hat, x - 23 * scale, y - 106 * scale, 46 * scale, 22 * scale);
            g.FillRectangle(hat, x - 15 * scale, y - 94 * scale, 30 * scale, 15 * scale);
        }
        using (Pen wand = new Pen(Color.FromArgb(116, 72, 42), 5 * scale))
            g.DrawLine(wand, x - 18 * scale, y - 45 * scale, x - 50 * scale, y - 74 * scale);
        using (SolidBrush spark = new SolidBrush(Color.FromArgb(255, 204, 74)))
            g.FillEllipse(spark, x - 60 * scale, y - 86 * scale, 16 * scale, 16 * scale);
    }

    static void DrawEnemies(Graphics g, int style)
    {
        DrawEnemy(g, 800, 360, style == 1 ? Color.FromArgb(39, 130, 105) : Color.FromArgb(211, 78, 62), 1.0f);
        DrawEnemy(g, 925, 455, style == 1 ? Color.FromArgb(220, 235, 225) : Color.FromArgb(224, 155, 64), 0.82f);
    }

    static void DrawEnemy(Graphics g, float x, float y, Color c, float scale)
    {
        using (SolidBrush shadow = new SolidBrush(Color.FromArgb(75, 30, 38, 46)))
            g.FillEllipse(shadow, x - 28 * scale, y - 2 * scale, 56 * scale, 16 * scale);
        using (SolidBrush body = new SolidBrush(c))
            g.FillEllipse(body, x - 25 * scale, y - 58 * scale, 50 * scale, 52 * scale);
        using (Pen edge = new Pen(Color.FromArgb(78, 45, 55), 2 * scale))
            g.DrawEllipse(edge, x - 25 * scale, y - 58 * scale, 50 * scale, 52 * scale);
        using (SolidBrush eye = new SolidBrush(Color.FromArgb(30, 35, 40)))
        {
            g.FillEllipse(eye, x - 10 * scale, y - 38 * scale, 6 * scale, 7 * scale);
            g.FillEllipse(eye, x + 6 * scale, y - 38 * scale, 6 * scale, 7 * scale);
        }
    }

    static void DrawBoss(Graphics g, float x, float y)
    {
        using (SolidBrush glow = new SolidBrush(Color.FromArgb(70, 255, 180, 65)))
            g.FillEllipse(glow, x - 120, y - 105, 240, 170);
        DrawEnemy(g, x, y + 72, Color.FromArgb(218, 112, 45), 2.25f);
        using (Pen noodle = new Pen(Color.FromArgb(255, 226, 114), 8))
        {
            g.DrawBezier(noodle, x - 70, y - 40, x - 15, y - 120, x + 40, y + 20, x + 100, y - 55);
            g.DrawBezier(noodle, x - 85, y - 5, x - 20, y - 80, x + 52, y + 65, x + 112, y - 5);
        }
        using (Pen horn = new Pen(Color.FromArgb(255, 235, 150), 7))
        {
            g.DrawLine(horn, x - 55, y - 10, x - 96, y - 66);
            g.DrawLine(horn, x + 55, y - 10, x + 96, y - 66);
        }
    }

    static void DrawShop(Graphics g, float x, float y)
    {
        using (SolidBrush shadow = new SolidBrush(Color.FromArgb(75, 30, 38, 46)))
            g.FillEllipse(shadow, x - 70, y - 8, 140, 34);
        using (SolidBrush wood = new SolidBrush(Color.FromArgb(138, 86, 43)))
            g.FillRectangle(wood, x - 54, y - 58, 108, 54);
        using (Pen edge = new Pen(Color.FromArgb(84, 51, 31), 3))
            g.DrawRectangle(edge, x - 54, y - 58, 108, 54);
        using (LinearGradientBrush awning = new LinearGradientBrush(new RectangleF(x - 66, y - 96, 132, 42), Color.White, Color.FromArgb(93, 176, 231), LinearGradientMode.Horizontal))
            g.FillRectangle(awning, x - 66, y - 96, 132, 42);
        using (Pen border = new Pen(Color.FromArgb(53, 120, 174), 3))
            g.DrawRectangle(border, x - 66, y - 96, 132, 42);
    }

    static void DrawFoodCrate(Graphics g, float x, float y, Color food)
    {
        using (SolidBrush wood = new SolidBrush(Color.FromArgb(139, 85, 45)))
            g.FillRectangle(wood, x - 34, y - 42, 68, 38);
        using (Pen edge = new Pen(Color.FromArgb(82, 52, 34), 3))
            g.DrawRectangle(edge, x - 34, y - 42, 68, 38);
        using (SolidBrush f = new SolidBrush(food))
        {
            g.FillEllipse(f, x - 26, y - 58, 24, 20);
            g.FillEllipse(f, x + 1, y - 60, 24, 22);
            g.FillEllipse(f, x - 8, y - 68, 18, 20);
        }
    }

    static void DrawSushi(Graphics g, float x, float y)
    {
        using (SolidBrush nori = new SolidBrush(Color.FromArgb(28, 72, 62)))
            g.FillEllipse(nori, x - 32, y - 58, 64, 42);
        using (SolidBrush rice = new SolidBrush(Color.FromArgb(246, 250, 248)))
            g.FillEllipse(rice, x - 23, y - 53, 46, 32);
        using (SolidBrush fish = new SolidBrush(Color.FromArgb(233, 85, 82)))
            g.FillEllipse(fish, x - 9, y - 43, 18, 16);
    }

    static void DrawBowl(Graphics g, float x, float y)
    {
        using (SolidBrush bowl = new SolidBrush(Color.FromArgb(235, 248, 255)))
            g.FillEllipse(bowl, x - 45, y - 44, 90, 42);
        using (Pen edge = new Pen(Color.FromArgb(44, 121, 179), 3))
            g.DrawEllipse(edge, x - 45, y - 44, 90, 42);
        using (SolidBrush soup = new SolidBrush(Color.FromArgb(194, 116, 54)))
            g.FillEllipse(soup, x - 35, y - 39, 70, 30);
        using (Pen noodle = new Pen(Color.FromArgb(255, 225, 117), 5))
            g.DrawBezier(noodle, x - 28, y - 28, x - 3, y - 52, x + 18, y - 9, x + 30, y - 28);
    }

    static void DrawLantern(Graphics g, float x, float y)
    {
        using (Pen pole = new Pen(Color.FromArgb(82, 70, 59), 4))
            g.DrawLine(pole, x, y - 92, x, y - 10);
        using (SolidBrush glow = new SolidBrush(Color.FromArgb(85, 255, 220, 110)))
            g.FillEllipse(glow, x - 38, y - 108, 76, 54);
        using (SolidBrush red = new SolidBrush(Color.FromArgb(232, 75, 67)))
            g.FillEllipse(red, x - 19, y - 96, 38, 36);
    }

    static void DrawPortal(Graphics g, float x, float y)
    {
        using (Pen ring = new Pen(Color.FromArgb(150, 100, 199, 244), 7))
            g.DrawEllipse(ring, x - 62, y - 62, 124, 76);
        using (SolidBrush core = new SolidBrush(Color.FromArgb(92, 55, 159, 225)))
            g.FillEllipse(core, x - 35, y - 50, 70, 46);
    }

    static void DrawRain(Graphics g)
    {
        using (Pen rain = new Pen(Color.FromArgb(105, 215, 238, 255), 2))
        {
            for (int i = 0; i < 130; i++)
            {
                int x = rng.Next(W);
                int y = rng.Next(H);
                g.DrawLine(rain, x, y, x + 18, y + 38);
            }
        }
        using (SolidBrush wash = new SolidBrush(Color.FromArgb(34, 140, 181, 220)))
            g.FillRectangle(wash, 0, 0, W, H);
    }

    static void DrawFakeHud(Graphics g, string title, string subtitle, int style)
    {
        DrawPanel(g, 34, 28, 355, 116);
        using (Font h = new Font("Segoe UI", 17, FontStyle.Bold))
        using (Font p = new Font("Segoe UI", 10, FontStyle.Regular))
        using (SolidBrush text = new SolidBrush(Color.FromArgb(28, 76, 111)))
        {
            g.DrawString(title, h, text, 58, 45);
            g.DrawString(subtitle, p, text, 59, 77);
        }
        DrawBar(g, 60, 105, 215, 13, Color.FromArgb(226, 70, 80), 0.82f);
        DrawBar(g, 60, 124, 215, 13, Color.FromArgb(55, 144, 231), 0.64f);
        DrawPanel(g, W - 340, 28, 296, 92);
        using (Font p = new Font("Segoe UI", 10, FontStyle.Regular))
        using (SolidBrush text = new SolidBrush(Color.FromArgb(38, 84, 116)))
        {
            string quest = style == 2 ? "Derrota al Dragon Noodle" : style == 1 ? "Purifica el templo lunar" : "Encuentra la tienda de bento";
            g.DrawString("Mision", p, text, W - 312, 48);
            g.DrawString(quest, p, text, W - 312, 76);
        }
    }

    static void DrawPanel(Graphics g, int x, int y, int w, int h)
    {
        using (GraphicsPath path = Rounded(new Rectangle(x, y, w, h), 8))
        using (SolidBrush b = new SolidBrush(Color.FromArgb(230, 250, 253, 255)))
        using (Pen edge = new Pen(Color.FromArgb(120, 116, 178, 222), 1))
        {
            g.FillPath(b, path);
            g.DrawPath(edge, path);
        }
    }

    static void DrawBar(Graphics g, int x, int y, int w, int h, Color color, float ratio)
    {
        using (SolidBrush bg = new SolidBrush(Color.FromArgb(220, 229, 241, 249)))
            g.FillRectangle(bg, x, y, w, h);
        using (SolidBrush fill = new SolidBrush(color))
            g.FillRectangle(fill, x, y, w * ratio, h);
        using (Pen edge = new Pen(Color.FromArgb(120, 77, 120, 155), 1))
            g.DrawRectangle(edge, x, y, w, h);
    }

    static PointF Iso(int x, int z, int ox, int oy)
    {
        return new PointF(ox + (x - z) * TileW / 2, oy + (x + z) * TileH / 2);
    }

    static RectangleF Bounds(PointF[] p)
    {
        float minX = p[0].X, maxX = p[0].X, minY = p[0].Y, maxY = p[0].Y;
        for (int i = 1; i < p.Length; i++)
        {
            minX = Math.Min(minX, p[i].X);
            maxX = Math.Max(maxX, p[i].X);
            minY = Math.Min(minY, p[i].Y);
            maxY = Math.Max(maxY, p[i].Y);
        }
        return new RectangleF(minX, minY, maxX - minX, maxY - minY);
    }

    static GraphicsPath Rounded(Rectangle r, int radius)
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

    static Color Light(Color c, float k)
    {
        return Color.FromArgb(Math.Min(255, (int)(c.R * k)), Math.Min(255, (int)(c.G * k)), Math.Min(255, (int)(c.B * k)));
    }

    static Color Dark(Color c, float k)
    {
        return Color.FromArgb(Math.Max(0, (int)(c.R * k)), Math.Max(0, (int)(c.G * k)), Math.Max(0, (int)(c.B * k)));
    }
}
