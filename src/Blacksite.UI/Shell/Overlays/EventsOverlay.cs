using Avalonia;
using Blacksite.Core.Configuration;
using Blacksite.Core.Events;
using Blacksite.UI.Rendering;

namespace Blacksite.UI.Shell.Overlays;

/// <summary>Cinematic event launcher plus the sequence editor.</summary>
public sealed class EventsOverlay : Overlay
{
    int _seq = -1;
    double _duration = 0;   // 0 = event default
    string _status = "";
    bool _renaming; string _nameBuf = "";

    public EventsOverlay(BlacksiteView v) : base(v) { }
    AppSettings S => C.Settings;
    public override bool WantsText => _renaming;

    static uint SevColor(Gfx g, Severity s) => s switch { Severity.Critical => 0xFFFF3030, Severity.Warning => g.P.Warning, Severity.Notice => g.P.Accent, _ => g.P.Primary };

    public override void Draw(Gfx g, Rect b)
    {
        var inner = Frame(g, b, "CINEMATIC EVENT ENGINE", 1100, 700);
        bool narrow = inner.Width < g.U(760);
        double gap = g.U(8);
        Rect left, right;
        if (narrow) { var rr = Layout.Rows(inner, gap, 1.1, 1); left = rr[0]; right = rr[1]; }
        else { var cc = Layout.Cols(inner, gap, 1, 1); left = cc[0]; right = cc[1]; }
        DrawEvents(g, left);
        DrawSequences(g, right);
    }

    void DrawEvents(Gfx g, Rect r)
    {
        var inner = g.Panel(r, "TRIGGER EVENT");
        double bh = g.U(g.Touch ? 34 : 28), gap = g.U(4);
        int cols = inner.Width > g.U(380) ? 2 : 1;
        double bw = (inner.Width - gap * (cols - 1)) / cols;
        int n = EventCatalog.All.Count, rowsN = (n + cols - 1) / cols;
        double ctrlH = g.U(78);
        double avail = inner.Height - ctrlH;
        bh = Math.Min(bh, (avail - gap * (rowsN - 1)) / rowsN);
        for (int i = 0; i < n; i++)
        {
            var e = EventCatalog.All[i];
            var rc = new Rect(inner.X + (i % cols) * (bw + gap), inner.Y + (i / cols) * (bh + gap), bw, bh);
            g.Button("ev:fire:" + e.Id, rc, e.Title, C.Events.Active.Any(a => a.Def.Id == e.Id), true, SevColor(g, e.Severity), bw < g.U(190) ? 7.5 : 8.5);
        }
        double y = inner.Y + rowsN * (bh + gap) + g.U(4);
        g.Slider("ev:dur", new Rect(inner.X, y, inner.Width, g.U(34)), _duration / 30, "Duration override (0 = default)", _duration < 0.5 ? "DEFAULT" : $"{_duration:0}s");
        g.Button("ev:auto", new Rect(inner.X, y + g.U(36), inner.Width / 2 - g.U(3), g.U(28)), S.AutoEvents ? "AUTO EVENTS: ON" : "AUTO EVENTS: OFF", S.AutoEvents, true, null, 9);
        g.Button("ev:clear", new Rect(inner.Center.X + g.U(3), y + g.U(36), inner.Width / 2 - g.U(3), g.U(28)), "CLEAR ALL", false, true, null, 9);
    }

