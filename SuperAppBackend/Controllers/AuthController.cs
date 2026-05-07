using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore; 

namespace SuperAppBackend.Controllers;

// Request Models
public record RegisterRequest(string PhoneNumber, string Password);
public record LoginRequest(string PhoneNumber, string Password);
public record VerifyRequest(string Token);

[ApiController]
[Route("[controller]")]
public class AuthController : ControllerBase
{
    private static readonly RSA _rsaKey = RSA.Create(2048);
    private static readonly string _keyId = "superapp-poc-key-001";
    private const string IssuerUrl = "https://superapp-backend-bog6.onrender.com"; 

    private readonly AppDbContext _db;

    public AuthController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// 1. REGISTER API: Create a real user in the In-Memory DB
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        // Check if user already exists
        if (await _db.Users.AnyAsync(u => u.PhoneNumber == request.PhoneNumber))
            return BadRequest(new { message = "Phone number already exists." });

        var newUser = new AppUser { 
            PhoneNumber = request.PhoneNumber, 
            Password = request.Password // Remember, real apps hash this!
        };

        _db.Users.Add(newUser);
        await _db.SaveChangesAsync();

        return Ok(new { message = "User registered successfully!" });
    }

    /// <summary>
    /// 2. REAL LOGIN API: Checks DB and generates the 30-second token
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        // Actually verify against the database
        var user = await _db.Users.FirstOrDefaultAsync(u => 
            u.PhoneNumber == request.PhoneNumber && 
            u.Password == request.Password);

        if (user == null) return Unauthorized(new { message = "Invalid phone or password." });

        // Generate the token
        var handler = new JwtSecurityTokenHandler();
        var key = new RsaSecurityKey(_rsaKey) { KeyId = _keyId };

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = IssuerUrl,
            Audience = "pizza-miniapp-vendor",
            Subject = new ClaimsIdentity(new[] { 
                new Claim("phone_number", user.PhoneNumber),
                new Claim("user_name", "Azure") // Hardcoded name for POC
            }),
            
            // THE EXPIRATION TEST: Token dies in exactly 30 seconds
            Expires = DateTime.UtcNow.AddSeconds(30), 
            
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256)
        };

        var token = handler.WriteToken(handler.CreateToken(descriptor));
        
        // Return the token!
        return Ok(new { message = "Login success", miniappToken = token });
    }

    /// <summary>
    /// 3. VERIFY API: The Miniapp uses this to check the token
    /// </summary>
    [HttpPost("verify-token")]
    public IActionResult Verify([FromBody] VerifyRequest request)
    {
        var handler = new JwtSecurityTokenHandler();
        var key = new RsaSecurityKey(_rsaKey);

        try
        {
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true, ValidIssuer = IssuerUrl,
                ValidateAudience = true, ValidAudience = "pizza-miniapp-vendor",
                ValidateIssuerSigningKey = true, IssuerSigningKey = key,
                ValidateLifetime = true, 
                ClockSkew = TimeSpan.Zero // STRICT CLOCK for the 30 sec test
            };

            var principal = handler.ValidateToken(request.Token, validationParameters, out _);
            var phone = principal.Claims.FirstOrDefault(c => c.Type == "phone_number")?.Value;

            return Ok(new { status = "Verified", message = $"Welcome back, {phone}!" });
        }
        catch (SecurityTokenExpiredException)
        {
            // Catches the 30-second timeout
            return Unauthorized(new { status = "Failed", error = "TOKEN_EXPIRED" });
        }
        catch (Exception ex)
        {
            return Unauthorized(new { status = "Failed", error = ex.Message });
        }
    }

    /// <summary>
    /// Legacy IssueToken for Flutter to use without a UI login screen
    /// </summary>
    [HttpGet("issue-miniapp-token")]
    public async Task<IActionResult> IssueToken()
    {
        var user = await _db.Users.FirstOrDefaultAsync();
        if (user == null)
        {
            user = new AppUser { PhoneNumber = "0123456789", Password = "password123" };
            _db.Users.Add(user);
            await _db.SaveChangesAsync();
        }

        var handler = new JwtSecurityTokenHandler();
        var key = new RsaSecurityKey(_rsaKey) { KeyId = _keyId };
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = IssuerUrl, Audience = "pizza-miniapp-vendor",
            Subject = new ClaimsIdentity(new[] { new Claim("phone_number", user.PhoneNumber), new Claim("user_name", "Azure") }),
            Expires = DateTime.UtcNow.AddSeconds(30), 
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256)
        };

        return Ok(new { miniappToken = handler.WriteToken(handler.CreateToken(descriptor)) });
    }

    [HttpGet(".well-known/jwks.json")]
    public IActionResult GetJwks()
    {
        var parameters = _rsaKey.ExportParameters(false);
        var jwk = new JsonWebKey { Kty = "RSA", Use = "sig", Alg = "RS256", Kid = _keyId, N = Base64UrlEncoder.Encode(parameters.Modulus), E = Base64UrlEncoder.Encode(parameters.Exponent) };
        return Ok(new { keys = new[] { jwk } });
    }
}