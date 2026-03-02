using FileExplorer.App.ViewModels;

namespace FileExplorer.Tests;

public class CommandResultTests
{
    [Fact]
    public void IsSuccess_ExitCodeZero_ReturnsTrue()
    {
        var result = new CommandResult(0, "output", "", TimeSpan.FromSeconds(1), "cmd");
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void IsSuccess_NonZeroExitCode_ReturnsFalse()
    {
        var result = new CommandResult(1, "", "error", TimeSpan.FromSeconds(1), "cmd");
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void ErrorSummary_NoError_ReturnsNull()
    {
        var result = new CommandResult(0, "ok", "", TimeSpan.Zero, "cmd");
        Assert.Null(result.ErrorSummary);
    }

    [Fact]
    public void ErrorSummary_WhitespaceOnly_ReturnsNull()
    {
        var result = new CommandResult(1, "", "   \n  ", TimeSpan.Zero, "cmd");
        Assert.Null(result.ErrorSummary);
    }

    [Fact]
    public void ErrorSummary_MultiLineError_ReturnsLastLine()
    {
        var result = new CommandResult(1, "", "first line\nsecond line\nfinal error",
            TimeSpan.Zero, "cmd");
        Assert.Equal("final error", result.ErrorSummary);
    }

    [Fact]
    public void ErrorSummary_SingleLineError_ReturnsThatLine()
    {
        var result = new CommandResult(1, "", "single error", TimeSpan.Zero, "cmd");
        Assert.Equal("single error", result.ErrorSummary);
    }

    [Fact]
    public void ErrorSummary_TrailingNewlines_Trimmed()
    {
        var result = new CommandResult(1, "", "error message\n\n", TimeSpan.Zero, "cmd");
        Assert.Equal("error message", result.ErrorSummary);
    }
}

public class ExecutionMetricsTests
{
    [Fact]
    public void Initial_AllZero()
    {
        var m = new ExecutionMetrics();
        Assert.Equal(0, m.TotalCommands);
        Assert.Equal(0, m.SuccessCount);
        Assert.Equal(0, m.FailureCount);
        Assert.Equal(TimeSpan.Zero, m.TotalDuration);
        Assert.Equal(TimeSpan.Zero, m.AverageDuration);
        Assert.Null(m.LastCommand);
        Assert.Null(m.LastExitCode);
        Assert.Null(m.LastDuration);
        Assert.Null(m.SlowestDuration);
        Assert.Null(m.SlowestCommand);
    }

    [Fact]
    public void Record_SingleSuccess_TrackedCorrectly()
    {
        var m = new ExecutionMetrics();
        m.Record("git status", 0, TimeSpan.FromMilliseconds(150));

        Assert.Equal(1, m.TotalCommands);
        Assert.Equal(1, m.SuccessCount);
        Assert.Equal(0, m.FailureCount);
        Assert.Equal("git status", m.LastCommand);
        Assert.Equal(0, m.LastExitCode);
        Assert.Equal(TimeSpan.FromMilliseconds(150), m.LastDuration);
    }

    [Fact]
    public void Record_SingleFailure_TrackedCorrectly()
    {
        var m = new ExecutionMetrics();
        m.Record("bad-cmd", 1, TimeSpan.FromMilliseconds(50));

        Assert.Equal(1, m.TotalCommands);
        Assert.Equal(0, m.SuccessCount);
        Assert.Equal(1, m.FailureCount);
        Assert.Equal(1, m.LastExitCode);
    }

    [Fact]
    public void Record_Multiple_AverageDurationCorrect()
    {
        var m = new ExecutionMetrics();
        m.Record("cmd1", 0, TimeSpan.FromMilliseconds(100));
        m.Record("cmd2", 0, TimeSpan.FromMilliseconds(200));
        m.Record("cmd3", 0, TimeSpan.FromMilliseconds(300));

        Assert.Equal(3, m.TotalCommands);
        Assert.Equal(TimeSpan.FromMilliseconds(600), m.TotalDuration);
        Assert.Equal(TimeSpan.FromMilliseconds(200), m.AverageDuration);
    }

    [Fact]
    public void Record_TracksSlowest()
    {
        var m = new ExecutionMetrics();
        m.Record("fast", 0, TimeSpan.FromMilliseconds(10));
        m.Record("slow", 0, TimeSpan.FromMilliseconds(5000));
        m.Record("medium", 0, TimeSpan.FromMilliseconds(500));

        Assert.Equal(TimeSpan.FromMilliseconds(5000), m.SlowestDuration);
        Assert.Equal("slow", m.SlowestCommand);
    }

    [Fact]
    public void Record_LastCommand_UpdatesOnEach()
    {
        var m = new ExecutionMetrics();
        m.Record("first", 0, TimeSpan.FromMilliseconds(10));
        Assert.Equal("first", m.LastCommand);

        m.Record("second", 1, TimeSpan.FromMilliseconds(20));
        Assert.Equal("second", m.LastCommand);
        Assert.Equal(1, m.LastExitCode);
    }

    [Fact]
    public void Record_MixedSuccessFailure_CountsCorrectly()
    {
        var m = new ExecutionMetrics();
        m.Record("ok1", 0, TimeSpan.FromMilliseconds(10));
        m.Record("err1", 1, TimeSpan.FromMilliseconds(20));
        m.Record("ok2", 0, TimeSpan.FromMilliseconds(30));
        m.Record("err2", 2, TimeSpan.FromMilliseconds(40));
        m.Record("ok3", 0, TimeSpan.FromMilliseconds(50));

        Assert.Equal(5, m.TotalCommands);
        Assert.Equal(3, m.SuccessCount);
        Assert.Equal(2, m.FailureCount);
    }
}
