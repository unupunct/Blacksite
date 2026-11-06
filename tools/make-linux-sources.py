"""Derives src/Blacksite.Linux/{LinuxPlatform,Program}.cs from the Windows head (run once; the outputs are then maintained by hand)."""
import os
root = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'src')
win, lin = os.path.join(root, 'Blacksite.Desktop'), os.path.join(root, 'Blacksite.Linux')

w = open(os.path.join(win, 'WindowsPlatform.cs'), encoding='utf-8').read()
w = w.replace('using Microsoft.Win32;\n', '').replace('namespace Blacksite.Desktop;', 'namespace Blacksite.Linux;').replace('WindowsPlatform', 'LinuxPlatform')
w = w.replace('PlatformName => "Windows"', 'PlatformName => "Linux"').replace('DeviceModel => $"PC / ', 'DeviceModel => $"Linux PC / ')
a = w.index('    public void SetStartWithWindows'); b = w.index('    public void Quit()')
w = w[:a] + '''    public void SetStartWithWindows(bool on)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "autostart");
            var f = Path.Combine(dir, "blacksite.desktop");
            if (on)
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(f, $"[Desktop Entry]\\nType=Application\\nName=BLACKSITE\\nExec=\\"{Environment.ProcessPath}\\" --startup\\nTerminal=false\\nX-GNOME-Autostart-enabled=true\\n");
            }
            else if (File.Exists(f)) File.Delete(f);
        }
        catch { }
    }

''' + w[b:]
w = w.replace('public void SetKeepScreenOn(bool on) { SetThreadExecutionState(on ? 0x80000003u : 0x80000000u); }', 'public void SetKeepScreenOn(bool on) { }')
w = w.replace('    [DllImport("kernel32.dll")] static extern uint SetThreadExecutionState(uint flags);\n\n', '')
w = w.replace('    public double? BatteryLevel => null;\n    public bool? IsCharging => null;', '''    public double? BatteryLevel
    {
        get
        {
            try { foreach (var d in Directory.GetDirectories("/sys/class/power_supply", "BAT*")) if (double.TryParse(File.ReadAllText(Path.Combine(d, "capacity")).Trim(), out var v)) return v / 100; } catch { }
            return null;
        }
    }
    public bool? IsCharging
    {
        get
        {
            try { foreach (var d in Directory.GetDirectories("/sys/class/power_supply", "BAT*")) return File.ReadAllText(Path.Combine(d, "status")).Trim() != "Discharging"; } catch { }
            return null;
        }
    }''')
open(os.path.join(lin, 'LinuxPlatform.cs'), 'w', encoding='utf-8').write(w)

p = open(os.path.join(win, 'Program.cs'), encoding='utf-8').read()
p = p.replace('namespace Blacksite.Desktop;', 'namespace Blacksite.Linux;').replace('WindowsPlatform', 'LinuxPlatform').replace('WindowsAudioOutput', 'LinuxAudioOutput').replace('WindowsSpeech', 'LinuxSpeech')
p = p.replace('SpecialFolder.ApplicationData), "BLACKSITE")', 'SpecialFolder.ApplicationData), "blacksite")').replace('    [STAThread]\n', '')
open(os.path.join(lin, 'Program.cs'), 'w', encoding='utf-8').write(p)
print('ok')
