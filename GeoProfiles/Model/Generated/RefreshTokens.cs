using System;
using System.Collections.Generic;

namespace GeoProfiles.Model;

public partial class RefreshTokens
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Token { get; set; } = null!;

    public DateTime ExpiresAt { get; set; }

    public bool IsRevoked { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual Users User { get; set; } = null!;
}
