using Avalonia;
using Blacksite.UI.Rendering;

namespace Blacksite.UI.Shell.Overlays;

/// <summary>First-run notice for always-on mobile use.</summary>
public sealed class HeatWarningOverlay : Overlay
{
    public HeatWarningOverlay(BlacksiteView v) : base(v) { }

    public const string Text =
        "BLACKSITE can run continuously as a wall display. Prolonged screen-on operation heats the device, " +
        "accelerates battery wear and can cause permanent screen burn-in on some panels. For permanent mounting: " +
        "use a power source with charge-limit support, keep brightness low, enable the screen saver, and ensure airflow. " +
        "Android manufacturers may restrict auto-start and background behaviour; BLACKSITE does not override those limits.";

    public override void Draw(Gfx g, Rect b)
    {
        var inner = Frame(g, b, "OPERATING NOTICE", 560, 330, false);
        g.Text("PROLONGED OPERATION WARNING", inner.X, inner.Y, 14, g.P.Warning, Align.Left, true);
        Wrap(g, Text, inner.X, inner.Y + g.U(26), inner.Width, 10.5);
        g.Text("ALL DATA SHOWN IS FICTIONAL. FOR ENTERTAINMENT ONLY.", inner.X, inner.Bottom - g.U(60), 9, g.P.Primary, Align.Left, true);
        g.Button("heat:ok", new Rect(inner.Center.X - g.U(90), inner.Bottom - g.U(40), g.U(180), g.U(38)), "ACKNOWLEDGE", true);
    }

    public static void Wrap(Gfx g, string text, double x, double y, double w, double fs, uint? col = null)
    {
        var words = text.Split(' ');
        string line = ""; double lh = g.U(fs + 5);
        foreach (var word in words)
        {
            string t = line.Length == 0 ? word : line + " " + word;
            if (g.TextWidth(t, fs * g.Scale * g.S.FontScale) > w && line.Length > 0)
            { g.Text(line, x, y, fs, col ?? g.P.Text, Align.Left, false, 0.9); y += lh; line = word; }
            else line = t;
        }
        if (line.Length > 0) g.Text(line, x, y, fs, col ?? g.P.Text, Align.Left, false, 0.9);
    }

    public override void OnButton(string id)
    {
        if (id == "heat:ok") { C.Settings.HeatWarningAccepted = true; V.CloseOverlay(); }
    }

    public override void Tap(Point p, string? regionId) { }
}
