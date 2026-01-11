using System.Net.Http.Headers;
using System.Net.Http.Json;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;

namespace KennelManagementBlazor.Services;

public class AuthService
{
    private readonly HttpClient _httpClient;
    private readonly ILocalStorageService _localStorage;
    private readonly AuthenticationStateProvider _authStateProvider;

    public AuthService(
        HttpClient httpClient, 
        ILocalStorageService localStorage,
        AuthenticationStateProvider authStateProvider)
    {
        _httpClient = httpClient;
        _localStorage = localStorage;
        _authStateProvider = authStateProvider;
    }

    public bool IsAuthenticated { get; private set; }
    public string? UserRole { get; private set; }
    public string? UserEmail { get; private set; }

    public event Action? AuthenticationStateChanged;

    public async Task<bool> Login(string email, string password)
    {
        try
        {
            var loginData = new { email, password };
            var response = await _httpClient.PostAsJsonAsync("api/auth/login", loginData);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
                if (result != null)
                {
                    await _localStorage.SetItemAsync("authToken", result.Token);
                    await _localStorage.SetItemAsync("userRole", result.Role);
                    await _localStorage.SetItemAsync("userEmail", result.Email);

                    _httpClient.DefaultRequestHeaders.Authorization = 
                        new AuthenticationHeaderValue("Bearer", result.Token);

                    IsAuthenticated = true;
                    UserRole = result.Role;
                    UserEmail = result.Email;

                    // Notify the AuthenticationStateProvider
                    ((CustomAuthStateProvider)_authStateProvider).NotifyAuthenticationStateChanged();
                    AuthenticationStateChanged?.Invoke();

                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Login error: {ex.Message}");
            return false;
        }
    }

    public async Task InitializeAsync()
    {
        try
        {
            var token = await _localStorage.GetItemAsync<string>("authToken");
            var role = await _localStorage.GetItemAsync<string>("userRole");
            var email = await _localStorage.GetItemAsync<string>("userEmail");

            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue("Bearer", token);

                IsAuthenticated = true;
                UserRole = role;
                UserEmail = email;

                // Notify the AuthenticationStateProvider
                ((CustomAuthStateProvider)_authStateProvider).NotifyAuthenticationStateChanged();
                AuthenticationStateChanged?.Invoke();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Initialize error: {ex.Message}");
        }
    }

    public async Task Logout()
    {
        await _localStorage.RemoveItemAsync("authToken");
        await _localStorage.RemoveItemAsync("userRole");
        await _localStorage.RemoveItemAsync("userEmail");

        _httpClient.DefaultRequestHeaders.Authorization = null;

        IsAuthenticated = false;
        UserRole = null;
        UserEmail = null;

        // Notify the AuthenticationStateProvider
        ((CustomAuthStateProvider)_authStateProvider).NotifyAuthenticationStateChanged();
        AuthenticationStateChanged?.Invoke();
    }

    public bool IsInRole(params string[] roles)
    {
        return IsAuthenticated && roles.Contains(UserRole);
    }
}

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
