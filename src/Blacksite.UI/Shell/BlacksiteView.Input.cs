using Avalonia;
using Avalonia.Input;
using Blacksite.Core.Configuration;
using Blacksite.Core.Presets;
using Blacksite.UI.Rendering;

namespace Blacksite.UI.Shell;

public sealed partial class BlacksiteView
{
    Point _pointer = new(-1000, -1000), _pressPt, _menuPoint;
    bool _down, _moved, _longFired, _dragConsumed, _menuRadial, _radialVisible;
    string? _pressedId, _hoverId, _dragRegion;
    HitKind _pressKind;
    double _pressT, _lastTapT, _lastPinch = 1;
    Point _lastTapPt;
    public string? RebindTarget { get; set; }

    Hit? HitAt(Point p)
    {
        var hits = _g.Hits;
        for (int i = hits.Count - 1; i >= 0; i--) if (hits[i].R.Contains(p)) return hits[i];
        return null;
    }

    void Touched()
    {
        _idle = 0;
        if (_saver) _saver = false;
    }

    void UpdateIdle()
    {
        int m = Ctx.Settings.ScreenSaverMinutes;
        if (m > 0 && _idle > m * 60 && !_saver) _saver = true;
    }

    void CheckLongPress()
    {
        UpdateIdle();
        if (!_down || _moved || _longFired || _pressedId != null && _pressKind != HitKind.Region) return;
        if (_clock.Elapsed.TotalSeconds - _pressT < 0.55 || Active != null || _booting) return;
        _longFired = true;
        _menuPoint = _pressPt; _menuRadial = true;
        OpenOverlay("menu");
        _down = false; _pressedId = null;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();
        var pt = e.GetCurrentPoint(this);
        _lastTouch = e.Pointer.Type == PointerType.Touch || e.Pointer.Type == PointerType.Pen;
        Touched();
        _pointer = pt.Position;
        if (_booting) { _booting = false; return; }
        if (Ctx.Settings.AmbientMode) { ToggleAmbient(); return; }
        if (pt.Properties.IsRightButtonPressed && Active == null)
        {
            _menuPoint = _pointer; _menuRadial = false; OpenOverlay("menu"); return;
        }
        e.Pointer.Capture(this);
        _down = true; _moved = false; _longFired = false; _dragConsumed = false;
        _pressPt = _pointer; _pressT = _clock.Elapsed.TotalSeconds;
        var h = HitAt(_pointer);
        _pressedId = h?.Id; _pressKind = h?.Kind ?? HitKind.Region; _dragRegion = h?.Kind == HitKind.Region ? h?.Id : null;
        if (h is { Kind: HitKind.Slider } sl && sl.Enabled) SliderTo(sl);
    }

    void SliderTo(Hit h)
    {
        double v = Math.Clamp((_pointer.X - h.R.X - _g.U(6)) / Math.Max(1, h.R.Width - _g.U(12)), 0, 1);
        if (h.Id == "vd:speed") { Ctx.Settings.AnimationSpeed = Math.Round((0.25 + v * 1.75) * 20) / 20; return; }
        if (Active != null) Active.OnSlider(h.Id, v); else Module.OnModuleSlider(h.Id, v);
        ApplyLive();
    }

