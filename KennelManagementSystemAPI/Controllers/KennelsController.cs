using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KennelManagementSystemAPI.Data;
using KennelManagementSystemAPI.Models;

namespace KennelManagementSystemAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class KennelsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public KennelsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Kennel>>> GetKennels()
    {
        return await _context.Kennels.ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Kennel>> GetKennel(int id)
    {
        var kennel = await _context.Kennels.FindAsync(id);
        if (kennel == null) return NotFound();
        return kennel;
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<ActionResult<Kennel>> CreateKennel(Kennel kennel)
    {
        _context.Kennels.Add(kennel);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetKennel), new { id = kennel.Id }, kennel);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Staff")]
    public async Task<IActionResult> UpdateKennel(int id, Kennel kennel)
    {
        if (id != kennel.Id) return BadRequest();
        _context.Entry(kennel).State = EntityState.Modified;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteKennel(int id)
    {
        var kennel = await _context.Kennels.FindAsync(id);
        if (kennel == null) return NotFound();
        _context.Kennels.Remove(kennel);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
