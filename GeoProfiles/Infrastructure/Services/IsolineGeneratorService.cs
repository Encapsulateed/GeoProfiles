using GeoProfiles.Model;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using NetTopologySuite.Precision;

namespace GeoProfiles.Infrastructure.Services;

public record GeneratedIsoline(int Level, Polygon Geometry);

public interface IIsolineGeneratorService
{
    Task<(Polygon Bbox, IReadOnlyList<GeneratedIsoline> Isolines)> GenerateAsync(
        int count,
        CancellationToken ct = default);
}

public class MockIsolineGeneratorService : IIsolineGeneratorService
{
    private static readonly GeometryFactory Gf =
        NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);

    private static readonly Random Rnd = new();

    private const double DegPerMeter = 1.0 / 111_320d;

    public Task<(Polygon Bbox, IReadOnlyList<GeneratedIsoline> Isolines)> GenerateAsync(
        int count,
        CancellationToken ct = default)
    {
        if (count <= 0) count = 5;

        var cx = (Rnd.NextDouble() - 0.5) * 0.1;
        var cy = (Rnd.NextDouble() - 0.5) * 0.1;
        var center = Gf.CreatePoint(new Coordinate(cx, cy));

        const double baseRadiusMeters = 200;
        const double stepMeters = 150;
        const int quadSegs = 8;

        var isolines = new List<GeneratedIsoline>(count);

        for (var level = 0; level < count; level++)
        {
            var radiusDeg = (baseRadiusMeters + level * stepMeters) * DegPerMeter;

            var poly = (Polygon) center.Buffer(radiusDeg, quadSegs);

            poly = (Polygon) new GeometryPrecisionReducer(
                    new PrecisionModel(scale: 1e5))
                .Reduce(poly);

            poly.SRID = 4326;

            isolines.Add(new GeneratedIsoline(level, poly));
        }

        var bbox = (Polygon) isolines[^1].Geometry.Envelope;
        bbox.SRID = 4326;

        return Task.FromResult<(Polygon, IReadOnlyList<GeneratedIsoline>)>(
            (bbox, isolines));
    }
}