using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;
using NetTopologySuite.Operation.Distance;

namespace GeoProfiles.Services
{
    public class MockDemOptions
    {
        public double StepHeight { get; set; } = 50;
        public double JitterFactor { get; set; } = 0.0;
    }

    public interface IElevationProvider
    {
        Task<decimal> GetElevationAsync(Point pt, CancellationToken ct = default);
    }

    public sealed class MockElevationProvider : IElevationProvider
    {
        private sealed record Contour(Polygon Poly, int Level);

        private readonly STRtree<Contour> _tree = new();
        private readonly MockDemOptions _opts;
        private readonly ConcurrentDictionary<long, decimal> _cache = new();
        private readonly GeometryFactory _gf = new GeometryFactory();

        public MockElevationProvider(
            GeoProfilesContext db,
            IOptions<MockDemOptions> opts)
        {
            _opts = opts.Value;

            foreach (var iso in db.Isolines.AsNoTracking().Select(i => new { i.Level, Geom = (Polygon)i.Geom }))
            {
                iso.Geom.SRID = 4326;
                _tree.Insert(iso.Geom.EnvelopeInternal, new Contour(iso.Geom, iso.Level));
            }

            _tree.Build();
        }

        public Task<decimal> GetElevationAsync(Point pt, CancellationToken _ = default)
        {
            long hash = GeoHash(pt.Coordinate, precision: 5);
            if (_cache.TryGetValue(hash, out var cached))
                return Task.FromResult(cached);

            var cand = _tree.Query(pt.EnvelopeInternal);
            var containingContours = cand.Where(c => c.Poly.Contains(pt)).ToList();

            if (containingContours.Count == 0)
            {
                var elevation = FindNearestContourElevation(pt);
                _cache.TryAdd(hash, elevation);
                return Task.FromResult(elevation);
            }

            var inner = containingContours.OrderByDescending(c => c.Level).First();
            int Linner = inner.Level;

            var outer = containingContours
                .Where(c => c.Level < Linner)
                .OrderByDescending(c => c.Level)
                .FirstOrDefault();

            int Louter = outer?.Level ?? 0;

            var distanceOp = new DistanceOp(inner.Poly, pt);
            var closestPoints = distanceOp.NearestPoints();
            var boundaryPoint = _gf.CreatePoint(closestPoints[0]);
            
            double r = pt.Distance(boundaryPoint);
            double R = CalculateR(inner, boundaryPoint, outer, pt);

            if (R <= 0) R = 1;

            double t = Math.Clamp(r / R, 0, 1);
            double hInner = Linner * _opts.StepHeight;
            double hOuter = Louter * _opts.StepHeight;
            double elev = hOuter + (hInner - hOuter) * (1 - t * t);

            if (_opts.JitterFactor > 0)
            {
                elev += _opts.StepHeight * _opts.JitterFactor * (Random.Shared.NextDouble() - 0.5);
            }

            var result = (decimal)elev;
            _cache.TryAdd(hash, result);
            return Task.FromResult(result);
        }

        private decimal FindNearestContourElevation(Point pt)
        {
            var searchEnv = new Envelope(pt.Coordinate);
            searchEnv.ExpandBy(10.0);
            var candidates = _tree.Query(searchEnv).ToList();

            if (candidates.Count == 0) return 0m;

            var nearestContour = candidates
                .OrderBy(c => c.Poly.Distance(pt))
                .First();

            return (decimal)(nearestContour.Level * _opts.StepHeight);
        }

        private double CalculateR(Contour inner, Point boundaryPoint, Contour? outer, Point pt)
        {
            if (outer != null)
            {
                var directionVector = new Coordinate(
                    pt.X - boundaryPoint.X,
                    pt.Y - boundaryPoint.Y);
                
                var rayEnd = new Coordinate(
                    boundaryPoint.X + directionVector.X * 10,
                    boundaryPoint.Y + directionVector.Y * 10);
                
                var ray = new LineString([boundaryPoint.Coordinate, rayEnd]);
                var intersection = ray.Intersection(outer.Poly);

                return !intersection.IsEmpty
                    ? boundaryPoint.Distance(intersection.GetGeometryN(0))
                    : outer.Poly.Distance(boundaryPoint);
            }

            return inner.Poly.Distance(boundaryPoint);
        }

        private static long GeoHash(Coordinate coord, int precision)
        {
            double Round(double value) => Math.Round(value, precision);
            return ((long)(Round(coord.X) * 1e6) << 32) | (uint)(Round(coord.Y) * 1e6);
        }
    }
}