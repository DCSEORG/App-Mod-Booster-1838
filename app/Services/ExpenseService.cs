using System.Data;
using System.Runtime.CompilerServices;
using ExpenseManagement.Models;
using Microsoft.Data.SqlClient;

namespace ExpenseManagement.Services;

public class ExpenseService : IExpenseService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ExpenseService> _logger;

    // Dummy data used when database is unavailable
    private static readonly List<Expense> _dummyExpenses = new()
    {
        new Expense { ExpenseId = 1, UserId = 1, UserName = "Alice Example", UserEmail = "alice@example.co.uk", CategoryId = 1, CategoryName = "Travel", StatusId = 2, StatusName = "Submitted", AmountMinor = 2540, Currency = "GBP", AmountDecimal = 25.40m, ExpenseDate = DateTime.Today.AddDays(-10), Description = "Taxi from airport to client site", SubmittedAt = DateTime.UtcNow.AddDays(-9), CreatedAt = DateTime.UtcNow.AddDays(-10) },
        new Expense { ExpenseId = 2, UserId = 1, UserName = "Alice Example", UserEmail = "alice@example.co.uk", CategoryId = 2, CategoryName = "Meals", StatusId = 3, StatusName = "Approved", AmountMinor = 1425, Currency = "GBP", AmountDecimal = 14.25m, ExpenseDate = DateTime.Today.AddDays(-30), Description = "Client lunch meeting", SubmittedAt = DateTime.UtcNow.AddDays(-29), ReviewedBy = 2, ReviewedByName = "Bob Manager", ReviewedAt = DateTime.UtcNow.AddDays(-28), CreatedAt = DateTime.UtcNow.AddDays(-30) },
        new Expense { ExpenseId = 3, UserId = 1, UserName = "Alice Example", UserEmail = "alice@example.co.uk", CategoryId = 3, CategoryName = "Supplies", StatusId = 1, StatusName = "Draft", AmountMinor = 799, Currency = "GBP", AmountDecimal = 7.99m, ExpenseDate = DateTime.Today.AddDays(-3), Description = "Office stationery", CreatedAt = DateTime.UtcNow.AddDays(-3) },
        new Expense { ExpenseId = 4, UserId = 1, UserName = "Alice Example", UserEmail = "alice@example.co.uk", CategoryId = 4, CategoryName = "Accommodation", StatusId = 3, StatusName = "Approved", AmountMinor = 12300, Currency = "GBP", AmountDecimal = 123.00m, ExpenseDate = DateTime.Today.AddDays(-60), Description = "Hotel during client visit", SubmittedAt = DateTime.UtcNow.AddDays(-59), ReviewedBy = 2, ReviewedByName = "Bob Manager", ReviewedAt = DateTime.UtcNow.AddDays(-58), CreatedAt = DateTime.UtcNow.AddDays(-60) },
    };

    private static readonly List<User> _dummyUsers = new()
    {
        new User { UserId = 1, UserName = "Alice Example", Email = "alice@example.co.uk", RoleId = 1, RoleName = "Employee", ManagerId = 2, ManagerName = "Bob Manager", IsActive = true, CreatedAt = DateTime.UtcNow.AddMonths(-6) },
        new User { UserId = 2, UserName = "Bob Manager", Email = "bob.manager@example.co.uk", RoleId = 2, RoleName = "Manager", IsActive = true, CreatedAt = DateTime.UtcNow.AddMonths(-12) },
    };

    private static readonly List<Category> _dummyCategories = new()
    {
        new Category { CategoryId = 1, CategoryName = "Travel", IsActive = true },
        new Category { CategoryId = 2, CategoryName = "Meals", IsActive = true },
        new Category { CategoryId = 3, CategoryName = "Supplies", IsActive = true },
        new Category { CategoryId = 4, CategoryName = "Accommodation", IsActive = true },
        new Category { CategoryId = 5, CategoryName = "Other", IsActive = true },
    };

    private static readonly List<ExpenseStatus> _dummyStatuses = new()
    {
        new ExpenseStatus { StatusId = 1, StatusName = "Draft" },
        new ExpenseStatus { StatusId = 2, StatusName = "Submitted" },
        new ExpenseStatus { StatusId = 3, StatusName = "Approved" },
        new ExpenseStatus { StatusId = 4, StatusName = "Rejected" },
    };

    public ExpenseService(IConfiguration configuration, ILogger<ExpenseService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    private SqlConnection CreateConnection()
    {
        var connectionString = _configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
        return new SqlConnection(connectionString);
    }

    public async Task<IEnumerable<Expense>> GetExpensesAsync(ExpenseFilter? filter = null)
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            await using var command = new SqlCommand("usp_GetExpenses", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            command.Parameters.AddWithValue("@StatusId", (object?)filter?.StatusId ?? DBNull.Value);
            command.Parameters.AddWithValue("@UserId", (object?)filter?.UserId ?? DBNull.Value);
            command.Parameters.AddWithValue("@DateFrom", (object?)filter?.DateFrom ?? DBNull.Value);
            command.Parameters.AddWithValue("@DateTo", (object?)filter?.DateTo ?? DBNull.Value);

            var expenses = new List<Expense>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                expenses.Add(MapExpense(reader));
            }
            return expenses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in GetExpensesAsync - returning dummy data");
            throw;
        }
    }

    public async Task<Expense?> GetExpenseByIdAsync(int expenseId)
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            await using var command = new SqlCommand("usp_GetExpenseById", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);

            await using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return MapExpense(reader);
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in GetExpenseByIdAsync({ExpenseId}) - returning dummy data", expenseId);
            throw;
        }
    }

    public async Task<int> CreateExpenseAsync(CreateExpenseRequest request)
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            await using var command = new SqlCommand("usp_CreateExpense", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@UserId", request.UserId);
            command.Parameters.AddWithValue("@CategoryId", request.CategoryId);
            command.Parameters.AddWithValue("@AmountMinor", request.AmountMinor);
            command.Parameters.AddWithValue("@Currency", request.Currency);
            command.Parameters.AddWithValue("@ExpenseDate", request.ExpenseDate.Date);
            command.Parameters.AddWithValue("@Description", (object?)request.Description ?? DBNull.Value);
            command.Parameters.AddWithValue("@ReceiptFile", (object?)request.ReceiptFile ?? DBNull.Value);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in CreateExpenseAsync");
            throw;
        }
    }

    public async Task<bool> UpdateExpenseAsync(int expenseId, UpdateExpenseRequest request)
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            await using var command = new SqlCommand("usp_UpdateExpense", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@CategoryId", request.CategoryId);
            command.Parameters.AddWithValue("@AmountMinor", request.AmountMinor);
            command.Parameters.AddWithValue("@Currency", request.Currency);
            command.Parameters.AddWithValue("@ExpenseDate", request.ExpenseDate.Date);
            command.Parameters.AddWithValue("@Description", (object?)request.Description ?? DBNull.Value);
            command.Parameters.AddWithValue("@ReceiptFile", (object?)request.ReceiptFile ?? DBNull.Value);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in UpdateExpenseAsync({ExpenseId})", expenseId);
            throw;
        }
    }

    public async Task<bool> SubmitExpenseAsync(int expenseId)
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            await using var command = new SqlCommand("usp_SubmitExpense", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@SubmittedAt", DateTime.UtcNow);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in SubmitExpenseAsync({ExpenseId})", expenseId);
            throw;
        }
    }

    public async Task<bool> ApproveExpenseAsync(int expenseId, int reviewedBy)
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            await using var command = new SqlCommand("usp_ApproveExpense", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@ReviewedBy", reviewedBy);
            command.Parameters.AddWithValue("@ReviewedAt", DateTime.UtcNow);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in ApproveExpenseAsync({ExpenseId})", expenseId);
            throw;
        }
    }

    public async Task<bool> RejectExpenseAsync(int expenseId, int reviewedBy)
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            await using var command = new SqlCommand("usp_RejectExpense", connection)
            {
                CommandType = CommandType.StoredProcedure
            };
            command.Parameters.AddWithValue("@ExpenseId", expenseId);
            command.Parameters.AddWithValue("@ReviewedBy", reviewedBy);
            command.Parameters.AddWithValue("@ReviewedAt", DateTime.UtcNow);

            var result = await command.ExecuteScalarAsync();
            return Convert.ToInt32(result) > 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in RejectExpenseAsync({ExpenseId})", expenseId);
            throw;
        }
    }

    public async Task<IEnumerable<User>> GetUsersAsync()
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            await using var command = new SqlCommand("usp_GetUsers", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            var users = new List<User>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                users.Add(new User
                {
                    UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
                    UserName = reader.GetString(reader.GetOrdinal("UserName")),
                    Email = reader.GetString(reader.GetOrdinal("Email")),
                    RoleId = reader.GetInt32(reader.GetOrdinal("RoleId")),
                    RoleName = reader.GetString(reader.GetOrdinal("RoleName")),
                    ManagerId = reader.IsDBNull(reader.GetOrdinal("ManagerId")) ? null : reader.GetInt32(reader.GetOrdinal("ManagerId")),
                    ManagerName = reader.IsDBNull(reader.GetOrdinal("ManagerName")) ? null : reader.GetString(reader.GetOrdinal("ManagerName")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                    CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
                });
            }
            return users;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in GetUsersAsync - returning dummy data");
            throw;
        }
    }

    public async Task<IEnumerable<Category>> GetCategoriesAsync()
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            await using var command = new SqlCommand("usp_GetCategories", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            var categories = new List<Category>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                categories.Add(new Category
                {
                    CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
                    CategoryName = reader.GetString(reader.GetOrdinal("CategoryName")),
                    IsActive = reader.GetBoolean(reader.GetOrdinal("IsActive")),
                });
            }
            return categories;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in GetCategoriesAsync - returning dummy data");
            throw;
        }
    }

    public async Task<IEnumerable<ExpenseStatus>> GetStatusesAsync()
    {
        try
        {
            await using var connection = CreateConnection();
            await connection.OpenAsync();

            await using var command = new SqlCommand("usp_GetStatuses", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            var statuses = new List<ExpenseStatus>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                statuses.Add(new ExpenseStatus
                {
                    StatusId = reader.GetInt32(reader.GetOrdinal("StatusId")),
                    StatusName = reader.GetString(reader.GetOrdinal("StatusName")),
                });
            }
            return statuses;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database error in GetStatusesAsync - returning dummy data");
            throw;
        }
    }

    // ─── Dummy Data Accessors (used when DB unavailable) ────────────────────
    public static IEnumerable<Expense> GetDummyExpenses(ExpenseFilter? filter = null)
    {
        var data = _dummyExpenses.AsEnumerable();
        if (filter?.StatusId.HasValue == true)
            data = data.Where(e => e.StatusId == filter.StatusId.Value);
        if (filter?.UserId.HasValue == true)
            data = data.Where(e => e.UserId == filter.UserId.Value);
        if (filter?.DateFrom.HasValue == true)
            data = data.Where(e => e.ExpenseDate >= filter.DateFrom.Value);
        if (filter?.DateTo.HasValue == true)
            data = data.Where(e => e.ExpenseDate <= filter.DateTo.Value);
        return data;
    }

    public static Expense? GetDummyExpenseById(int id) =>
        _dummyExpenses.FirstOrDefault(e => e.ExpenseId == id);

    public static IEnumerable<User> GetDummyUsers() => _dummyUsers;
    public static IEnumerable<Category> GetDummyCategories() => _dummyCategories;
    public static IEnumerable<ExpenseStatus> GetDummyStatuses() => _dummyStatuses;

    private static Expense MapExpense(SqlDataReader reader) => new()
    {
        ExpenseId = reader.GetInt32(reader.GetOrdinal("ExpenseId")),
        UserId = reader.GetInt32(reader.GetOrdinal("UserId")),
        UserName = reader.GetString(reader.GetOrdinal("UserName")),
        UserEmail = reader.GetString(reader.GetOrdinal("UserEmail")),
        CategoryId = reader.GetInt32(reader.GetOrdinal("CategoryId")),
        CategoryName = reader.GetString(reader.GetOrdinal("CategoryName")),
        StatusId = reader.GetInt32(reader.GetOrdinal("StatusId")),
        StatusName = reader.GetString(reader.GetOrdinal("StatusName")),
        AmountMinor = reader.GetInt32(reader.GetOrdinal("AmountMinor")),
        Currency = reader.GetString(reader.GetOrdinal("Currency")),
        AmountDecimal = reader.GetDecimal(reader.GetOrdinal("AmountDecimal")),
        ExpenseDate = reader.GetDateTime(reader.GetOrdinal("ExpenseDate")),
        Description = reader.IsDBNull(reader.GetOrdinal("Description")) ? null : reader.GetString(reader.GetOrdinal("Description")),
        ReceiptFile = reader.IsDBNull(reader.GetOrdinal("ReceiptFile")) ? null : reader.GetString(reader.GetOrdinal("ReceiptFile")),
        SubmittedAt = reader.IsDBNull(reader.GetOrdinal("SubmittedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("SubmittedAt")),
        ReviewedBy = reader.IsDBNull(reader.GetOrdinal("ReviewedBy")) ? null : reader.GetInt32(reader.GetOrdinal("ReviewedBy")),
        ReviewedByName = reader.IsDBNull(reader.GetOrdinal("ReviewedByName")) ? null : reader.GetString(reader.GetOrdinal("ReviewedByName")),
        ReviewedAt = reader.IsDBNull(reader.GetOrdinal("ReviewedAt")) ? null : reader.GetDateTime(reader.GetOrdinal("ReviewedAt")),
        CreatedAt = reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
    };
}
