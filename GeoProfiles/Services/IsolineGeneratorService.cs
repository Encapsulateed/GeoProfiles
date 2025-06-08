using NetTopologySuite.Geometries;
using NetTopologySuite.Geometries.Implementation;
using NetTopologySuite.Operation.Buffer;
using NetTopologySuite.Operation.Union;

namespace GeoProfiles.Services
{
    public sealed class IsolineGeneratorService : IIsolineGeneratorService
    {
        private static readonly GeometryFactory Gf =
            new(new PrecisionModel(PrecisionModels.Floating), 4326, CoordinateArraySequenceFactory.Instance);

        private const double Tau = Math.PI * 2;

        public Task<IEnumerable<Polygon>> GenerateAsync(
            BoundingBox bbox, int levels = 12, Guid? seed = null)
        {
            if (levels < 2) throw new ArgumentOutOfRangeException(nameof(levels));

            var rnd = seed.HasValue ? new Random(seed.Value.GetHashCode()) : Random.Shared;

            var baseContour = CreateStableTerrain(bbox, rnd);
            if (baseContour == null || baseContour.IsEmpty)
                return Task.FromResult(Enumerable.Empty<Polygon>());

            var result = new List<Polygon>();
            double minDim = Math.Min(bbox.Width, bbox.Height);

            double totalBuffer = minDim * 0.25;
            double stepSize = totalBuffer / levels;

            var innerBbox = new BoundingBox(
                bbox.MinX + minDim * 0.05,
                bbox.MinY + minDim * 0.05,
                bbox.MaxX - minDim * 0.05,
                bbox.MaxY - minDim * 0.05
            );
            var clipPolygon = BboxPolygon(innerBbox);

            for (int li = 0; li < levels; li++)
            {
                double bufferDistance = -(stepSize * (li + 1));
                var iso = BufferWithParams(baseContour, bufferDistance);

                if (iso == null || iso.IsEmpty) continue;

                iso = SafeIntersection(iso, clipPolygon);
                if (iso == null || iso.IsEmpty) continue;

                iso = SmoothContour(iso, 3);

                foreach (var p in Flatten(iso))
                {
                    if (p != null && p.IsValid && !p.IsEmpty)
                    {
                        double areaThreshold = minDim * minDim * 0.001;
                        if (p.Area > areaThreshold)
                        {
                            result.Add(p);
                        }
                    }
                }
            }

            return Task.FromResult<IEnumerable<Polygon>>(result);
        }

        private Geometry CreateStableTerrain(BoundingBox bbox, Random rnd)
        {
            var features = new List<Geometry>();

            var mountain = CreateMountain(bbox, rnd);
            features.Add(mountain);

            features.Add(CreateDepression(mountain, rnd));

            features.AddRange(CreatePeaks(bbox, mountain, rnd, 7)); 

            features.AddRange(CreateHills(bbox, rnd, 5));

            return SafeUnion(features);
        }


        private Polygon CreateMountain(BoundingBox bbox, Random rnd)
        {
            double minDim = Math.Min(bbox.Width, bbox.Height);
            return CreateEllipse(
                bbox.CenterX,
                bbox.CenterY,
                minDim * 0.35,
                minDim * 0.25,
                64, 
                rnd
            );
        }

