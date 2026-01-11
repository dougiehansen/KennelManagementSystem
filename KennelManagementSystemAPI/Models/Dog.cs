namespace KennelManagementSystemAPI.Models;

public class Dog
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Breed { get; set; }
    public int Age { get; set; }
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public ICollection<Booking> Bookings { get; set; } = new List<Booking>();
}
