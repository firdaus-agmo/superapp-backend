using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace SuperAppBackend.Controllers;

// Simple models for the simulation
public record LoginRequest(string PhoneNumber, string Password);
public record VerifyRequest(string Token);

[ApiController]
[Route("[controller]")]
public class AuthController : ControllerBase
{
    // For the POC, we use a static RSA key so it persists while the app is running.
    private static readonly RSA _rsaKey = RSA.Create(2048);
    private static readonly string _keyId = "superapp-poc-key-001";
    
    // IMPORTANT: Once you deploy to Render, change this to your Render URL!
    private const string IssuerUrl = "https://superapp-backend-bog6.onrender.com"; 

    /// <summary>
    /// PHASE 1.1: Superapp Login
    /// Simulates the user logging into the main Superapp.
    /// </summary>
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        // Simulation: In real life, check your database here.
        if (request.PhoneNumber == "0123456789" && request.Password == "password123")
        {
            return Ok(new { message = "Authenticated in Superapp", userId = "USER-789" });
        }
        return Unauthorized(new { message = "Invalid credentials" });
    }

    /// <summary>
    /// PHASE 1.2: Issue Restricted Miniapp Token
    /// The Superapp calls this to get a 'wristband' specifically for one Miniapp.
    /// </summary>
    [HttpGet("issue-miniapp-token")]
    public IActionResult IssueToken()
    {
        var handler = new JwtSecurityTokenHandler();
        var key = new RsaSecurityKey(_rsaKey) { KeyId = _keyId };
        var credentials = new SigningCredentials(key, SecurityAlgorithms.RsaSha256);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = IssuerUrl,
            Audience = "pizza-miniapp-vendor", // Restricts the token to only this vendor
            Subject = new ClaimsIdentity(new[] { 
                new Claim("phone_number", "0123456789"),
                new Claim("user_name", "Firdaus"),
                new Claim("tier", "Premium")
            }),
            Expires = DateTime.UtcNow.AddMinutes(10), // Short lifespan for security
            SigningCredentials = credentials
        };

        var token = handler.WriteToken(handler.CreateToken(descriptor));
        return Ok(new { miniappToken = token });
    }

    /// <summary>
    /// PHASE 1.3: JWKS Endpoint
    /// The 'Public Blacklight' used by 3rd parties to verify your signature.
    /// </summary>
    [HttpGet(".well-known/jwks.json")]
    public IActionResult GetJwks()
    {
        var parameters = _rsaKey.ExportParameters(false);
        var jwk = new JsonWebKey
        {
            Kty = "RSA",
            Use = "sig",
            Alg = "RS256",
            Kid = _keyId,
            N = Base64UrlEncoder.Encode(parameters.Modulus),
            E = Base64UrlEncoder.Encode(parameters.Exponent)
        };

        return Ok(new { keys = new[] { jwk } });
    }

    /// <summary>
    /// PHASE 1.4: Verify Token (Miniapp Backend Simulation)
    /// This simulates a 3rd party server checking the token with the Superapp.
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
                ValidateIssuer = true,
                ValidIssuer = IssuerUrl,
                ValidateAudience = true,
                ValidAudience = "pizza-miniapp-vendor",
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateLifetime = true
            };

            var principal = handler.ValidateToken(request.Token, validationParameters, out _);
            var phone = principal.Claims.FirstOrDefault(c => c.Type == "phone_number")?.Value;

            return Ok(new { 
                status = "Verified", 
                message = $"Welcome back, {phone}!",
                details = "Signature is valid and token is trusted."
            });
        }
        catch (Exception ex)
        {
            return Unauthorized(new { status = "Failed", error = ex.Message });
        }
    }
}