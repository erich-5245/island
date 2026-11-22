using System; using System.Drawing; using System.Drawing.Drawing2D; using System.Drawing.Imaging;

public sealed class AtollPalette {
  public Color SeaCenter, SeaEdge, Foam, SandTop, SandBottom, LagoonTop, LagoonBottom, LagoonLine, Leaf, LeafDark, Trunk, Boat, Frame;
  public int FoamAlpha;
}

public static class AtollIcons {
  static Color C(int rgb) { return Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255); }
  static GraphicsPath Round(float x, float y, float w, float h, float r) {
    var p = new GraphicsPath(); float d = r * 2;
    p.AddArc(x, y, d, d, 180, 90); p.AddArc(x + w - d, y, d, d, 270, 90);
    p.AddArc(x + w - d, y + h - d, d, d, 0, 90); p.AddArc(x, y + h - d, d, d, 90, 90); p.CloseFigure(); return p; }
  static GraphicsPath Pill(float cx, float cy, float w, float h) { return Round(cx - w / 2, cy - h / 2, w, h, h / 2); }

  public static AtollPalette Navy() { return new AtollPalette {
    SeaCenter = C(0x12284A), SeaEdge = C(0x0A1730), Foam = C(0xFFFFFF), FoamAlpha = 45,
    SandTop = C(0xE2C79C), SandBottom = C(0xC9A774), LagoonTop = C(0x1C3D66), LagoonBottom = C(0x15304F), LagoonLine = C(0x0A1730),
    Leaf = C(0xA8854F), LeafDark = C(0x8A6B3D), Trunk = C(0x6E5230), Boat = C(0xF3E6CF), Frame = C(0x2A3E60) }; }

  public static AtollPalette BlackGold() { return new AtollPalette {
    SeaCenter = C(0x1A1A1C), SeaEdge = C(0x0A0A0A), Foam = C(0xD4B58A), FoamAlpha = 40,
    SandTop = C(0xE0C397), SandBottom = C(0xC4A06C), LagoonTop = C(0x0D0D0E), LagoonBottom = C(0x050505), LagoonLine = C(0x000000),
    Leaf = C(0x8F7650), LeafDark = C(0x6F5B3C), Trunk = C(0x4A3B26), Boat = C(0xD4B58A), Frame = C(0x3A342C) }; }

  public static AtollPalette Mist() { return new AtollPalette {
    SeaCenter = C(0x1F5A60), SeaEdge = C(0x123B42), Foam = C(0xFFFFFF), FoamAlpha = 55,
    SandTop = C(0xF2E6CC), SandBottom = C(0xDCCBA6), LagoonTop = C(0xA9DCD2), LagoonBottom = C(0x86C6BB), LagoonLine = C(0x5E9E95),
    Leaf = C(0x3E7F6E), LeafDark = C(0x2F6556), Trunk = C(0x7A6446), Boat = C(0xF6EFE0), Frame = C(0x2E6A70) }; }

  static void PalmTop(Graphics g, float x, float y, float r, AtollPalette p) {
    using (var leaf = new SolidBrush(p.Leaf)) using (var dark = new SolidBrush(p.LeafDark)) {
      for (int i = 0; i < 6; i++) { double a = i * Math.PI * 2 / 6 + 0.3; var path = new GraphicsPath();
        float ex = x + (float)Math.Cos(a) * r, ey = y + (float)Math.Sin(a) * r;
        float nx = -(float)Math.Sin(a) * r * 0.26f, ny = (float)Math.Cos(a) * r * 0.26f;
        path.AddBezier(x, y, x + (ex - x) * 0.5f + nx, y + (ey - y) * 0.5f + ny, ex, ey, ex, ey);
        path.AddBezier(ex, ey, ex, ey, x + (ex - x) * 0.5f - nx, y + (ey - y) * 0.5f - ny, x, y);
        g.FillPath(i % 2 == 0 ? leaf : dark, path); }
      using (var c = new SolidBrush(p.Trunk)) g.FillEllipse(c, x - r * 0.14f, y - r * 0.14f, r * 0.28f, r * 0.28f); } }

  public static Bitmap Draw(int s, AtollPalette p) {
    bool simple = s <= 32;
    var b = new Bitmap(s, s, PixelFormat.Format32bppArgb);
    using (var g = Graphics.FromImage(b)) {
      g.SmoothingMode = SmoothingMode.AntiAlias; g.PixelOffsetMode = PixelOffsetMode.HighQuality; g.Clear(Color.Transparent);
      float m = s * 0.035f; var frame = Round(m, m, s - 2 * m, s - 2 * m, s * 0.225f);
      g.SetClip(frame);
      using (var sea = new PathGradientBrush(new[] { new PointF(-s * 0.2f, -s * 0.2f), new PointF(s * 1.2f, -s * 0.2f), new PointF(s * 1.2f, s * 1.2f), new PointF(-s * 0.2f, s * 1.2f) })) {
        sea.CenterPoint = new PointF(s * 0.5f, s * 0.46f); sea.CenterColor = p.SeaCenter; sea.SurroundColors = new[] { p.SeaEdge, p.SeaEdge, p.SeaEdge, p.SeaEdge };
        g.FillRectangle(sea, 0, 0, s, s); }

      float cx = s * 0.5f, cy = s * 0.5f, w = s * 0.72f, h = s * 0.33f, ring = s * 0.065f;
      if (!simple) using (var foam = new Pen(Color.FromArgb(p.FoamAlpha, p.Foam), Math.Max(1f, s * 0.012f))) {
        g.DrawPath(foam, Pill(cx, cy, w + s * 0.07f, h + s * 0.07f));
        using (var foam2 = new Pen(Color.FromArgb(p.FoamAlpha / 2, p.Foam), Math.Max(1f, s * 0.008f))) g.DrawPath(foam2, Pill(cx, cy, w + s * 0.14f, h + s * 0.14f)); }
      using (var sand = new LinearGradientBrush(new PointF(0, cy - h / 2 - 1), new PointF(0, cy + h / 2 + 1), p.SandTop, p.SandBottom)) g.FillPath(sand, Pill(cx, cy, w, h));
      using (var lagoon = new LinearGradientBrush(new PointF(0, cy - h / 2), new PointF(0, cy + h / 2), p.LagoonTop, p.LagoonBottom)) g.FillPath(lagoon, Pill(cx, cy, w - ring * 2, h - ring * 2));
      if (!simple) using (var line = new Pen(Color.FromArgb(90, p.LagoonLine), Math.Max(1f, s * 0.008f))) g.DrawPath(line, Pill(cx, cy, w - ring * 2, h - ring * 2));

      if (simple) PalmTop(g, cx - w * 0.33f, cy - h * 0.10f, s * 0.13f, p);
      else { PalmTop(g, cx - w * 0.36f, cy - h * 0.16f, s * 0.085f, p); PalmTop(g, cx - w * 0.25f, cy + h * 0.32f, s * 0.055f, p); }

      if (!simple) using (var boat = new SolidBrush(p.Boat)) using (var wake = new Pen(Color.FromArgb(110, p.Boat), Math.Max(1f, s / 220f))) {
        float bx = cx + w * 0.20f, by = cy;
        g.DrawLine(wake, bx - s * 0.12f, by, bx - s * 0.04f, by);
        g.FillPath(boat, Pill(bx, by, s * 0.07f, s * 0.026f)); }
      g.ResetClip();
      using (var pen = new Pen(p.Frame, Math.Max(1f, s / 128f))) g.DrawPath(pen, frame);
    }
    return b; }
}
