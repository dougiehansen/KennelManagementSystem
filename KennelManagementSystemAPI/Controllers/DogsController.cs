using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KennelManagementSystemAPI.Data;
using KennelManagementSystemAPI.Models;
using System.Security.Claims;

namespace KennelManagementSystemAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DogsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public DogsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IEnumerable<Dog>>> GetDogs()
    {
        var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        // Admin and Staff see all dogs
        if (userRole == "Admin" || userRole == "Staff")
        {
            return await _context.Dogs.ToListAsync();
        }

        // Customer sees only their dogs
        if (userRole == "Customer")
        {
            // Find customer by UserId
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
            if (customer == null)
            {
                return Ok(new List<Dog>()); // Return empty list if customer not found
            }

            return await _context.Dogs.Where(d => d.CustomerId == customer.Id).ToListAsync();
        }

        return Forbid();
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<Dog>> GetDog(int id)
    {
        var dog = await _context.Dogs.FindAsync(id);
        if (dog == null) return NotFound();

        // Check if customer is authorized to view this dog
        var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
        if (userRole == "Customer")
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
            
            if (customer == null || dog.CustomerId != customer.Id)
            {
                return Forbid();
            }
        }

        return dog;
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Dog>> CreateDog(Dog dog)
    {
        var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

        // If customer, automatically set their CustomerId
        if (userRole == "Customer")
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
            
            if (customer == null)
            {
                return BadRequest(new { error = "Customer profile not found. Please contact administrator." });
            }

            dog.CustomerId = customer.Id;
        }

        // Clear navigation properties
        dog.Customer = null;
        dog.Bookings = new List<Booking>();

        _context.Dogs.Add(dog);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetDog), new { id = dog.Id }, dog);
    }

  


[HttpPut("{id}")]
[Authorize]
public async Task<IActionResult> UpdateDog(int id, Dog dog)
{
    if (id != dog.Id) return BadRequest();

    // Check authorization for customers
    var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
    if (userRole == "Customer")
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
        
        var existingDog = await _context.Dogs.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
        if (existingDog == null || customer == null || existingDog.CustomerId != customer.Id)
        {
            return Forbid();
        }

        // Ensure customer can't change ownership
        dog.CustomerId = customer.Id;
    }

    // Clear navigation properties
    dog.Customer = null;
    dog.Bookings = new List<Booking>();

    // Update without tracking conflicts
    _context.Dogs.Update(dog);
    await _context.SaveChangesAsync();

    return NoContent();
}






    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> DeleteDog(int id)
    {
        var dog = await _context.Dogs.FindAsync(id);
        if (dog == null) return NotFound();

        _context.Dogs.Remove(dog);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
