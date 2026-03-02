using ExpenseManagement.Models;
using ExpenseManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Runtime.CompilerServices;

namespace ExpenseManagement.Pages;

public class IndexModel : PageModel
{
    private readonly IExpenseService _expenseService;
    private readonly ILogger<IndexModel> _logger;

    public IEnumerable<Expense> Expenses { get; set; } = Enumerable.Empty<Expense>();
    public IEnumerable<User> Users { get; set; } = Enumerable.Empty<User>();
    public IEnumerable<ExpenseStatus> Statuses { get; set; } = Enumerable.Empty<ExpenseStatus>();

    // Summary stats
    public int TotalExpenses => Expenses.Count();
    public int DraftCount => Expenses.Count(e => e.StatusName == "Draft");
    public int SubmittedCount => Expenses.Count(e => e.StatusName == "Submitted");
    public int ApprovedCount => Expenses.Count(e => e.StatusName == "Approved");
    public decimal TotalApprovedAmount => Expenses.Where(e => e.StatusName == "Approved").Sum(e => e.AmountDecimal);

    // Filters
    [BindProperty(SupportsGet = true)]
    public int? FilterStatusId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? FilterUserId { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? FilterDateFrom { get; set; }

    [BindProperty(SupportsGet = true)]
    public DateTime? FilterDateTo { get; set; }

    public IndexModel(IExpenseService expenseService, ILogger<IndexModel> logger)
    {
        _expenseService = expenseService;
        _logger = logger;
    }

    public async Task OnGetAsync()
    {
        var filter = new ExpenseFilter
        {
            StatusId = FilterStatusId,
            UserId = FilterUserId,
            DateFrom = FilterDateFrom,
            DateTo = FilterDateTo
        };

        // Load expenses
        try
        {
            Expenses = await _expenseService.GetExpensesAsync(filter);
        }
        catch (Exception ex)
        {
            SetDbError(ex);
            Expenses = ExpenseService.GetDummyExpenses(filter);
        }

        // Load users for filter dropdown
        try
        {
            Users = await _expenseService.GetUsersAsync();
        }
        catch
        {
            Users = ExpenseService.GetDummyUsers();
        }

        // Load statuses for filter dropdown
        try
        {
            Statuses = await _expenseService.GetStatusesAsync();
        }
        catch
        {
            Statuses = ExpenseService.GetDummyStatuses();
        }
    }

    private void SetDbError(Exception ex, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
    {
        var shortFile = Path.GetFileName(file);
        ViewData["DbError"] = $"Unable to connect to the database. Showing demo data. ({shortFile}, line {line})";
        ViewData["DbErrorDetail"] = BuildErrorDetail(ex);
    }

    private static string BuildErrorDetail(Exception ex) => $"""
        Error Type: {ex.GetType().Name}
        Message: {ex.Message}

        ── Managed Identity Troubleshooting ──────────────────────────────────
        This app uses Azure Active Directory Managed Identity to connect to
        Azure SQL Database. If you see authentication errors, check:

        1. The App Service has a User-Assigned Managed Identity attached.
           → Azure Portal → App Service → Identity → User Assigned

        2. The managed identity has been added as a database user:
              CREATE USER [mid-AppModAssist-01-01-00] FROM EXTERNAL PROVIDER;
              ALTER ROLE db_datareader ADD MEMBER [mid-AppModAssist-01-01-00];
              ALTER ROLE db_datawriter ADD MEMBER [mid-AppModAssist-01-01-00];
              GRANT EXECUTE TO [mid-AppModAssist-01-01-00];

        3. The App Service settings contain:
              ManagedIdentityClientId = <client-id-of-managed-identity>
              AZURE_CLIENT_ID          = <same-client-id>
              ConnectionStrings__DefaultConnection = Server=tcp:...;
                Authentication=Active Directory Managed Identity;
                User Id=<client-id>;

        4. For LOCAL development, change the connection string to:
              Authentication=Active Directory Default
           Then run: az login
           This uses your developer credentials instead of managed identity.

        5. Make sure the SQL Server firewall allows Azure services:
              az sql server firewall-rule create --name AllowAllAzureIPs \
                --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0

        DO NOT add SQL username/password - this app uses managed identity only.
        ──────────────────────────────────────────────────────────────────────
        """;
}
