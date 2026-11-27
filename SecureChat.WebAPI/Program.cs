using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using SecureChat.WebAPI.Hubs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SecureChat.Infrastructure;
using System;

var apiServerBuilder = WebApplication.CreateBuilder(args);

// 1. Controller und SignalR registrieren
apiServerBuilder.Services.AddControllers();
apiServerBuilder.Services.AddSignalR();

// 2. SQL Server Verbindung konfigurieren
apiServerBuilder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(apiServerBuilder.Configuration.GetConnectionString("DefaultConnection")));

// 3. Identity-System initialisieren (Der flache Ansatz umgeht jeden Überladungsfehler!)
var identityBuilder = apiServerBuilder.Services.AddIdentityCore<IdentityUser>();

// 4. Stores und Rollen an den flachen Identity-Builder anfügen
identityBuilder.AddRoles<IdentityRole>();
identityBuilder.AddEntityFrameworkStores<ApplicationDbContext>();

// 1. JWT-Bearer-Authentifizierung konfigurieren
var secretKey = apiServerBuilder.Configuration["JwtSettings:Secret"] ?? "PLATZHALTER_FUER_DEN_GEHEIMEN_SCHLUESSEL_MIN_32_ZEICHEN!";
var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(secretKey));

apiServerBuilder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = key,
        ValidateIssuer = false,
        ValidateAudience = false,
        ClockSkew = TimeSpan.Zero
    };

    //  SECURITY-TRICK FÜR SIGNALR: Token aus der URL fischen
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

var app = apiServerBuilder.Build(); // Fehler korrigiert: Build() statt Builder()
// Korrektur: var app = apiServerBuilder.Build();

app.UseHttpsRedirection();

//  REIHENFOLGE UNBEDINGT PRÜFEN:
app.UseAuthentication(); // 1. Wer bist du? (Liest das Token für SignalR)
app.UseAuthorization();  // 2. Was darfst du?

app.MapControllers();
app.MapHub<ChatHub>("/chathub");

app.Run();
