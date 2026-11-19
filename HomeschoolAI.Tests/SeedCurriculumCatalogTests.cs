using HomeschoolAI.Adapters.Learning;
using HomeschoolAI.Domain.Progress;
using Xunit;

namespace HomeschoolAI.Tests;

public class SeedCurriculumCatalogTests
{
    [Fact]
    public void Every_topic_has_three_slides_with_lesson_image_urls()
    {
        // Reúne todos los temas semilla (grados 1 a 6) sin duplicados.
        var topics = Enumerable.Range(1, 6)
            .SelectMany(grade => SeedCurriculumCatalog.GetTopicsForGrade(grade))
            .GroupBy(topic => topic.TopicId)
            .Select(group => group.First())
            .ToList();

        Assert.NotEmpty(topics);

        foreach (var topic in topics)
        {
            Assert.Equal(3, topic.Blocks.Count);

            for (var i = 0; i < topic.Blocks.Count; i++)
            {
                var block = topic.Blocks[i];

                Assert.False(
                    string.IsNullOrWhiteSpace(block.ImageUrl),
                    $"El tema '{topic.TopicId}' (lámina {i + 1}) no tiene ImageUrl.");

                Assert.StartsWith("/lessons/", block.ImageUrl);
                Assert.EndsWith($"/slide-{i + 1}.svg", block.ImageUrl);
                Assert.False(string.IsNullOrWhiteSpace(block.Content));
                Assert.False(string.IsNullOrWhiteSpace(block.AltText));
            }
        }
    }

    [Fact]
    public void Grade4_has_at_least_twelve_topics_with_slides_and_images()
    {
        var grade4 = SeedCurriculumCatalog.GetTopicsForGrade(4);

        Assert.True(grade4.Count >= 12, $"Grado 4 debe tener al menos 12 temas; tiene {grade4.Count}.");

        foreach (var topic in grade4)
        {
            Assert.Equal(3, topic.Blocks.Count);
            foreach (var block in topic.Blocks)
            {
                Assert.False(string.IsNullOrWhiteSpace(block.ImageUrl));
                Assert.StartsWith("/lessons/", block.ImageUrl);
            }
        }
    }

    [Fact]
    public void SelectMissionOptions_returns_new_pending_topics_after_completing_three()
    {
        var completed = SeedCurriculumCatalog.GetTopicsForGrade(4)
            .Take(3)
            .Select(t => new ChildTopicProgress
            {
                TopicId = t.TopicId,
                TopicTitle = t.Title,
                CampoFormativo = t.CampoFormativo,
                Status = TopicStatus.Completed,
                Attempts = 1,
                LastActivityAtUtc = DateTime.UtcNow
            })
            .ToList();

        var options = SeedCurriculumCatalog.SelectMissionOptions(4, completed, limit: 3);

        Assert.Equal(3, options.Count);
        var completedIds = completed.Select(c => c.TopicId).ToHashSet(StringComparer.Ordinal);
        Assert.All(options, o => Assert.DoesNotContain(o.TopicId, completedIds));

        // Los 3 siguientes NotStarted en orden curricular (OrderIndex 4, 5, 6).
        Assert.Contains(options, o => o.TopicId == "fase4-saberes-nutricion");
        Assert.Contains(options, o => o.TopicId == "fase4-saberes-movimiento");
        Assert.Contains(options, o => o.TopicId == "fase4-lenguajes-instructivos");
    }
}
