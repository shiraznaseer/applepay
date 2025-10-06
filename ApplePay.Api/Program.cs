using Polly;

namespace ApplePay.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Bind options for Credimax
        builder.Services.Configure<ApplePay.Api.Options.CredimaxOptions>(
            builder.Configuration.GetSection(ApplePay.Api.Options.CredimaxOptions.SectionName));

        // Bind options for ZATCA
        builder.Services.Configure<ApplePay.Api.Options.ZatcaOptions>(
            builder.Configuration.GetSection(ApplePay.Api.Options.ZatcaOptions.SectionName));

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

        // ZATCA services
        builder.Services.AddSingleton<ApplePay.Api.Services.IZatcaService, ApplePay.Api.Services.ZatcaService>();
        builder.Services.AddSingleton<ApplePay.Api.Services.QrService>();
        builder.Services.AddSingleton<ApplePay.Api.Services.CertificateService>();
        builder.Services.AddSingleton<ApplePay.Api.Services.XmlSigningService>();

        // Authorization
        builder.Services.AddAuthorization();

        // Swagger
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        var app = builder.Build();

        // Swagger UI
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }
        else
        {
            app.UseHsts();
        }

        app.UseHttpsRedirection();
        app.UseAuthorization();

        // Map controller endpoints
        app.MapControllers();

        // Optional: keep minimal API if you want WeatherForecast
        /*
        var summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        app.MapGet("/weatherforecast", (HttpContext httpContext) =>
        {
            var forecast = Enumerable.Range(1, 5).Select(index =>
                new WeatherForecast
                {
                    Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    TemperatureC = Random.Shared.Next(-20, 55),
                    Summary = summaries[Random.Shared.Next(summaries.Length)]
                })
                .ToArray();
            return forecast;
        })
        .WithName("GetWeatherForecast")
        .WithOpenApi();
        */

        app.Run();
    }
}
