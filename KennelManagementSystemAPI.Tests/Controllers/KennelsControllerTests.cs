using Xunit;
using Microsoft.EntityFrameworkCore;
using KennelManagementSystemAPI.Controllers;
using KennelManagementSystemAPI.Data;
using KennelManagementSystemAPI.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace KennelManagementSystemAPI.Tests.Controllers;

public class KennelsControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly KennelsController _controller;

    public KennelsControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _controller = new KennelsController(_context);

        // Seed test data
        SeedTestData();
    }

    private void SeedTestData()
    {
        var kennels = new List<Kennel>
        {
            new Kennel { Id = 1, Name = "Small Kennel", Size = "Small", IsAvailable = true, PricePerDay = 25.00m },
            new Kennel { Id = 2, Name = "Medium Kennel", Size = "Medium", IsAvailable = true, PricePerDay = 35.00m },
            new Kennel { Id = 3, Name = "Large Kennel", Size = "Large", IsAvailable = false, PricePerDay = 50.00m }
        };

        _context.Kennels.AddRange(kennels);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    // ==================== GET ALL KENNELS ====================

    [Fact]
    public async Task GetKennels_ReturnsAllKennels()
    {
        // Act
        var result = await _controller.GetKennels();

        // Assert
        var actionResult = result.Result;
        actionResult.Should().BeNull();
        result.Value.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetKennels_ReturnsCorrectKennelNames()
    {
        // Act
        var result = await _controller.GetKennels();

        // Assert
        var kennels = result.Value!.ToList();
        kennels.Should().Contain(k => k.Name == "Small Kennel");
        kennels.Should().Contain(k => k.Name == "Medium Kennel");
        kennels.Should().Contain(k => k.Name == "Large Kennel");
    }

    [Fact]
    public async Task GetKennels_ReturnsKennelsWithCorrectPrices()
    {
        // Act
        var result = await _controller.GetKennels();

        // Assert
        var kennels = result.Value!.ToList();
        kennels.First(k => k.Name == "Small Kennel").PricePerDay.Should().Be(25.00m);
        kennels.First(k => k.Name == "Large Kennel").PricePerDay.Should().Be(50.00m);
    }

    // ==================== GET SINGLE KENNEL ====================

    [Fact]
    public async Task GetKennel_WithValidId_ReturnsKennel()
    {
        // Act
        var result = await _controller.GetKennel(1);

        // Assert
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("Small Kennel");
    }

    [Fact]
    public async Task GetKennel_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var result = await _controller.GetKennel(999);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetKennel_ReturnsCorrectKennelDetails()
    {
        // Act
        var result = await _controller.GetKennel(2);

        // Assert
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("Medium Kennel");
        result.Value.Size.Should().Be("Medium");
        result.Value.PricePerDay.Should().Be(35.00m);
        result.Value.IsAvailable.Should().BeTrue();
    }

    // ==================== CREATE KENNEL ====================

    [Fact]
    public async Task CreateKennel_WithValidData_ReturnsCreatedAtAction()
    {
        // Arrange
        var newKennel = new Kennel
        {
            Name = "Extra Large Kennel",
            Size = "Extra Large",
            IsAvailable = true,
            PricePerDay = 75.00m
        };

        // Act
        var result = await _controller.CreateKennel(newKennel);

        // Assert
        result.Result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task CreateKennel_WithValidData_AddsKennelToDatabase()
    {
        // Arrange
        var newKennel = new Kennel
        {
            Name = "Premium Suite",
            Size = "Large",
            IsAvailable = true,
            PricePerDay = 100.00m
        };

        // Act
        await _controller.CreateKennel(newKennel);

        // Assert
        var kennels = await _context.Kennels.ToListAsync();
        kennels.Should().HaveCount(4);
        kennels.Should().Contain(k => k.Name == "Premium Suite");
    }

    [Fact]
    public async Task CreateKennel_ReturnsCreatedKennelWithId()
    {
        // Arrange
        var newKennel = new Kennel
        {
            Name = "VIP Kennel",
            Size = "Large",
            IsAvailable = true,
            PricePerDay = 150.00m
        };

        // Act
        var result = await _controller.CreateKennel(newKennel);

        // Assert
        var createdResult = result.Result as CreatedAtActionResult;
        var createdKennel = createdResult!.Value as Kennel;
        createdKennel!.Id.Should().BeGreaterThan(0);
        createdKennel.Name.Should().Be("VIP Kennel");
    }

    // ==================== UPDATE KENNEL ====================

    [Fact]
    public async Task UpdateKennel_WithValidData_ReturnsNoContent()
    {
        // Arrange
        var kennel = await _context.Kennels.FindAsync(1);
        _context.Entry(kennel!).State = EntityState.Detached;
        
        var updatedKennel = new Kennel
        {
            Id = 1,
            Name = "Updated Small Kennel",
            Size = "Small",
            IsAvailable = true,
            PricePerDay = 30.00m
        };

        // Act
        var result = await _controller.UpdateKennel(1, updatedKennel);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task UpdateKennel_WithMismatchedId_ReturnsBadRequest()
    {
        // Arrange
        var kennel = new Kennel
        {
            Id = 1,
            Name = "Test",
            Size = "Small",
            IsAvailable = true,
            PricePerDay = 25.00m
        };

        // Act
        var result = await _controller.UpdateKennel(999, kennel);

        // Assert
        result.Should().BeOfType<BadRequestResult>();
    }

    [Fact]
    public async Task UpdateKennel_UpdatesKennelInDatabase()
    {
        // Arrange
        var kennel = await _context.Kennels.FindAsync(1);
        _context.Entry(kennel!).State = EntityState.Detached;
        
        var updatedKennel = new Kennel
        {
            Id = 1,
            Name = "Renamed Kennel",
            Size = "Small",
            IsAvailable = false,
            PricePerDay = 40.00m
        };

        // Act
        await _controller.UpdateKennel(1, updatedKennel);

        // Assert
        var result = await _context.Kennels.FindAsync(1);
        result!.Name.Should().Be("Renamed Kennel");
        result.PricePerDay.Should().Be(40.00m);
        result.IsAvailable.Should().BeFalse();
    }

    // ==================== DELETE KENNEL ====================

    [Fact]
    public async Task DeleteKennel_WithValidId_ReturnsNoContent()
    {
        // Act
        var result = await _controller.DeleteKennel(1);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteKennel_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var result = await _controller.DeleteKennel(999);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteKennel_RemovesKennelFromDatabase()
    {
        // Act
        await _controller.DeleteKennel(1);

        // Assert
        var kennels = await _context.Kennels.ToListAsync();
        kennels.Should().HaveCount(2);
        kennels.Should().NotContain(k => k.Id == 1);
    }

    // ==================== AVAILABILITY TESTS ====================

    [Fact]
    public async Task GetKennels_ReturnsAvailableAndUnavailableKennels()
    {
        // Act
        var result = await _controller.GetKennels();

        // Assert
        var kennels = result.Value!.ToList();
        kennels.Count(k => k.IsAvailable).Should().Be(2);
        kennels.Count(k => !k.IsAvailable).Should().Be(1);
    }

    [Fact]
    public async Task GetKennel_ReturnsCorrectAvailabilityStatus()
    {
        // Act
        var availableKennel = await _controller.GetKennel(1);
        var unavailableKennel = await _controller.GetKennel(3);

        // Assert
        availableKennel.Value!.IsAvailable.Should().BeTrue();
        unavailableKennel.Value!.IsAvailable.Should().BeFalse();
    }
}
