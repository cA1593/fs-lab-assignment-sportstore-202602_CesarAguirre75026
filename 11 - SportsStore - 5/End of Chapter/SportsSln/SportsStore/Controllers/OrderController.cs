using Microsoft.AspNetCore.Mvc;
using SportsStore.Models;
using SportsStore.Services;
using Stripe.Checkout;

namespace SportsStore.Controllers
{
    public class OrderController : Controller
    {
        private readonly IOrderRepository repository;
        private readonly Cart cart;
        private readonly ILogger<OrderController> logger;
        private readonly IStripePaymentService stripeService;

        public OrderController(
            IOrderRepository repoService,
            Cart cartService,
            ILogger<OrderController> loggerService,
            IStripePaymentService stripePaymentService)
        {
            repository = repoService;
            cart = cartService;
            logger = loggerService;
            stripeService = stripePaymentService;
        }

        public ViewResult Checkout()
        {
            logger.LogInformation("Checkout page accessed");
            return View(new Order());
        }

        [HttpPost]
        public async Task<IActionResult> Checkout(Order order)
        {
            logger.LogInformation(
                "Checkout submitted for customer {Name}, {Line1}, {City}",
                order.Name, order.Line1, order.City);

            if (!cart.Lines.Any())
            {
                logger.LogWarning("Checkout attempted with empty cart by {Name}", order.Name);
                ModelState.AddModelError("", "Sorry, your cart is empty!");
            }

            if (!ModelState.IsValid)
            {
                logger.LogWarning("Order checkout failed validation for {Name}", order.Name);
                return View(order);
            }

            try
            {
                order.Lines = cart.Lines.ToArray();
                order.PaymentStatus = "Pending";

                string successUrl = Url.Action(
                    "PaymentSuccess",
                    "Order",
                    null,
                    Request.Scheme)!;

                string cancelUrl = Url.Action(
                    "PaymentCancel",
                    "Order",
                    null,
                    Request.Scheme)!;

                logger.LogInformation(
                    "Creating Stripe Checkout Session for customer {Name} with {ItemCount} items",
                    order.Name, order.Lines.Count);

                var session = await stripeService.CreateCheckoutSessionAsync(order, cart, successUrl, cancelUrl);

                logger.LogInformation(
                    "Stripe Checkout Session created with SessionId {SessionId} for customer {Name}",
                    session.Id, order.Name);

                TempData["PendingOrderName"] = order.Name;
                TempData["PendingOrderLine1"] = order.Line1;
                TempData["PendingOrderLine2"] = order.Line2;
                TempData["PendingOrderLine3"] = order.Line3;
                TempData["PendingOrderCity"] = order.City;
                TempData["PendingOrderState"] = order.State;
                TempData["PendingOrderZip"] = order.Zip;
                TempData["PendingOrderCountry"] = order.Country;
                TempData["PendingOrderGiftWrap"] = order.GiftWrap.ToString();
                TempData["StripeSessionId"] = session.Id;

                logger.LogInformation(
                    "Redirecting customer {Name} to Stripe Checkout page with SessionId {SessionId}",
                    order.Name, session.Id);

                return Redirect(session.Url!);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to start Stripe checkout for customer {Name}", order.Name);
                ModelState.AddModelError("", "There was an error processing your payment. Please try again.");
                return View(order);
            }
        }

        public IActionResult PaymentCancel()
        {
            logger.LogWarning("Stripe payment was cancelled by the user");
            return View("Checkout", new Order());
        }

        public IActionResult PaymentSuccess()
        {
            string? sessionId = TempData["StripeSessionId"]?.ToString();

            logger.LogInformation(
                "Stripe payment success callback reached for SessionId {SessionId}",
                sessionId);

            if (string.IsNullOrEmpty(sessionId))
            {
                logger.LogWarning("Stripe payment success callback failed because no session id was found in TempData");
                return RedirectToAction(nameof(PaymentCancel));
            }

            try
            {
                var sessionService = new SessionService();
                var session = sessionService.Get(sessionId);

                if (session.PaymentStatus != "paid")
                {
                    logger.LogWarning(
                        "Stripe session {SessionId} returned unpaid status {PaymentStatus}",
                        session.Id, session.PaymentStatus);

                    return RedirectToAction(nameof(PaymentCancel));
                }

                var order = new Order
                {
                    Name = TempData["PendingOrderName"]?.ToString(),
                    Line1 = TempData["PendingOrderLine1"]?.ToString(),
                    Line2 = TempData["PendingOrderLine2"]?.ToString(),
                    Line3 = TempData["PendingOrderLine3"]?.ToString(),
                    City = TempData["PendingOrderCity"]?.ToString(),
                    State = TempData["PendingOrderState"]?.ToString(),
                    Zip = TempData["PendingOrderZip"]?.ToString(),
                    Country = TempData["PendingOrderCountry"]?.ToString(),
                    GiftWrap = bool.TryParse(
                        TempData["PendingOrderGiftWrap"]?.ToString(),
                        out bool giftWrap) && giftWrap,
                    StripeSessionId = session.Id,
                    PaymentIntentId = session.PaymentIntentId,
                    PaymentStatus = session.PaymentStatus,
                    Lines = cart.Lines.ToArray()
                };

                logger.LogInformation(
                    "Saving paid order for customer {Name} with Stripe SessionId {SessionId}",
                    order.Name, session.Id);

                repository.SaveOrder(order);

                logger.LogInformation(
                    "Order {OrderId} saved successfully for customer {Name}",
                    order.OrderID, order.Name);

                cart.Clear();

                logger.LogInformation(
                    "Cart cleared after successful payment for Order {OrderId}",
                    order.OrderID);

                return RedirectToPage("/Completed", new { orderId = order.OrderID });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to complete order after successful Stripe callback for SessionId {SessionId}", sessionId);
                return RedirectToAction(nameof(PaymentCancel));
            }
        }
    }
}