using Xunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using KennelManagementSystemAPI.Controllers;
using KennelManagementSystemAPI.Data;
using KennelManagementSystemAPI.Models;
using FluentAssertions;

namespace KennelManagementSystemAPI.Tests.Controllers;

public class CustomersControllerTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly CustomersController _controller;

    public CustomersControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _controller = new CustomersController(_context);

        SeedTestData();
    }

    private void SeedTestData()
    {
        var customers = new List<Customer>
        {
            new Customer { Id = 1, Name = "John Doe", Email = "john@test.com", Phone = "123-456-7890", UserId = "user1" },
            new Customer { Id = 2, Name = "Jane Smith", Email = "jane@test.com", Phone = "098-765-4321", UserId = "user2" },
            new Customer { Id = 3, Name = "Bob Wilson", Email = "bob@test.com", Phone = "555-555-5555", UserId = "user3" }
        };

        var dogs = new List<Dog>
        {
            new Dog { Id = 1, Name = "Buddy", Breed = "Golden Retriever", Age = 3, CustomerId = 1 }
        };

        _context.Customers.AddRange(customers);
        _context.Dogs.AddRange(dogs);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    // ==================== GET ALL CUSTOMERS ====================

    [Fact]
    public async Task GetCustomers_ReturnsAllCustomers()
    {
        // Act
        var result = await _controller.GetCustomers();

        // Assert
        result.Value.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetCustomers_ReturnsCustomersWithCorrectNames()
    {
        // Act
        var result = await _controller.GetCustomers();

        // Assert
        var customers = result.Value!.ToList();
        customers.Should().Contain(c => c.Name == "John Doe");
        customers.Should().Contain(c => c.Name == "Jane Smith");
        customers.Should().Contain(c => c.Name == "Bob Wilson");
    }

    [Fact]
    public async Task GetCustomers_IncludesDogs()
    {
        // Act
        var result = await _controller.GetCustomers();

        // Assert
        var john = result.Value!.First(c => c.Name == "John Doe");
        john.Dogs.Should().NotBeNull();
        john.Dogs.Should().HaveCount(1);
    }

    // ==================== GET SINGLE CUSTOMER ====================

    [Fact]
    public async Task GetCustomer_WithValidId_ReturnsCustomer()
    {
        // Act
        var result = await _controller.GetCustomer(1);

        // Assert
        result.Value.Should().NotBeNull();
        result.Value!.Name.Should().Be("John Doe");
    }

    [Fact]
    public async Task GetCustomer_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var result = await _controller.GetCustomer(999);

        // Assert
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetCustomer_ReturnsCorrectCustomerDetails()
    {
        // Act
        var result = await _controller.GetCustomer(2);

        // Assert
        result.Value!.Name.Should().Be("Jane Smith");
        result.Value.Email.Should().Be("jane@test.com");
        result.Value.Phone.Should().Be("098-765-4321");
    }

    [Fact]
    public async Task GetCustomer_IncludesDogsInResponse()
    {
        // Act
        var result = await _controller.GetCustomer(1);

        // Assert
        result.Value!.Dogs.Should().NotBeNull();
        result.Value.Dogs.Should().Contain(d => d.Name == "Buddy");
    }

    // ==================== CREATE CUSTOMER ====================

    [Fact]
    public async Task CreateCustomer_WithValidData_ReturnsCreatedAtAction()
    {
        // Arrange
        var newCustomer = new Customer
        {
            Name = "New Customer",
            Email = "new@test.com",
            Phone = "111-222-3333"
        };

        // Act
        var result = await _controller.CreateCustomer(newCustomer);

        // Assert
        result.Result.Should().BeOfType<CreatedAtActionResult>();
    }

    [Fact]
    public async Task CreateCustomer_WithValidData_AddsCustomerToDatabase()
    {
        // Arrange
        var newCustomer = new Customer
        {
            Name = "Alice Brown",
            Email = "alice@test.com",
            Phone = "444-555-6666"
        };

        // Act
        await _controller.CreateCustomer(newCustomer);

        // Assert
        var customers = await _context.Customers.ToListAsync();
        customers.Should().HaveCount(4);
        customers.Should().Contain(c => c.Name == "Alice Brown");
    }

    [Fact]
    public async Task CreateCustomer_WithDuplicateEmail_ReturnsBadRequest()
    {
        // Arrange
        var newCustomer = new Customer
        {
            Name = "Duplicate",
            Email = "john@test.com", // Already exists
            Phone = "000-000-0000"
        };

        // Act
        var result = await _controller.CreateCustomer(newCustomer);

        // Assert
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateCustomer_ReturnsCreatedCustomerWithId()
    {
        // Arrange
        var newCustomer = new Customer
        {
            Name = "Test Customer",
            Email = "testcustomer@test.com",
            Phone = "999-888-7777"
        };

        // Act
        var result = await _controller.CreateCustomer(newCustomer);

        // Assert
        var createdResult = result.Result as CreatedAtActionResult;
        var createdCustomer = createdResult!.Value as Customer;
        createdCustomer!.Id.Should().BeGreaterThan(0);
    }

    // ==================== UPDATE CUSTOMER ====================

    [Fact]
    public async Task UpdateCustomer_WithValidData_ReturnsNoContent()
    {
        // Arrange
        var customer = await _context.Customers.AsNoTracking().FirstAsync(c => c.Id == 1);
        customer.Name = "John Doe Updated";

        // Act
        var result = await _controller.UpdateCustomer(1, customer);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task UpdateCustomer_WithMismatchedId_ReturnsBadRequest()
    {
        // Arrange
        var customer = new Customer { Id = 1, Name = "Test", Email = "test@test.com", Phone = "123" };

        // Act
        var result = await _controller.UpdateCustomer(999, customer);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdateCustomer_UpdatesCustomerInDatabase()
    {
        // Arrange
        var customer = await _context.Customers.AsNoTracking().FirstAsync(c => c.Id == 2);
        customer.Phone = "NEW-PHONE-NUMBER";

        // Act
        await _controller.UpdateCustomer(2, customer);

        // Assert
        var updatedCustomer = await _context.Customers.FindAsync(2);
        updatedCustomer!.Phone.Should().Be("NEW-PHONE-NUMBER");
    }

    // ==================== DELETE CUSTOMER ====================

    [Fact]
    public async Task DeleteCustomer_WithValidIdAndNoDogs_ReturnsNoContent()
    {
        // Arrange - Customer 2 has no dogs

        // Act
        var result = await _controller.DeleteCustomer(2);

        // Assert
        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteCustomer_WithInvalidId_ReturnsNotFound()
    {
        // Act
        var result = await _controller.DeleteCustomer(999);

        // Assert
        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task DeleteCustomer_WithDogs_ReturnsBadRequest()
    {
        // Arrange - Customer 1 has dogs

        // Act
        var result = await _controller.DeleteCustomer(1);

        // Assert
        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task DeleteCustomer_RemovesCustomerFromDatabase()
    {
        // Arrange - Customer 3 has no dogs

        // Act
        await _controller.DeleteCustomer(3);

        // Assert
        var customers = await _context.Customers.ToListAsync();
        customers.Should().HaveCount(2);
        customers.Should().NotContain(c => c.Id == 3);
    }

    // ==================== DATA VALIDATION ====================

    [Fact]
    public async Task GetCustomers_ReturnsCorrectEmailFormats()
    {
        // Act
        var result = await _controller.GetCustomers();

        // Assert
        var customers = result.Value!.ToList();
        customers.Should().OnlyContain(c => c.Email.Contains("@"));
    }

    [Fact]
    public async Task GetCustomers_ReturnsCorrectPhoneFormats()
    {
        // Act
        var result = await _controller.GetCustomers();

        // Assert
        var customers = result.Value!.ToList();
        customers.Should().OnlyContain(c => !string.IsNullOrEmpty(c.Phone));
    }
}
