using System;
using System.Collections.Generic;
using NetTopologySuite.Geometries;

namespace GeoProfiles.Model;

public partial class ContourLines
{
    public int Fid { get; set; }

    public LineString? Geom { get; set; }

    public int? Id { get; set; }

    public double? Level { get; set; }
}
