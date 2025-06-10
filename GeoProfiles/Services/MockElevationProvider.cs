using System.Collections.Concurrent;
using GeoProfiles.Model;
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
        ValueTask<decimal> GetElevationAsync(Point pt, CancellationToken ct = default);
    }

    public sealed class ContourLineElevationProvider(GeoProfilesContext db)
        : IElevationProvider
    {
        private const double SearchRadiusDeg = 1e-3; // ≈ 110 м
        private const double OnLineTolDeg = 1e-5; // ≈ 1 м

        private readonly STRtree<ContourLines> _index = BuildIndex(db);

        public ValueTask<decimal> GetElevationAsync(Point p, CancellationToken ct = default)
        {
            // Берём все линии в небольшом радиусе.
            var env = p.EnvelopeInternal;
            env.ExpandBy(SearchRadiusDeg);
            var candidates = _index.Query(env);

            if (candidates.Count == 0)
                return new(decimal.Zero); // нет данных → 0 м (MVP)

            // Сначала ищем «точное» пересечение.
            foreach (var cl in candidates)
                if (cl.Geom!.IsWithinDistance(p, OnLineTolDeg))
                    return new((decimal)(cl.Level ?? 0));

            // Иначе линейно интерполируем между двух ближайших.
            var nearest = candidates
                .Select(cl => (
                    dist: cl.Geom!.Distance(p),
                    level: cl.Level ?? 0))
                .OrderBy(t => t.dist)
                .Take(2)
                .ToArray();

            if (nearest.Length == 1) // только одна линия
                return new((decimal)nearest[0].level);

            var (d0, h0) = nearest[0];
            var (d1, h1) = nearest[1];
            var h = h0 + (h1 - h0) * d0 / (d0 + d1);
            return new((decimal)h);
        }

        // ──────────────────────────────────────────────────────────────────────────
        private static STRtree<ContourLines> BuildIndex(GeoProfilesContext db)
        {
            var lines = db.ContourLines
                .AsNoTracking()
                .Where(cl => cl.Geom != null)
                .ToList();

            foreach (var l in lines)
                l.Geom!.SRID = 4326;

            var tree = new STRtree<ContourLines>();
            foreach (var l in lines)
                tree.Insert(l.Geom!.EnvelopeInternal, l);
            tree.Build();
            return tree;
        }
    }
}