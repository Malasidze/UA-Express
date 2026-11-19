namespace FoodExpress.models;

/// <summary>Defines an application user's role.</summary>
public enum Role
{
    Customer,
    Admin
}

/// <summary>Represents an authenticated FoodExpress user.</summary>
public abstract class User
{
    protected User(string id, string username, string passwordHash, string email, Role role)
    {
        Id = id;
        Username = username;
        PasswordHash = passwordHash;
        Email = email;
        Role = role;
    }

    public string Id { get; }
    public string Username { get; }
    public string PasswordHash { get; set; }
    public string Email { get; }
    public Role Role { get; }
}

/// <summary>Represents a customer with a cart and order history.</summary>
public sealed class Customer : User
{
    public Customer(string id, string username, string passwordHash, string email, bool isEmailVerified = false)
        : base(id, username, passwordHash, email, Role.Customer)
    {
        IsEmailVerified = isEmailVerified;
    }

    public Cart Cart { get; } = new();
    public List<Order> Orders { get; } = [];
    public bool IsEmailVerified { get; set; }
}

/// <summary>Represents a menu and order administrator.</summary>
public sealed class Admin : User
{
    public Admin(string id, string username, string passwordHash, string email)
        : base(id, username, passwordHash, email, Role.Admin)
    {
    }
}
