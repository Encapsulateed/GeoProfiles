using GeoProfiles.Model.Dto;

namespace GeoProfiles.Model;

public static class UserMappingExtensions
{
    public static UserDto ToDto(this Model.Users entity)
    {
        return new UserDto
        {
            Id = entity.Id,
            Username = entity.Username,
            Email = entity.Email
        };
    }

    public static Model.Users ToEntity(this UserDto dto)
    {
        return new Model.Users
        {
            Id = dto.Id == Guid.Empty ? Guid.NewGuid() : dto.Id,
            Username = dto.Username,
            Email = dto.Email,
            PasswordHash = string.Empty, 
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
    }
}