using Swashbuckle.AspNetCore.Filters;

namespace GeoProfiles.Features.JWT;

public record TokenDto
{
    public string Token { get; set; } = null!;
    public string TokenType { get; set; } = null!;
    public int ExpiresIn { get; set; }
}

public record TokenDtoExample : IExamplesProvider<TokenDto>
{
    public TokenDto GetExamples()
    {
        return new TokenDto
        {
            Token = "eyJ...example-token...XYZ",
            TokenType = "Bearer",
            ExpiresIn = 3600
        };
    }
}