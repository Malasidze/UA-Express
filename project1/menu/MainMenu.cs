using FoodExpress.exception;
using FoodExpress.models;
using FoodExpress.repositories;
using FoodExpress.services;

namespace FoodExpress.menu;

/// <summary>Provides registration, verification, login, and application exit workflows.</summary>
public sealed class MainMenu
{
    private readonly AuthService _auth;
    private readonly EmailVerificationService _verification;
    private readonly IUserRepository _users;
    private readonly CustomerMenu _customerMenu;
    private readonly AdminMenu _adminMenu;
    private readonly LoggerService _logger;

    public MainMenu(AuthService auth, EmailVerificationService verification, IUserRepository users, CustomerMenu customerMenu, AdminMenu adminMenu, LoggerService logger)
    {
        _auth = auth;
        _verification = verification;
        _users = users;
        _customerMenu = customerMenu;
        _adminMenu = adminMenu;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        bool running = true;
        while (running)
        {
            Console.WriteLine("\n=== FoodExpress ===\n1. Register\n2. Verify email\n3. Login\n0. Exit");
            switch (ReadInt("Choose an option: "))
            {
                case 1:
                    await RegisterAsync();
                    break;
                case 2:
                    await VerifyAsync();
                    break;
                case 3:
                    await LoginAsync();
                    break;
                case 0:
                    running = false;
                    break;
                default:
                    Console.WriteLine("Select one of the listed options.");
                    break;
            }
        }
    }

    private async Task RegisterAsync()
    {
        try
        {
            string username = ReadText("Username: ");
            string email = ReadText("Email: ");
            string password = ReadText("Password: ");
            Customer customer = _auth.Register(username, email, password);
            Console.WriteLine("Account created. A verification code is being sent to your email.");
            await _verification.SendCodeAsync(customer);
            Console.WriteLine("Use Verify email from the main menu to activate your account.");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async Task VerifyAsync()
    {
        try
        {
            string username = ReadText("Username: ");
            if (_users.FindByUsername(username) is not Customer customer)
            {
                throw new ValidationException("Customer account was not found.");
            }

            Console.WriteLine("1. Enter verification code\n2. Resend code");
            int choice = ReadInt("Choose an option: ");
            if (choice == 2)
            {
                await _verification.SendCodeAsync(customer, isResend: true);
                Console.WriteLine("A new code has been sent.");
                return;
            }

            _verification.Verify(customer, ReadText("6-digit code: "));
            Console.WriteLine("Email verified. Your account is active.");
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private async Task LoginAsync()
    {
        try
        {
            User user = _auth.Login(ReadText("Email: "), ReadText("Password: "));
            if (user is Customer customer)
            {
                _customerMenu.LoadCart(customer);
                await _customerMenu.RunAsync(customer);
            }
            else if (user is Admin admin)
            {
                await _adminMenu.RunAsync(admin);
            }
        }
        catch (Exception ex)
        {
            ShowError(ex);
        }
    }

    private void ShowError(Exception exception) => Console.WriteLine(ErrorHandler.Handle(exception, _logger));

    private static int ReadInt(string prompt)
    {
        while (true)
        {
            Console.Write(prompt);
            if (int.TryParse(Console.ReadLine(), out int value)) return value;
            Console.WriteLine("Enter a whole number.");
        }
    }

    private static string ReadText(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine() ?? string.Empty;
    }
}
