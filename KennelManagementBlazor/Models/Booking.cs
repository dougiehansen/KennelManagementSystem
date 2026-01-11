namespace KennelManagementBlazor.Models;

public class Booking
{
    public int Id { get; set; }
    public int DogId { get; set; }
    public int KennelId { get; set; }
    public DateTime CheckInDate { get; set; }
    public DateTime CheckOutDate { get; set; }
    public decimal TotalCost { get; set; }
    public string Status { get; set; } = string.Empty;
}
