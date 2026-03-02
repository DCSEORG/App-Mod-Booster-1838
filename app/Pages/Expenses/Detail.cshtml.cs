using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages.Expenses;

public class DetailModel : PageModel
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<DetailModel> _logger;

    public Expense? Expense { get; set; }

    public DetailModel(IExpenseService expenseService, ILogger<DetailModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        try
        {
            Expense = await _expenseService.GetExpenseByIdAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DB error loading expense {Id} - using dummy data", id);
            Expense = ExpenseService.GetDummyExpenseById(id);
        }

        return Page();
    }
}
