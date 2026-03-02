using ExpenseManagement.Models;

namespace ExpenseManagement.Services;

public interface IExpenseService
{
    Task<IEnumerable<Expense>> GetExpensesAsync(ExpenseFilter? filter = null);
    Task<Expense?> GetExpenseByIdAsync(int expenseId);
    Task<int> CreateExpenseAsync(CreateExpenseRequest request);
    Task<bool> UpdateExpenseAsync(int expenseId, UpdateExpenseRequest request);
    Task<bool> SubmitExpenseAsync(int expenseId);
    Task<bool> ApproveExpenseAsync(int expenseId, int reviewedBy);
    Task<bool> RejectExpenseAsync(int expenseId, int reviewedBy);
    Task<IEnumerable<User>> GetUsersAsync();
    Task<IEnumerable<Category>> GetCategoriesAsync();
    Task<IEnumerable<ExpenseStatus>> GetStatusesAsync();
}
