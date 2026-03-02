using Nexplorer.App.Services;

namespace Nexplorer.Tests;

public class UpdateServiceTests
{
    // ─── IsNewerVersion: remote 3-part vs current 4-part (the actual bug) ─────

    [Theory]
    [InlineData("1.0.8", "1.0.5.0", true)]   // remote newer (build differs)
    [InlineData("1.0.8", "1.0.8.0", false)]  // same version — was broken before fix
    [InlineData("1.0.5", "1.0.8.0", false)]  // remote older
    [InlineData("2.0.0", "1.9.9.0", true)]   // major bump
    [InlineData("1.1.0", "1.0.9.0", true)]   // minor bump
    public void IsNewerVersion_ThreePartRemote_FourPartCurrent(
        string remoteStr, string currentStr, bool expected)
    {
        var remote  = Version.Parse(remoteStr);
        var current = Version.Parse(currentStr);

        Assert.Equal(expected, UpdateService.IsNewerVersion(remote, current));
    }

    // ─── IsNewerVersion: both 3-part ──────────────────────────────────────────

    [Theory]
    [InlineData("1.0.8", "1.0.5", true)]
    [InlineData("1.0.8", "1.0.8", false)]
    [InlineData("1.0.5", "1.0.8", false)]
    public void IsNewerVersion_BothThreePart(
        string remoteStr, string currentStr, bool expected)
    {
        var remote  = Version.Parse(remoteStr);
        var current = Version.Parse(currentStr);

        Assert.Equal(expected, UpdateService.IsNewerVersion(remote, current));
    }

    // ─── IsNewerVersion: both 4-part ──────────────────────────────────────────

    [Theory]
    [InlineData("1.0.8.0", "1.0.5.0", true)]
    [InlineData("1.0.8.0", "1.0.8.0", false)]
    [InlineData("1.0.5.0", "1.0.8.0", false)]
    public void IsNewerVersion_BothFourPart(
        string remoteStr, string currentStr, bool expected)
    {
        var remote  = Version.Parse(remoteStr);
        var current = Version.Parse(currentStr);

        Assert.Equal(expected, UpdateService.IsNewerVersion(remote, current));
    }

    // ─── Edge cases ───────────────────────────────────────────────────────────

    [Fact]
    public void IsNewerVersion_MajorBump_DetectedCorrectly()
    {
        var remote  = Version.Parse("2.0.0");
        var current = new Version(1, 9, 9, 0);

        Assert.True(UpdateService.IsNewerVersion(remote, current));
    }

    [Fact]
    public void IsNewerVersion_MinorBump_DetectedCorrectly()
    {
        var remote  = Version.Parse("1.2.0");
        var current = new Version(1, 1, 99, 0);

        Assert.True(UpdateService.IsNewerVersion(remote, current));
    }

    [Fact]
    public void IsNewerVersion_SameVersion_NoUpdate()
    {
        // This is the exact scenario that was failing before the fix:
        // Version.Parse("1.0.8") gives Revision=-1, assembly gives Revision=0
        var remote  = Version.Parse("1.0.8");   // Revision = -1
        var current = new Version(1, 0, 8, 0);  // Revision = 0

        // Before the fix, remote < current due to -1 < 0 revision mismatch
        Assert.False(UpdateService.IsNewerVersion(remote, current));
    }

    [Fact]
    public void IsNewerVersion_OldInstalled_UpdateDetected()
    {
        // The user's exact scenario: installed 1.0.5, remote 1.0.8
        var remote  = Version.Parse("1.0.8");
        var current = new Version(1, 0, 5, 0);

        Assert.True(UpdateService.IsNewerVersion(remote, current));
    }
}
