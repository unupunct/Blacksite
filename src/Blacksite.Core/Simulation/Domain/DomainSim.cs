namespace Blacksite.Core.Simulation.Domain;

/// <summary>Base for the isolated per-preset simulations: time, speed, seeded RNG, activity log, reset and scenarios.</summary>
public abstract class DomainSim
{
    protected Random Rng = new(1);
    public double Time { get; protected set; }
    public double Speed { get; set; } = 1.0;
    public int Scenario { get; protected set; }
    public readonly List<string> Log = new();
    public event Action<string, bool>? Alert;     // (message, isWarning)

    protected void Note(string s, bool warn = false)
    {
        Log.Add($"{DateTime.Now:HH:mm:ss}  {s}");
        if (Log.Count > 200) Log.RemoveAt(0);
        if (warn) Alert?.Invoke(s, true);
    }

    public void Step(double dt) { dt *= Speed; Time += dt; OnStep(dt); }
    protected abstract void OnStep(double dt);

    public void Reset(int scenario = 0)
    {
        Scenario = scenario; Rng = new Random(1000 + scenario * 17); Time = 0; Log.Clear();
        OnReset();
    }
    protected abstract void OnReset();

    protected double Noise(double a) => (Rng.NextDouble() * 2 - 1) * a;
    protected static double Approach(double v, double target, double rate, double dt) => v + (target - v) * Math.Min(1, rate * dt);
    protected static double Clamp(double v, double a, double b) => Math.Max(a, Math.Min(b, v));
}
