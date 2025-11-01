using System.Text;
using IrcChat.Api.Data;
using IrcChat.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace IrcChat.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabaseServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");

        // Regular DbContext for standard usage
        services.AddDbContext<ChatDbContext>(options =>
            options.UseNpgsql(connectionString));

        // Separate factory with its own options for background service
        services.AddSingleton<IDbContextFactory<ChatDbContext>>(sp =>
        {
            var optionsBuilder = new DbContextOptionsBuilder<ChatDbContext>();
            optionsBuilder.UseNpgsql(connectionString);
            return new PooledDbContextFactory<ChatDbContext>(optionsBuilder.Options);
        });

        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<OAuthService>();

        // Configure ConnectionManager options
        services.Configure<ConnectionManagerOptions>(
                configuration.GetSection(ConnectionManagerOptions.SectionName))
            .Configure<AutoMuteOptions>(
                configuration.GetSection(AutoMuteOptions.SectionName));

        services.AddScoped<OAuthService>()
            .AddSignalR();

        services.AddHostedService<ConnectionManagerService>()
            .AddHostedService<AutoMuteService>();

        return services;
    }

    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtKey = configuration["Jwt:Key"] ?? "VotreCleSecrete123456789012345678901234567890";
        var jwtIssuer = configuration["Jwt:Issuer"] ?? "IrcChatApi";
        var jwtAudience = configuration["Jwt:Audience"] ?? "IrcChatClient";

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtIssuer,
                    ValidAudience = jwtAudience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
                };

                // Support pour SignalR avec JWT
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;

                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chathub"))
                        {
                            context.Token = accessToken;
                        }
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorization();

        return services;
    }

    public static IServiceCollection AddCorsConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["https://localhost:7001", "http://localhost:5001"];

        services.AddCors(options => options.AddPolicy("AllowBlazor", policy => policy.WithOrigins(allowedOrigins)
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials()));

        return services;
    }

    public static IServiceCollection AddSwaggerConfiguration(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "IRC Chat API",
                Version = "v1",
                Description = "API REST pour application de chat IRC avec SignalR"
            });

            // Configuration pour l'authentification JWT dans Swagger
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Description = "JWT Authorization header utilisant le schéma Bearer. Exemple: \"Bearer {token}\"",
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.ApiKey,
                Scheme = "Bearer"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return services;
    }
}