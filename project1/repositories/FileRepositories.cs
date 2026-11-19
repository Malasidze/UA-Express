using System.Globalization;
using FoodExpress.data;
using FoodExpress.exception;
using FoodExpress.models;

namespace FoodExpress.repositories;

/// <summary>File-backed user repository with an in-memory cache.</summary>
public sealed class FileUserRepository : IUserRepository
{
    private readonly FileManager _files;
    private readonly Action<string>? _log;
    private readonly List<User> _users = [];
    private readonly HashSet<string> _unverifiedIds = new(StringComparer.Ordinal);

    public FileUserRepository(FileManager files, Action<string>? log = null)
    {
        _files = files;
        _log = log;
        Load();
    }

    public IReadOnlyList<User> GetAll() => _users.ToList();
    public User? GetById(string id) => _users.FirstOrDefault(user => user.Id == id);
    public User? FindByUsername(string username) => _users.FirstOrDefault(user => string.Equals(user.Username, username, StringComparison.OrdinalIgnoreCase));
    public User? FindByEmail(string email) => _users.FirstOrDefault(user => string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase));

    public void Add(User user)
    {
        if (FindByUsername(user.Username) is not null || FindByEmail(user.Email) is not null)
        {
            throw new ValidationException("That username or email address is already registered.");
        }

        _users.Add(user);
        Persist();
        UpdateVerification(user);
    }

    public void Update(User user)
    {
        int index = _users.FindIndex(existing => existing.Id == user.Id);
        if (index < 0)
        {
            throw new ValidationException("User was not found.");
        }

        if (_users.Any(existing => existing.Id != user.Id && (string.Equals(existing.Username, user.Username, StringComparison.OrdinalIgnoreCase) || string.Equals(existing.Email, user.Email, StringComparison.OrdinalIgnoreCase))))
        {
            throw new ValidationException("That username or email address is already registered.");
        }

        _users[index] = user;
        Persist();
        UpdateVerification(user);
    }

    private void Load()
    {
        bool changed = false;
        foreach (string line in _files.ReadLines(FileManager.EmailVerificationFile))
        {
            if (!string.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith("//", StringComparison.Ordinal))
            {
                _unverifiedIds.Add(line.Trim());
            }
        }

        foreach (string line in _files.ReadLines(FileManager.UsersFile))
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("//", StringComparison.Ordinal))
            {
                continue;
            }

            try
            {
                string[] fields = Split(line, 5);
                string id = fields[0];
                string username = fields[1];
                string password = fields[2];
                string email = fields[3];
                if (!password.StartsWith("$2", StringComparison.Ordinal))
                {
                    password = BCrypt.Net.BCrypt.HashPassword(password);
                    changed = true;
                }

                User user = fields[4].Trim().Equals("admin", StringComparison.OrdinalIgnoreCase)
                    ? new Admin(id, username, password, email)
                    : new Customer(id, username, password, email, isEmailVerified: !_unverifiedIds.Contains(id));
                _users.Add(user);
            }
            catch (Exception ex)
            {
                _log?.Invoke($"Skipped malformed Users.txt row '{line}': {ex.Message}");
            }
        }

        if (changed)
        {
            Persist();
        }
    }

    private void Persist() => _files.WriteLines(FileManager.UsersFile,
        new[] { "// ID | Username | Password | Email | Role" }.Concat(_users.Select(user =>
            $"{user.Id} | {user.Username} | {user.PasswordHash} | {user.Email} | {user.Role.ToString().ToLowerInvariant()}")));

    private void UpdateVerification(User user)
    {
        if (user is not Customer customer) return;
        if (customer.IsEmailVerified) _unverifiedIds.Remove(customer.Id);
        else _unverifiedIds.Add(customer.Id);
        _files.WriteLines(FileManager.EmailVerificationFile, new[] { "// UserID" }.Concat(_unverifiedIds));
    }

    internal static string[] Split(string line, int expectedCount)
    {
        string[] fields = line.Split('|').Select(field => field.Trim()).ToArray();
        if (fields.Length != expectedCount)
        {
            throw new FormatException($"Expected {expectedCount} pipe-delimited fields.");
        }

        return fields;
    }

    internal static decimal ParseMoney(string value) => decimal.Parse(value.Replace("₾", string.Empty, StringComparison.Ordinal).Trim(), CultureInfo.InvariantCulture);
    internal static string Money(decimal value) => $"{value.ToString("0.00", CultureInfo.InvariantCulture)} ₾";
}

