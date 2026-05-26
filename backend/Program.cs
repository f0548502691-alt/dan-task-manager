using DanTaskManager.Data;
using DanTaskManager.Domain.Handlers;
using DanTaskManager.Middleware;
using DanTaskManager.Services;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ✅ הוספת DbContext עם SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
);

// ✅ הרשמה של Task Handlers
builder.Services.AddTaskHandlersFromAssembly(typeof(ITaskHandler).Assembly);

// ✅ הרשמה של TaskHandlerFactory
builder.Services.AddScoped<TaskHandlerFactory>();

// ✅ הרשמה של Task Workflow Service
builder.Services.AddScoped<ITaskWorkflowService, TaskWorkflowService>();

// ✅ הרשמה של FluentValidation validators
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// ✅ הרשמה של Application Services
builder.Services.AddScoped<ITaskApplicationService, TaskApplicationService>();
builder.Services.AddScoped<IUserApplicationService, UserApplicationService>();

// הוספת Swagger/OpenAPI (אופציונלי)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// הוספת Controllers (אופציונלי)
builder.Services.AddControllers();

var app = builder.Build();

// ✅ יצירת הטבלאות אם לא קיימות (Migration אוטומטי)
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    dbContext.Database.Migrate();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Global API error handling with consistent JSON responses.
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseHttpsRedirection();
app.MapControllers();

app.Run();
