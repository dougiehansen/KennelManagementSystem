namespace KennelManagementSystemAPI.Models;

public class Booking
{
    public int Id { get; set; }
    public int DogId { get; set; }
    public Dog? Dog { get; set; }
    public int KennelId { get; set; }
    public Kennel? Kennel { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public decimal TotalCost { get; set; }
    public string Status { get; set; } = "Pending";
}