/// <summary>File-backed restaurant repository with an in-memory cache.</summary>
public sealed class FileRestaurantRepository : IRestaurantRepository
{
    private readonly List<Restaurant> _restaurants = [];

    public FileRestaurantRepository(FileManager files, Action<string>? log = null)
    {
        foreach (string line in files.ReadLines(FileManager.RestaurantsFile))
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("//", StringComparison.Ordinal)) continue;
            try
            {
                string[] fields = FileUserRepository.Split(line, 3);
                if (!bool.TryParse(fields[2], out bool isOpen)) throw new FormatException("Invalid restaurant open state.");
                _restaurants.Add(new Restaurant(fields[0], fields[1], isOpen));
            }
            catch (Exception ex)
            {
                log?.Invoke($"Skipped malformed Restaurants.txt row '{line}': {ex.Message}");
            }
        }
    }

    public IReadOnlyList<Restaurant> GetAll() => _restaurants.ToList();
    public Restaurant? GetById(string id) => _restaurants.FirstOrDefault(restaurant => restaurant.Id == id);
}

/// <summary>File-backed menu repository with an in-memory cache.</summary>
public sealed class FileMenuRepository : IMenuRepository
{
    private readonly FileManager _files;
    private readonly Action<string>? _log;
    private readonly List<MenuItem> _items = [];

    public FileMenuRepository(FileManager files, Action<string>? log = null)
    {
        _files = files;
        _log = log;
        Load();
    }

    public IReadOnlyList<MenuItem> GetAll() => _items.ToList();
    public MenuItem? GetById(string id) => _items.FirstOrDefault(item => item.Id == id);

    public void Save(MenuItem item)
    {
        int index = _items.FindIndex(existing => existing.Id == item.Id);
        if (index < 0) _items.Add(item);
        else _items[index] = item;
        Persist();
    }

    public void Delete(string id)
    {
        _items.RemoveAll(item => item.Id == id);
        Persist();
    }

    private void Load()
    {
        foreach (string line in _files.ReadLines(FileManager.MenuItemsFile))
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("//", StringComparison.Ordinal)) continue;
            try
            {
                string[] fields = FileUserRepository.Split(line, 8);
                decimal price = FileUserRepository.ParseMoney(fields[6]);
                if (!bool.TryParse(fields[7], out bool available)) throw new FormatException("Invalid availability value.");
                MenuItem item = fields[2].ToLowerInvariant() switch
                {
                    "pizza" => CreatePizza(fields, price, available),
                    "drink" => new Drink(fields[0], fields[1], fields[3], fields[4], price, fields[5], available),
                    "dessert" => new Dessert(fields[0], fields[1], fields[3], fields[4], price, fields[5], available),
                    _ => throw new FormatException("Unknown menu item type.")
                };
                _items.Add(item);
            }
            catch (Exception ex)
            {
                _log?.Invoke($"Skipped malformed MenuItems.txt row '{line}': {ex.Message}");
            }
        }
    }

    private static Pizza CreatePizza(string[] fields, decimal price, bool available)
    {
        string[] details = fields[5].Split(';');
        if (details.Length != 2) throw new FormatException("Pizza details must contain size and dough.");
        return new Pizza(fields[0], fields[1], fields[3], fields[4], price, details[0], details[1], available);
    }

    private void Persist() => _files.WriteLines(FileManager.MenuItemsFile,
        new[] { "// ItemID | RestaurantID | Type | Name | Category | Details | Price | IsAvailable" }
        .Concat(_items.Select(item => $"{item.Id} | {item.RestaurantId} | {item.GetType().Name} | {item.Name} | {item.Category} | {item.GetDetails()} | {FileUserRepository.Money(item.Price)} | {item.IsAvailable.ToString().ToLowerInvariant()}")));
}

