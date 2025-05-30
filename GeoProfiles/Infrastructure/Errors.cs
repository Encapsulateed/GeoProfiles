namespace GeoProfiles.Infrastructure;

public abstract class Errors
{
    public record UserAlreadyExists(string Message);

    public record UserUnauthorized(string Message);

    public record UserNotFound(string Message);

    public record ProjectNotFound(string Message);

    public record ResourceNotFound(string Message);
}