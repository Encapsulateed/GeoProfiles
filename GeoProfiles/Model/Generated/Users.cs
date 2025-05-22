using System;
using System.Collections.Generic;

namespace GeoProfiles.Model;

public partial class Users
{
    public Guid Id { get; set; }

    public string Username { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<RefreshTokens> RefreshTokens { get; set; } = new List<RefreshTokens>();
}
