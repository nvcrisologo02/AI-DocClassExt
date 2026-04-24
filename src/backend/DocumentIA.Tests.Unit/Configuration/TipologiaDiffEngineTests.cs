using System.Text.Json.Nodes;
using FluentAssertions;

namespace DocumentIA.Tests.Unit.Configuration;

public class TipologiaDiffEngineTests
{
    [Fact]
    public void JsonDiff_WhenPropertyModified_ReturnsModifiedChange()
    {
        var left = JsonNode.Parse("""
        {
          "a": 1,
          "nested": { "x": "old" }
        }
        """);

        var right = JsonNode.Parse("""
        {
          "a": 1,
          "nested": { "x": "new" }
        }
        """);

        var changes = new List<(string Path, string Type)>();
        Compare("$", left, right, changes);

        changes.Should().ContainSingle(c => c.Path == "$.nested.x" && c.Type == "modified");
    }

    [Fact]
    public void JsonDiff_WhenPropertyAddedAndRemoved_ReturnsExpectedChanges()
    {
        var left = JsonNode.Parse("""
        {
          "a": 1,
          "b": 2
        }
        """);

        var right = JsonNode.Parse("""
        {
          "a": 1,
          "c": 3
        }
        """);

        var changes = new List<(string Path, string Type)>();
        Compare("$", left, right, changes);

        changes.Should().Contain(c => c.Path == "$.b" && c.Type == "removed");
        changes.Should().Contain(c => c.Path == "$.c" && c.Type == "added");
    }

    private static void Compare(string path, JsonNode? left, JsonNode? right, List<(string Path, string Type)> changes)
    {
        if (left is null && right is null)
        {
            return;
        }

        if (left is null)
        {
            changes.Add((path, "added"));
            return;
        }

        if (right is null)
        {
            changes.Add((path, "removed"));
            return;
        }

        if (left is JsonObject leftObj && right is JsonObject rightObj)
        {
            var keys = leftObj.Select(x => x.Key).Union(rightObj.Select(x => x.Key), StringComparer.Ordinal).OrderBy(x => x);
            foreach (var key in keys)
            {
                Compare($"{path}.{key}", leftObj[key], rightObj[key], changes);
            }
            return;
        }

        if (left is JsonArray leftArr && right is JsonArray rightArr)
        {
            var max = Math.Max(leftArr.Count, rightArr.Count);
            for (var i = 0; i < max; i++)
            {
                Compare($"{path}[{i}]", i < leftArr.Count ? leftArr[i] : null, i < rightArr.Count ? rightArr[i] : null, changes);
            }
            return;
        }

        if (!string.Equals(left.ToJsonString(), right.ToJsonString(), StringComparison.Ordinal))
        {
            changes.Add((path, "modified"));
        }
    }
}
