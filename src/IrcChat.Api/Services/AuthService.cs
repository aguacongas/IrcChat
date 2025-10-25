using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using IrcChat.Shared.Models;
using IrcChat.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace IrcChat.Api.Services;

public class AuthService(ChatDbContext db, IConfiguration config)
{
    public string HashPassword(string password)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }

    public async Task<Admin?> ValidateAdmin(string username, string password)
    {
        var admin = await db.Admins.FirstOrDefaultAsync(a => a.Username == username);
        if (admin == null)
        {
            return null;
        }

        var passwordHash = HashPassword(password);
        return passwordHash == admin.PasswordHash ? admin : null;
    }

    public string GenerateToken(Admin admin)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"] ?? "VotreCleSecrete123456789012345678901234567890"));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, admin.Username),
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim("AdminId", admin.Id.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: config["Jwt:Issuer"] ?? "IrcChatApi",
            audience: config["Jwt:Audience"] ?? "IrcChatClient",
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<Admin> CreateAdmin(string username, string password)
    {
        var admin = new Admin
        {
            Username = username,
            PasswordHash = HashPassword(password),
            CreatedAt = DateTime.UtcNow
        };

        db.Admins.Add(admin);
        await db.SaveChangesAsync();
        return admin;
    }

    internal async Task ForgetUsernameAndLogoutAsync(string value)
    {
        throw new NotImplementedException();
    }
}