    void DrawSequences(Gfx g, Rect r)
    {
        var inner = g.Panel(r, "SEQUENCE EDITOR");
        double bh = g.U(g.Touch ? 34 : 28), gap = g.U(4);
        // sequence selector row
        var seqs = S.Sequences;
        string cur = _seq >= 0 && _seq < seqs.Count ? seqs[_seq].Name : "NONE";
        if (_renaming) cur = _nameBuf + (g.Pulse(2) > 0.5 ? "_" : " ");
        double w4 = (inner.Width - gap * 3) / 4;
        g.Button("ev:prev", new Rect(inner.X, inner.Y, w4 * 0.5, bh), "<", false, true, null, 10);
        g.Button("ev:rename", new Rect(inner.X + w4 * 0.5 + gap, inner.Y, w4 * 2.0 + gap * 2 - w4 * 0.0 + w4 * 0.5 - w4 * 0.5, bh), cur, _renaming, true, null, 9);
        double nx = inner.X + w4 * 0.5 + gap + w4 * 2 + gap * 2;
        g.Button("ev:next", new Rect(nx, inner.Y, w4 * 0.5, bh), ">", false, true, null, 10);
        g.Button("ev:new", new Rect(nx + w4 * 0.5 + gap, inner.Y, inner.Right - (nx + w4 * 0.5 + gap), bh), "+ NEW", false, true, null, 9);

        double y = inner.Y + bh + gap;
        var seq = _seq >= 0 && _seq < seqs.Count ? seqs[_seq] : null;
        double footH = bh + gap + g.U(16);
        var list = new Rect(inner.X, y, inner.Width - g.U(6), inner.Bottom - y - footH);
        double rh = g.U(g.Touch ? 36 : 30);
        ContentHeight = (seq?.Steps.Count ?? 0) * (rh + gap);
        BeginList(g, list);
        using (g.Clip(list))
        {
            if (seq == null) g.Text("CREATE A SEQUENCE, THEN APPEND EVENTS WITH [+] ON THE LEFT", list.X, list.Y + g.U(6), 9, g.P.Text, Align.Left, false, 0.6);
            else if (seq.Steps.Count == 0) g.Text("EMPTY — PRESS ADD STEP, OR TAP EVENTS WHILE HOLDING ADD", list.X, list.Y + g.U(6), 9, g.P.Text, Align.Left, false, 0.6);
            else
                for (int i = 0; i < seq.Steps.Count; i++)
                {
                    var st = seq.Steps[i];
                    var rc = new Rect(list.X, list.Y + i * (rh + gap) - Scroll, list.Width, rh);
                    if (rc.Bottom < list.Y || rc.Y > list.Bottom) continue;
                    var ev = EventCatalog.Get(st.EventId);
                    g.Fill(rc, g.P.Primary, 0.06); g.Frame(rc, g.P.Primary, 1, 0.3);
                    g.TextIn(new Rect(rc.X + g.U(6), rc.Y, rc.Width - g.U(150), rc.Height), $"{i + 1}. {ev?.Title ?? st.EventId}", 9, g.P.Text, Align.Left, true);
                    double bw = g.U(30);
                    g.TextIn(new Rect(rc.Right - g.U(142), rc.Y, g.U(44), rc.Height), $"+{st.DelaySeconds:0}s", 9, g.P.Primary, Align.Center, true);
                    g.Button($"ev:dm:{i}", new Rect(rc.Right - g.U(142) - bw, rc.Y + 2, bw, rc.Height - 4), "-", false, true, null, 10);
                    g.Button($"ev:dp:{i}", new Rect(rc.Right - g.U(96), rc.Y + 2, bw, rc.Height - 4), "+", false, true, null, 10);
                    g.Button($"ev:rm:{i}", new Rect(rc.Right - g.U(60), rc.Y + 2, g.U(56), rc.Height - 4), "REMOVE", false, true, g.P.Warning, 7.5);
                }
        }
        DrawScrollbar(g);
        double fy = inner.Bottom - footH + gap;
        double fw = (inner.Width - gap * 3) / 4;
        g.Button("ev:add", new Rect(inner.X, fy, fw, bh), "ADD STEP", false, seq != null, null, 8.5);
        g.Button("ev:play", new Rect(inner.X + (fw + gap), fy, fw, bh), C.Events.PlayingSequence != null ? "PLAYING…" : "PLAY", C.Events.PlayingSequence != null, seq != null && seq.Steps.Count > 0, null, 8.5);
        g.Button("ev:stop", new Rect(inner.X + 2 * (fw + gap), fy, fw, bh), "STOP", false, true, null, 8.5);
        g.Button("ev:del", new Rect(inner.X + 3 * (fw + gap), fy, fw, bh), "DELETE", false, seq != null, g.P.Warning, 8.5);
        if (_status.Length > 0) g.Text(_status, inner.X, inner.Bottom - g.U(12), 9, g.P.Primary, Align.Left, true);
    }

