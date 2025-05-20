using GeoProfiles;
using GeoProfiles.Infrastructure.Extensions;
using GeoProfiles.Infrastructure.Middlewares;
using Microsoft.EntityFrameworkCore;
using static Microsoft.AspNetCore.Builder.WebApplication;

var builder = CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();
builder.Services.AddHealthChecks();

// Add Db
builder.Services.AddDbContext<GeoProfilesContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);
// Add logging
builder.AddSerilogLogging();

// Swagger 
builder.Services.AddSwaggerDocumentation();

var app = builder.Build();

app.UseSwaggerDocumentation();

// Add middlewares
app.UseMiddleware<RequestIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();
app.MapControllers();
app.MapHealthChecks("/healthz");

app.Run();