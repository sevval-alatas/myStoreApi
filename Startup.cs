using System;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using MyStore.Api.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using MyStore.Api.Authorization.Requirements;
using MyStore.Api.Authorization.Handlers;
using MyStore.Api.Services;

namespace MyStore.Api
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers()
                .AddJsonOptions(options =>
                {
                    // Market -> UserMarkets -> User -> UserMarkets -> Market ... gibi dongusel
                    // navigation'lar var. DTO kullanilmayan bir yer kacarsa serialization
                    // sonsuz donguye girmesin diye ek bir güvenlik agi.
                    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
                });

            // ---------------- DB ----------------
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(Configuration.GetConnectionString("DefaultConnection")));

            // ---------------- SWAGGER ----------------
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "MyStore API",
                    Version = "v1"
                });

                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.ApiKey,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Bearer {token}"
                });

                c.AddSecurityRequirement(new OpenApiSecurityRequirement
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
                        new string[] {}
                    }
                });
            });

            // ---------------- JWT ----------------
            var jwtSettings = Configuration.GetSection("JwtSettings");
            var secretKey = jwtSettings["SecretKey"];
            if (string.IsNullOrWhiteSpace(secretKey))
            {
                throw new InvalidOperationException(
                    "JwtSettings:SecretKey ayarlanmamış. Development'ta 'dotnet user-secrets set \"JwtSettings:SecretKey\" \"<deger>\"' ile, " +
                    "production'da ise JwtSettings__SecretKey ortam değişkeniyle ayarlayın. Değeri appsettings.json'a yazmayın.");
            }
            var key = Encoding.ASCII.GetBytes(secretKey);

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = jwtSettings["Issuer"],
                        ValidAudience = jwtSettings["Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(key)
                    };
                });

            // ---------------- AUTHORIZATION CORE ----------------
            services.AddAuthorization(options =>
            {
                options.AddPolicy("OwnerOnly", policy =>
                    policy.Requirements.Add(new OwnerOnlyRequirement()));

                options.AddPolicy("MemberOrOwner", policy =>
                    policy.Requirements.Add(new MemberOrOwnerRequirement()));

                options.AddPolicy("ViewerOrAbove", policy =>
                    policy.Requirements.Add(new ViewerOrAboveRequirement()));
            });

            // ---------------- SERVICES ----------------
            services.AddScoped<IMarketAuthorizationService, MarketAuthorizationService>();

            // ---------------- HANDLERS ----------------
            services.AddScoped<IAuthorizationHandler, OwnerOnlyHandler>();
            services.AddScoped<IAuthorizationHandler, MemberOrOwnerHandler>();
            services.AddScoped<IAuthorizationHandler, ViewerOrAboveHandler>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "MyStore API v1");
                });
            }

            if (!env.IsDevelopment())
            {
                app.UseHttpsRedirection();
            }
            app.UseRouting();

            app.UseAuthentication(); // 🔑
            app.UseAuthorization();  // 🔒

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
