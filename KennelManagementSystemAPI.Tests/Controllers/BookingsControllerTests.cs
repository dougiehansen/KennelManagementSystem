using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using KennelManagementSystemAPI.Controllers;
using KennelManagementSystemAPI.Data;
using KennelManagementSystemAPI.Models;
using FluentAssertions;
using System.Security.Claims;

namespace KennelManagementSystemAPI.Tests.Controllers;

public class BookingsControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly BookingsController _controller;

    public BookingsControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _controller = new BookingsController(_context);

        SeedTestData();
    }

    private void SeedTestData()
    {
        var customers = new List<Customer>
        {
            new Customer { Id = 1, Name = "John Doe", Email = "john@test.com", Phone = "123456", UserId = "user1" },
            new Customer { Id = 2, Name = "Jane Smith", Email = "jane@test.com", Phone = "789012", UserId = "user2" }
        };

        var kennels = new List<Kennel>
        {
            new Kennel { Id = 1, Name = "Small Kennel", Size = "Small", IsAvailable = true, PricePerDay = 25.00m },
            new Kennel { Id = 2, Name = "Large Kennel", Size = "Large", IsAvailable = true, PricePerDay = 50.00m }
        };

        var dogs = new List<Dog>
        {
            new Dog { Id = 1, Name = "Buddy", Breed = "Golden Retriever", Age = 3, CustomerId = 1 },
            new Dog { Id = 2, Name = "Luna", Breed = "Labrador", Age = 2, CustomerId = 2 }
        };

        var bookings = new List<Booking>
        {
            new Booking 
            { 
                Id = 1, 
                DogId = 1, 
                KennelId = 1, 
                CheckInDate = DateTime.Today, 
                CheckOutDate = DateTime.Today.AddDays(3),
                TotalCost = 75.00m,
                Status = "Confirmed"
            },
            new Booking 
            { 
                Id = 2, 
                DogId = 2, 
                KennelId = 2, 
                CheckInDate = DateTime.Today.AddDays(5), 
                CheckOutDate = DateTime.Today.AddDays(7),
                TotalCost = 100.00m,
                Status = "Pending"
            }
        };

        _context.Customers.AddRange(customers);
        _context.Kennels.AddRange(kennels);
        _context.Dogs.AddRange(dogs);
        _context.Bookings.AddRange(bookings);
        _context.SaveChanges();
    }

    private void SetupUserContext(string userId, string role)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, role)
        };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = claimsPrincipal }
        };
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    // ==================== GET ALL BOOKINGS ====================

    [Fact]
    public async Task GetBookings_AsAdmin_ReturnsAllBookings()
    {
        // Arrange
        SetupUserContext("admin1", "Admin");

        // Act
        var result = await _controller.GetBookings();

        // Assert
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetBookings_AsStaff_ReturnsAllBookings()
    {
        // Arrange
        SetupUserContext("staff1", "Staff");

        // Act
        var result = await _controller.GetBookings();

        // Assert
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetBookings_AsCustomer_ReturnsOnlyOwnBookings()
    {
        // Arrange
        SetupUserContext("user1", "Customer");

        // Act
        var result = await _controller.GetBookings();

        // Assert
        result.Value.Should().HaveCount(1);
        result.Value.Should().OnlyContain(b => b.DogId == 1);
    }

    [Fact]
    public async Task GetBookings_AsCustomerWithNoBookings_ReturnsEmptyList()
    {
        // Arrange
        SetupUserContext("user3", "Customer");

        // Act
        var result = await _controller.GetBookings();

        // Assert
        var okResult = result.Result as OkObjectResult;
        var bookings = okResult!.Value as List<Booking>;
        bookings.Should().BeEmpty();
    }

    // ==================== GET SINGLE BOOKING ====================

    [Fact]
    public async Task GetBooking_WithValidId_ReturnsBooking()
    {
        // Arrange
        SetupUserContext("admin1", "Admin");

        // Act
        var result = await _controller.GetBooking(1);

        // Assert
        result.Value.Should().NotBeNull();
        result.Value!.Id.Should().Be(1);
    }

    [Fact]
    public async Task GetBooking_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        SetupUserContext("admin1", "Admin");

        // Act
        var result = await _controller.GetBooking(999);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetBooking_AsCustomer_CanAccessOwnBooking()
    {
        // Arrange
        SetupUserContext("user1", "Customer");

        // Act
        var result = await _controller.GetBooking(1);

        // Assert
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task GetBooking_AsCustomer_CannotAccessOthersBooking()
    {
        // Arrange
        SetupUserContext("user1", "Customer");

        // Act
        var result = await _controller.GetBooking(2); // Booking for user2's dog

        // Assert
        result.Result.Should().BeOfType<ForbidResult>();
    }

    // ==================== CREATE BOOKING ====================

    [Fact]
    public async Task CreateBooking_AsAdmin_ReturnsCreatedAtAction()
    {
        // Arrange
        SetupUserContext("admin1", "Admin");
        var newBooking = new Booking
        {
            DogId = 1,
            KennelId = 2,
            CheckInDate = DateTime.Today.AddDays(10),
            CheckOutDate = DateTime.Today.AddDays(12),
            TotalCost = 100.00m,
            Status = "Pending"
        };

        // Act
        var result = await _controller.CreateBooking(newBooking);

        // Assert
        result.Result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task CreateBooking_AsAdmin_AddsBookingToDatabase()
    {
        // Arrange
        SetupUserContext("admin1", "Admin");
        var newBooking = new Booking
        {
            DogId = 1,
            KennelId = 2,
            CheckInDate = DateTime.Today.AddDays(15),
            CheckOutDate = DateTime.Today.AddDays(18),
            TotalCost = 150.00m,
            Status = "Confirmed"
        };

        // Act
        await _controller.CreateBooking(newBooking);

        // Assert
        var bookings = await _context.Bookings.ToListAsync();
        bookings.Should().HaveCount(3);
    }

    [Fact]
    public async Task CreateBooking_AsCustomer_CanBookOwnDog()
    {
        // Arrange
        SetupUserContext("user1", "Customer");
        var newBooking = new Booking
        {
            DogId = 1, // Buddy belongs to user1
            KennelId = 2,
            CheckInDate = DateTime.Today.AddDays(20),
            CheckOutDate = DateTime.Today.AddDays(22),
            TotalCost = 100.00m,
            Status = "Pending"
        };

        // Act
        var result = await _controller.CreateBooking(newBooking);

        // Assert
        result.Result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task CreateBooking_AsCustomer_CannotBookOthersDog()
    {
        // Arrange
        SetupUserContext("user1", "Customer");
        var newBooking = new Booking
        {
            DogId = 2, // Luna belongs to user2
            KennelId = 1,
            CheckInDate = DateTime.Today.AddDays(25),
            CheckOutDate = DateTime.Today.AddDays(27),
            TotalCost = 75.00m,
            Status = "Pending"
        };

        // Act
        var result = await _controller.CreateBooking(newBooking);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ==================== UPDATE BOOKING ====================

    [Fact]
    public async Task UpdateBooking_AsAdmin_ReturnsNoContent()
    {
        // Arrange
        SetupUserContext("admin1", "Admin");
        var booking = await _context.Bookings.AsNoTracking().FirstAsync(b => b.Id == 1);
        booking.Status = "Cancelled";

        // Act
        var result = await _controller.UpdateBooking(1, booking);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task UpdateBooking_WithMismatchedId_ReturnsBadRequest()
    {
        // Arrange
        SetupUserContext("admin1", "Admin");
        var booking = new Booking { Id = 1, DogId = 1, KennelId = 1 };

        // Act
        var result = await _controller.UpdateBooking(999, booking);

        // Assert
        result.Should().BeOfType<BadRequestResult>();
    }

    [Fact]
    public async Task UpdateBooking_AsCustomer_CanUpdateOwnBooking()
    {
        // Arrange
        SetupUserContext("user1", "Customer");
        var booking = await _context.Bookings.AsNoTracking().FirstAsync(b => b.Id == 1);
        booking.CheckOutDate = DateTime.Today.AddDays(5);

        // Act
        var result = await _controller.UpdateBooking(1, booking);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task UpdateBooking_AsCustomer_CannotUpdateOthersBooking()
    {
        // Arrange
        SetupUserContext("user1", "Customer");
        var booking = await _context.Bookings.AsNoTracking().FirstAsync(b => b.Id == 2);
        booking.Status = "Cancelled";

        // Act
        var result = await _controller.UpdateBooking(2, booking);

        // Assert
        result.Should().BeOfType<ForbidResult>();
    }

    // ==================== DELETE BOOKING ====================

    [Fact]
    public async Task DeleteBooking_AsAdmin_ReturnsNoContent()
    {
        // Arrange
        SetupUserContext("admin1", "Admin");

        // Act
        var result = await _controller.DeleteBooking(1);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteBooking_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        SetupUserContext("admin1", "Admin");

        // Act
        var result = await _controller.DeleteBooking(999);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteBooking_RemovesBookingFromDatabase()
    {
        // Arrange
        SetupUserContext("admin1", "Admin");

        // Act
        await _controller.DeleteBooking(1);

        // Assert
        var bookings = await _context.Bookings.ToListAsync();
        bookings.Should().HaveCount(1);
        bookings.Should().NotContain(b => b.Id == 1);
    }

    // ==================== BOOKING DATA VALIDATION ====================

    [Fact]
    public async Task GetBooking_ReturnsCorrectBookingDetails()
    {
        // Arrange
        SetupUserContext("admin1", "Admin");

        // Act
        var result = await _controller.GetBooking(1);

        // Assert
        result.Value!.DogId.Should().Be(1);
        result.Value.KennelId.Should().Be(1);
        result.Value.TotalCost.Should().Be(75.00m);
        result.Value.Status.Should().Be("Confirmed");
    }

    [Fact]
    public async Task GetBookings_ReturnsBookingsWithCorrectStatuses()
    {
        // Arrange
        SetupUserContext("admin1", "Admin");

        // Act
        var result = await _controller.GetBookings();

        // Assert
        var bookings = result.Value!.ToList();
        bookings.Should().Contain(b => b.Status == "Confirmed");
        bookings.Should().Contain(b => b.Status == "Pending");
    }

    [Fact]
    public async Task GetBookings_IncludesDogAndKennelData()
    {
        // Arrange
        SetupUserContext("admin1", "Admin");

        // Act
        var result = await _controller.GetBookings();

        // Assert
        var booking = result.Value!.First();
        booking.Dog.Should().NotBeNull();
        booking.Kennel.Should().NotBeNull();
    }
}
