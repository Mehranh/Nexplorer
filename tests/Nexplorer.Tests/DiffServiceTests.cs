using System.Collections.Specialized;
using System.Diagnostics;
using Nexplorer.App.Collections;
using Nexplorer.App.Services;

namespace Nexplorer.Tests;

public class DiffServiceTests
{
    [Fact]
    public void ComputeSideBySide_IdenticalTexts_AllUnchanged()
    {
        var text = "line1\nline2\nline3";
        var (oldLines, newLines) = DiffService.ComputeSideBySide(text, text);

        Assert.Equal(3, oldLines.Count);
        Assert.Equal(3, newLines.Count);
        Assert.All(oldLines, l => Assert.Equal(DiffLineKind.Unchanged, l.Kind));
        Assert.All(newLines, l => Assert.Equal(DiffLineKind.Unchanged, l.Kind));
    }

    [Fact]
    public void ComputeSideBySide_EmptyOld_AllAdded()
    {
        var (oldLines, newLines) = DiffService.ComputeSideBySide("", "line1\nline2");

        Assert.Equal(2, oldLines.Count);
        Assert.Equal(2, newLines.Count);
        Assert.All(newLines, l => Assert.Equal(DiffLineKind.Added, l.Kind));
    }

    [Fact]
    public void ComputeSideBySide_EmptyNew_AllRemoved()
    {
        var (oldLines, newLines) = DiffService.ComputeSideBySide("line1\nline2", "");

        Assert.Equal(2, oldLines.Count);
        Assert.Equal(2, newLines.Count);
        Assert.All(oldLines, l => Assert.Equal(DiffLineKind.Removed, l.Kind));
    }

    [Fact]
    public void ComputeSideBySide_BothEmpty_NoLines()
    {
        var (oldLines, newLines) = DiffService.ComputeSideBySide("", "");

        Assert.Empty(oldLines);
        Assert.Empty(newLines);
    }

    [Fact]
    public void ComputeSideBySide_SingleLineChange_ProducesRemoveAndAdd()
    {
        var (oldLines, newLines) = DiffService.ComputeSideBySide("hello", "world");

        Assert.Contains(oldLines, l => l.Kind == DiffLineKind.Removed && l.Text == "hello");
        Assert.Contains(newLines, l => l.Kind == DiffLineKind.Added && l.Text == "world");
    }

    [Fact]
    public void ComputeSideBySide_OldAndNewLineCountsMatch()
    {
        var oldText = "a\nb\nc\nd";
        var newText = "a\nx\nc\ny\nd";

        var (oldLines, newLines) = DiffService.ComputeSideBySide(oldText, newText);

        // Side-by-side diff always produces equal line counts
        Assert.Equal(oldLines.Count, newLines.Count);
    }

    [Fact]
    public void ComputeSideBySide_PreservesLineNumbers()
    {
        var oldText = "a\nb\nc";
        var newText = "a\nc";

        var (oldLines, newLines) = DiffService.ComputeSideBySide(oldText, newText);

        // 'a' should be unchanged and have line number 1 on both sides
        var firstOld = oldLines.First(l => l.Text == "a");
        var firstNew = newLines.First(l => l.Text == "a");
        Assert.Equal(1, firstOld.LineNumber);
        Assert.Equal(1, firstNew.LineNumber);
    }

    [Fact]
    public void ComputeSideBySide_LargeFile_DoesNotBlockExcessively()
    {
        // Generate a large file (2000 lines) with some differences
        var oldLines = Enumerable.Range(1, 2000).Select(i => $"line {i}").ToList();
        var newLines = new List<string>(oldLines);
        // Modify every 10th line
        for (int i = 0; i < newLines.Count; i += 10)
            newLines[i] = $"modified line {i}";
        // Add 200 new lines
        newLines.AddRange(Enumerable.Range(1, 200).Select(i => $"new line {i}"));

        var oldText = string.Join("\n", oldLines);
        var newText = string.Join("\n", newLines);

        var sw = Stopwatch.StartNew();
        var (resultOld, resultNew) = DiffService.ComputeSideBySide(oldText, newText);
        sw.Stop();

        // Should complete in well under 5 seconds (was blocking UI before)
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(5),
            $"ComputeSideBySide took {sw.Elapsed.TotalSeconds:F2}s, expected < 5s");

        Assert.Equal(resultOld.Count, resultNew.Count);
        Assert.True(resultOld.Count > 0);
    }

    [Fact]
    public async Task ComputeSideBySide_CanRunOnBackgroundThread()
    {
        var oldText = "line1\nline2\nline3";
        var newText = "line1\nmodified\nline3";

        // Verify the diff can be computed via Task.Run (the fix we applied)
        var (oldLines, newLines) = await Task.Run(() =>
            DiffService.ComputeSideBySide(oldText, newText));

        Assert.Equal(oldLines.Count, newLines.Count);
        Assert.Contains(oldLines, l => l.Kind == DiffLineKind.Removed);
        Assert.Contains(newLines, l => l.Kind == DiffLineKind.Added);
        Assert.Contains(oldLines, l => l.Kind == DiffLineKind.Unchanged && l.Text == "line1");
    }
}

public class RangeObservableCollectionTests
{
    [Fact]
    public void AddRange_AddsAllItems()
    {
        var collection = new RangeObservableCollection<int>();
        collection.AddRange([1, 2, 3, 4, 5]);

        Assert.Equal(5, collection.Count);
        Assert.Equal([1, 2, 3, 4, 5], collection.ToList());
    }

    [Fact]
    public void AddRange_FiresSingleCollectionChangedEvent()
    {
        var collection = new RangeObservableCollection<string>();
        int eventCount = 0;

        collection.CollectionChanged += (_, _) => eventCount++;

        collection.AddRange(["a", "b", "c", "d", "e"]);

        // Should fire exactly ONE Reset notification, not 5 individual Add notifications
        Assert.Equal(1, eventCount);
    }

    [Fact]
    public void AddRange_EmptyEnumerable_StillFiresEvent()
    {
        var collection = new RangeObservableCollection<int>();
        int eventCount = 0;
        collection.CollectionChanged += (_, _) => eventCount++;

        collection.AddRange([]);

        // Still fires reset even for empty (consistent behavior)
        Assert.Equal(1, eventCount);
        Assert.Empty(collection);
    }

    [Fact]
    public void AddRange_FiresResetAction()
    {
        var collection = new RangeObservableCollection<int>();
        NotifyCollectionChangedAction? action = null;

        collection.CollectionChanged += (_, e) => action = e.Action;

        collection.AddRange([1, 2, 3]);

        Assert.Equal(NotifyCollectionChangedAction.Reset, action);
    }

    [Fact]
    public void AddRange_LargeCollection_OnlyOneNotification()
    {
        var collection = new RangeObservableCollection<DiffLine>();
        int eventCount = 0;
        collection.CollectionChanged += (_, _) => eventCount++;

        var lines = Enumerable.Range(1, 1000)
            .Select(i => new DiffLine($"line {i}", DiffLineKind.Unchanged, i))
            .ToList();

        collection.AddRange(lines);

        Assert.Equal(1, eventCount);
        Assert.Equal(1000, collection.Count);
    }
}
