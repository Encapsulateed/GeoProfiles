using System;
using System.Collections.Generic;
using NetTopologySuite.Geometries;

namespace GeoProfiles.Model;

public partial class Isolines
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public int Level { get; set; }

    public Polygon Geom { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public int? Direction { get; set; }

    public virtual Projects Project { get; set; } = null!;
}
