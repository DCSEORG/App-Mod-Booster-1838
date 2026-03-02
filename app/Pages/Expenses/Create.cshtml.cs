using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ExpenseManagement.Pages.Expenses;

public class CreateModel : PageModel
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<CreateModel> _logger;

    [BindProperty(SupportsGet = true)]
    public int? Id { get; set; }

    public bool IsEdit => Id.HasValue;

    [BindProperty] public int ExpenseId { get; set; }
    [BindProperty] public int UserId { get; set; }
    [BindProperty] public int CategoryId { get; set; }
    [BindProperty] public int AmountMinor { get; set; }
    [BindProperty] public string Currency { get; set; } = "GBP";
    [BindProperty] public DateTime ExpenseDate { get; set; } = DateTime.Today;
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public string? ReceiptFile { get; set; }

    public IEnumerable<User> Users { get; set; } = Enumerable.Empty<User>();
    public IEnumerable<Category> Categories { get; set; } = Enumerable.Empty<Category>();

    public CreateModel(IExpenseService expenseService, ILogger<CreateModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadDropdownsAsync();

        if (IsEdit && Id.HasValue)
        {
            Expense? expense;
            try { expense = await _expenseService.GetExpenseByIdAsync(Id.Value); }
            catch { expense = ExpenseService.GetDummyExpenseById(Id.Value); }

            if (expense == null) return NotFound();

            ExpenseId = expense.ExpenseId;
            UserId = expense.UserId;
            CategoryId = expense.CategoryId;
            AmountMinor = expense.AmountMinor;
            Currency = expense.Currency;
            ExpenseDate = expense.ExpenseDate;
            Description = expense.Description;
            ReceiptFile = expense.ReceiptFile;
        }
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(bool submitAfter = false)
    {
        await LoadDropdownsAsync();

        if (!ModelState.IsValid) return Page();

        try
        {
            if (IsEdit && ExpenseId > 0)
            {
                var updateRequest = new UpdateExpenseRequest
                {
                    CategoryId = CategoryId,
                    AmountMinor = AmountMinor,
                    Currency = Currency,
                    ExpenseDate = ExpenseDate,
                    Description = Description,
                    ReceiptFile = ReceiptFile
                };
                await _expenseService.UpdateExpenseAsync(ExpenseId, updateRequest);
                return RedirectToPage("/Expenses/Detail", new { id = ExpenseId });
            }
            else
            {
                var createRequest = new CreateExpenseRequest
                {
                    UserId = UserId,
                    CategoryId = CategoryId,
                    AmountMinor = AmountMinor,
                    Currency = Currency,
                    ExpenseDate = ExpenseDate,
                    Description = Description,
                    ReceiptFile = ReceiptFile
                };
                var newId = await _expenseService.CreateExpenseAsync(createRequest);

                if (submitAfter)
                {
                    await _expenseService.SubmitExpenseAsync(newId);
                }

                return RedirectToPage("/Index");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving expense");
            ModelState.AddModelError(string.Empty, $"Failed to save expense: {ex.Message}. Database may be unavailable.");
            return Page();
        }
    }

    private async Task LoadDropdownsAsync()
    {
        try { Users = await _expenseService.GetUsersAsync(); }
        catch { Users = ExpenseService.GetDummyUsers(); }

        try { Categories = await _expenseService.GetCategoriesAsync(); }
        catch { Categories = ExpenseService.GetDummyCategories(); }
    }
}
