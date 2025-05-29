using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NetTopologySuite.Geometries;
using NetTopologySuite.Index.Strtree;

namespace GeoProfiles.Infrastructure.Services
{
    public class MockDemOptions
    {
        public double StepHeight { get; set; } = 50;
        public double JitterFactor { get; set; } = 0.1;
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
        private readonly ConcurrentDictionary<Coordinate, decimal> _cache = new();
        private readonly Guid _projectId;

        public MockElevationProvider(
            GeoProfilesContext db,
            IOptions<MockDemOptions> opts)
        {
            _opts = opts.Value;

            foreach (var iso in db.Isolines.AsNoTracking().Select(i => new {i.Level, Geom = (Polygon) i.Geom}))
            {
                iso.Geom.SRID = 4326;
                _tree.Insert(iso.Geom.EnvelopeInternal, new Contour(iso.Geom, iso.Level));
            }

            _tree.Build();
        }

        public Task<decimal> GetElevationAsync(Point pt, CancellationToken _ = default)
        {
            if (_cache.TryGetValue(pt.Coordinate, out var cached))
                return Task.FromResult(cached);

            var cand = _tree.Query(pt.EnvelopeInternal);

            var inner = cand
                .Where(c => c.Poly.Contains(pt)) 
                .OrderByDescending(c => c.Level)
                .FirstOrDefault();

            if (inner is null)
                return Task.FromResult(0m);

            var outer = cand
                .Where(c => c.Level < inner.Level && c.Poly.Covers(pt))
                .OrderByDescending(c => c.Level)
                .FirstOrDefault();

            int Linner = inner.Level;
            int Louter = outer?.Level ?? 0;

            var center = inner.Poly.Centroid;
            double r = pt.Distance(center);
            double R;
            if (outer is not null)
            {
                var ray = new LineString(new[] {center.Coordinate, pt.Coordinate}) {SRID = 4326};
                var inter = ray.Intersection(outer.Poly);
                R = inter.IsEmpty
                    ? outer.Poly.Distance(center)
                    : center.Distance(inter.GetGeometryN(0));
            }
            else
            {
                R = inner.Poly.Distance(center);
            }

            if (R <= 0)
                R = inner.Poly.Distance(center);

            double t = Math.Clamp(r / R, 0, 1);

            double hInner = Linner * _opts.StepHeight;
            double hOuter = Louter * _opts.StepHeight;
            double elev = hOuter + (hInner - hOuter) * (1 - t * t);

            if (_opts.JitterFactor > 0)
                elev += _opts.StepHeight
                        * _opts.JitterFactor
                        * (Random.Shared.NextDouble() - 0.5);

            var result = (decimal) elev;
            _cache.TryAdd(pt.Coordinate, result);
            return Task.FromResult(result);
        }
    }
}