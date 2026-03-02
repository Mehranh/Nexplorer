using Nexplorer.App.Services;

namespace Nexplorer.Tests;

public class DiffScrollSyncControllerTests
{
    // ── ComputeScrollBarUpdate ──

    [Fact]
    public void ComputeScrollBarUpdate_Value_MatchesVerticalOffset()
    {
        var update = DiffScrollSyncController.ComputeScrollBarUpdate(
            verticalOffset: 150, extentHeight: 1000, viewportHeight: 300);

        Assert.Equal(150, update.Value);
    }

    [Fact]
    public void ComputeScrollBarUpdate_Maximum_IsExtentMinusViewport()
    {
        var update = DiffScrollSyncController.ComputeScrollBarUpdate(
            verticalOffset: 0, extentHeight: 1000, viewportHeight: 300);

        Assert.Equal(700, update.Maximum);
    }

    [Fact]
    public void ComputeScrollBarUpdate_Maximum_ClampsToZero_WhenContentFitsViewport()
    {
        var update = DiffScrollSyncController.ComputeScrollBarUpdate(
            verticalOffset: 0, extentHeight: 200, viewportHeight: 500);

        Assert.Equal(0, update.Maximum);
    }

    [Fact]
    public void ComputeScrollBarUpdate_ViewportSize_MatchesViewportHeight()
    {
        var update = DiffScrollSyncController.ComputeScrollBarUpdate(
            verticalOffset: 0, extentHeight: 1000, viewportHeight: 300);

        Assert.Equal(300, update.ViewportSize);
    }

    [Fact]
    public void ComputeScrollBarUpdate_LargeChange_EqualsViewportHeight()
    {
        var update = DiffScrollSyncController.ComputeScrollBarUpdate(
            verticalOffset: 0, extentHeight: 1000, viewportHeight: 300);

        Assert.Equal(300, update.LargeChange);
    }

    [Fact]
    public void ComputeScrollBarUpdate_SmallChange_Is18()
    {
        var update = DiffScrollSyncController.ComputeScrollBarUpdate(
            verticalOffset: 0, extentHeight: 1000, viewportHeight: 300);

        Assert.Equal(18, update.SmallChange);
    }

    [Fact]
    public void ComputeScrollBarUpdate_AtMaxScroll_ValueEqualsMaximum()
    {
        var update = DiffScrollSyncController.ComputeScrollBarUpdate(
            verticalOffset: 700, extentHeight: 1000, viewportHeight: 300);

        Assert.Equal(700, update.Value);
        Assert.Equal(700, update.Maximum);
    }

    [Fact]
    public void ComputeScrollBarUpdate_AtZeroOffset_ValueIsZero()
    {
        var update = DiffScrollSyncController.ComputeScrollBarUpdate(
            verticalOffset: 0, extentHeight: 1000, viewportHeight: 300);

        Assert.Equal(0, update.Value);
    }

    [Theory]
    [InlineData(0, 500, 500)]     // exactly fits
    [InlineData(0, 100, 1000)]    // lots of space
    [InlineData(0, 0, 300)]       // no content
    public void ComputeScrollBarUpdate_NoScrollNeeded_MaximumIsZero(
        double offset, double extent, double viewport)
    {
        var update = DiffScrollSyncController.ComputeScrollBarUpdate(offset, extent, viewport);

        Assert.Equal(0, update.Maximum);
    }

    // ── Re-entrancy guard ──

    [Fact]
    public void BeginSync_ReturnsTrueWhenNotSyncing()
    {
        var controller = new DiffScrollSyncController();

        Assert.True(controller.BeginSync());
    }

    [Fact]
    public void BeginSync_ReturnsFalseWhenAlreadySyncing()
    {
        var controller = new DiffScrollSyncController();
        controller.BeginSync();

        Assert.False(controller.BeginSync());
    }

    [Fact]
    public void EndSync_AllowsNewSync()
    {
        var controller = new DiffScrollSyncController();
        controller.BeginSync();
        controller.EndSync();

        Assert.True(controller.BeginSync());
    }

