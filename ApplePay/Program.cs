using ApplePay.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddHttpClient();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<CredimaxOptions>(
    builder.Configuration.GetSection(CredimaxOptions.SectionName));
// ✅ Controllers + JSON enum converter
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// ✅ CORS (allow all origins, methods, headers)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

var app = builder.Build();

// ✅ Swagger + Swagger UI always enabled
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "My API v1");
    options.RoutePrefix = string.Empty; // Swagger UI at root URL "/"
});

// ✅ Global exception handling
app.UseExceptionHandler(exceptionApp =>
{
    exceptionApp.Run(async context =>
    {
        context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
        context.Response.ContentType = "application/json";

        var exceptionHandlerPathFeature = context.Features.Get<IExceptionHandlerPathFeature>();

        var result = JsonSerializer.Serialize(new
        {
            error = exceptionHandlerPathFeature?.Error.Message ?? "An unexpected error occurred"
        });

        await context.Response.WriteAsync(result);
    });
});

app.UseHsts();
app.UseHttpsRedirection();

app.UseRouting();

// ✅ Enable CORS
app.UseCors("AllowAll");

app.MapControllers();

app.Run();
