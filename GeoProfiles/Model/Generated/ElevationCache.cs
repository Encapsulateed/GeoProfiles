using System;
using System.Collections.Generic;
using NetTopologySuite.Geometries;

namespace GeoProfiles.Model;

public partial class ElevationCache
{
    public Point Pt { get; set; } = null!;

    public decimal ElevM { get; set; }

    public DateTime UpdatedAt { get; set; }
}
