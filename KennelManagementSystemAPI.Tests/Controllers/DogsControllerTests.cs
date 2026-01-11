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

public class DogsControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly DogsController _controller;

    public DogsControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _controller = new DogsController(_context);

        SeedTestData();
    }

    private void SeedTestData()
    {
        var customers = new List<Customer>
        {
            new Customer { Id = 1, Name = "John Doe", Email = "john@test.com", Phone = "123456", UserId = "user1" },
            new Customer { Id = 2, Name = "Jane Smith", Email = "jane@test.com", Phone = "789012", UserId = "user2" }
        };

        var dogs = new List<Dog>
        {
            new Dog { Id = 1, Name = "Buddy", Breed = "Golden Retriever", Age = 3, CustomerId = 1 },
            new Dog { Id = 2, Name = "Max", Breed = "German Shepherd", Age = 5, CustomerId = 1 },
            new Dog { Id = 3, Name = "Luna", Breed = "Labrador", Age = 2, CustomerId = 2 }
        };

        _context.Customers.AddRange(customers);
        _context.Dogs.AddRange(dogs);
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

    // ==================== GET ALL DOGS ====================

    [Fact]
    public async Task GetDogs_AsAdmin_ReturnsAllDogs()
    {
        // Arrange
        SetupUserContext("admin1", "Admin");

        // Act
        var result = await _controller.GetDogs();

        // Assert
        result.Value.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetDogs_AsStaff_ReturnsAllDogs()
    {
        // Arrange
        SetupUserContext("staff1", "Staff");

        // Act
        var result = await _controller.GetDogs();

        // Assert
        result.Value.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetDogs_AsCustomer_ReturnsOnlyOwnDogs()
    {
        // Arrange
        SetupUserContext("user1", "Customer");

        // Act
        var result = await _controller.GetDogs();

        // Assert
        result.Value.Should().HaveCount(2);
        result.Value.Should().OnlyContain(d => d.CustomerId == 1);
    }

    [Fact]
    public async Task GetDogs_AsCustomerWithNoDogs_ReturnsEmptyList()
    {
        // Arrange
        SetupUserContext("user3", "Customer");

        // Act
        var result = await _controller.GetDogs();

        // Assert
        var okResult = result.Result as OkObjectResult;
        var dogs = okResult!.Value as List<Dog>;
        dogs.Should().BeEmpty();
    }

    // ==================== GET SINGLE DOG ====================

    [Fact]
    public async Task GetDog_WithValidId_ReturnsDog()
    {
        // Arrange
        SetupUserContext("admin1", "Admin");

        // Act
        var result = await _controller.GetDog(1);

        // Assert
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("Buddy");
    }

    [Fact]
    public async Task GetDog_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        SetupUserContext("admin1", "Admin");

        // Act
        var result = await _controller.GetDog(999);

        // Assert
        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetDog_AsCustomer_CanAccessOwnDog()
    {
        // Arrange
        SetupUserContext("user1", "Customer");

        // Act
        var result = await _controller.GetDog(1);

        // Assert
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("Buddy");
    }

    [Fact]
    public async Task GetDog_AsCustomer_CannotAccessOthersDog()
    {
        // Arrange
        SetupUserContext("user1", "Customer");

        // Act
        var result = await _controller.GetDog(3); // Luna belongs to user2

        // Assert
        result.Result.Should().BeOfType<ForbidResult>();
    }

    // ==================== CREATE DOG ====================

    [Fact]
    public async Task CreateDog_AsAdmin_ReturnsCreatedAtAction()
    {
        // Arrange
        SetupUserContext("admin1", "Admin");
        var newDog = new Dog
        {
            Name = "Rex",
            Breed = "Bulldog",
            Age = 4,
            CustomerId = 1
        };

        // Act
        var result = await _controller.CreateDog(newDog);

        // Assert
        result.Result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task CreateDog_AsAdmin_AddsDogToDatabase()
    {
        // Arrange
        SetupUserContext("admin1", "Admin");
        var newDog = new Dog
        {
            Name = "Charlie",
            Breed = "Poodle",
            Age = 1,
            CustomerId = 2
        };

        // Act
        await _controller.CreateDog(newDog);

        // Assert
        var dogs = await _context.Dogs.ToListAsync();
        dogs.Should().HaveCount(4);
        dogs.Should().Contain(d => d.Name == "Charlie");
    }

    [Fact]
    public async Task CreateDog_AsCustomer_AutoSetsCustomerId()
    {
        // Arrange
        SetupUserContext("user1", "Customer");
        var newDog = new Dog
        {
            Name = "Daisy",
            Breed = "Beagle",
            Age = 2
        };

        // Act
        var result = await _controller.CreateDog(newDog);

        // Assert
        var createdResult = result.Result as CreatedAtActionResult;
        var createdDog = createdResult!.Value as Dog;
        createdDog!.CustomerId.Should().Be(1); // Customer 1 has UserId "user1"
    }

    [Fact]
    public async Task CreateDog_AsCustomerWithNoProfile_ReturnsBadRequest()
    {
        // Arrange
        SetupUserContext("unknownUser", "Customer");
        var newDog = new Dog
        {
            Name = "Test",
            Breed = "Test",
            Age = 1
        };

        // Act
        var result = await _controller.CreateDog(newDog);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ==================== UPDATE DOG ====================

    [Fact]
    public async Task UpdateDog_AsAdmin_ReturnsNoContent()
    {
        // Arrange
        SetupUserContext("admin1", "Admin");
        var dog = await _context.Dogs.AsNoTracking().FirstAsync(d => d.Id == 1);
        dog.Name = "Buddy Updated";

        // Act
        var result = await _controller.UpdateDog(1, dog);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task UpdateDog_WithMismatchedId_ReturnsBadRequest()
    {
        // Arrange
        SetupUserContext("admin1", "Admin");
        var dog = new Dog { Id = 1, Name = "Test", Breed = "Test", Age = 1 };

        // Act
        var result = await _controller.UpdateDog(999, dog);

        // Assert
        result.Should().BeOfType<BadRequestResult>();
    }

    [Fact]
    public async Task UpdateDog_AsCustomer_CanUpdateOwnDog()
    {
        // Arrange
        SetupUserContext("user1", "Customer");
        var dog = await _context.Dogs.AsNoTracking().FirstAsync(d => d.Id == 1);
        dog.Name = "Buddy Renamed";

        // Act
        var result = await _controller.UpdateDog(1, dog);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task UpdateDog_AsCustomer_CannotUpdateOthersDog()
    {
        // Arrange
        SetupUserContext("user1", "Customer");
        var dog = await _context.Dogs.AsNoTracking().FirstAsync(d => d.Id == 3);
        dog.Name = "Luna Renamed";

        // Act
        var result = await _controller.UpdateDog(3, dog);

        // Assert
        result.Should().BeOfType<ForbidResult>();
    }

    // ==================== DELETE DOG ====================

    [Fact]
    public async Task DeleteDog_AsAdmin_ReturnsNoContent()
    {
        // Arrange
        SetupUserContext("admin1", "Admin");

        // Act
        var result = await _controller.DeleteDog(1);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteDog_AsStaff_ReturnsNoContent()
    {
        // Arrange
        SetupUserContext("staff1", "Staff");

        // Act
        var result = await _controller.DeleteDog(1);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteDog_WithInvalidId_ReturnsNotFound()
    {
        // Arrange
        SetupUserContext("admin1", "Admin");

        // Act
        var result = await _controller.DeleteDog(999);

        // Assert
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task DeleteDog_RemovesDogFromDatabase()
    {
        // Arrange
        SetupUserContext("admin1", "Admin");

        // Act
        await _controller.DeleteDog(1);

        // Assert
        var dogs = await _context.Dogs.ToListAsync();
        dogs.Should().HaveCount(2);
        dogs.Should().NotContain(d => d.Id == 1);
    }

    // ==================== DOG DATA VALIDATION ====================

    [Fact]
    public async Task GetDog_ReturnsCorrectDogDetails()
    {
        // Arrange
        SetupUserContext("admin1", "Admin");

        // Act
        var result = await _controller.GetDog(1);

        // Assert
        result.Value!.Name.Should().Be("Buddy");
        result.Value.Breed.Should().Be("Golden Retriever");
        result.Value.Age.Should().Be(3);
        result.Value.CustomerId.Should().Be(1);
    }

    [Fact]
    public async Task GetDogs_ReturnsDogsWithCorrectBreeds()
    {
        // Arrange
        SetupUserContext("admin1", "Admin");

        // Act
        var result = await _controller.GetDogs();

        // Assert
        var dogs = result.Value!.ToList();
        dogs.Should().Contain(d => d.Breed == "Golden Retriever");
        dogs.Should().Contain(d => d.Breed == "German Shepherd");
        dogs.Should().Contain(d => d.Breed == "Labrador");
    }
}
