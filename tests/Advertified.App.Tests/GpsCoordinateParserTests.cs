using Advertified.App.Support;
using FluentAssertions;

namespace Advertified.App.Tests;

public sealed class GpsCoordinateParserTests
{
    [Fact]
    public void TryParse_ParsesDecimalCoordinates()
    {
        var result = GpsCoordinateParser.TryParse("-26.2041, 28.0473");

        result.Should().NotBeNull();
        result!.Value.Latitude.Should().BeApproximately(-26.2041, 0.0001);
        result.Value.Longitude.Should().BeApproximately(28.0473, 0.0001);
    }

    [Fact]
    public void TryParse_ParsesDmsCoordinatesWithNoisySeparators()
    {
        var result = GpsCoordinateParser.TryParse("26?14'59.13\"S 27?57'14.20\"E");

        result.Should().NotBeNull();
        result!.Value.Latitude.Should().BeApproximately(-26.249758, 0.0005);
        result.Value.Longitude.Should().BeApproximately(27.953944, 0.0005);
    }
}
