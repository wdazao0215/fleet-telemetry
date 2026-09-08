using System.Text;
using System.Text.Json.Serialization;
using FleetTelemetry.Application.Abstractions.Messaging;
using FleetTelemetry.Application.Alerts.GetRecentAlerts;
using FleetTelemetry.Application.Fleet.GetFleetSnapshot;
using FleetTelemetry.Application.Fleet.GetVehicleTrack;
using FleetTelemetry.Infrastructure;
using FleetTelemetry.Query.Api.Consumers;
using FleetTelemetry.Query.Api.Endpoints;
using FleetTelemetry.Query.Api.Hubs;
using FleetTelemetry.Query.Api.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .WriteTo.Console());

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSingleton<TokenIssuer>();

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.FromMinutes(1),
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                // El WebSocket del navegador no permite enviar cabeceras propias, así que SignalR
                // manda el token por query string. Se acepta solo en la ruta del hub para no abrir
                // esa vía —donde el token acabaría en los logs de acceso— en el resto de la API.
                var accessToken = context.Request.Query["access_token"];

                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs/telemetry"))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
        };
    });

builder.Services.AddAuthorization();

builder.Services
    .AddTelemetryCore(builder.Configuration)
    .AddFleetPersistence(builder.Configuration)
    .AddRedisCache(builder.Configuration)
    .AddResilientEventBus(builder.Configuration);

builder.Services.AddScoped<IQueryHandler<GetFleetSnapshotQuery, IReadOnlyList<VehicleSnapshot>>, GetFleetSnapshotHandler>();
builder.Services.AddScoped<IQueryHandler<GetVehicleTrackQuery, VehicleTrack>, GetVehicleTrackHandler>();
builder.Services.AddScoped<IQueryHandler<GetRecentAlertsQuery, IReadOnlyList<AlertDto>>, GetRecentAlertsHandler>();

// Los enums viajan como texto ("Moving", "Stopped") y no como enteros: un 2 en el JSON obliga al
// frontend a mantener su propia tabla de equivalencias, que se desincroniza en cuanto se añade un
// estado. El mismo convertidor se aplica a SignalR para que ambos canales hablen igual.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services
    .AddSignalR()
    .AddJsonProtocol(options =>
        options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddHostedService<VehicleStateBroadcastConsumer>();
builder.Services.AddHostedService<AlertBroadcastConsumer>();

// El dashboard corre en otro origen. AllowCredentials es obligatorio para SignalR, y eso impide
// usar '*': hay que nombrar los orígenes permitidos explícitamente.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:3000"];

builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapOpenApi();
app.MapHealthChecks("/health");
app.MapAuthEndpoints();
app.MapFleetEndpoints();
app.MapHub<TelemetryHub>("/hubs/telemetry");

await app.RunAsync().ConfigureAwait(false);

public partial class Program;
