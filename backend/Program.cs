using DanTaskManager.Data;
using DanTaskManager.Domain.Handlers;
using DanTaskManager.Middleware;
using DanTaskManager.Services;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ✅ הוספת DbContext עם SQL Server
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
);

// ✅ הרשמה של Task Handlers עבור הסוגים הנתמכים בלבד
builder.Services.AddTransient<ITaskHandler, ProcurementTaskHandler>();
builder.Services.AddTransient<ITaskHandler, DevelopmentTaskHandler>();

// ✅ הרשמה של TaskHandlerFactory
builder.Services.AddScoped<TaskHandlerFactory>();

// ✅ cache לחוקיות מסוגי משימות
builder.Services.AddMemoryCache();

// ✅ הרשמת שירות metadata + ולידציה מבוססי DB
builder.Services.AddScoped<TaskTypeValidationService>();
builder.Services.AddScoped<ITaskTypeValidationService>(sp => sp.GetRequiredService<TaskTypeValidationService>());
builder.Services.AddScoped<ITaskTypeMetadataService>(sp => sp.GetRequiredService<TaskTypeValidationService>());

// ✅ הרשמה של Task Workflow Service
builder.Services.AddScoped<ITaskWorkflowRuleProvider, MetadataTaskWorkflowRuleProvider>();
builder.Services.AddScoped<ITaskWorkflowRuleProvider, HandlerTaskWorkflowRuleProvider>();
builder.Services.AddScoped<ITaskWorkflowService, TaskWorkflowService>();

// ✅ הרשמה של FluentValidation validators
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// ✅ הרשמה של Application Services
builder.Services.AddScoped<ITaskApplicationService, TaskApplicationService>();
builder.Services.AddScoped<IUserApplicationService, UserApplicationService>();

// ✅ הרשמה של MediatR (מיגרציה הדרגתית ל-commands/queries)
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

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
    if (dbContext.Database.GetMigrations().Any())
    {
        dbContext.Database.Migrate();
    }
    else
    {
        dbContext.Database.EnsureCreated();
    }

    HybridSchemaBootstrapper.EnsureSchema(dbContext);
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
