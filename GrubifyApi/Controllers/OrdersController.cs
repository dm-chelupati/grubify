using Microsoft.AspNetCore.Mvc;
using GrubifyApi.Models;

namespace GrubifyApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        // In-memory order storage (in production, use database)
        // WARNING: This static list will grow unbounded and is not suitable for production
        // TODO: Replace with proper database storage or implement cleanup mechanism
        private static readonly List<Order> Orders = new();
        private static int NextOrderId = 1;
        
        // Limit to prevent memory exhaustion in demo environment
        private const int MAX_ORDERS = 10000;

        [HttpPost]
        public ActionResult<Order> PlaceOrder([FromBody] PlaceOrderRequest request)
        {
            // Prevent unbounded growth of orders list in demo environment
            lock (Orders)
            {
                if (Orders.Count >= MAX_ORDERS)
                {
                    // Remove oldest orders to prevent memory exhaustion
                    var oldestOrders = Orders.OrderBy(o => o.OrderDate).Take(1000).ToList();
                    foreach (var oldOrder in oldestOrders)
                    {
                        Orders.Remove(oldOrder);
                    }
                    Console.WriteLine($"Cleaned up old orders. Current count: {Orders.Count}");
                }
            }
            
            var order = new Order
            {
                Id = NextOrderId++,
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

            Orders.Add(order);

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
            var order = Orders.FirstOrDefault(o => o.Id == id);
            if (order == null)
            {
                return NotFound();
            }
            return Ok(order);
        }

        [HttpGet("user/{userId}")]
        public ActionResult<IEnumerable<Order>> GetUserOrders(string userId)
        {
            var userOrders = Orders.Where(o => o.UserId == userId)
                                 .OrderByDescending(o => o.OrderDate)
                                 .ToList();
            return Ok(userOrders);
        }

        [HttpGet("user/{userId}/active")]
        public ActionResult<IEnumerable<Order>> GetActiveUserOrders(string userId)
        {
            var activeOrders = Orders.Where(o => o.UserId == userId && 
                                          o.Status != OrderStatus.Delivered && 
                                          o.Status != OrderStatus.Cancelled)
                                   .OrderByDescending(o => o.OrderDate)
                                   .ToList();
            return Ok(activeOrders);
        }

        [HttpPut("{id}/cancel")]
        public ActionResult<Order> CancelOrder(int id)
        {
            var order = Orders.FirstOrDefault(o => o.Id == id);
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
            return Ok(order);
        }

        [HttpGet("restaurant/{restaurantId}")]
        public ActionResult<IEnumerable<Order>> GetRestaurantOrders(int restaurantId)
        {
            var restaurantOrders = Orders.Where(o => o.RestaurantId == restaurantId)
                                       .OrderByDescending(o => o.OrderDate)
                                       .ToList();
            return Ok(restaurantOrders);
        }

        [HttpPut("{id}/status")]
        public ActionResult<Order> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusRequest request)
        {
            var order = Orders.FirstOrDefault(o => o.Id == id);
            if (order == null)
            {
                return NotFound();
            }

            order.Status = request.Status;
            if (request.Status == OrderStatus.Delivered)
            {
                order.DeliveryTime = DateTime.UtcNow;
            }

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