    void ApplyLive() { /* settings that must apply continuously are read each frame */ }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var p = e.GetPosition(this);
        var delta = p - _pointer;
        _pointer = p;
        if (!_down)
        {
            if (e.Pointer.Type == PointerType.Mouse) { _hoverId = HitAt(p) is { Kind: HitKind.Button, Enabled: true } hb ? hb.Id : null; }
            return;
        }
        Touched();
        if (!_moved && (p - _pressPt).X * (p - _pressPt).X + (p - _pressPt).Y * (p - _pressPt).Y > _g.U(8) * _g.U(8)) _moved = true;
        if (!_moved) return;
        if (_pressKind == HitKind.Slider && HitAt(_pressPt) is { } sh) { SliderTo(sh); return; }
        if (_pressKind == HitKind.Button) { return; }
        bool used = Active != null ? Active.Drag(p, delta, _dragRegion) : Module.Drag(p, delta, _dragRegion);
        if (used) _dragConsumed = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_down) return;
        _down = false;
        e.Pointer.Capture(null);
        var p = e.GetPosition(this); _pointer = p;
        double now = _clock.Elapsed.TotalSeconds;

        if (_pressKind == HitKind.Slider) { _pressedId = null; Ctx.Save(); return; }
        if (_pressKind == HitKind.Button && _pressedId != null)
        {
            var h = HitAt(p);
            if (h is { Kind: HitKind.Button } hb && hb.Id == _pressedId && hb.Enabled) Activate(_pressedId);
            _pressedId = null; return;
        }
        _pressedId = null;
        if (!_moved)
        {
            var h = HitAt(p);
            string? rid = h is { Kind: HitKind.Region } r ? r.Id : null;
            bool dbl = now - _lastTapT < 0.35 && (p - _lastTapPt).X * (p - _lastTapPt).X + (p - _lastTapPt).Y * (p - _lastTapPt).Y < _g.U(30) * _g.U(30);
            _lastTapT = dbl ? 0 : now; _lastTapPt = p;
            if (Active != null) Active.Tap(p, rid);
            else if (dbl) Module.DoubleTap(p, rid);
            else Module.Tap(p, rid);
        }
        else if (!_dragConsumed && Active == null)
        {
            var d = p - _pressPt; double dt = now - _pressT;
            if (Math.Abs(d.X) > _g.U(110) && Math.Abs(d.Y) < Math.Abs(d.X) * 0.45 && dt < 0.7) SwitchByOffset(d.X < 0 ? 1 : -1);
        }
    }

    protected override void OnPointerExited(PointerEventArgs e) { base.OnPointerExited(e); _hoverId = null; }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        Touched();
        if (Active != null) Active.Wheel(e.GetPosition(this), e.Delta.Y);
        else if (!_booting) Module.Wheel(e.GetPosition(this), e.Delta.Y);
    }

    void OnPinch(object? s, PinchEventArgs e)
    {
        Touched();
        double ratio = e.Scale / _lastPinch; _lastPinch = e.Scale;
        if (Active == null && !_booting) Module.Wheel(e.ScaleOrigin, (ratio - 1) / 0.12);
        _dragConsumed = true; _moved = true;
    }

    // ----------------------------------------------------------------- activation
    void Activate(string id)
    {
        _g.FlashPress(id);
        if (id.StartsWith("act:"))
        {
            var a = id[4..];
            Ctx.Audio.Click();
            var msg = Module.OnAction(a);
            if (!Module.CustomSounds) Ctx.Audio.Confirm();
            AddToast(msg ?? a + " EXECUTED", false);
            return;
        }
        if (id.StartsWith("top:"))
        {
            Ctx.Audio.Click();
            switch (id[4..])
            {
                case "presets": Toggle("presets"); break;
                case "events": Toggle("events"); break;
                case "settings": Toggle("settings"); break;
                case "ambient": ToggleAmbient(); break;
                case "mute": ToggleMute(); break;
                case "vis": _dock = !_dock; break;
                case "fs": ToggleFullscreen(); break;
            }
            return;
        }
        if (id.StartsWith("cr:")) { HandleCrisis(id); return; }
        Ctx.Audio.Click();
        if (id.StartsWith("vd:")) { HandleDock(id); return; }
        if (Active == null) { var m = Module.OnModuleButton(id); if (m != null) AddToast(m, false); return; }
        Active.OnButton(id);
    }

    void Toggle(string name)
    {
        string cur = Active switch { Overlays.PresetManagerOverlay => "presets", Overlays.EventsOverlay => "events", Overlays.SettingsOverlay => "settings", _ => "" };
        if (cur == name) CloseOverlay(); else OpenOverlay(name);
    }

    // ----------------------------------------------------------------- keyboard
    static string Combo(KeyEventArgs e)
    {
        string k = e.Key switch { Key.Escape => "Escape", Key.Space => "Space", Key.Return => "Enter", Key.Back => "Back", _ => e.Key.ToString() };
        if (e.Key >= Key.D0 && e.Key <= Key.D9) k = ((int)e.Key - (int)Key.D0).ToString();
        string m = "";
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) m += "Ctrl+";
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt)) m += "Alt+";
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) m += "Shift+";
        return m + k;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        Touched();
        if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt) return;
        if (_booting) { _booting = false; e.Handled = true; return; }
        if (Ctx.Settings.AmbientMode) { if (e.Key == Key.F12 || e.Key == Key.Escape) ToggleAmbient(); else ToggleAmbient(); e.Handled = true; return; }
        string combo = Combo(e);

        if (RebindTarget != null)
        {
            if (e.Key != Key.Escape) { Ctx.Settings.Shortcuts[RebindTarget] = combo; AddToast($"{RebindTarget.ToUpper()} = {combo.ToUpper()}", false); Ctx.Save(); }
            RebindTarget = null; e.Handled = true; return;
        }
        if (Active?.WantsText == true && Active.Key(combo, null)) { e.Handled = true; return; }

        bool typing = Active == null && Module.WantsText;
        bool plain = combo.Length == 1 || combo == "Space";
        if (typing && (plain || combo is "Enter" or "Back" or "Up" or "Down" or "Tab" or "Shift+Tab") ) { Module.Key(combo, null); e.Handled = true; return; }

        foreach (var kv in Ctx.Settings.Shortcuts)
            if (string.Equals(kv.Value, combo, StringComparison.OrdinalIgnoreCase)) { if (RunShortcut(kv.Key)) { e.Handled = true; return; } }
        if (Active == null && Module.TryKey(combo)) e.Handled = true;
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (string.IsNullOrEmpty(e.Text)) return;
        if (Active?.WantsText == true) { Active.Key("", e.Text); e.Handled = true; }
        else if (Active == null && Module.WantsText) { Module.Key("", e.Text); e.Handled = true; }
    }

    bool RunShortcut(string name)
    {
        switch (name)
        {
            case "Fullscreen": ToggleFullscreen(); return true;
            case "Escape":
                if (CrisisEscape()) return true;
                if (Active != null) CloseOverlay();
                else if (Ctx.Platform.IsFullscreen) ToggleFullscreen();
                return true;
            case "Ambient": ToggleAmbient(); return true;
            case "Mute": ToggleMute(); return true;
            case "Hud": Ctx.Settings.ShowHud = !Ctx.Settings.ShowHud; AddToast(Ctx.Settings.ShowHud ? "HUD ON" : "HUD OFF", false); return true;
            case "Settings": Toggle("settings"); return true;
            case "Diagnostics": Ctx.Settings.ShowDiagnostics = !Ctx.Settings.ShowDiagnostics; return true;
            case "Presets": Toggle("presets"); return true;
            case "Events": Toggle("events"); return true;
            case "Visual": _dock = !_dock; return true;
            case "Action":
                if (Active == null && Module.PrimaryAction.Length > 0) Activate("act:" + Module.PrimaryAction);
                return true;
        }
        if (name.StartsWith("Preset") && int.TryParse(name[6..], out int n) && n >= 1 && n <= PresetCatalog.All.Count)
        { SwitchTo(PresetCatalog.All[n - 1].Id, true); return true; }
        return false;
    }
}
