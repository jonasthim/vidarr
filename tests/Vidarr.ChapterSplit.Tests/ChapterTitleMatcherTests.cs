using Vidarr.ChapterSplit;

namespace Vidarr.ChapterSplit.Tests;

public class ChapterTitleMatcherTests
{
    private readonly ChapterTitleMatcher _sut = new();

    [Fact]
    public void Similarity_returns_1_for_identical_titles()
    {
        _sut.Similarity("Around the World", "Around the World").Should().Be(1);
    }

    [Fact]
    public void Similarity_returns_0_for_completely_disjoint_titles()
    {
        _sut.Similarity("Around the World", "Material Girl").Should().Be(0);
    }

    [Fact]
    public void Similarity_returns_0_for_empty_inputs()
    {
        _sut.Similarity("", "Anything").Should().Be(0);
        _sut.Similarity("Anything", "").Should().Be(0);
    }

    [Fact]
    public void Artist_name_in_chapter_title_does_not_dilute_match()
    {
        // chapter has "Daft Punk - Around the World", candidate has "Around the World"
        var noContext = _sut.Similarity("Daft Punk - Around the World", "Around the World");
        var withContext = _sut.Similarity("Daft Punk - Around the World", "Around the World", artistName: "Daft Punk");
        withContext.Should().BeGreaterThan(noContext);
    }

    [Fact]
    public void Partial_token_overlap_returns_partial_score()
    {
        // chapter "Around the World" vs candidate "Around the World (Edit)" share 3 of 4 unique tokens
        var score = _sut.Similarity("Around the World", "Around the World Edit");
        score.Should().BeInRange(0.5, 0.9);
    }

    [Fact]
    public void Assign_picks_best_match_per_chapter_with_1_to_1_constraint()
    {
        var chapters = new[]
        {
            new MediaChapter(0, TimeSpan.Zero, TimeSpan.FromMinutes(3), "Around the World"),
            new MediaChapter(1, TimeSpan.FromMinutes(3), TimeSpan.FromMinutes(6), "One More Time"),
            new MediaChapter(2, TimeSpan.FromMinutes(6), TimeSpan.FromMinutes(9), "Da Funk"),
        };
        var candidates = new[]
        {
            new ChapterMatchCandidate(100, "Around the World"),
            new ChapterMatchCandidate(101, "One More Time"),
            new ChapterMatchCandidate(102, "Around the Park"), // distractor close to chapter 0
        };

        var result = _sut.Assign(chapters, candidates);
        result[0].Match!.CandidateId.Should().Be(100);
        result[1].Match!.CandidateId.Should().Be(101);
        result[2].Match.Should().BeNull(); // Da Funk has no candidate
    }

    [Fact]
    public void Assign_leaves_unmatched_when_score_below_minimum()
    {
        var chapters = new[]
        {
            new MediaChapter(0, TimeSpan.Zero, TimeSpan.FromMinutes(3), "Mystery Track"),
        };
        var candidates = new[]
        {
            new ChapterMatchCandidate(100, "Completely Unrelated Title Goes Here"),
        };
        var result = _sut.Assign(chapters, candidates);
        result.Single().Match.Should().BeNull();
    }

    [Fact]
    public void Assign_returns_chapter_with_null_match_when_no_candidates()
    {
        var chapters = new[]
        {
            new MediaChapter(0, TimeSpan.Zero, TimeSpan.FromMinutes(3), "Around the World"),
        };
        var result = _sut.Assign(chapters, []);
        result.Single().Match.Should().BeNull();
    }

    [Fact]
    public void Assign_respects_custom_minimum_score()
    {
        var strict = new ChapterTitleMatcher { MinimumScore = 0.95 };
        var chapters = new[]
        {
            new MediaChapter(0, TimeSpan.Zero, TimeSpan.FromMinutes(3), "Around the World Edit"),
        };
        var candidates = new[]
        {
            new ChapterMatchCandidate(100, "Around the World"),
        };
        strict.Assign(chapters, candidates).Single().Match.Should().BeNull();
    }

    [Fact]
    public void Tokenisation_strips_artist_name_before_comparison()
    {
        // With artist context "Daft Punk", both chapter and candidate degenerate to
        // the same residual tokens "around" "the" "world".
        var score = _sut.Similarity(
            "Daft Punk - Around the World",
            "Daft Punk - Around the World",
            artistName: "Daft Punk");
        score.Should().Be(1);
    }
}
