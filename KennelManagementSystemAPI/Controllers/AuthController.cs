using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using KennelManagementSystemAPI.Models;
using KennelManagementSystemAPI.Models.DTOs;
using KennelManagementSystemAPI.Data;

namespace KennelManagementSystemAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _context;

    public AuthController(
        UserManager<ApplicationUser> userManager, 
        IConfiguration configuration,
        ApplicationDbContext context)
    {
        _userManager = userManager;
        _configuration = configuration;
        _context = context;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDto model)
    {
        // Check if user already exists
        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
        {
            return BadRequest(new { error = $"An account with email '{model.Email}' already exists. Please login instead." });
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FirstName = model.FirstName,
            LastName = model.LastName
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (!result.Succeeded)
        {
            // Build detailed error message for password requirements
            var errors = result.Errors.Select(e => e.Description).ToList();
            
            var errorMessage = "Registration failed. Please check the following:\n\n";
            
            // Check for specific password errors
            if (errors.Any(e => e.Contains("Password")))
            {
                errorMessage += "Password Requirements:\n";
                errorMessage += "• Must be at least 6 characters long\n";
                errorMessage += "• Must contain at least one uppercase letter (A-Z)\n";
                errorMessage += "• Must contain at least one lowercase letter (a-z)\n";
                errorMessage += "• Must contain at least one digit (0-9)\n\n";
            }
            
            errorMessage += "Errors:\n" + string.Join("\n", errors.Select(e => "• " + e));
            
            return BadRequest(new { error = errorMessage, errors = errors });
        }

        await _userManager.AddToRoleAsync(user, model.Role);

        // Auto-create Customer profile for Customer role
        if (model.Role == "Customer")
        {
            var customer = new Customer
            {
                Name = $"{model.FirstName} {model.LastName}",
                Email = model.Email,
                Phone = "",
                UserId = user.Id
            };

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();
        }

        return Ok(new { message = $"Account created successfully! You can now login with {model.Email}." });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDto model)
    {
        var user = await _userManager.FindByEmailAsync(model.Email);
        
        if (user == null)
        {
            return Unauthorized(new { error = $"No account found with email '{model.Email}'. Please check your email or register for a new account." });
        }

        if (!await _userManager.CheckPasswordAsync(user, model.Password))
        {
            return Unauthorized(new { error = "Incorrect password. Please try again." });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var token = GenerateJwtToken(user, roles);

        return Ok(new AuthResponseDto
        {
            Token = token,
            Email = user.Email!,
            Role = roles.FirstOrDefault() ?? "Customer"
        });
    }

    private string GenerateJwtToken(ApplicationUser user, IList<string> roles)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["Secret"] ?? throw new InvalidOperationException("JWT Secret not configured");
        
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Email, user.Email!),
            new(ClaimTypes.Name, $"{user.FirstName} {user.LastName}")
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiryMinutes = int.Parse(jwtSettings["ExpiryInMinutes"] ?? "60");

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
