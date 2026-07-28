using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace TaynDM;

/// <summary>
/// Tracks download speed over time for charting.
/// Maintains a rolling window of samples (one per second).
/// </summary>
public sealed class SpeedHistory : INotifyPropertyChanged
{
    private const int MaxSamples = 60; // 60 seconds of history
    private readonly List<double> _samples = new();
    private double _peakSpeed;

    public IReadOnlyList<double> Samples => _samples;
    public double PeakSpeed => _peakSpeed;
    public double CurrentSpeed => _samples.Count > 0 ? _samples[^1] : 0;
    public double AverageSpeed => _samples.Count > 0 ? _samples.Average() : 0;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Add a speed sample in bytes per second.</summary>
    public void AddSample(double bytesPerSecond)
    {
        _samples.Add(bytesPerSecond);
        if (_samples.Count > MaxSamples)
            _samples.RemoveAt(0);
        if (bytesPerSecond > _peakSpeed)
            _peakSpeed = bytesPerSecond;

        OnPropertyChanged(nameof(CurrentSpeed));
        OnPropertyChanged(nameof(AverageSpeed));
        OnPropertyChanged(nameof(PeakSpeed));
    }

    /// <summary>Reset all history.</summary>
    public void Clear()
    {
        _samples.Clear();
        _peakSpeed = 0;
        OnPropertyChanged(nameof(CurrentSpeed));
        OnPropertyChanged(nameof(AverageSpeed));
        OnPropertyChanged(nameof(PeakSpeed));
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