        private Geometry CreateDepression(Geometry mountain, Random rnd)
        {
            var centroid = mountain.Centroid;
            double minDim = Math.Min(
                mountain.EnvelopeInternal.Width,
                mountain.EnvelopeInternal.Height
            );
            
            var depression = CreateEllipse(
                centroid.X + (rnd.NextDouble() - 0.5) * minDim * 0.1,
                centroid.Y + (rnd.NextDouble() - 0.5) * minDim * 0.1,
                minDim * 0.12,
                minDim * 0.08,
                48,
                rnd
            );
            
            return depression.Buffer(-minDim * 0.05);
        }

 
        private IEnumerable<Geometry> CreatePeaks(BoundingBox bbox, Geometry mountain, Random rnd, int count)
        {
            double minDim = Math.Min(bbox.Width, bbox.Height);
            var peaks = new List<Geometry>();
            var centroid = mountain.Centroid;

            for (int i = 0; i < count; i++)
            {
                double angle = rnd.NextDouble() * Tau;
                double distance = rnd.NextDouble() * minDim * 0.2; 
                double x = centroid.X + Math.Cos(angle) * distance;
                double y = centroid.Y + Math.Sin(angle) * distance;

                if (mountain.Contains(Gf.CreatePoint(new Coordinate(x, y))))
                {
                    double size = minDim * (0.06 + rnd.NextDouble() * 0.10); 
                    peaks.Add(CreateEllipse(x, y, size, size * 0.7, 36, rnd)); 
                }
            }

            return peaks;
        }


        private IEnumerable<Geometry> CreateHills(BoundingBox bbox, Random rnd, int count)
        {
            double minDim = Math.Min(bbox.Width, bbox.Height);
            var hills = new List<Geometry>();

            for (int i = 0; i < count; i++)
            {
                double angle = rnd.NextDouble() * Tau;
                double distance = minDim * (0.3 + rnd.NextDouble() * 0.25); 
                double x = bbox.CenterX + Math.Cos(angle) * distance;
                double y = bbox.CenterY + Math.Sin(angle) * distance;
                double size = minDim * (0.12 + rnd.NextDouble() * 0.18);

                double ratio = 0.6 + rnd.NextDouble() * 0.3;
                hills.Add(CreateEllipse(x, y, size, size * ratio, 48, rnd));
            }

            return hills;
        }
        
        private Polygon CreateEllipse(double x, double y, double a, double b, int points, Random rnd)
        {
            if (points < 36) points = 36;

            var coords = new Coordinate[points + 1];
            double angleStep = Tau / points;
            double noiseIntensity = 0.18;

            double SmoothNoise(double seed, double angle, double frequency)
            {
                return Math.Cos(seed + angle * frequency) * noiseIntensity;
            }

            double seed1 = rnd.NextDouble() * 100;
            double seed2 = rnd.NextDouble() * 100;

            for (int i = 0; i < points; i++)
            {
                double angle = i * angleStep;

                double noiseFactor = 1.0 +
                    SmoothNoise(seed1, angle, 3.0) +
                    SmoothNoise(seed2, angle, 8.0) * 0.5;

                double effectiveA = a * noiseFactor * (0.92 + rnd.NextDouble() * 0.16);
                double effectiveB = b * noiseFactor * (0.88 + rnd.NextDouble() * 0.20);

                double offsetX = (rnd.NextDouble() - 0.5) * a * 0.06;
                double offsetY = (rnd.NextDouble() - 0.5) * b * 0.06;

                coords[i] = new Coordinate(
                    x + offsetX + effectiveA * Math.Cos(angle),
                    y + offsetY + effectiveB * Math.Sin(angle)
                );
            }

            coords[points] = coords[0];

            try
            {
                return Gf.CreatePolygon(coords);
            }
            catch
            {
                return (Polygon) Gf.CreatePoint(new Coordinate(x, y)).Buffer(a);
            }
        }

        private Geometry SafeUnion(IEnumerable<Geometry> geometries)
        {
            var cleaned = geometries
                .Where(g => g != null && !g.IsEmpty)
                .Select(g => g.Buffer(0.001))
                .ToList();

            if (!cleaned.Any())
                return Gf.CreateGeometryCollection();

            return UnaryUnionOp.Union(cleaned);
        }

        private Geometry BufferWithParams(Geometry geom, double distance)
        {
            var parameters = new BufferParameters
            {
                EndCapStyle = EndCapStyle.Round,
                JoinStyle = JoinStyle.Round,
                MitreLimit = 5.0,
                QuadrantSegments = 36,
                IsSingleSided = false
            };

            return BufferOp.Buffer(geom, distance, parameters);
        }

