using System;
using System.Collections.Generic;
using NetTopologySuite.Geometries;

namespace GeoProfiles.Model;

public partial class TerrainProfiles
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public Point StartPt { get; set; } = null!;

    public Point EndPt { get; set; } = null!;

    public decimal LengthM { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Projects Project { get; set; } = null!;
}
