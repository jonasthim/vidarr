using Vidarr.Contracts.Models;
using Vidarr.Indexers;

namespace Vidarr.Indexers.Tests;

public class YouTubeQualityMapperTests
{
    private readonly IYouTubeQualityMapper _sut = new YouTubeQualityMapper();

    [Theory]
    [InlineData(null, 1)]    // Unknown
    [InlineData(0, 1)]        // Unknown
    [InlineData(-10, 1)]      // Unknown
    [InlineData(360, 2)]      // WEBDL-480p (anything > 0, < 720)
    [InlineData(479, 2)]
    [InlineData(480, 2)]      // 480 still rounds down to 480p
    [InlineData(719, 2)]
    [InlineData(720, 3)]      // WEBDL-720p
    [InlineData(1079, 3)]     // boundary just under 1080
    [InlineData(1080, 4)]     // WEBDL-1080p
    [InlineData(1440, 4)]     // 2K (1440) maps to 1080 ladder (no quality entry for 1440)
    [InlineData(2159, 4)]     // boundary just under 2160
    [InlineData(2160, 5)]     // WEBDL-2160p
    [InlineData(4320, 5)]     // 8K maps to 2160 ladder
    public void Height_maps_to_expected_quality_ladder(int? height, int expectedQualityId) =>
        _sut.FromHeight(height).Id.Should().Be(expectedQualityId);

    [Fact]
    public void Mapper_is_deterministic_within_a_band()
    {
        new YouTubeQualityMapper().FromHeight(1080)
            .Should().Be(new YouTubeQualityMapper().FromHeight(1080));
    }
}
