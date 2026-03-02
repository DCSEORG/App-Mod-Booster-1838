using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(IExpenseService expenseService, ILogger<UsersController> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    /// <summary>Get all active users</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<User>), 200)]
    public async Task<IActionResult> GetUsers()
    {
        try
        {
            var users = await _expenseService.GetUsersAsync();
            return Ok(users);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GET /api/users - returning dummy data");
            return Ok(ExpenseService.GetDummyUsers());
        }
    }
}
