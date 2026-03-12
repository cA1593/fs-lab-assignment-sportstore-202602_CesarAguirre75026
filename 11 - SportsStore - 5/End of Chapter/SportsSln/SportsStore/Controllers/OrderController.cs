using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SportsStore.Models;
using SportsStore.Services;
using Stripe.Checkout;

namespace SportsStore.Controllers {

    public class OrderController : Controller
    {
        private IOrderRepository repository;
        private Cart cart;
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
            logger.LogInformation("Checkout page opened");
            return View(new Order());
        }

        [HttpPost]

        // This action is called when the user submits the checkout form. It creates a Stripe Checkout Session and redirects the user to Stripe's hosted payment page.
        public async Task<IActionResult> Checkout(Order order)
        {
            logger.LogInformation("Checkout started");

            if (cart.Lines.Count() == 0)
            {
                logger.LogWarning("Checkout failed because the cart was empty");
                ModelState.AddModelError("", "Sorry, your cart is empty!");
            }

            if (!ModelState.IsValid)
            {
                logger.LogWarning("Checkout failed because the model state was invalid");
                return View(order);
            }

            order.Lines = cart.Lines.ToArray();

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

            logger.LogInformation("Creating Stripe Checkout Session for {ItemCount} items", order.Lines.Count);

            var session = await stripeService.CreateCheckoutSessionAsync(order, cart, successUrl, cancelUrl);

            logger.LogInformation("Stripe Checkout Session created with SessionId {SessionId}", session.Id);

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

            return Redirect(session.Url!);
        }

        // This action is called when the user cancels the Stripe payment process
        public IActionResult PaymentCancel()
        {
            logger.LogWarning("Stripe payment was cancelled by the user");
            return View("Checkout", new Order());
        }

        public IActionResult PaymentSuccess()
        {
            string? sessionId = TempData["StripeSessionId"]?.ToString();

            logger.LogInformation("Stripe payment success callback reached for SessionId {SessionId}", sessionId);

            if (string.IsNullOrEmpty(sessionId))
            {
                logger.LogWarning("Stripe payment success callback failed because no session id was found in TempData");
                return RedirectToAction(nameof(PaymentCancel));
            }

            var sessionService = new SessionService();
            var session = sessionService.Get(sessionId);

            if (session.PaymentStatus != "paid")
            {
                logger.LogWarning("Stripe session {SessionId} was returned, but payment status was {PaymentStatus}",
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
                GiftWrap = bool.TryParse(TempData["PendingOrderGiftWrap"]?.ToString(), out bool giftWrap) && giftWrap,
                StripeSessionId = session.Id,
                PaymentIntentId = session.PaymentIntentId,
                PaymentStatus = session.PaymentStatus,
                Lines = cart.Lines.ToArray()
            };

            logger.LogInformation("Saving paid order after verified Stripe payment");

            repository.SaveOrder(order);

            logger.LogInformation("Order saved successfully with OrderID {OrderId}", order.OrderID);

            cart.Clear();

            logger.LogInformation("Cart cleared after successful Stripe payment");

            return RedirectToPage("/Completed", new { orderId = order.OrderID });
        }

    }
}
