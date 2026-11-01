using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Blacksite.Core.Configuration;
using Blacksite.Core.Services;
using Orientation = Blacksite.Core.Configuration.Orientation;

namespace Blacksite.Android;

public sealed class AndroidPlatform : IPlatformServices
{
    readonly Activity _a;
    public Func<TopLevel?> TopLevelProvider { get; set; } = () => null;

    public AndroidPlatform(Activity a) { _a = a; }

    public string PlatformName => "Android";
    public string DeviceModel => $"{Build.Manufacturer} {Build.Model}";
    public string OsVersion => $"Android {Build.VERSION.Release} (API {(int)Build.VERSION.SdkInt})";
    public bool IsDesktop => false;
    public bool IsTouchFirst => true;
    public string DataDirectory => _a.FilesDir!.AbsolutePath;
    public IReadOnlyList<MonitorInfo> Monitors => Array.Empty<MonitorInfo>();
    public bool IsFullscreen => true;

    public void ApplyWindowMode(WindowMode mode, int monitorIndex) { }
    public void SetAlwaysOnTop(bool on) { }
    public void SetTray(bool on) { }
    public void MoveToMonitor(int monitorIndex) { }
    public void OpenNewInstance(string presetId, int monitorIndex, bool fullscreen) { }
    public void Quit() => _a.FinishAffinity();

    /// <summary>On Android this controls the optional "start when the device boots" receiver.</summary>
    public void SetStartWithWindows(bool on) =>
        _a.GetSharedPreferences("blacksite", FileCreationMode.Private)!.Edit()!.PutBoolean("start_on_boot", on)!.Apply();

    public void ApplyImmersive(bool on)
    {
        _a.RunOnUiThread(() =>
        {
            var w = _a.Window!;
            if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
            {
                w.SetDecorFitsSystemWindows(!on);
                var c = w.InsetsController;
                if (c != null)
                {
                    if (on) { c.Hide(WindowInsets.Type.SystemBars()); c.SystemBarsBehavior = (int)WindowInsetsControllerBehavior.ShowTransientBarsBySwipe; }
                    else c.Show(WindowInsets.Type.SystemBars());
                }
            }
            else
            {
#pragma warning disable CS0618
                w.DecorView.SystemUiVisibility = on
                    ? (StatusBarVisibility)(SystemUiFlags.ImmersiveSticky | SystemUiFlags.HideNavigation | SystemUiFlags.Fullscreen | SystemUiFlags.LayoutStable | SystemUiFlags.LayoutHideNavigation | SystemUiFlags.LayoutFullscreen)
                    : StatusBarVisibility.Visible;
#pragma warning restore CS0618
            }
        });
    }

    public void SetKeepScreenOn(bool on) => _a.RunOnUiThread(() => { if (on) _a.Window!.AddFlags(WindowManagerFlags.KeepScreenOn); else _a.Window!.ClearFlags(WindowManagerFlags.KeepScreenOn); });

    public void SetBrightness(double value01) => _a.RunOnUiThread(() =>
    {
        var lp = _a.Window!.Attributes!;
        lp.ScreenBrightness = value01 < 0 ? WindowManagerLayoutParams.BrightnessOverrideNone : (float)Math.Clamp(value01, 0.02, 1);
        _a.Window.Attributes = lp;
    });

    public void ApplyOrientation(Orientation o) => _a.RunOnUiThread(() =>
        _a.RequestedOrientation = o switch { Orientation.Landscape => ScreenOrientation.SensorLandscape, Orientation.Portrait => ScreenOrientation.SensorPortrait, _ => ScreenOrientation.Unspecified });

    public double? BatteryLevel
    {
        get
        {
            try
            {
                var i = _a.RegisterReceiver(null, new IntentFilter(Intent.ActionBatteryChanged));
                if (i == null) return null;
                int lvl = i.GetIntExtra(BatteryManager.ExtraLevel, -1), scale = i.GetIntExtra(BatteryManager.ExtraScale, 100);
                return lvl < 0 ? null : (double)lvl / scale;
            }
            catch { return null; }
        }
    }

    public bool? IsCharging
    {
        get
        {
            try
            {
                var i = _a.RegisterReceiver(null, new IntentFilter(Intent.ActionBatteryChanged));
                int st = i?.GetIntExtra(BatteryManager.ExtraStatus, -1) ?? -1;
                return st == (int)BatteryStatus.Charging || st == (int)BatteryStatus.Full;
            }
            catch { return null; }
        }
    }

    public async Task<string?> PickTextFileAsync(string title)
    {
        var tl = TopLevelProvider(); if (tl == null) return null;
        var files = await tl.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = title, AllowMultiple = false });
        if (files.Count == 0) return null;
        await using var s = await files[0].OpenReadAsync();
        using var r = new StreamReader(s);
        return await r.ReadToEndAsync();
    }

    public async Task<bool> SaveTextFileAsync(string suggestedName, string content)
    {
        var tl = TopLevelProvider(); if (tl == null) return false;
        var f = await tl.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { SuggestedFileName = suggestedName, DefaultExtension = "json" });
        if (f == null) return false;
        await using var s = await f.OpenWriteAsync();
        s.SetLength(0);
        await using var w = new StreamWriter(s);
        await w.WriteAsync(content);
        return true;
    }

    public double? ProcessCpuPercent => null;

    public long ProcessMemoryBytes
    {
        get { try { return System.Diagnostics.Process.GetCurrentProcess().WorkingSet64; } catch { return 0; } }
    }

    public long TotalMemoryBytes
    {
        get
        {
            try
            {
                var am = (ActivityManager)_a.GetSystemService(Context.ActivityService)!;
                var mi = new ActivityManager.MemoryInfo(); am.GetMemoryInfo(mi); return mi.TotalMem;
            }
            catch { return 4L << 30; }
        }
    }
}
