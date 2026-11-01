namespace Blacksite.Core.Simulation;

/// <summary>Fixed-size history buffer; no allocation after construction.</summary>
public sealed class RingBuffer
{
    readonly float[] _d;
    int _head;
    public int Count { get; private set; }
    public int Capacity => _d.Length;

    public RingBuffer(int capacity) { _d = new float[capacity]; }

    public void Push(float v)
    {
        _d[_head] = v;
        _head = (_head + 1) % _d.Length;
        if (Count < _d.Length) Count++;
    }

    /// <summary>0 = oldest, Count-1 = newest.</summary>
    public float this[int i] => _d[(_head - Count + i + _d.Length * 2) % _d.Length];

    public float Last => Count == 0 ? 0 : this[Count - 1];

    public (float min, float max) Range()
    {
        if (Count == 0) return (0, 1);
        float mn = float.MaxValue, mx = float.MinValue;
        for (int i = 0; i < Count; i++) { var v = this[i]; if (v < mn) mn = v; if (v > mx) mx = v; }
        if (mx - mn < 1e-4f) mx = mn + 1;
        return (mn, mx);
    }
}
