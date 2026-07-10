using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Npgsql;
using System.Data.Common;
using System.Text;
using User_Service.Migration;
using User_Service.Models;
using User_Service.Services;

namespace User_Service
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Env.Load();
            var conStr = Environment.GetEnvironmentVariable("CONNECTION_STRING")    ?? throw new ArgumentNullException("CONNECTION_STRING not found");
            var origin = Environment.GetEnvironmentVariable("ORIGIN")               ?? throw new ArgumentNullException("ORIGIN not found");
            var isReflectionEnabled = Environment.GetEnvironmentVariable("GRPC_REFLECTION_ENABLED") ?? throw new ArgumentNullException("REFLECTION_ENABLED not found");
            var secret = Environment.GetEnvironmentVariable("JWT_SECRET")           ?? throw new ArgumentNullException("JWT_SECRET not found"); 
            var sqlSecretPassword = Environment.GetEnvironmentVariable("SQL_SECRET_PASSWORD") ?? throw new ArgumentNullException("SQL_SECRET_PASSWORD not found");

            DbCreate.CreateDatabase(conStr,sqlSecretPassword);


            var builder = WebApplication.CreateBuilder(args);
            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(
                    builder => builder
                        .WithOrigins(origin ?? "http://localhost:5173")
                        .AllowAnyMethod()
                        .AllowAnyHeader().WithExposedHeaders("Grpc-Status", "Grpc-Message", "Grpc-Encoding", "Grpc-Accept-Encoding")
                        .AllowCredentials()
                        );
            });

            builder.Services.AddAuthorization();
            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options => 
            {
                options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                {
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))
                };
            });

            builder.Services.AddTransient<DbConnection>(sp => new NpgsqlConnection(conStr));

            builder.Services.AddTransient<List<UserPresence>>(sp => new List<UserPresence>());

            builder.Services.AddHttpClient();
            if (isReflectionEnabled != null && isReflectionEnabled == "true")
            {
                builder.Services.AddGrpcReflection();
            }

            // Add services to the container.
            builder.Services.AddGrpc();



            var app = builder.Build();

            app.UseRouting();
            app.UseCors();

            app.UseAuthentication();
            app.UseAuthorization();
            

            app.UseGrpcWeb();

            app.MapGrpcService<UserGrpcService>().EnableGrpcWeb();
            

            app.Run();
        }
    }
}