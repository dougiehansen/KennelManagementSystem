using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.Components.Authorization;
using KennelManagementBlazor;
using KennelManagementBlazor.Services;
using Blazored.LocalStorage;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Register Blazored LocalStorage
builder.Services.AddBlazoredLocalStorage();

// Register HttpClient with base address
builder.Services.AddScoped(sp => new HttpClient 
{ 
     BaseAddress = new Uri("https://d19rs8umoxmnxn.cloudfront.net") 
});

// Register Authentication services
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<CustomAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(provider => 
    provider.GetRequiredService<CustomAuthStateProvider>());

// Register application services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ApiService>();

var host = builder.Build();

// Initialize AuthService on startup
var authService = host.Services.GetRequiredService<AuthService>();
await authService.InitializeAsync();

await host.RunAsync();
