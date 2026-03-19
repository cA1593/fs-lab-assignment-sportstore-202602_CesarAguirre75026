using Microsoft.AspNetCore.Mvc.RazorPages;

public class CompletedModel : PageModel
{
    private readonly ILogger<CompletedModel> _logger;

    public CompletedModel(ILogger<CompletedModel> logger)
    {
        _logger = logger;
    }

    public void OnGet(int orderId)
    {
        _logger.LogInformation("Order completed page shown for OrderID {OrderId}", orderId);
    }
}