    string _pickEvent = "incoming_tx";

    public override void OnButton(string id)
    {
        base.OnButton(id);
        if (!id.StartsWith("ev:")) return;
        var p = id.Split(':');
        var seqs = S.Sequences;
        CinematicSequence? seq = _seq >= 0 && _seq < seqs.Count ? seqs[_seq] : null;
        switch (p[1])
        {
            case "fire":
                _pickEvent = p[2];
                C.Events.Trigger(p[2], _duration < 0.5 ? null : _duration);
                // events double as step picker: tapping while a sequence is selected and ADD armed appends
                if (_armed && seq != null) { seq.Steps.Add(new SequenceStep { EventId = p[2], DelaySeconds = 6 }); _status = "STEP ADDED"; C.Save(); }
                break;
            case "auto": S.AutoEvents = !S.AutoEvents; V.ApplySettings(); break;
            case "clear": C.Events.Clear(); _status = "ALL EVENTS CLEARED"; break;
            case "prev": if (seqs.Count > 0) _seq = (_seq - 1 + seqs.Count) % seqs.Count; break;
            case "next": if (seqs.Count > 0) _seq = (_seq + 1) % seqs.Count; break;
            case "new": seqs.Add(new CinematicSequence { Name = $"SEQUENCE {seqs.Count + 1}" }); _seq = seqs.Count - 1; C.Save(); _status = "SEQUENCE CREATED"; break;
            case "rename": if (seq != null) { _renaming = !_renaming; _nameBuf = seq.Name; } break;
            case "add":
                if (seq != null)
                {
                    seq.Steps.Add(new SequenceStep { EventId = _pickEvent, DelaySeconds = 6 }); C.Save();
                    _status = $"ADDED {EventCatalog.Get(_pickEvent)?.Title} — TAP ANOTHER EVENT THEN ADD STEP TO CHANGE IT";
                }
                break;
            case "play": if (seq != null) { C.Events.PlaySequence(seq); _status = "PLAYING " + seq.Name; } break;
            case "stop": C.Events.Clear(); _status = "STOPPED"; break;
            case "del": if (seq != null) { seqs.Remove(seq); _seq = seqs.Count - 1; C.Save(); _status = "SEQUENCE DELETED"; } break;
            case "dm": if (seq != null) { var s = seq.Steps[int.Parse(p[2])]; s.DelaySeconds = Math.Max(1, s.DelaySeconds - 1); C.Save(); } break;
            case "dp": if (seq != null) { var s = seq.Steps[int.Parse(p[2])]; s.DelaySeconds = Math.Min(120, s.DelaySeconds + 1); C.Save(); } break;
            case "rm": if (seq != null) { seq.Steps.RemoveAt(int.Parse(p[2])); C.Save(); } break;
        }
    }

    bool _armed = false;

    public override void OnSlider(string id, double v) { if (id == "ev:dur") _duration = Math.Round(v * 30); }

    public override bool Key(string combo, string? text)
    {
        if (!_renaming) return false;
        var seqs = S.Sequences; if (_seq < 0 || _seq >= seqs.Count) { _renaming = false; return false; }
        if (text != null) { foreach (var ch in text) if (!char.IsControl(ch) && _nameBuf.Length < 24) _nameBuf += char.ToUpperInvariant(ch); return true; }
        if (combo == "Back") { if (_nameBuf.Length > 0) _nameBuf = _nameBuf[..^1]; return true; }
        if (combo == "Enter") { if (_nameBuf.Trim().Length > 0) seqs[_seq].Name = _nameBuf.Trim(); _renaming = false; C.Save(); return true; }
        if (combo == "Escape") { _renaming = false; return true; }
        return false;
    }
}
