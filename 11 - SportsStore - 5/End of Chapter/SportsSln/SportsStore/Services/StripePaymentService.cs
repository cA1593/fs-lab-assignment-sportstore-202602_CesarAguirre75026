using SportsStore.Models;
using Stripe.Checkout;

namespace SportsStore.Services
{
    public interface IStripePaymentService
    {
        Task<Session> CreateCheckoutSessionAsync(
            Order order,
            Cart cart,
            string successUrl,
            string cancelUrl);
    }

    public class StripePaymentService : IStripePaymentService
    {
        public async Task<Session> CreateCheckoutSessionAsync(
            Order order,
            Cart cart,
            string successUrl,
            string cancelUrl)
        {
            var lineItems = cart.Lines.Select(line => new SessionLineItemOptions
            {
                PriceData = new SessionLineItemPriceDataOptions
                {
                    Currency = "usd",
                    UnitAmount = (long)(line.Product.Price * 100),
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = line.Product.Name
                    }
                },
                Quantity = line.Quantity
            }).ToList();

            var options = new SessionCreateOptions
            {
                Mode = "payment",
                SuccessUrl = successUrl,
                CancelUrl = cancelUrl,
                LineItems = lineItems,
                PaymentMethodTypes = new List<string> { "card" },
                Metadata = new Dictionary<string, string>
                {
                    { "CustomerName", order.Name ?? string.Empty },
                    { "City", order.City ?? string.Empty },
                    { "Country", order.Country ?? string.Empty }
                }
            };

            var service = new SessionService();
            return await service.CreateAsync(options);
        }
    }
}