/// <summary>File-backed cart repository.</summary>
public sealed class FileCartRepository : ICartRepository
{
    private readonly FileManager _files;
    private readonly Action<string>? _log;

    public FileCartRepository(FileManager files, Action<string>? log = null)
    {
        _files = files;
        _log = log;
    }

    public Cart Load(string userId, IMenuRepository menuRepository)
    {
        Cart cart = new();
        foreach (string line in _files.ReadLines(FileManager.CartsFile))
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("//", StringComparison.Ordinal)) continue;
            try
            {
                string[] fields = FileUserRepository.Split(line, 3);
                if (fields[0] != userId) continue;
                if (!int.TryParse(fields[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int quantity)) throw new FormatException("Invalid cart quantity.");
                MenuItem? item = menuRepository.GetById(fields[1]);
                if (item is null) throw new FormatException("Cart references an unknown menu item.");
                cart.AddItem(item, quantity);
            }
            catch (Exception ex)
            {
                _log?.Invoke($"Skipped malformed Carts.txt row '{line}': {ex.Message}");
            }
        }

        return cart;
    }

    public void Save(string userId, Cart cart)
    {
        List<string> rows = _files.ReadLines(FileManager.CartsFile)
            .Where(line => string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("//", StringComparison.Ordinal) || !string.Equals(line.Split('|')[0].Trim(), userId, StringComparison.Ordinal))
            .ToList();
        if (rows.Count == 0 || !rows.Any(line => line.TrimStart().StartsWith("//", StringComparison.Ordinal))) rows.Insert(0, "// UserID | ItemID | Quantity");
        rows.AddRange(cart.Items.Select(item => $"{userId} | {item.Item.Id} | {item.Quantity}"));
        _files.WriteLines(FileManager.CartsFile, rows);
    }

    public void Clear(string userId) => Save(userId, new Cart());
}

/// <summary>File-backed order repository with an in-memory cache.</summary>
public sealed class FileOrderRepository : IOrderRepository
{
    private readonly FileManager _files;
    private readonly Action<string>? _log;
    private readonly List<Order> _orders = [];

    public FileOrderRepository(FileManager files, Action<string>? log = null)
    {
        _files = files;
        _log = log;
        Load();
    }

    public IReadOnlyList<Order> GetAll() => _orders.ToList();
    public Order? GetById(string id) => _orders.FirstOrDefault(order => order.Id == id);
    public IReadOnlyList<Order> GetForUser(string userId) => _orders.Where(order => order.UserId == userId).ToList();

    public void Save(Order order)
    {
        int index = _orders.FindIndex(existing => existing.Id == order.Id);
        if (index < 0) _orders.Add(order);
        else _orders[index] = order;
        Persist();
    }

