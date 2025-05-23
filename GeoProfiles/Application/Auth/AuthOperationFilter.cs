using System.Reflection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace GeoProfiles.Application.Auth;

public class AuthOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var attributes = GetAttributes<AuthorizeAttribute>(context.MethodInfo, true);

        var isAuthorized = attributes.Any();

        if (isAuthorized is false)
        {
            return;
        }

        AddSecurity(operation, JwtBearerDefaults.AuthenticationScheme);
    }

    private T[] GetAttributes<T>(MethodInfo methodInfo, bool inherit)
        where T : Attribute
    {
        var actionAttributes = methodInfo.GetCustomAttributes(inherit);
        var controllerAttributes = methodInfo.DeclaringType!.GetTypeInfo().GetCustomAttributes(inherit);
        var actionAndControllerAttributes = actionAttributes.Union(controllerAttributes);

        return actionAndControllerAttributes.Where(attr => attr is T).Cast<T>().ToArray();
    }

    private void AddSecurity(OpenApiOperation operation, string id)
    {
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference {Id = id, Type = ReferenceType.SecurityScheme,},
                },
                Array.Empty<string>()
            },
        });
    }
}