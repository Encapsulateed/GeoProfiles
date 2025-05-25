using Swashbuckle.AspNetCore.Filters;

namespace GeoProfiles.Model.Dto;

public record TokenDto
{
    public string Token { get; set; } = null!;
    public string TokenType { get; set; } = null!;

    public string RefreshToken { get; set; } = null!;
    public int ExpiresIn { get; set; }
}

public record TokenDtoExample : IExamplesProvider<TokenDto>
{
    public TokenDto GetExamples()
    {
        return new TokenDto
        {
            Token = "eyJhbG\u0063iOiJSUzI1NiIsInR5cCI6IkpXVCJ9...",
            RefreshToken = "d9\u003428888-122b-11e1-b85c-61cd3cbb3210",
            TokenType = "Bearer",
            ExpiresIn = 3600
        };
    }
}