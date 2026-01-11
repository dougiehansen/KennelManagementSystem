using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KennelManagementSystemAPI.Models;
using KennelManagementSystemAPI.Data;

namespace KennelManagementSystemAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ApplicationDbContext _context;

    public UsersController(
        UserManager<ApplicationUser> userManager, 
        RoleManager<IdentityRole> roleManager,
        ApplicationDbContext context)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers()
    {
        var users = await _userManager.Users.ToListAsync();
        var userDtos = new List<UserDto>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            userDtos.Add(new UserDto
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email ?? "",
                Role = roles.FirstOrDefault() ?? "Customer"
            });
        }

        return Ok(userDtos);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound(new { error = "User not found." });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var userDto = new UserDto
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email ?? "",
            Role = roles.FirstOrDefault() ?? "Customer"
        };

        return Ok(userDto);
    }

    [HttpPost]
    public async Task<ActionResult<UserDto>> CreateUser(CreateUserDto createUserDto)
    {
        // Check if user already exists
        var existingUser = await _userManager.FindByEmailAsync(createUserDto.Email);
        if (existingUser != null)
        {
            return BadRequest(new { error = $"A user with email '{createUserDto.Email}' already exists." });
        }

        // Create new user
        var user = new ApplicationUser
        {
            UserName = createUserDto.Email,
            Email = createUserDto.Email,
            FirstName = createUserDto.FirstName,
            LastName = createUserDto.LastName,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, createUserDto.Password);
        if (!result.Succeeded)
        {
            // Build detailed password error message
            var errors = result.Errors.Select(e => e.Description).ToList();
            var errorMessage = "Password does not meet requirements:\n" + string.Join("\n", errors);
            
            // Add helpful requirements message
            errorMessage += "\n\nPassword must:\n• Be at least 6 characters long\n• Contain at least one uppercase letter\n• Contain at least one lowercase letter\n• Contain at least one digit";
            
            return BadRequest(new { error = errorMessage, errors = errors });
        }

        // Assign role
        if (!string.IsNullOrEmpty(createUserDto.Role))
        {
            await _userManager.AddToRoleAsync(user, createUserDto.Role);
        }

        // Auto-create Customer profile for Customer role
        if (createUserDto.Role == "Customer")
        {
            var customer = new Customer
            {
                Name = $"{createUserDto.FirstName} {createUserDto.LastName}",
                Email = createUserDto.Email,
                Phone = "",
                UserId = user.Id
            };

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();
        }

        var userDto = new UserDto
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email ?? "",
            Role = createUserDto.Role
        };

        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, userDto);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateUser(string id, UpdateUserDto updateUserDto)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound(new { error = "User not found." });
        }

        // Check if email is being changed and if new email already exists
        if (user.Email != updateUserDto.Email)
        {
            var existingUser = await _userManager.FindByEmailAsync(updateUserDto.Email);
            if (existingUser != null)
            {
                return BadRequest(new { error = $"Email '{updateUserDto.Email}' is already in use." });
            }
        }

        // Update user details
        user.FirstName = updateUserDto.FirstName;
        user.LastName = updateUserDto.LastName;
        user.Email = updateUserDto.Email;
        user.UserName = updateUserDto.Email;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return BadRequest(new { error = $"Failed to update user: {errors}" });
        }

        // Update role if changed
        if (!string.IsNullOrEmpty(updateUserDto.Role))
        {
            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, updateUserDto.Role);
        }

        return NoContent();
    }

    [HttpPut("{id}/role")]
    public async Task<IActionResult> ChangeUserRole(string id, ChangeRoleDto changeRoleDto)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound(new { error = "User not found." });
        }

        // Check if role exists
        var roleExists = await _roleManager.RoleExistsAsync(changeRoleDto.NewRole);
        if (!roleExists)
        {
            return BadRequest(new { error = $"Role '{changeRoleDto.NewRole}' does not exist. Valid roles are: Admin, Staff, Customer." });
        }

        // Remove current roles
        var currentRoles = await _userManager.GetRolesAsync(user);
        await _userManager.RemoveFromRolesAsync(user, currentRoles);

        // Add new role
        var result = await _userManager.AddToRoleAsync(user, changeRoleDto.NewRole);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return BadRequest(new { error = $"Failed to change role: {errors}" });
        }

        return NoContent();
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteUser(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound(new { error = "User not found." });
        }

        // Don't allow deleting yourself
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.Id == id)
        {
            return BadRequest(new { error = "You cannot delete your own account. Please ask another administrator." });
        }

        // Check if user has a customer profile with dogs
        var customer = await _context.Customers.Include(c => c.Dogs).FirstOrDefaultAsync(c => c.UserId == id);
        if (customer != null && customer.Dogs != null && customer.Dogs.Any())
        {
            return BadRequest(new 
            { 
                error = $"Cannot delete user '{user.FirstName} {user.LastName}' because they have {customer.Dogs.Count} dog(s) registered. Please reassign or remove their dogs first." 
            });
        }

        // Delete customer profile if exists
        if (customer != null)
        {
            _context.Customers.Remove(customer);
            await _context.SaveChangesAsync();
        }

        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return BadRequest(new { error = $"Failed to delete user: {errors}" });
        }

        return NoContent();
    }
}

// DTOs
public class UserDto
{
    public string Id { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
}

public class CreateUserDto
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string Role { get; set; } = "";
}

public class UpdateUserDto
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Role { get; set; } = "";
}

public class ChangeRoleDto
{
    public string NewRole { get; set; } = "";
}
