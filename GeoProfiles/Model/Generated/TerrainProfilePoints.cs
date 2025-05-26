using System;
using System.Collections.Generic;

namespace GeoProfiles.Model;

public partial class TerrainProfilePoints
{
    public Guid ProfileId { get; set; }

    public int Seq { get; set; }

    public decimal DistM { get; set; }

    public decimal ElevM { get; set; }

    public virtual TerrainProfiles Profile { get; set; } = null!;
}
