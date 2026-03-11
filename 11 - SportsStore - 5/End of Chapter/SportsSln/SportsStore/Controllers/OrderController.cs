using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SportsStore.Models;

namespace SportsStore.Controllers {

    public class OrderController : Controller
    {
        private IOrderRepository repository;
        private Cart cart;
        private readonly ILogger<OrderController> logger;

        public OrderController(IOrderRepository repoService, Cart cartService, ILogger<OrderController> loggerService)
        {
            repository = repoService;
            cart = cartService;
            logger = loggerService;
        }

        public ViewResult Checkout()
        {
            logger.LogInformation("Checkout page opened");
            return View(new Order());
        }

        [HttpPost]
        public IActionResult Checkout(Order order)
        {
            logger.LogInformation("Checkout started");

            if (cart.Lines.Count() == 0)
            {
                logger.LogWarning("Checkout failed because the cart was empty");
                ModelState.AddModelError("", "Sorry, your cart is empty!");
            }

            if (ModelState.IsValid)
            {
                order.Lines = cart.Lines.ToArray();

                logger.LogInformation("Saving order with {ItemCount} items", order.Lines.Count);

                repository.SaveOrder(order);

                logger.LogInformation("Order saved successfully with OrderID {OrderId}", order.OrderID);

                cart.Clear();

                logger.LogInformation("Cart cleared after successful checkout");

                return RedirectToPage("/Completed", new { orderId = order.OrderID });
            }
            else
            {
                logger.LogWarning("Checkout failed because the model state was invalid");
                return View();
            }
        }

    }
}
