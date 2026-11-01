using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Blacksite.Core.Audio;
using Blacksite.Core.Configuration;
using Blacksite.UI.Shell;

namespace Blacksite.Android;

[Application]
public class BlacksiteApplication : AvaloniaAndroidApplication<AndroidApp>
{
    public BlacksiteApplication(IntPtr handle, JniHandleOwnership transfer) : base(handle, transfer) { }
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) => base.CustomizeAppBuilder(builder);
}

// The splash theme provides the launch splash (logo on black); BlacksiteView then plays the animated boot sequence.
[Activity(Label = "BLACKSITE", Theme = "@style/Blacksite.Splash", MainLauncher = true, Exported = true, LaunchMode = LaunchMode.SingleTask,
    ScreenOrientation = ScreenOrientation.Unspecified,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density | ConfigChanges.KeyboardHidden)]
public class MainActivity : AvaloniaMainActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        AndroidApp.Attach(this);
        AndroidApp.ApplyImmersive();
    }

    public override void OnWindowFocusChanged(bool hasFocus)
    {
        base.OnWindowFocusChanged(hasFocus);
        if (hasFocus) AndroidApp.ApplyImmersive();
    }

    protected override void OnPause() { base.OnPause(); AndroidApp.SetPaused(true); }
    protected override void OnResume() { base.OnResume(); AndroidApp.SetPaused(false); AndroidApp.ApplyImmersive(); }

    protected override void OnDestroy()
    {
        AndroidApp.Shutdown();
        base.OnDestroy();
    }
}

public sealed class AndroidApp : Avalonia.Application
{
    static BlacksiteView? _view;
    static AndroidAudioOutput? _audio;
    static AndroidPlatform? _platform;
    static ConfigStore? _store;
    static AppSettings? _settings;

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = ThemeVariant.Dark;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is ISingleViewApplicationLifetime single)
            single.MainView = new Border { Background = Avalonia.Media.Brushes.Black };
        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>Builds the BLACKSITE view once the activity exists, then swaps it in as the main view.</summary>
    public static void Attach(Activity act)
    {
        if (_view != null) return;
        if (Current?.ApplicationLifetime is not ISingleViewApplicationLifetime single) return;
        _platform = new AndroidPlatform(act);
        _store = new ConfigStore(_platform.DataDirectory);
        _settings = _store.Load();
        var audio = new AudioEngine(_settings) { Speech = new AndroidSpeech(act) };
        var ctx = new BsContext(_settings, _store, _platform, audio);
        _view = new BlacksiteView(ctx);
        _platform.TopLevelProvider = () => TopLevel.GetTopLevel(_view);
        _audio = new AndroidAudioOutput(act);
        audio.Attach(_audio);
        Task.Run(audio.Warm);
        single.MainView = _view;
    }

    public static void ApplyImmersive() => _platform?.ApplyImmersive(true);

    public static void SetPaused(bool paused)
    {
        if (_view == null) return;
        // Pause rendering/simulation while backgrounded.
        _view.Paused = paused;
        if (paused) _store?.Save(_settings!);
    }

    public static void Shutdown() { try { _store?.Save(_settings!); _audio?.Stop(); } catch { } }
}

/// <summary>Optional: relaunch BLACKSITE after the device boots. Android 10+ may block this; it is best-effort.</summary>
[BroadcastReceiver(Enabled = true, Exported = true, DirectBootAware = false)]
[IntentFilter(new[] { Intent.ActionBootCompleted })]
public class BootReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context == null || intent?.Action != Intent.ActionBootCompleted) return;
        var prefs = context.GetSharedPreferences("blacksite", FileCreationMode.Private);
        if (prefs?.GetBoolean("start_on_boot", false) != true) return;
        var i = new Intent(context, typeof(MainActivity)); i.AddFlags(ActivityFlags.NewTask);
        try { context.StartActivity(i); } catch { }
    }
}
