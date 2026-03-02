using System.Text;
using System.Text.Json;
using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using ExpenseManagement.Models;
using OpenAI.Chat;

namespace ExpenseManagement.Services;

public class ChatService : IChatService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChatService> _logger;
    private readonly IExpenseService _expenseService;

    private static readonly string SystemPrompt = """
        You are an intelligent assistant for the Expense Management System.
        You help users manage their business expenses.
        
        You have access to the following functions:
        - get_expenses: Retrieve a list of expenses with optional filters (statusId, userId, dateFrom, dateTo)
        - get_expense_by_id: Get details of a specific expense by ID
        - create_expense: Create a new expense claim
        - submit_expense: Submit an expense for approval (transitions Draft → Submitted)
        - approve_expense: Approve a submitted expense (transitions Submitted → Approved)
        - reject_expense: Reject a submitted expense (transitions Submitted → Rejected)
        - get_users: Get a list of all active users
        - get_categories: Get all expense categories
        
        Status IDs: 1=Draft, 2=Submitted, 3=Approved, 4=Rejected
        Amounts are stored in minor units (pence). £12.34 = 1234 pence.
        
        Always use functions to retrieve real data. Format monetary amounts as £X.XX.
        When listing items, format them clearly with bullet points.
        Be helpful, concise, and professional.
        """;

    public ChatService(IConfiguration configuration, ILogger<ChatService> logger, IExpenseService expenseService)
    {
        _configuration = configuration;
        _logger = logger;
        _expenseService = expenseService;
    }

    public async Task<string> ChatAsync(string userMessage, string? conversationHistory = null)
    {
        var openAIEndpoint = _configuration["OpenAI:Endpoint"];
        var deploymentName = _configuration["OpenAI:DeploymentName"] ?? "gpt-4o";

        if (string.IsNullOrEmpty(openAIEndpoint))
        {
            _logger.LogWarning("OpenAI endpoint not configured - returning dummy response");
            return GetDummyResponse(userMessage);
        }

        try
        {
            // Use ManagedIdentityCredential with explicit client ID if configured
            var managedIdentityClientId = _configuration["ManagedIdentityClientId"];
            Azure.Core.TokenCredential credential;

            if (!string.IsNullOrEmpty(managedIdentityClientId))
            {
                _logger.LogInformation("Using ManagedIdentityCredential with client ID: {ClientId}", managedIdentityClientId);
                credential = new ManagedIdentityCredential(managedIdentityClientId);
            }
            else
            {
                _logger.LogInformation("Using DefaultAzureCredential");
                credential = new DefaultAzureCredential();
            }

            var client = new AzureOpenAIClient(new Uri(openAIEndpoint), credential);
            var chatClient = client.GetChatClient(deploymentName);

            // Build the messages list
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(SystemPrompt)
            };

            // Add conversation history if provided
            if (!string.IsNullOrEmpty(conversationHistory))
            {
                try
                {
                    var history = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(conversationHistory);
                    if (history != null)
                    {
                        foreach (var msg in history)
                        {
                            if (msg.TryGetValue("role", out var role) && msg.TryGetValue("content", out var content))
                            {
                                if (role == "user") messages.Add(new UserChatMessage(content));
                                else if (role == "assistant") messages.Add(new AssistantChatMessage(content));
                            }
                        }
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse conversation history");
                }
            }

            messages.Add(new UserChatMessage(userMessage));

            // Define function tools
            var options = new ChatCompletionOptions
            {
                Tools =
                {
                    ChatTool.CreateFunctionTool(
                        "get_expenses",
                        "Retrieves a list of expenses from the database with optional filters",
                        BinaryData.FromString("""
                        {
                            "type": "object",
                            "properties": {
                                "statusId": { "type": "integer", "description": "Filter by status: 1=Draft, 2=Submitted, 3=Approved, 4=Rejected" },
                                "userId": { "type": "integer", "description": "Filter by user ID" },
                                "dateFrom": { "type": "string", "format": "date", "description": "Filter expenses from this date (YYYY-MM-DD)" },
                                "dateTo": { "type": "string", "format": "date", "description": "Filter expenses to this date (YYYY-MM-DD)" }
                            }
                        }
                        """)),
                    ChatTool.CreateFunctionTool(
                        "get_expense_by_id",
                        "Gets the full details of a specific expense by its ID",
                        BinaryData.FromString("""
                        {
                            "type": "object",
                            "properties": {
                                "expenseId": { "type": "integer", "description": "The expense ID to retrieve" }
                            },
                            "required": ["expenseId"]
                        }
                        """)),
                    ChatTool.CreateFunctionTool(
                        "create_expense",
                        "Creates a new expense claim in Draft status",
                        BinaryData.FromString("""
                        {
                            "type": "object",
                            "properties": {
                                "userId": { "type": "integer", "description": "The user ID submitting the expense" },
                                "categoryId": { "type": "integer", "description": "Category ID (1=Travel, 2=Meals, 3=Supplies, 4=Accommodation, 5=Other)" },
                                "amountMinor": { "type": "integer", "description": "Amount in pence (e.g. £12.34 = 1234)" },
                                "currency": { "type": "string", "default": "GBP", "description": "Currency code" },
                                "expenseDate": { "type": "string", "format": "date", "description": "Date of the expense (YYYY-MM-DD)" },
                                "description": { "type": "string", "description": "Description of the expense" }
                            },
                            "required": ["userId", "categoryId", "amountMinor", "expenseDate"]
                        }
                        """)),
                    ChatTool.CreateFunctionTool(
                        "submit_expense",
                        "Submits an expense for manager approval (Draft → Submitted)",
                        BinaryData.FromString("""
                        {
                            "type": "object",
                            "properties": {
                                "expenseId": { "type": "integer", "description": "The expense ID to submit" }
                            },
                            "required": ["expenseId"]
                        }
                        """)),
                    ChatTool.CreateFunctionTool(
                        "approve_expense",
                        "Approves a submitted expense (Submitted → Approved)",
                        BinaryData.FromString("""
                        {
                            "type": "object",
                            "properties": {
                                "expenseId": { "type": "integer", "description": "The expense ID to approve" },
                                "reviewedBy": { "type": "integer", "description": "User ID of the manager approving" }
                            },
                            "required": ["expenseId", "reviewedBy"]
                        }
                        """)),
                    ChatTool.CreateFunctionTool(
                        "reject_expense",
                        "Rejects a submitted expense (Submitted → Rejected)",
                        BinaryData.FromString("""
                        {
                            "type": "object",
                            "properties": {
                                "expenseId": { "type": "integer", "description": "The expense ID to reject" },
                                "reviewedBy": { "type": "integer", "description": "User ID of the manager rejecting" }
                            },
                            "required": ["expenseId", "reviewedBy"]
                        }
                        """)),
                    ChatTool.CreateFunctionTool(
                        "get_users",
                        "Retrieves all active users from the system",
                        BinaryData.FromString("""{ "type": "object", "properties": {} }""")),
                    ChatTool.CreateFunctionTool(
                        "get_categories",
                        "Retrieves all expense categories",
                        BinaryData.FromString("""{ "type": "object", "properties": {} }""")),
                }
            };

            // Function calling loop (max 10 iterations to prevent runaway execution)
            const int maxIterations = 10;
            int iteration = 0;
            while (iteration < maxIterations)
            {
                iteration++;
                var response = await chatClient.CompleteChatAsync(messages, options);
                var completion = response.Value;

                // Check if the model wants to call functions
                if (completion.FinishReason == ChatFinishReason.ToolCalls)
                {
                    // Add the assistant message with tool calls
                    messages.Add(new AssistantChatMessage(completion));

                    // Execute each tool call
                    foreach (var toolCall in completion.ToolCalls)
                    {
                        var result = await ExecuteToolCallAsync(toolCall);
                        messages.Add(new ToolChatMessage(toolCall.Id, result));
                    }
                    // Continue the loop to get the final response
                }
                else
                {
                    // Return the final text response
                    return completion.Content[0].Text;
                }
            }
            return "I'm sorry, the response took too many steps to complete. Please try a simpler request.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Azure OpenAI");
            return $"I'm sorry, I encountered an error communicating with the AI service: {ex.Message}. Please check the Azure OpenAI configuration.";
        }
    }

    private async Task<string> ExecuteToolCallAsync(ChatToolCall toolCall)
    {
        try
        {
            _logger.LogInformation("Executing tool: {ToolName} with args: {Args}", toolCall.FunctionName, toolCall.FunctionArguments);

            var args = JsonDocument.Parse(toolCall.FunctionArguments.ToString());
            var root = args.RootElement;

            switch (toolCall.FunctionName)
            {
                case "get_expenses":
                {
                    var filter = new ExpenseFilter
                    {
                        StatusId = root.TryGetProperty("statusId", out var sid) ? sid.GetInt32() : null,
                        UserId = root.TryGetProperty("userId", out var uid) ? uid.GetInt32() : null,
                        DateFrom = root.TryGetProperty("dateFrom", out var df) ? DateTime.Parse(df.GetString()!) : null,
                        DateTo = root.TryGetProperty("dateTo", out var dt) ? DateTime.Parse(dt.GetString()!) : null,
                    };
                    IEnumerable<Expense> expenses;
                    try { expenses = await _expenseService.GetExpensesAsync(filter); }
                    catch { expenses = ExpenseService.GetDummyExpenses(filter); }
                    return JsonSerializer.Serialize(expenses);
                }

                case "get_expense_by_id":
                {
                    var expenseId = root.GetProperty("expenseId").GetInt32();
                    Expense? expense;
                    try { expense = await _expenseService.GetExpenseByIdAsync(expenseId); }
                    catch { expense = ExpenseService.GetDummyExpenseById(expenseId); }
                    return expense == null ? "{ \"error\": \"Expense not found\" }" : JsonSerializer.Serialize(expense);
                }

                case "create_expense":
                {
                    var request = new CreateExpenseRequest
                    {
                        UserId = root.GetProperty("userId").GetInt32(),
                        CategoryId = root.GetProperty("categoryId").GetInt32(),
                        AmountMinor = root.GetProperty("amountMinor").GetInt32(),
                        Currency = root.TryGetProperty("currency", out var curr) ? curr.GetString() ?? "GBP" : "GBP",
                        ExpenseDate = DateTime.Parse(root.GetProperty("expenseDate").GetString()!),
                        Description = root.TryGetProperty("description", out var desc) ? desc.GetString() : null,
                    };
                    try
                    {
                        var newId = await _expenseService.CreateExpenseAsync(request);
                        return JsonSerializer.Serialize(new { success = true, expenseId = newId });
                    }
                    catch (Exception ex)
                    {
                        return JsonSerializer.Serialize(new { success = false, error = ex.Message });
                    }
                }

                case "submit_expense":
                {
                    var expenseId = root.GetProperty("expenseId").GetInt32();
                    try
                    {
                        var success = await _expenseService.SubmitExpenseAsync(expenseId);
                        return JsonSerializer.Serialize(new { success });
                    }
                    catch (Exception ex)
                    {
                        return JsonSerializer.Serialize(new { success = false, error = ex.Message });
                    }
                }

                case "approve_expense":
                {
                    var expenseId = root.GetProperty("expenseId").GetInt32();
                    var reviewedBy = root.GetProperty("reviewedBy").GetInt32();
                    try
                    {
                        var success = await _expenseService.ApproveExpenseAsync(expenseId, reviewedBy);
                        return JsonSerializer.Serialize(new { success });
                    }
                    catch (Exception ex)
                    {
                        return JsonSerializer.Serialize(new { success = false, error = ex.Message });
                    }
                }

                case "reject_expense":
                {
                    var expenseId = root.GetProperty("expenseId").GetInt32();
                    var reviewedBy = root.GetProperty("reviewedBy").GetInt32();
                    try
                    {
                        var success = await _expenseService.RejectExpenseAsync(expenseId, reviewedBy);
                        return JsonSerializer.Serialize(new { success });
                    }
                    catch (Exception ex)
                    {
                        return JsonSerializer.Serialize(new { success = false, error = ex.Message });
                    }
                }

                case "get_users":
                {
                    IEnumerable<User> users;
                    try { users = await _expenseService.GetUsersAsync(); }
                    catch { users = ExpenseService.GetDummyUsers(); }
                    return JsonSerializer.Serialize(users);
                }

                case "get_categories":
                {
                    IEnumerable<Category> categories;
                    try { categories = await _expenseService.GetCategoriesAsync(); }
                    catch { categories = ExpenseService.GetDummyCategories(); }
                    return JsonSerializer.Serialize(categories);
                }

                default:
                    return JsonSerializer.Serialize(new { error = $"Unknown function: {toolCall.FunctionName}" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing tool {ToolName}", toolCall.FunctionName);
            return JsonSerializer.Serialize(new { error = ex.Message });
        }
    }

    private static string GetDummyResponse(string userMessage)
    {
        var lower = userMessage.ToLowerInvariant();

        if (lower.Contains("expense") || lower.Contains("list") || lower.Contains("show"))
        {
            return """
                ⚠️ **GenAI services not deployed** — showing sample response.
                
                To enable real AI responses, run `./deploy-with-chat.sh` to deploy Azure OpenAI.
                
                Here are some sample expenses (dummy data):
                - **Expense #1**: Travel — £25.40 — Submitted
                - **Expense #2**: Meals — £14.25 — Approved
                - **Expense #3**: Supplies — £7.99 — Draft
                - **Expense #4**: Accommodation — £123.00 — Approved
                
                Use the main app at `/Index` to manage expenses with the full interface.
                """;
        }

        return """
            ⚠️ **GenAI services not deployed** — I'm running in demo mode.
            
            To enable real AI-powered chat, run `./deploy-with-chat.sh` which will deploy:
            - Azure OpenAI (GPT-4o in Sweden Central)
            - Azure AI Search
            
            In the meantime, you can use the full expense management interface at `/Index`.
            """;
    }
}