        private Geometry SafeIntersection(Geometry a, Geometry b)
        {
            try
            {
                a = a.Buffer(0.001);
                b = b.Buffer(0.001);
                return a.Intersection(b);
            }
            catch
            {
                return null;
            }
        }


        private Geometry SmoothContour(Geometry geom, int iterations)
        {
            if (geom is Polygon poly)
                return SmoothPolygon(poly, iterations);

            if (geom is MultiPolygon mp)
            {
                var polygons = new List<Polygon>();
                for (int i = 0; i < mp.NumGeometries; i++)
                {
                    if (mp.GetGeometryN(i) is Polygon p)
                    {
                        var smoothed = SmoothPolygon(p, iterations);
                        if (smoothed != null) polygons.Add(smoothed);
                    }
                }

                return Gf.CreateMultiPolygon(polygons.ToArray());
            }

            return geom;
        }

        private Polygon SmoothPolygon(Polygon poly, int iterations)
        {
            try
            {
                var exterior = SmoothRing(poly.ExteriorRing, iterations);
                if (exterior == null) return null;

                var holes = new List<LinearRing>();
                for (int i = 0; i < poly.NumInteriorRings; i++)
                {
                    var hole = SmoothRing(poly.GetInteriorRingN(i), iterations);
                    if (hole != null) holes.Add(hole);
                }

                return Gf.CreatePolygon(exterior, holes.ToArray());
            }
            catch
            {
                return null;
            }
        }

        private LinearRing SmoothRing(LineString ring, int iterations)
        {
            var coords = ring.Coordinates;
            if (coords.Length < 4)
                return (LinearRing) ring;

            var currentCoords = coords;

            for (int it = 0; it < iterations; it++)
            {
                var newCoords = new List<Coordinate>();

                for (int i = 0; i < currentCoords.Length - 1; i++)
                {
                    var p0 = currentCoords[i];
                    var p1 = currentCoords[i + 1];

                    newCoords.Add(new Coordinate(
                        0.85 * p0.X + 0.15 * p1.X,
                        0.85 * p0.Y + 0.15 * p1.Y
                    ));
                    
                    newCoords.Add(new Coordinate(
                        0.50 * p0.X + 0.50 * p1.X,
                        0.50 * p0.Y + 0.50 * p1.Y
                    ));
                    
                    newCoords.Add(new Coordinate(
                        0.15 * p0.X + 0.85 * p1.X,
                        0.15 * p0.Y + 0.85 * p1.Y
                    ));
                }

                newCoords.Add(newCoords[0]);
                currentCoords = newCoords.ToArray();
            }

            return Gf.CreateLinearRing(currentCoords);
        }

        private static Polygon BboxPolygon(BoundingBox b) =>
            Gf.CreatePolygon([
                new Coordinate(b.MinX, b.MinY),
                new Coordinate(b.MaxX, b.MinY),
                new Coordinate(b.MaxX, b.MaxY),
                new Coordinate(b.MinX, b.MaxY),
                new Coordinate(b.MinX, b.MinY)
            ]);

        private static IEnumerable<Polygon> Flatten(Geometry g)
        {
            if (g is Polygon p)
                yield return p;
            else if (g is MultiPolygon mp)
                for (int i = 0; i < mp.NumGeometries; i++)
                    if (mp.GetGeometryN(i) is Polygon pi)
                        yield return pi;
        }
    }

    public readonly record struct BoundingBox(
        double MinX,
        double MinY,
        double MaxX,
        double MaxY)
    {
        public double Width => MaxX - MinX;
        public double Height => MaxY - MinY;
        public double CenterX => (MinX + MaxX) / 2;
        public double CenterY => (MinY + MaxY) / 2;
    }

    public interface IIsolineGeneratorService
    {
        Task<IEnumerable<Polygon>> GenerateAsync(
            BoundingBox bbox, int levels = 12, Guid? seed = null);
    }
}