using System;
using System.Collections.Generic;

namespace GeoProfiles.Model;

public partial class SystemLogs
{
    public string? RequestId { get; set; }

    public DateTime? RaiseDate { get; set; }

    public string? Message { get; set; }

    public string? Level { get; set; }

    public string? Exception { get; set; }
}
