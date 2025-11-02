using ApplePay.Models;
using ApplePay.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Polly;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Net.Http.Headers;

var builder = WebApplication.CreateBuilder(args);


// Bind options for ZATCA
builder.Services.Configure<ApplePay.Api.Options.ZatcaOptions>(
    builder.Configuration.GetSection(ApplePay.Api.Options.ZatcaOptions.SectionName));

// Bind options for Tabby
builder.Services.Configure<TabbyOptions>(
    builder.Configuration.GetSection(TabbyOptions.SectionName));

// Register controllers
builder.Services.AddControllers();

// IHttpClientFactory support
builder.Services.AddHttpClient();

// ZATCA typed HttpClient
builder.Services.AddHttpClient<ApplePay.Api.Services.ZatcaClient>((sp, client) =>
{
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ApplePay.Api.Options.ZatcaOptions>>().Value;
    var baseUrl = string.IsNullOrWhiteSpace(opts.BaseUrl) ? "https://gw-fatoora.zatca.gov.sa" : opts.BaseUrl!.TrimEnd('/');
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(Math.Max(10, Math.Min(300, opts.HttpTimeoutSeconds)));
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Add("Accept", "application/json");
})
.AddPolicyHandler(Polly.Policy.TimeoutAsync<HttpResponseMessage>(TimeSpan.FromSeconds(60)))
.AddTransientHttpErrorPolicy(builder => Polly.Extensions.Http.HttpPolicyExtensions
    .HandleTransientHttpError()
    .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Min(2 * retryAttempt, 10))));

// Tabby typed HttpClient
builder.Services.AddHttpClient<TabbyService>((sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<TabbyOptions>>().Value;
    var baseUrl = string.IsNullOrWhiteSpace(opts.BaseUrl) ? "https://api.tabby.ai" : opts.BaseUrl!.TrimEnd('/');
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(60);
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", opts.SecretKey);
});

// ZATCA services
builder.Services.AddSingleton<ApplePay.Api.Services.IZatcaService, ApplePay.Api.Services.ZatcaService>();
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
