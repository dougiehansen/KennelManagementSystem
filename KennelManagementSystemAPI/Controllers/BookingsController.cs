using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KennelManagementSystemAPI.Data;
using KennelManagementSystemAPI.Models;
using System.Security.Claims;

namespace KennelManagementSystemAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BookingsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public BookingsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [Authorize]
    public async Task<ActionResult<IEnumerable<Booking>>> GetBookings()
    {
        var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        // Admin and Staff see all bookings
        if (userRole == "Admin" || userRole == "Staff")
        {
            return await _context.Bookings
                .Include(b => b.Dog)
                .Include(b => b.Kennel)
                .ToListAsync();
        }

        // Customer sees only bookings for their dogs
        if (userRole == "Customer")
        {
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
            if (customer == null)
            {
                return Ok(new List<Booking>());
            }

            // Get all dogs owned by this customer
            var customerDogIds = await _context.Dogs
                .Where(d => d.CustomerId == customer.Id)
                .Select(d => d.Id)
                .ToListAsync();

            // Get bookings for those dogs
            return await _context.Bookings
                .Include(b => b.Dog)
                .Include(b => b.Kennel)
                .Where(b => customerDogIds.Contains(b.DogId))
                .ToListAsync();
        }

        return Forbid();
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<ActionResult<Booking>> GetBooking(int id)
    {
        var booking = await _context.Bookings
            .Include(b => b.Dog)
            .Include(b => b.Kennel)
            .FirstOrDefaultAsync(b => b.Id == id);

        if (booking == null) return NotFound();

        // Check if customer is authorized
        var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
        if (userRole == "Customer")
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
            
            var dog = await _context.Dogs.FindAsync(booking.DogId);
            if (customer == null || dog == null || dog.CustomerId != customer.Id)
            {
                return Forbid();
            }
        }

        return booking;
    }

    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Booking>> CreateBooking(Booking booking)
    {
        // Check if customer is trying to book their own dog
        var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
        if (userRole == "Customer")
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var customer = await _context.Customers.FirstOrDefaultAsync(c => c.UserId == userId);
            
            var dog = await _context.Dogs.FindAsync(booking.DogId);
            if (customer == null || dog == null || dog.CustomerId != customer.Id)
            {
                return BadRequest(new { error = "You can only create bookings for your own dogs." });
            }
        }

        // Clear navigation properties
        booking.Dog = null;
        booking.Kennel = null;

        _context.Bookings.Add(booking);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetBooking), new { id = booking.Id }, booking);
    }

  
[HttpPut("{id}")]
[Authorize]
public async Task<IActionResult> UpdateBooking(int id, Booking booking)
{
    if (id != booking.Id) return BadRequest();

    // Check authorization for customers
    var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
    if (userRole == "Customer")
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var customer = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == userId);
        
        var existingBooking = await _context.Bookings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id);
        if (existingBooking == null) return NotFound();

        var dog = await _context.Dogs.AsNoTracking().FirstOrDefaultAsync(d => d.Id == existingBooking.DogId);
        if (customer == null || dog == null || dog.CustomerId != customer.Id)
        {
            return Forbid();
        }
    }

    // Clear navigation properties
    booking.Dog = null;
    booking.Kennel = null;

    // Update without tracking conflicts
    _context.Bookings.Update(booking);
    await _context.SaveChangesAsync();

    return NoContent();
}





    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteBooking(int id)
    {
        var booking = await _context.Bookings.FindAsync(id);
        if (booking == null) return NotFound();

        _context.Bookings.Remove(booking);
        await _context.SaveChangesAsync();

        return NoContent();
    }
}
