using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace ExpenseManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ExpensesController : ControllerBase
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<ExpensesController> _logger;

    public ExpensesController(IExpenseService expenseService, ILogger<ExpensesController> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    /// <summary>List all expenses with optional filters</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<Expense>), 200)]
    public async Task<IActionResult> GetExpenses(
        [FromQuery] int? statusId,
        [FromQuery] int? userId,
        [FromQuery] DateTime? dateFrom,
        [FromQuery] DateTime? dateTo)
    {
        try
        {
            var filter = new ExpenseFilter
            {
                StatusId = statusId,
                UserId = userId,
                DateFrom = dateFrom,
                DateTo = dateTo
            };
            var expenses = await _expenseService.GetExpensesAsync(filter);
            return Ok(expenses);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GET /api/expenses - using dummy data");
            var filter = new ExpenseFilter { StatusId = statusId, UserId = userId, DateFrom = dateFrom, DateTo = dateTo };
            return Ok(ExpenseService.GetDummyExpenses(filter));
        }
    }

    /// <summary>Get a specific expense by ID</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(Expense), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetExpense(int id)
    {
        try
        {
            var expense = await _expenseService.GetExpenseByIdAsync(id);
            if (expense == null) return NotFound(new { message = $"Expense {id} not found." });
            return Ok(expense);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GET /api/expenses/{Id} - using dummy data", id);
            var expense = ExpenseService.GetDummyExpenseById(id);
            if (expense == null) return NotFound(new { message = $"Expense {id} not found." });
            return Ok(expense);
        }
    }

    /// <summary>Create a new expense</summary>
    [HttpPost]
    [ProducesResponseType(typeof(object), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CreateExpense([FromBody] CreateExpenseRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var newId = await _expenseService.CreateExpenseAsync(request);
            return CreatedAtAction(nameof(GetExpense), new { id = newId }, new { expenseId = newId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in POST /api/expenses");
            return StatusCode(500, new { message = "Failed to create expense.", detail = ex.Message });
        }
    }

    /// <summary>Update an existing expense</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> UpdateExpense(int id, [FromBody] UpdateExpenseRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var success = await _expenseService.UpdateExpenseAsync(id, request);
            if (!success) return NotFound(new { message = $"Expense {id} not found or could not be updated." });
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in PUT /api/expenses/{Id}", id);
            return StatusCode(500, new { message = "Failed to update expense.", detail = ex.Message });
        }
    }

    /// <summary>Submit an expense for approval</summary>
    [HttpPost("{id:int}/submit")]
    [ProducesResponseType(204)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> SubmitExpense(int id)
    {
        try
        {
            var success = await _expenseService.SubmitExpenseAsync(id);
            if (!success) return NotFound(new { message = $"Expense {id} not found." });
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in POST /api/expenses/{Id}/submit", id);
            return StatusCode(500, new { message = "Failed to submit expense.", detail = ex.Message });
        }
    }

    /// <summary>Approve a submitted expense</summary>
    [HttpPost("{id:int}/approve")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> ApproveExpense(int id, [FromBody] ReviewExpenseRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var success = await _expenseService.ApproveExpenseAsync(id, request.ReviewedBy);
            if (!success) return NotFound(new { message = $"Expense {id} not found." });
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in POST /api/expenses/{Id}/approve", id);
            return StatusCode(500, new { message = "Failed to approve expense.", detail = ex.Message });
        }
    }

    /// <summary>Reject a submitted expense</summary>
    [HttpPost("{id:int}/reject")]
    [ProducesResponseType(204)]
    [ProducesResponseType(400)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RejectExpense(int id, [FromBody] ReviewExpenseRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        try
        {
            var success = await _expenseService.RejectExpenseAsync(id, request.ReviewedBy);
            if (!success) return NotFound(new { message = $"Expense {id} not found." });
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in POST /api/expenses/{Id}/reject", id);
            return StatusCode(500, new { message = "Failed to reject expense.", detail = ex.Message });
        }
    }
}
