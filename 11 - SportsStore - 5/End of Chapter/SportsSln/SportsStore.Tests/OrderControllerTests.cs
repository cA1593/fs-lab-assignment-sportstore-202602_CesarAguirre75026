using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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
            // Arrange - create a mock repository
            Mock<IOrderRepository> mock = new Mock<IOrderRepository>();
            // Arrange - create an empty cart
            Cart cart = new Cart();
            // Arrange - create the order
            Order order = new Order();

            // Arrange - create a mock logger
            Mock<ILogger<OrderController>> logger = new Mock<ILogger<OrderController>>();
            Mock<IStripePaymentService> stripeService = new Mock<IStripePaymentService>();
            // Arrange - create an instance of the controller
            OrderController target = new OrderController(mock.Object, cart, logger.Object, stripeService.Object);

            // Act
            IActionResult actionResult = await target.Checkout(order);
            ViewResult? result = actionResult as ViewResult;

            // Assert - check that the order hasn't been stored 
            mock.Verify(m => m.SaveOrder(It.IsAny<Order>()), Times.Never);
            // Assert - check that the method is returning the default view
            Assert.True(string.IsNullOrEmpty(result?.ViewName));
            // Assert - check that I am passing an invalid model to the view
            Assert.False(result?.ViewData.ModelState.IsValid);
        }

        [Fact]
        public async Task Cannot_Checkout_Invalid_ShippingDetails(){

            // Arrange - create a mock order repository
            Mock<IOrderRepository> mock = new Mock<IOrderRepository>();
            // Arrange - create a cart with one item
            Cart cart = new Cart();
            cart.AddItem(new Product(), 1);

            // Arrange - create a mock logger
            Mock<ILogger<OrderController>> logger = new Mock<ILogger<OrderController>>();
            Mock<IStripePaymentService> stripeService = new Mock<IStripePaymentService>();
            // Arrange - create an instance of the controller
            OrderController target = new OrderController(mock.Object, cart, logger.Object, stripeService.Object);

            // Arrange - add an error to the model
            target.ModelState.AddModelError("error", "error");

            // Act - try to checkout
            IActionResult actionResult = await target.Checkout(new Order());
            ViewResult? result = actionResult as ViewResult;

            // Assert - check that the order hasn't been passed stored
            mock.Verify(m => m.SaveOrder(It.IsAny<Order>()), Times.Never);
            // Assert - check that the method is returning the default view
            Assert.True(string.IsNullOrEmpty(result?.ViewName));
            // Assert - check that I am passing an invalid model to the view
            Assert.False(result?.ViewData.ModelState.IsValid);
        }

        [Fact]
        public async Task Can_Checkout_And_Submit_Order(){
            // Arrange - create a mock order repository
            Mock<IOrderRepository> mock = new Mock<IOrderRepository>();
            // Arrange - create a cart with one item
            Cart cart = new Cart();
            cart.AddItem(new Product(), 1);

            // Arrange - create a mock logger
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

            // Arrange - create an instance of the controller
            OrderController target = new OrderController(mock.Object, cart, logger.Object, stripeService.Object);

            // Arrange - mock HttpContext so Request.Scheme/Host don't throw NullReferenceException
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new HostString("localhost");
            target.ControllerContext = new ControllerContext()
            {
                HttpContext = httpContext
            };

            // Act - try to checkout
            IActionResult actionResult = await target.Checkout(new Order());
            RedirectResult? result = actionResult as RedirectResult;

            // Assert - check that the order has been stored
            mock.Verify(m => m.SaveOrder(It.IsAny<Order>()), Times.Once);
            // Assert - check that the result is not null and redirects to Stripe
            Assert.NotNull(result);
            Assert.Equal("https://checkout.stripe.com/test-session", result?.Url);
        }
    }
}
