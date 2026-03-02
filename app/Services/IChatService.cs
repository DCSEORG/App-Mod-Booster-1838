namespace ExpenseManagement.Services;

public interface IChatService
{
    Task<string> ChatAsync(string userMessage, string? conversationHistory = null);
}
