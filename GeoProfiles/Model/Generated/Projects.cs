using System;
using System.Collections.Generic;
using NetTopologySuite.Geometries;

namespace GeoProfiles.Model;

public partial class Projects
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Name { get; set; } = null!;

    public Polygon Bbox { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<Isolines> Isolines { get; set; } = new List<Isolines>();

    public virtual Users User { get; set; } = null!;
}
