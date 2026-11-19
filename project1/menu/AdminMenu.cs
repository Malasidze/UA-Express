using System.Globalization;
using FoodExpress.exception;
using FoodExpress.models;
using FoodExpress.repositories;
using FoodExpress.services;

namespace FoodExpress.menu;

/// <summary>Provides administrator menu, order lifecycle, and reporting workflows.</summary>
public sealed class AdminMenu
{
    private readonly IMenuRepository _items;
    private readonly IOrderRepository _ordersRepository;
    private readonly IRestaurantRepository _restaurants;
    private readonly MenuService _menuService;
    private readonly OrderService _orders;
    private readonly MenuQueryService _queries;
    private readonly LoggerService _logger;

    public AdminMenu(IMenuRepository items, IOrderRepository ordersRepository, IRestaurantRepository restaurants, MenuService menuService, OrderService orders, MenuQueryService queries, LoggerService logger)
    {
        _items = items;
        _ordersRepository = ordersRepository;
        _restaurants = restaurants;
        _menuService = menuService;
        _orders = orders;
        _queries = queries;
        _logger = logger;
    }

    public async Task RunAsync(Admin admin)
    {
        bool running = true;
        while (running)
        {
            Console.WriteLine($"\n=== Admin: {admin.Username} ===\n1. Manage menu\n2. View active orders\n3. Advance order status\n4. Statistics\n0. Logout");
            switch (ReadInt("Choose an option: "))
            {
                case 1: ManageMenu(admin); break;
                case 2: ViewActiveOrders(admin); break;
                case 3: AdvanceOrder(admin); break;
                case 4: ShowStatistics(); break;
                case 0: running = false; break;
                default: Console.WriteLine("Select one of the listed options."); break;
            }
        }
        await Task.CompletedTask;
    }

    private void ManageMenu(Admin admin)
    {
        bool back = false;
        while (!back)
        {
            Console.WriteLine("\nMenu items:");
            PrintItems(_items.GetAll());
            Console.WriteLine("1. Add item\n2. Edit item\n3. Toggle availability\n4. Delete item\n0. Back");
            try
            {
                switch (ReadInt("Choose an option: "))
                {
                    case 1: AddItem(admin); break;
                    case 2: EditItem(admin); break;
                    case 3: ToggleAvailability(admin); break;
                    case 4: _menuService.Delete(admin, ReadText("Item ID to delete: ")); break;
                    case 0: back = true; break;
                    default: Console.WriteLine("Select one of the listed options."); break;
                }
            }
            catch (Exception ex) { ShowError(ex); }
        }
    }

    private void AddItem(Admin admin)
    {
        string id = ReadText("New item ID: ");
        string restaurantId = ReadText($"Restaurant ID ({string.Join(", ", _restaurants.GetAll().Select(item => item.Id))}): ");
        if (_restaurants.GetById(restaurantId) is null) throw new ValidationException("Restaurant was not found.");
        string kind = ReadText("Type (Pizza, Drink, Dessert): ");
        string name = ReadText("Name: ");
        string category = ReadText("Category: ");
        decimal price = ReadDecimal("Price: ");
        MenuItem item = kind.Trim().ToLowerInvariant() switch
        {
            "pizza" => new Pizza(id, restaurantId, name, category, price, ReadText("Size: "), ReadText("Dough: ")),
            "drink" => new Drink(id, restaurantId, name, category, price, ReadText("Volume (e.g. 500ml): ")),
            "dessert" => new Dessert(id, restaurantId, name, category, price, ReadText("Portion size: ")),
            _ => throw new ValidationException("Choose Pizza, Drink, or Dessert.")
        };
        _menuService.Add(admin, item);
        Console.WriteLine("Menu item added.");
    }

    private void EditItem(Admin admin)
    {
        string id = ReadText("Item ID to edit: ");
        MenuItem item = _items.GetById(id) ?? throw new ValidationException("Menu item was not found.");
        string name = ReadText($"Name [{item.Name}]: ");
        if (string.IsNullOrWhiteSpace(name)) name = item.Name;
        string priceText = ReadText($"Price [{item.Price:0.00}]: ");
        decimal price = string.IsNullOrWhiteSpace(priceText) ? item.Price : ParseDecimal(priceText);
        bool available = ReadYesNo($"Available? (y/n) [{(item.IsAvailable ? "y" : "n")}]: ", item.IsAvailable);
        _menuService.Update(admin, id, name, price, available);
        Console.WriteLine("Menu item updated.");
    }

    private void ToggleAvailability(Admin admin)
    {
        string id = ReadText("Item ID to toggle: ");
        MenuItem item = _items.GetById(id) ?? throw new ValidationException("Menu item was not found.");
        _menuService.Update(admin, id, item.Name, item.Price, !item.IsAvailable);
        Console.WriteLine($"Item is now {(item.IsAvailable ? "available" : "unavailable")}.");
    }

