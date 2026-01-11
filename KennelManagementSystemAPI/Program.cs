using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json;
using Amazon;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using KennelManagementSystemAPI.Data;
using KennelManagementSystemAPI.Models;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel to listen on port 5000 for Elastic Beanstalk
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(5000);
});

// Get connection string from AWS Secrets Manager
string connectionString;
try
{
    using var client = new AmazonSecretsManagerClient(RegionEndpoint.USEast1);
    var request = new GetSecretValueRequest { SecretId = "kennel-api/database" };
    var response = await client.GetSecretValueAsync(request);
    var secret = JsonSerializer.Deserialize<Dictionary<string, string>>(response.SecretString);
    connectionString = secret?["ConnectionString"] ?? throw new Exception("ConnectionString not found in secret");
    Console.WriteLine("Successfully loaded connection string from Secrets Manager");
}
catch (Exception ex)
{
    Console.WriteLine($"Failed to get secret, using appsettings: {ex.Message}");
    connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
        ?? throw new Exception("No connection string available");
}

// Add database context - SQL Server for AWS RDS
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// Add Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["Secret"] ?? throw new InvalidOperationException("JWT Secret not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
});

// Add Authorization
builder.Services.AddAuthorization();

// Add CORS for Blazor client
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorClient", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Add Controllers with JSON options to handle circular references
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

builder.Services.AddEndpointsApiExplorer();

// Configure Swagger with JWT support - enabled for all environments
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Kennel Management API", Version = "v1" });
    
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Apply migrations and seed data
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    
    // Create database if it doesn't exist
    context.Database.EnsureCreated();
    
    // Create roles
    string[] roles = { "Admin", "Staff", "Customer" };
    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }
    
    // Create default admin user
    var adminEmail = "admin@kennel.com";
    var adminUser = await userManager.FindByEmailAsync(adminEmail);
    if (adminUser == null)
    {
        adminUser = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            FirstName = "Admin",
            LastName = "User",
            EmailConfirmed = true
        };
        await userManager.CreateAsync(adminUser, "Admin123!");
        await userManager.AddToRoleAsync(adminUser, "Admin");
    }

    // Create default staff user
    var staffEmail = "staff@kennel.com";
    var staffUser = await userManager.FindByEmailAsync(staffEmail);
    if (staffUser == null)
    {
        staffUser = new ApplicationUser
        {
            UserName = staffEmail,
            Email = staffEmail,
            FirstName = "Staff",
            LastName = "User",
            EmailConfirmed = true
        };
        await userManager.CreateAsync(staffUser, "Staff123!");
        await userManager.AddToRoleAsync(staffUser, "Staff");
    }

    // Create default customer user
    var customerEmail = "customer@kennel.com";
    var customerUser = await userManager.FindByEmailAsync(customerEmail);
    if (customerUser == null)
    {
        customerUser = new ApplicationUser
        {
            UserName = customerEmail,
            Email = customerEmail,
            FirstName = "Customer",
            LastName = "User",
            EmailConfirmed = true
        };
        await userManager.CreateAsync(customerUser, "Customer123!");
        await userManager.AddToRoleAsync(customerUser, "Customer");
    }
    
    // Create Customer profile for the seeded customer user
    if (customerUser != null)
    {
        var existingCustomer = await context.Customers.FirstOrDefaultAsync(c => c.UserId == customerUser.Id);
        if (existingCustomer == null)
        {
            var customerProfile = new Customer
            {
                Name = "Customer User",
                Email = customerEmail,
                Phone = "",
                UserId = customerUser.Id
            };
            context.Customers.Add(customerProfile);
            await context.SaveChangesAsync();
        }
    }
}

// Enable Swagger in all environments for API testing
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowBlazorClient");
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();