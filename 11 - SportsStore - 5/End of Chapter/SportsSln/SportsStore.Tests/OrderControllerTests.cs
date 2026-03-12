using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using Moq;
using SportsStore.Controllers;
using SportsStore.Models;
using SportsStore.Services;
using System.Threading.Tasks;
using Xunit;

namespace SportsStore.Tests {

    public class OrderControllerTests {

        [Fact]
        public async Task Cannot_Checkout_Empty_Cart(){
            Mock<IOrderRepository> mock = new Mock<IOrderRepository>();
            Cart cart = new Cart();
            Order order = new Order();

            Mock<ILogger<OrderController>> logger = new Mock<ILogger<OrderController>>();
            Mock<IStripePaymentService> stripeService = new Mock<IStripePaymentService>();
            OrderController target = new OrderController(mock.Object, cart, logger.Object, stripeService.Object);

            IActionResult actionResult = await target.Checkout(order);
            ViewResult? result = actionResult as ViewResult;

            mock.Verify(m => m.SaveOrder(It.IsAny<Order>()), Times.Never);
            Assert.True(string.IsNullOrEmpty(result?.ViewName));
            Assert.False(result?.ViewData.ModelState.IsValid);
        }

        [Fact]
        public async Task Cannot_Checkout_Invalid_ShippingDetails(){
            Mock<IOrderRepository> mock = new Mock<IOrderRepository>();
            Cart cart = new Cart();
            cart.AddItem(new Product(), 1);

            Mock<ILogger<OrderController>> logger = new Mock<ILogger<OrderController>>();
            Mock<IStripePaymentService> stripeService = new Mock<IStripePaymentService>();
            OrderController target = new OrderController(mock.Object, cart, logger.Object, stripeService.Object);

            target.ModelState.AddModelError("error", "error");

            IActionResult actionResult = await target.Checkout(new Order());
            ViewResult? result = actionResult as ViewResult;

            mock.Verify(m => m.SaveOrder(It.IsAny<Order>()), Times.Never);
            Assert.True(string.IsNullOrEmpty(result?.ViewName));
            Assert.False(result?.ViewData.ModelState.IsValid);
        }

        [Fact]
        public async Task Can_Checkout_And_Submit_Order(){
            Mock<IOrderRepository> mock = new Mock<IOrderRepository>();
            Cart cart = new Cart();
            cart.AddItem(new Product(), 1);

            Mock<ILogger<OrderController>> logger = new Mock<ILogger<OrderController>>();
            Mock<IStripePaymentService> stripeService = new Mock<IStripePaymentService>();

            stripeService
                .Setup(s => s.CreateCheckoutSessionAsync(
                    It.IsAny<Order>(),
                    It.IsAny<Cart>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .ReturnsAsync(new Stripe.Checkout.Session
                {
                    Id = "cs_test_123",
                    Url = "https://checkout.stripe.com/test-session"
                });

            OrderController target = new OrderController(mock.Object, cart, logger.Object, stripeService.Object);

            // Mock HttpContext with Request.Scheme
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new HostString("localhost");

            // Mock IUrlHelper so Url.Action(...) returns a valid string
            Mock<IUrlHelper> urlHelper = new Mock<IUrlHelper>();
            urlHelper
                .Setup(u => u.Action(It.IsAny<UrlActionContext>()))
                .Returns("https://localhost/order/callback");

            // Mock TempData so TempData[...] = ... doesn't throw
            var tempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

            target.ControllerContext = new ControllerContext()
            {
                HttpContext = httpContext
            };
            target.Url = urlHelper.Object;
            target.TempData = tempData;

            IActionResult actionResult = await target.Checkout(new Order());
            RedirectResult? result = actionResult as RedirectResult;

            mock.Verify(m => m.SaveOrder(It.IsAny<Order>()), Times.Once);
            Assert.NotNull(result);
            Assert.Equal("https://checkout.stripe.com/test-session", result?.Url);
        }
    }
}