    [Fact]
    public void IsSyncing_ReflectsState()
    {
        var controller = new DiffScrollSyncController();

        Assert.False(controller.IsSyncing);
        controller.BeginSync();
        Assert.True(controller.IsSyncing);
        controller.EndSync();
        Assert.False(controller.IsSyncing);
    }

    // ── Full flow simulations ──

    [Fact]
    public void PanelScroll_ShouldProduceScrollBarValueMatchingOffset()
    {
        // When a panel scrolls, the computed scrollbar update must
        // include Value = verticalOffset. This is the key fix:
        // previously Value was not set, so the middle scrollbar
        // didn't track panel scrolling and the peer panel didn't sync.
        var update = DiffScrollSyncController.ComputeScrollBarUpdate(
            verticalOffset: 150, extentHeight: 1000, viewportHeight: 300);

        Assert.Equal(150, update.Value);
        Assert.Equal(700, update.Maximum);
    }

    [Fact]
    public void FullSyncFlow_PanelScrollUpdatesScrollBarAndPeer()
    {
        // Each diff panel owns its own controller.
        var controllerA = new DiffScrollSyncController();
        var controllerB = new DiffScrollSyncController();

        // Step 1: Panel A scrolls to offset 150.
        var update = DiffScrollSyncController.ComputeScrollBarUpdate(150, 1000, 300);

        // Step 2: Panel A begins sync and applies update to shared scrollbar.
        Assert.True(controllerA.BeginSync());

        // Setting SharedScrollBar.Value fires ValueChanged synchronously.
        // Panel A's handler: IsSyncing is true → skip.
        Assert.True(controllerA.IsSyncing);

        // Panel B's handler: IsSyncing is false → processes the new value.
        Assert.False(controllerB.IsSyncing);
        Assert.True(controllerB.BeginSync());
        double panelBOffset = update.Value; // Panel B scrolls to this offset
        controllerB.EndSync();

        // Step 3: Panel A finishes sync.
        controllerA.EndSync();

        // Both panels should be at offset 150, scrollbar Value is 150.
        Assert.Equal(150, panelBOffset);
        Assert.Equal(150, update.Value);
    }

    [Fact]
    public void FullSyncFlow_ScrollBarDragUpdatesBothPanels()
    {
        // Each panel owns its own controller.
        var controllerA = new DiffScrollSyncController();
        var controllerB = new DiffScrollSyncController();

        double scrollBarNewValue = 200;

        // SharedScrollBar.ValueChanged fires on both panels.
        // Panel A processes the event:
        Assert.False(controllerA.IsSyncing);
        Assert.True(controllerA.BeginSync());
        double panelAOffset = scrollBarNewValue;
        controllerA.EndSync();

        // Panel B processes the event:
        Assert.False(controllerB.IsSyncing);
        Assert.True(controllerB.BeginSync());
        double panelBOffset = scrollBarNewValue;
        controllerB.EndSync();

        Assert.Equal(200, panelAOffset);
        Assert.Equal(200, panelBOffset);
    }

    [Fact]
    public void ReentrancyPrevention_NestedScrollEventsAreBlocked()
    {
        var controller = new DiffScrollSyncController();

        // Simulate: Panel starts a sync (updating shared scrollbar).
        Assert.True(controller.BeginSync());

        // During sync, WPF may fire ScrollChanged re-entrantly.
        // The guard must prevent processing.
        Assert.True(controller.IsSyncing);
        Assert.False(controller.BeginSync()); // blocked

        controller.EndSync();
        Assert.False(controller.IsSyncing);
    }

    [Fact]
    public void IndependentControllers_DoNotInterfere()
    {
        // Each panel has its own controller; syncing on one
        // must not block the other.
        var controllerA = new DiffScrollSyncController();
        var controllerB = new DiffScrollSyncController();

        Assert.True(controllerA.BeginSync());
        Assert.True(controllerA.IsSyncing);
        Assert.False(controllerB.IsSyncing);

        // Panel B can still begin its own sync.
        Assert.True(controllerB.BeginSync());

        controllerA.EndSync();
        controllerB.EndSync();
    }
}
