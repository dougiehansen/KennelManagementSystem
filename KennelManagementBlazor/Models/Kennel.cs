namespace KennelManagementBlazor.Models;

public class Kennel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
    public decimal PricePerDay { get; set; }
}