using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using GrubifyApi.Models;

namespace GrubifyApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly IMemoryCache _memoryCache;
        private static readonly TimeSpan OrderCacheExpiration = TimeSpan.FromDays(7);
        private const string OrdersCacheKey = "all_orders";
        private const string OrderIdCounterCacheKey = "order_id_counter";

        public OrdersController(IMemoryCache memoryCache)
        {
            _memoryCache = memoryCache;
        }

        private List<Order> GetOrders()
        {
            if (!_memoryCache.TryGetValue(OrdersCacheKey, out List<Order>? orders))
            {
                orders = new List<Order>();
                var cacheEntryOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = OrderCacheExpiration,
                    Size = 1
                };
                _memoryCache.Set(OrdersCacheKey, orders, cacheEntryOptions);
            }
            return orders;
        }

        private int GetNextOrderId()
        {
            if (!_memoryCache.TryGetValue(OrderIdCounterCacheKey, out int nextId))
            {
                nextId = 1;
            }
            var newId = nextId + 1;
            _memoryCache.Set(OrderIdCounterCacheKey, newId);
            return nextId;
        }

        [HttpPost]
        public ActionResult<Order> PlaceOrder([FromBody] PlaceOrderRequest request)
        {
            var orders = GetOrders();
            var order = new Order
            {
                Id = GetNextOrderId(),
                UserId = request.UserId,
                RestaurantId = request.RestaurantId,
                Items = request.Items.Select(item => new CartItem
                {
                    Id = item.Id,
                    FoodItemId = item.FoodItemId,
                    FoodItem = item.FoodItem,
                    Quantity = item.Quantity,
                    SpecialInstructions = item.SpecialInstructions
                }).ToList(),
                Status = OrderStatus.Placed,
                OrderDate = DateTime.UtcNow,
                DeliveryAddress = request.DeliveryAddress,
                PaymentMethod = request.PaymentMethod,
                SpecialInstructions = request.SpecialInstructions
            };

            orders.Add(order);

            // Update cache
            var cacheEntryOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = OrderCacheExpiration,
                Size = 1
            };
            _memoryCache.Set(OrdersCacheKey, orders, cacheEntryOptions);

            // Simulate order status updates after placement
            Task.Run(async () =>
            {
                await Task.Delay(30000); // 30 seconds
                order.Status = OrderStatus.Confirmed;
                
                await Task.Delay(600000); // 10 minutes
                order.Status = OrderStatus.Preparing;
                
                await Task.Delay(900000); // 15 minutes
                order.Status = OrderStatus.OutForDelivery;
                
                await Task.Delay(600000); // 10 minutes
                order.Status = OrderStatus.Delivered;
                order.DeliveryTime = DateTime.UtcNow;
            });

            return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
        }

        [HttpGet("{id}")]
        public ActionResult<Order> GetOrder(int id)
        {
            var orders = GetOrders();
            var order = orders.FirstOrDefault(o => o.Id == id);
            if (order == null)
            {
                return NotFound();
            }
            return Ok(order);
        }

        [HttpGet("user/{userId}")]
        public ActionResult<IEnumerable<Order>> GetUserOrders(string userId)
        {
            var orders = GetOrders();
            var userOrders = orders.Where(o => o.UserId == userId)
                                 .OrderByDescending(o => o.OrderDate)
                                 .ToList();
            return Ok(userOrders);
        }

        [HttpGet("user/{userId}/active")]
        public ActionResult<IEnumerable<Order>> GetActiveUserOrders(string userId)
        {
            var orders = GetOrders();
            var activeOrders = orders.Where(o => o.UserId == userId && 
                                          o.Status != OrderStatus.Delivered && 
                                          o.Status != OrderStatus.Cancelled)
                                   .OrderByDescending(o => o.OrderDate)
                                   .ToList();
            return Ok(activeOrders);
        }

        [HttpPut("{id}/cancel")]
        public ActionResult<Order> CancelOrder(int id)
        {
            var orders = GetOrders();
            var order = orders.FirstOrDefault(o => o.Id == id);
            if (order == null)
            {
                return NotFound();
            }

            if (order.Status == OrderStatus.Preparing || 
                order.Status == OrderStatus.OutForDelivery)
            {
                return BadRequest("Cannot cancel order that is already being prepared or delivered");
            }

            order.Status = OrderStatus.Cancelled;

            // Update cache
            var cacheEntryOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = OrderCacheExpiration,
                Size = 1
            };
            _memoryCache.Set(OrdersCacheKey, orders, cacheEntryOptions);

            return Ok(order);
        }

        [HttpGet("restaurant/{restaurantId}")]
        public ActionResult<IEnumerable<Order>> GetRestaurantOrders(int restaurantId)
        {
            var orders = GetOrders();
            var restaurantOrders = orders.Where(o => o.RestaurantId == restaurantId)
                                       .OrderByDescending(o => o.OrderDate)
                                       .ToList();
            return Ok(restaurantOrders);
        }

        [HttpPut("{id}/status")]
        public ActionResult<Order> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusRequest request)
        {
            var orders = GetOrders();
            var order = orders.FirstOrDefault(o => o.Id == id);
            if (order == null)
            {
                return NotFound();
            }

            order.Status = request.Status;
            if (request.Status == OrderStatus.Delivered)
            {
                order.DeliveryTime = DateTime.UtcNow;
            }

            // Update cache
            var cacheEntryOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = OrderCacheExpiration,
                Size = 1
            };
            _memoryCache.Set(OrdersCacheKey, orders, cacheEntryOptions);

            return Ok(order);
        }
    }

    public class PlaceOrderRequest
    {
        public string UserId { get; set; } = string.Empty;
        public int RestaurantId { get; set; }
        public List<CartItem> Items { get; set; } = new();
        public string DeliveryAddress { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = string.Empty;
        public string SpecialInstructions { get; set; } = string.Empty;
    }

    public class UpdateOrderStatusRequest
    {
        public OrderStatus Status { get; set; }
    }
}
