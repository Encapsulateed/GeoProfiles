using GeoProfiles.Infrastructure.Extensions;
using GeoProfiles.Infrastructure.Middlewares;
using static Microsoft.AspNetCore.Builder.WebApplication;

var builder = CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddControllers();

// Add logging
builder.AddSerilogLogging();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Add middlewares
app.UseMiddleware<RequestIdMiddleware>();
app.UseMiddleware<ValidationExceptionMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseHttpsRedirection();
app.MapControllers();

app.Run();