using FoodExpress.models;

namespace FoodExpress.repositories;

/// <summary>Defines user lookup and persistence operations.</summary>
public interface IUserRepository
{
    IReadOnlyList<User> GetAll();
    User? GetById(string id);
    User? FindByUsername(string username);
    User? FindByEmail(string email);
    void Add(User user);
    void Update(User user);
}

/// <summary>Defines restaurant lookup operations.</summary>
public interface IRestaurantRepository
{
    IReadOnlyList<Restaurant> GetAll();
    Restaurant? GetById(string id);
}

/// <summary>Defines menu item lookup and persistence operations.</summary>
public interface IMenuRepository
{
    IReadOnlyList<MenuItem> GetAll();
    MenuItem? GetById(string id);
    void Save(MenuItem item);
    void Delete(string id);
}

/// <summary>Defines persisted customer cart operations.</summary>
public interface ICartRepository
{
    Cart Load(string userId, IMenuRepository menuRepository);
    void Save(string userId, Cart cart);
    void Clear(string userId);
}

/// <summary>Defines order lookup and persistence operations.</summary>
public interface IOrderRepository
{
    IReadOnlyList<Order> GetAll();
    Order? GetById(string id);
    IReadOnlyList<Order> GetForUser(string userId);
    void Save(Order order);
}

/// <summary>Defines promo-code lookup and persistence operations.</summary>
public interface IPromoCodeRepository
{
    PromoCode? GetByCode(string code);
    IReadOnlyList<PromoCode> GetAll();
    void Save(PromoCode promoCode);
}
