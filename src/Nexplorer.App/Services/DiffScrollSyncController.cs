namespace Nexplorer.App.Services;

public sealed record ScrollBarUpdate(
    double Maximum, double ViewportSize, double SmallChange, double LargeChange, double Value);

/// <summary>
/// Manages re-entrancy prevention and scrollbar property computation
/// for synchronized diff panel scrolling. Each diff panel owns one instance.
/// </summary>
public sealed class DiffScrollSyncController
{
    private bool _isSyncing;
    public bool IsSyncing => _isSyncing;

    /// <summary>
    /// Computes the shared scrollbar properties from a panel's scroll state.
    /// The returned Value MUST be applied to the shared scrollbar so that
    /// it tracks the panel position and triggers peer synchronisation.
    /// </summary>
    public static ScrollBarUpdate ComputeScrollBarUpdate(
        double verticalOffset, double extentHeight, double viewportHeight) =>
        new(
            Maximum: Math.Max(0, extentHeight - viewportHeight),
            ViewportSize: viewportHeight,
            SmallChange: 18,
            LargeChange: viewportHeight,
            Value: verticalOffset);

    /// <summary>
    /// Begin a sync operation. Returns false if already syncing (re-entrancy guard).
    /// Must be paired with <see cref="EndSync"/>.
    /// </summary>
    public bool BeginSync()
    {
        if (_isSyncing) return false;
        _isSyncing = true;
        return true;
    }

    public void EndSync() => _isSyncing = false;
}