    private void Load()
    {
        Dictionary<string, List<OrderItem>> itemsByOrder = new(StringComparer.Ordinal);
        foreach (string line in _files.ReadLines(FileManager.OrderItemsFile))
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("//", StringComparison.Ordinal)) continue;
            try
            {
                string[] fields = FileUserRepository.Split(line, 6);
                if (!int.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int quantity)) throw new FormatException("Invalid order item quantity.");
                OrderItem item = new(fields[1], fields[2], quantity, FileUserRepository.ParseMoney(fields[4]));
                if (!itemsByOrder.TryGetValue(fields[0], out List<OrderItem>? orderItems)) itemsByOrder[fields[0]] = orderItems = [];
                orderItems.Add(item);
            }
            catch (Exception ex)
            {
                _log?.Invoke($"Skipped malformed OrderItems.txt row '{line}': {ex.Message}");
            }
        }

        foreach (string line in _files.ReadLines(FileManager.OrdersFile))
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("//", StringComparison.Ordinal)) continue;
            try
            {
                string[] fields = FileUserRepository.Split(line, 9);
                DateTime createdAt = DateTime.ParseExact(fields[2], "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
                if (!Enum.TryParse(fields[8], true, out OrderStatus status)) throw new FormatException("Invalid order status.");
                itemsByOrder.TryGetValue(fields[0], out List<OrderItem>? orderItems);
                _orders.Add(new Order(fields[0], fields[1], createdAt, fields[3], FileUserRepository.ParseMoney(fields[4]), FileUserRepository.ParseMoney(fields[5]), FileUserRepository.ParseMoney(fields[6]), orderItems ?? [], status));
            }
            catch (Exception ex)
            {
                _log?.Invoke($"Skipped malformed Orders.txt row '{line}': {ex.Message}");
            }
        }
    }

    private void Persist()
    {
        _files.WriteLines(FileManager.OrdersFile,
            new[] { "// OrderID | UserID | CreatedAt | Address | Subtotal | DeliveryFee | Discount | Total | Status" }
            .Concat(_orders.Select(order => $"{order.Id} | {order.UserId} | {order.CreatedAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} | {order.Address} | {FileUserRepository.Money(order.Subtotal)} | {FileUserRepository.Money(order.DeliveryFee)} | {FileUserRepository.Money(order.Discount)} | {FileUserRepository.Money(order.Total)} | {order.Status}")));
        _files.WriteLines(FileManager.OrderItemsFile,
            new[] { "// OrderID | ItemID | Name | Quantity | UnitPrice | LineTotal" }
            .Concat(_orders.SelectMany(order => order.Items.Select(item => $"{order.Id} | {item.ItemId} | {item.Name} | {item.Quantity} | {FileUserRepository.Money(item.UnitPrice)} | {FileUserRepository.Money(item.LineTotal)}"))));
    }
}

/// <summary>File-backed promo-code repository.</summary>
public sealed class FilePromoCodeRepository : IPromoCodeRepository
{
    private readonly FileManager _files;
    private readonly Action<string>? _log;
    private readonly List<PromoCode> _promoCodes = [];

    public FilePromoCodeRepository(FileManager files, Action<string>? log = null)
    {
        _files = files;
        _log = log;
        foreach (string line in files.ReadLines(FileManager.PromoCodesFile))
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("//", StringComparison.Ordinal)) continue;
            try
            {
                string[] fields = FileUserRepository.Split(line, 5);
                if (!bool.TryParse(fields[1], out bool isPercentage)) throw new FormatException("Invalid promo type.");
                PromoCode promo = new(fields[0], isPercentage, decimal.Parse(fields[2], CultureInfo.InvariantCulture), DateTime.ParseExact(fields[3], "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
                if (!string.IsNullOrWhiteSpace(fields[4])) promo.MarkUsed(fields[4]);
                _promoCodes.Add(promo);
            }
            catch (Exception ex)
            {
                _log?.Invoke($"Skipped malformed PromoCodes.txt row '{line}': {ex.Message}");
            }
        }
    }

    public PromoCode? GetByCode(string code) => _promoCodes.FirstOrDefault(promo => string.Equals(promo.Code, code, StringComparison.OrdinalIgnoreCase));
    public IReadOnlyList<PromoCode> GetAll() => _promoCodes.ToList();

    public void Save(PromoCode promoCode)
    {
        int index = _promoCodes.FindIndex(existing => string.Equals(existing.Code, promoCode.Code, StringComparison.OrdinalIgnoreCase));
        if (index < 0) _promoCodes.Add(promoCode);
        else _promoCodes[index] = promoCode;
        _files.WriteLines(FileManager.PromoCodesFile,
            new[] { "// Code | IsPercentage | Value | ExpiresAt | OrderID" }
            .Concat(_promoCodes.Select(promo => $"{promo.Code} | {promo.IsPercentage.ToString().ToLowerInvariant()} | {promo.Value.ToString(CultureInfo.InvariantCulture)} | {promo.ExpiresAt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} | {promo.OrderId ?? string.Empty}")));
    }
}
