using GeoProfiles;
using GeoProfiles.Infrastructure.Extensions;
using GeoProfiles.Infrastructure.Middlewares;
using Microsoft.EntityFrameworkCore;
using GeoProfiles.Infrastructure.Modules;
using static Microsoft.AspNetCore.Builder.WebApplication;

var builder = CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();
builder.Services.AddHealthChecks();

builder.Services.AddMockElevationProvider(builder.Configuration);
builder.Services.RegisterProfiles();

// Add Db
builder.Services.AddDbContext<GeoProfilesContext>(options =>
    {
        options.UseNpgsql(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            o => o.UseNetTopologySuite()
        );
    }
);


// Add logging
builder.AddSerilogLogging();

// Swagger 
builder.Services.AddSwaggerDocumentation();

builder.Services.AddJwtAuthentication(builder.Configuration);

builder.Services.RegisterIsoline();

builder.Services.AddCors(options =>
{
    options.AddPolicy("ApiCors",
        policy =>
        {
            policy
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

var app = builder.Build();

app.UseCors("ApiCors");

// Add authentication
app.UseJwtAuthentication();

app.UseSwaggerDocumentation();

// Add middlewares
app.UseMiddleware<RequestIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();
app.MapControllers();
app.MapHealthChecks("/healthz");

app.Run();