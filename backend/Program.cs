using DanTaskManager.Data;
using DanTaskManager.Domain.Handlers;
using DanTaskManager.Middleware;
using DanTaskManager.Services;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
);

// Discover every IRegisterableTaskHandler in the assembly by reflection.
// Adding a code-backed task type does not require touching this file.
builder.Services.AddTaskHandlersFromAssembly(typeof(Program).Assembly);

builder.Services.AddScoped<TaskHandlerFactory>();
builder.Services.AddScoped<ITaskTypeCatalog, TaskTypeCatalogService>();

builder.Services.AddMemoryCache();

builder.Services.AddScoped<TaskTypeValidationService>();
builder.Services.AddScoped<ITaskTypeValidationService>(sp => sp.GetRequiredService<TaskTypeValidationService>());
builder.Services.AddScoped<ITaskTypeMetadataService>(sp => sp.GetRequiredService<TaskTypeValidationService>());

builder.Services.AddScoped<ITaskWorkflowRuleProvider, MetadataTaskWorkflowRuleProvider>();
builder.Services.AddScoped<ITaskWorkflowRuleProvider, HandlerTaskWorkflowRuleProvider>();
builder.Services.AddScoped<ITaskWorkflowService, TaskWorkflowService>();

// Startup diagnostic that detects task-type codes claimed by multiple rule providers.
builder.Services.Configure<TaskTypeConflictValidatorOptions>(
    builder.Configuration.GetSection(TaskTypeConflictValidatorOptions.SectionName));
builder.Services.AddHostedService<TaskTypeConflictValidator>();

builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddScoped<ITaskApplicationService, TaskApplicationService>();
builder.Services.AddScoped<IUserApplicationService, UserApplicationService>();

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddControllers();

var app = builder.Build();

// Single source of truth for the schema is the EF model in ApplicationDbContext.
// In development we materialize it via EnsureCreated; in production deployments
// migrations should be applied out-of-band before the app starts. If migrations
// are added to the project later, they take precedence here.
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
