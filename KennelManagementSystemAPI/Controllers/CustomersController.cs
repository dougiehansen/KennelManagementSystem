using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KennelManagementSystemAPI.Data;
using KennelManagementSystemAPI.Models;

namespace KennelManagementSystemAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CustomersController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public CustomersController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<ActionResult<IEnumerable<Customer>>> GetCustomers()
    {
        return await _context.Customers.Include(c => c.Dogs).ToListAsync();
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "Admin,Staff,Customer")]
    public async Task<ActionResult<Customer>> GetCustomer(int id)
    {
        var customer = await _context.Customers.Include(c => c.Dogs).FirstOrDefaultAsync(c => c.Id == id);
        if (customer == null)
        {
            return NotFound(new { error = "Customer not found." });
        }
        return customer;
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<ActionResult<Customer>> CreateCustomer(Customer customer)
    {
        try
        {
            // Check for duplicate email
            var existingCustomer = await _context.Customers.FirstOrDefaultAsync(c => c.Email == customer.Email);
            if (existingCustomer != null)
            {
                return BadRequest(new { error = $"A customer with email '{customer.Email}' already exists." });
            }

            customer.Dogs = new List<Dog>();
            customer.User = null;

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetCustomer), new { id = customer.Id }, customer);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = "Failed to create customer. Please try again." });
        }
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Staff,Customer")]
    public async Task<IActionResult> UpdateCustomer(int id, Customer customer)
    {
        if (id != customer.Id)
        {
            return BadRequest(new { error = "Customer ID mismatch." });
        }

        try
        {
            customer.Dogs = new List<Dog>();
            customer.User = null;

            _context.Customers.Update(customer);
            await _context.SaveChangesAsync();
            return NoContent();
        }
        catch (DbUpdateConcurrencyException)
        {
            if (!_context.Customers.Any(c => c.Id == id))
            {
                return NotFound(new { error = "Customer not found." });
            }
            return BadRequest(new { error = "Failed to update customer. The record may have been modified by another user." });
        }
        catch (Exception)
        {
            return BadRequest(new { error = "Failed to update customer. Please try again." });
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteCustomer(int id)
    {
        var customer = await _context.Customers.Include(c => c.Dogs).FirstOrDefaultAsync(c => c.Id == id);
        
        if (customer == null)
        {
            return NotFound(new { error = "Customer not found." });
        }

        // Check if customer has dogs
        if (customer.Dogs != null && customer.Dogs.Any())
        {
            return BadRequest(new 
            { 
                error = $"Cannot delete customer '{customer.Name}' because they have {customer.Dogs.Count} dog(s) registered. Please reassign or remove their dogs first." 
            });
        }

        try
        {
            _context.Customers.Remove(customer);
            await _context.SaveChangesAsync();
            return NoContent();
        }
        catch (Exception)
        {
            return BadRequest(new { error = "Failed to delete customer. Please try again." });
        }
    }
}
