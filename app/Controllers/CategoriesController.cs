using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class CategoriesController : ControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<CategoriesController> _logger;

    public CategoriesController(IExpenseService expenseService, ILogger<CategoriesController> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    /// <summary>Get all active expense categories</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Category>), 200)]
    public async Task<IActionResult> GetCategories()
    {
        try
        {
            var categories = await _expenseService.GetCategoriesAsync();
            return Ok(categories);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GET /api/categories - returning dummy data");
            return Ok(ExpenseService.GetDummyCategories());
        }
    }

    /// <summary>Get all expense statuses</summary>
    [HttpGet("/api/statuses")]
    [ProducesResponseType(typeof(IEnumerable<ExpenseStatus>), 200)]
    public async Task<IActionResult> GetStatuses()
    {
        try
        {
            var statuses = await _expenseService.GetStatusesAsync();
            return Ok(statuses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GET /api/statuses - returning dummy data");
            return Ok(ExpenseService.GetDummyStatuses());
        }
    }
}
