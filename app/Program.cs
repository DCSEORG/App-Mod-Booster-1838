using ExpenseManagement.Services;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add Razor Pages + Controllers
builder.Services.AddRazorPages();
builder.Services.AddControllers()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.DateFormatString = "yyyy-MM-dd";
    });

// Register application services
builder.Services.AddScoped<IExpenseService, ExpenseService>();
builder.Services.AddScoped<IChatService, ChatService>();

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Expense Management API",
        Version = "v1",
        Description = "REST API for the Expense Management System. All data access uses stored procedures."
    });
});

// HTTP context accessor (used for error reporting)
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Swagger always enabled (useful for POC / demo)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Expense Management API v1");
    c.RoutePrefix = "swagger";
});

app.UseStaticFiles();

// Serve chatui folder as static files under /chatui path
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "chatui")),
    RequestPath = "/chatui"
});

app.UseRouting();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();
