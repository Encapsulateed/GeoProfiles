using Microsoft.AspNetCore.Mvc;

namespace GeoProfiles.Features.Profiles.Get;

public class ProfileGetRequest
{
    [FromRoute(Name = "projectId")]
    public Guid ProjectId { get; set; }

    [FromRoute(Name = "profileId")]
    public Guid ProfileId { get; set; }
}