    private void ViewActiveOrders(Admin admin)
    {
        try
        {
            IReadOnlyList<Order> active = _orders.GetAllOrders(admin).Where(order => order.Status is not (OrderStatus.Delivered or OrderStatus.Cancelled)).OrderBy(order => order.CreatedAt).ToList();
            if (active.Count == 0) Console.WriteLine("No active orders.");
            foreach (Order order in active) PrintOrder(order);
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void AdvanceOrder(Admin admin)
    {
        try
        {
            IReadOnlyList<Order> active = _orders.GetAllOrders(admin).Where(order => order.Status is not (OrderStatus.Delivered or OrderStatus.Cancelled)).ToList();
            foreach (Order order in active) PrintOrder(order);
            string id = ReadText("Order ID: ");
            Order orderToChange = active.FirstOrDefault(order => order.Id == id) ?? throw new ValidationException("Active order was not found.");
            switch (orderToChange.Status)
            {
                case OrderStatus.Created:
                    Console.WriteLine("Next action: confirm");
                    if (ReadYesNo("Confirm this order? (y/n): ", false)) _orders.ConfirmOrder(admin, id);
                    break;
                case OrderStatus.Confirmed:
                    Console.WriteLine("Next action: start preparing");
                    if (ReadYesNo("Start preparing? (y/n): ", false)) _orders.StartPreparing(admin, id);
                    break;
                case OrderStatus.Preparing:
                    Console.WriteLine("Next action: send for delivery");
                    if (ReadYesNo("Send for delivery? (y/n): ", false)) _orders.SendForDelivery(admin, id);
                    break;
                case OrderStatus.OnTheWay:
                    Console.WriteLine("Next action: complete delivery");
                    if (ReadYesNo("Mark delivered? (y/n): ", false)) _orders.CompleteDelivery(admin, id);
                    break;
            }
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void ShowStatistics()
    {
        try
        {
            IReadOnlyList<ItemSales> sales = _queries.GetSalesStatistics();
            ItemSales? best = sales.OrderByDescending(item => item.Quantity).FirstOrDefault();
            int orderCount = _ordersRepository.GetAll().Count(order => order.Status != OrderStatus.Cancelled);
            decimal revenue = _ordersRepository.GetAll().Where(order => order.Status != OrderStatus.Cancelled).Sum(order => order.Total);
            Console.WriteLine($"Best-selling item: {(best is null ? "No sales" : $"{best.Name} ({best.Quantity} sold)")}");
            Console.WriteLine($"Orders: {orderCount} | Revenue: {revenue:0.00} ₾");
            Console.WriteLine("Sales per item:");
            foreach (ItemSales item in sales)
                Console.WriteLine($"{item.ItemId} | {item.Name} | quantity {item.Quantity} | order lines {item.OrderLineCount} | revenue {item.Revenue:0.00} ₾");
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private static void PrintItems(IEnumerable<MenuItem> items)
    {
        foreach (MenuItem item in items)
            Console.WriteLine($"{item.Id} | {item.RestaurantId} | {item.GetType().Name} | {item.Name} | {item.Category} | {item.GetDetails()} | {item.Price:0.00} ₾ | {(item.IsAvailable ? "available" : "disabled")}");
    }

    private static void PrintOrder(Order order) => Console.WriteLine($"{order.Id} | user {order.UserId} | {order.CreatedAt:g} | {order.Status} | {order.Total:0.00} ₾ | {order.Address}");
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

    private static decimal ReadDecimal(string prompt)
    {
        while (true)
        {
            Console.Write(prompt);
            if (decimal.TryParse(Console.ReadLine(), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value)) return value;
            Console.WriteLine("Enter a valid decimal amount.");
        }
    }

    private static decimal ParseDecimal(string text) => decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value) ? value : throw new ValidationException("Enter a valid decimal price.");

    private static bool ReadYesNo(string prompt, bool defaultValue)
    {
        while (true)
        {
            Console.Write(prompt);
            string value = Console.ReadLine() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(value)) return defaultValue;
            if (value.Equals("y", StringComparison.OrdinalIgnoreCase) || value.Equals("yes", StringComparison.OrdinalIgnoreCase)) return true;
            if (value.Equals("n", StringComparison.OrdinalIgnoreCase) || value.Equals("no", StringComparison.OrdinalIgnoreCase)) return false;
            Console.WriteLine("Enter y or n.");
        }
    }

    private static string ReadText(string prompt)
    {
        Console.Write(prompt);
        return Console.ReadLine() ?? string.Empty;
    }
}
