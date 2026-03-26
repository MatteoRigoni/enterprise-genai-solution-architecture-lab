using System.Text.Json;
using AiSa.Domain.Eval;

namespace AiSa.Tests;

public class EvalDatasetParsingTests
{
    [Fact]
    public void BaseDataset_Deserializes_WithExpectedShape()
    {
        var datasetPath = GetDatasetPath();
        Assert.True(File.Exists(datasetPath), $"Expected dataset file to exist at '{datasetPath}'.");

        var json = File.ReadAllText(datasetPath);
        var dataset = JsonSerializer.Deserialize<EvalDataset>(json);

        Assert.NotNull(dataset);
        Assert.False(string.IsNullOrWhiteSpace(dataset!.Name));
        Assert.False(string.IsNullOrWhiteSpace(dataset.Version));

        Assert.NotNull(dataset.Questions);
        Assert.Equal(20, dataset.Questions.Count);

        foreach (var question in dataset.Questions)
        {
            Assert.False(string.IsNullOrWhiteSpace(question.Question));
            Assert.NotNull(question.ExpectedKeyFacts);
            Assert.NotEmpty(question.ExpectedKeyFacts);
        }
    }

    private static string GetDatasetPath()
    {
        var baseDirectory = AppContext.BaseDirectory;

        // bin/Debug/net10.0 -> project root -> repo root
        var repoRoot = Path.GetFullPath(Path.Combine(baseDirectory, "../../../../.."));
        return Path.Combine(repoRoot, "eval", "datasets", "base.json");
    }
}

