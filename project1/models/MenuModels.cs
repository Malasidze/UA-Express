using FoodExpress.exception;

namespace FoodExpress.models;

/// <summary>A restaurant that offers menu items.</summary>
public sealed class Restaurant
{
    public Restaurant(string id, string name, bool isOpen = true)
    {
        Id = id;
        Name = name;
        IsOpen = isOpen;
    }

    public string Id { get; }
    public string Name { get; set; }
    public bool IsOpen { get; set; }
}

/// <summary>Base type for restaurant menu items.</summary>
public abstract class MenuItem
{
    protected MenuItem(string id, string restaurantId, string name, string category, decimal price, bool isAvailable = true)
    {
        if (price <= 0)
        {
            throw new ValidationException("Price must be greater than zero.");
        }

        Id = id;
        RestaurantId = restaurantId;
        Name = name;
        Category = category;
        Price = price;
        IsAvailable = isAvailable;
    }

    public string Id { get; }
    public string RestaurantId { get; }
    public string Name { get; set; }
    public string Category { get; set; }
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; }
    public abstract string GetDetails();
    public virtual decimal GetFinalPrice() => Price;

    public virtual void DisplayDetails()
    {
        System.Diagnostics.Trace.WriteLine($"{Id}: {Name} ({Category}) - {Price:0.00} ₾ - {GetDetails()}");
    }
}

/// <summary>A pizza menu item.</summary>
public sealed class Pizza : MenuItem
{
    public Pizza(string id, string restaurantId, string name, string category, decimal price, string size, string dough, bool isAvailable = true)
        : base(id, restaurantId, name, category, price, isAvailable)
    {
        Size = size;
        Dough = dough;
    }

    public string Size { get; set; }
    public string Dough { get; set; }
    public override string GetDetails() => $"{Size};{Dough}";
}

/// <summary>A drink menu item.</summary>
public sealed class Drink : MenuItem
{
    public Drink(string id, string restaurantId, string name, string category, decimal price, string volume, bool isAvailable = true)
        : base(id, restaurantId, name, category, price, isAvailable)
    {
        Volume = volume;
    }

    public string Volume { get; set; }
    public override string GetDetails() => Volume;
}

/// <summary>A dessert menu item.</summary>
public sealed class Dessert : MenuItem
{
    public Dessert(string id, string restaurantId, string name, string category, decimal price, string portionSize, bool isAvailable = true)
        : base(id, restaurantId, name, category, price, isAvailable)
    {
        PortionSize = portionSize;
    }

    public string PortionSize { get; set; }
    public override string GetDetails() => PortionSize;
}
