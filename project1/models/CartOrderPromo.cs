using FoodExpress.exception;

namespace FoodExpress.models;

/// <summary>A cart line whose quantity can only be changed by its cart.</summary>
public sealed class CartItem
{
    internal CartItem(MenuItem item, int quantity)
    {
        Item = item;
        _quantity = quantity;
    }

    private int _quantity;
    public MenuItem Item { get; }
    public int Quantity => _quantity;
    internal void SetQuantity(int quantity) => _quantity = quantity;
}

/// <summary>A customer's mutable shopping cart.</summary>
public sealed class Cart
{
    private readonly List<CartItem> _items = [];
    public IReadOnlyList<CartItem> Items => _items.AsReadOnly();
    public decimal TotalPrice => _items.Sum(item => item.Item.GetFinalPrice() * item.Quantity);

    public void AddItem(MenuItem item, int quantity)
    {
        ArgumentNullException.ThrowIfNull(item);
        ValidateQuantity(quantity);
        if (!item.IsAvailable)
        {
            throw new ItemUnavailableException($"'{item.Name}' is not currently available.");
        }

        CartItem? existing = _items.FirstOrDefault(entry => entry.Item.Id == item.Id);
        if (existing is null)
        {
            _items.Add(new CartItem(item, quantity));
        }
        else
        {
            existing.SetQuantity(checked(existing.Quantity + quantity));
        }
    }

    public void RemoveItem(string itemId)
    {
        CartItem? item = _items.FirstOrDefault(entry => entry.Item.Id == itemId);
        if (item is null)
        {
            throw new ValidationException("That item is not in the cart.");
        }

        _items.Remove(item);
    }

    public void ChangeQuantity(string itemId, int newQuantity)
    {
        ValidateQuantity(newQuantity);
        CartItem? item = _items.FirstOrDefault(entry => entry.Item.Id == itemId);
        if (item is null)
        {
            throw new ValidationException("That item is not in the cart.");
        }

        item.SetQuantity(newQuantity);
    }

    public void Clear() => _items.Clear();

    private static void ValidateQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ValidationException("Quantity must be a positive whole number.");
        }
    }
}

/// <summary>Defines order lifecycle states.</summary>
public enum OrderStatus
{
    Created,
    Confirmed,
    Preparing,
    OnTheWay,
    Delivered,
    Cancelled
}

/// <summary>A durable snapshot of one item in an order.</summary>
public sealed class OrderItem
{
    public OrderItem(string itemId, string name, int quantity, decimal unitPrice)
    {
        if (quantity <= 0 || unitPrice < 0)
        {
            throw new ValidationException("Order item quantity and price are invalid.");
        }

        ItemId = itemId;
        Name = name;
        Quantity = quantity;
        UnitPrice = unitPrice;
    }

    public string ItemId { get; }
    public string Name { get; }
    public int Quantity { get; }
    public decimal UnitPrice { get; }
    public decimal LineTotal => UnitPrice * Quantity;
}

/// <summary>An order with validated status transitions.</summary>
public sealed class Order
{
    public Order(string id, string userId, DateTime createdAt, string address, decimal subtotal, decimal deliveryFee, decimal discount, IEnumerable<OrderItem> items, OrderStatus status = OrderStatus.Created)
    {
        if (subtotal < 0 || deliveryFee < 0 || discount < 0 || discount > subtotal)
        {
            throw new ValidationException("Order totals are invalid.");
        }

        Id = id;
        UserId = userId;
        CreatedAt = createdAt;
        Address = address;
        Subtotal = subtotal;
        DeliveryFee = deliveryFee;
        Discount = discount;
        Total = subtotal + deliveryFee - discount;
        Items = items.ToList();
        Status = status;
    }

    public string Id { get; }
    public string UserId { get; }
    public DateTime CreatedAt { get; }
    public string Address { get; }
    public decimal Subtotal { get; }
    public decimal DeliveryFee { get; }
    public decimal Discount { get; }
    public decimal Total { get; }
    public OrderStatus Status { get; private set; }
    public List<OrderItem> Items { get; }

    public void Confirm() => Transition(OrderStatus.Created, OrderStatus.Confirmed);
    public void StartPreparing() => Transition(OrderStatus.Confirmed, OrderStatus.Preparing);
    public void SendForDelivery() => Transition(OrderStatus.Preparing, OrderStatus.OnTheWay);
    public void CompleteDelivery() => Transition(OrderStatus.OnTheWay, OrderStatus.Delivered);

    public void Cancel()
    {
        if (Status is not (OrderStatus.Created or OrderStatus.Confirmed))
        {
            throw new InvalidOrderStatusTransitionException($"An order in status {Status} cannot be cancelled.");
        }

        Status = OrderStatus.Cancelled;
    }

    private void Transition(OrderStatus expected, OrderStatus next)
    {
        if (Status != expected)
        {
            throw new InvalidOrderStatusTransitionException($"Cannot change order status from {Status} to {next}.");
        }

        Status = next;
    }
}

/// <summary>A discount code, reserved for at most one order.</summary>
public sealed class PromoCode
{
    public PromoCode(string code, bool isPercentage, decimal value, DateTime expiresAt)
    {
        if (value <= 0 || (isPercentage && value > 100))
        {
            throw new ValidationException("Promo code value is invalid.");
        }

        Code = code;
        IsPercentage = isPercentage;
        Value = value;
        ExpiresAt = expiresAt;
    }

    public string Code { get; }
    public bool IsPercentage { get; }
    public decimal Value { get; }
    public DateTime ExpiresAt { get; }
    public string? OrderId { get; private set; }
    public bool IsUsed => OrderId is not null;

    public void MarkUsed(string orderId)
    {
        if (IsUsed)
        {
            throw new PromoCodeException("This promo code has already been used.");
        }

        OrderId = orderId;
    }
}
