using System.IO;
using Nexplorer.App.Services;

namespace Nexplorer.Tests;

public class SearchServiceTests
{
    [Fact]
    public async Task Recursive_Substring_FindsDeeplyNestedFiles()
    {
        var root = NewTempDir();
        try
        {
            var l3 = Path.Combine(root, "level1", "level2", "level3");
            Directory.CreateDirectory(l3);
            File.WriteAllText(Path.Combine(root, "root_target.txt"), "x");
            File.WriteAllText(Path.Combine(root, "level1", "l1_target.txt"), "x");
            File.WriteAllText(Path.Combine(root, "level1", "level2", "l2_target.txt"), "x");
            File.WriteAllText(Path.Combine(l3, "l3_target.txt"), "x");

            var hits = await CollectAsync(new SearchCriteria
            {
                Query = "target",
                RootPath = root,
                Recursive = true,
            });

            Assert.Contains(hits, h => h.EndsWith("root_target.txt"));
            Assert.Contains(hits, h => h.EndsWith("l1_target.txt"));
            Assert.Contains(hits, h => h.EndsWith("l2_target.txt"));
            Assert.Contains(hits, h => h.EndsWith("l3_target.txt"));
        }
        finally { TryDelete(root); }
    }

    [Fact]
    public async Task Recursive_Wildcard_MatchesAcrossNestedDirectories()
    {
        var root = NewTempDir();
        try
        {
            var deep = Path.Combine(root, "a", "b", "c");
            Directory.CreateDirectory(deep);
            File.WriteAllText(Path.Combine(deep, "deep.txt"), "x");
            File.WriteAllText(Path.Combine(deep, "ignored.log"), "x");

            var hits = await CollectAsync(new SearchCriteria
            {
                Query = "*.txt",
                RootPath = root,
                Recursive = true,
            });

            Assert.Contains(hits, h => h.EndsWith("deep.txt"));
            Assert.DoesNotContain(hits, h => h.EndsWith("ignored.log"));
        }
        finally { TryDelete(root); }
    }

    [Fact]
    public async Task NonRecursive_DoesNotDescendIntoSubdirectories()
    {
        var root = NewTempDir();
        try
        {
            var sub = Path.Combine(root, "sub");
            Directory.CreateDirectory(sub);
            File.WriteAllText(Path.Combine(root, "top.txt"), "x");
            File.WriteAllText(Path.Combine(sub, "nested.txt"), "x");

            var hits = await CollectAsync(new SearchCriteria
            {
                Query = "*.txt",
                RootPath = root,
                Recursive = false,
            });

            Assert.Contains(hits, h => h.EndsWith("top.txt"));
            Assert.DoesNotContain(hits, h => h.EndsWith("nested.txt"));
        }
        finally { TryDelete(root); }
    }

    private static async Task<List<string>> CollectAsync(SearchCriteria criteria)
    {
        var hits = new List<string>();
        await foreach (var fi in SearchService.SearchAsync(criteria))
            hits.Add(fi.FullName);
        return hits;
    }

    private static string NewTempDir()
    {
        var p = Path.Combine(Path.GetTempPath(), "nex_search_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(p);
        return p;
    }

    private static void TryDelete(string p)
    {
        try { Directory.Delete(p, recursive: true); } catch { }
    }
}
