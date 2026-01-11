namespace KennelManagementSystemAPI.Models;

public class Kennel
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Size { get; set; } // Small, Medium, Large, Extra Large
    public bool IsAvailable { get; set; }
    public decimal PricePerDay { get; set; } // ← NEW! Price per day
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}