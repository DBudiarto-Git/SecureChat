using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SecureChat.Core;
using SecureChat.Infrastructure;
using SecureChat.WebAPI.DTOs;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SecureChat.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<IdentityUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _config;

    public AuthController(UserManager<IdentityUser> userManager, ApplicationDbContext context, IConfiguration config)
    {
        _userManager = userManager;
        _context = context;
        _config = config;
    }

    // 1. REGISTRIERUNG
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto dto)
    {
        // SECURITY: Schnelle Vorab-Prüfung, ob die E-Mail schon existiert
        var existingUser = await _userManager.FindByEmailAsync(dto.Email);
        if (existingUser != null)
            return BadRequest(new { Message = "Diese E-Mail-Adresse wird bereits verwendet." });

        // 1. Identity-User anlegen (Identity verwaltet diese Transaktion selbst!)
        var user = new IdentityUser { UserName = dto.Email, Email = dto.Email };
        var result = await _userManager.CreateAsync(user, dto.Password);

        if (!result.Succeeded)
            return BadRequest(result.Errors); // Gibt Passwort-Richtlinien-Fehler direkt an WPF zurück

        // 2. Rolle zuweisen (Läuft jetzt ohne Blockade durch)
        var roleResult = await _userManager.AddToRoleAsync(user, "User");
        if (!roleResult.Succeeded)
            return BadRequest(roleResult.Errors);

        // 3. Dein eigenes Benutzerprofil in der UserProfiles-Tabelle anlegen
        var profile = new UserProfile
        {
            Id = user.Id,
            DisplayName = dto.DisplayName,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.UserProfiles.Add(profile);
        await _context.SaveChangesAsync(); // Schreibt das Profil sicher in den SQL Server

        return Ok(new { Message = "Benutzer und Profil erfolgreich registriert!" });
    }


    // 2. LOGIN (Mit JWT-Generierung inkl. Rollen)
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user == null) return Unauthorized("Ungültige E-Mail oder Passwort.");

        // SECURITY: Brute-Force Check (Ist das Konto wegen zu vielen Fehlversuchen gesperrt?)
        if (await _userManager.IsLockedOutAsync(user))
            return StatusCode(423, "Konto ist temporär gesperrt. Bitte versuchen Sie es später erneut.");

        // SECURITY: Prüfen, ob das Profil vom Admin deaktiviert wurde
        var profile = await _context.UserProfiles.FindAsync(user.Id);
        if (profile == null || !profile.IsActive)
            return Unauthorized("Dieses Konto wurde deaktiviert.");

        // Passwort prüfen
        var isPasswordValid = await _userManager.CheckPasswordAsync(user, dto.Password);
        if (!isPasswordValid)
        {
            // Fehlversuch registrieren für Lockout-Zähler
            await _userManager.AccessFailedAsync(user);
            return Unauthorized("Ungültige E-Mail oder Passwort.");
        }

        // Login-Zähler bei Erfolg zurücksetzen
        await _userManager.ResetAccessFailedCountAsync(user);

        // Rollen aus der DB abrufen
        var roles = await _userManager.GetRolesAsync(user);

        // JWT Token mit Claims generieren
        string token = GenerateJwtToken(user, roles, profile.DisplayName);

        return Ok(new { AccessToken = token });
    }

    // 3. PASSWORT ÄNDERN (Eingeloggter Zustand)
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user == null) return BadRequest("Benutzer nicht gefunden.");

        var result = await _userManager.ChangePasswordAsync(user, dto.OldPassword, dto.NewPassword);
        if (!result.Succeeded) return BadRequest(result.Errors);

        return Ok(new { Message = "Passwort erfolgreich geändert!" });
    }

    // 4. PASSWORT VERGESSEN (Pseudocode / Logikvorbereitung)
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user == null)
        {
            // SECURITY: Gegen "User Enumeration" (Benutzer-Erkennung). 
            // Wir sagen immer "Ok", damit ein Angreifer nicht herausfinden kann, welche E-Mails existieren!
            return Ok(new { Message = "Falls die E-Mail existiert, wurde ein Reset-Token generiert." });
        }

        // Generiert ein sicheres Einmal-Token für den Reset
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);

        // HIER: In der Praxis das Token per E-Mail-Service versenden.
        // Da wir lokal testen, geben wir es für dich temporär im JSON zurück:
        return Ok(new { Message = "Reset-Token generiert.", DebugToken = token });
    }

    // HILFSMETHODE: JWT-Erzeugung
    private string GenerateJwtToken(IdentityUser user, IList<string> roles, string displayName)
    {
        // Hole den geheimen Schlüssel aus dem Secret-Manager, den wir gestern angelegt haben
        var secretKey = _config["JwtSettings:Secret"] ?? "PLATZHALTER_FUER_DEN_GEHEIMEN_SCHLUESSEL_MIN_32_ZEICHEN!";
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email!),
            new Claim("GivenName", displayName) // Für die UI-Anzeige im WPF-Fenster
        };

        // Rollen als separate Claims hinzufügen für die API-Autorisierung
        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(15), // Kurzlebig für maximale Sicherheit
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature)
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}

