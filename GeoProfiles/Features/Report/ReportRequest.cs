
using FluentValidation;

namespace GeoProfiles.Features.Reports.Get;

public record ReportRequest(Guid projectId, Guid profileId);

internal sealed class ReportRequestValidator : AbstractValidator<ReportRequest>
{
    public ReportRequestValidator()
    {
        RuleFor(x => x.projectId).NotEmpty();
        RuleFor(x => x.profileId).NotEmpty();
    }
}
