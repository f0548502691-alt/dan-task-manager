using DanTaskManager.Domain;
using Microsoft.EntityFrameworkCore;

namespace DanTaskManager.Data;

/// <summary>
/// EF Core context for the task-management database. Owns the entity
/// configuration and the seed data for users, task-type metadata, and
/// field-validation rules.
/// </summary>
public class ApplicationDbContext : DbContext
{
    private static readonly DateTime SeedTimestampUtc = new(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc);

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<AppUser> Users { get; set; } = null!;

    public DbSet<BaseTask> Tasks { get; set; } = null!;

    public DbSet<TaskTypeMetadata> TaskTypes { get; set; } = null!;

    public DbSet<TaskFieldDefinition> TaskFieldDefinitions { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureAppUser(modelBuilder);
        ConfigureBaseTask(modelBuilder);
        ConfigureTaskTypeMetadata(modelBuilder);
        ConfigureTaskFieldDefinition(modelBuilder);
        
        SeedData(modelBuilder);
    }

    private static void ConfigureAppUser(ModelBuilder modelBuilder)
    {
        var appUserBuilder = modelBuilder.Entity<AppUser>();

        appUserBuilder
            .HasKey(u => u.Id);

        appUserBuilder
            .Property(u => u.Name)
            .IsRequired()
            .HasMaxLength(255);

        appUserBuilder
            .Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(255);

        appUserBuilder
            .Property(u => u.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        appUserBuilder
            .HasIndex(u => u.Email)
            .IsUnique();

        appUserBuilder
            .HasMany(u => u.Tasks)
            .WithOne(t => t.AssignedToUser)
            .HasForeignKey(t => t.AssignedToUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    /// <summary>
    /// Configure <see cref="BaseTask"/>, including the JSON column used for
    /// per-type custom data and the check constraint that keeps it valid JSON.
    /// </summary>
    private static void ConfigureBaseTask(ModelBuilder modelBuilder)
    {
        var taskBuilder = modelBuilder.Entity<BaseTask>();

        taskBuilder
            .HasKey(t => t.Id);

        taskBuilder
            .Property(t => t.TaskType)
            .IsRequired()
            .HasMaxLength(100);

        taskBuilder
            .Property(t => t.CurrentStatus)
            .HasDefaultValue(WorkflowConstants.CreatedStatus);

        taskBuilder
            .Property(t => t.Description)
            .HasMaxLength(1000);

        taskBuilder
            .Property(t => t.CustomDataJson)
            .IsRequired()
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("{}");

        taskBuilder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_Tasks_CustomDataJson_IsJson",
                "ISJSON([CustomDataJson]) = 1");
        });

        taskBuilder
            .Property(t => t.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        taskBuilder
            .Property(t => t.UpdatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        taskBuilder
            .HasOne(t => t.AssignedToUser)
            .WithMany(u => u.Tasks)
            .HasForeignKey(t => t.AssignedToUserId)
            .OnDelete(DeleteBehavior.Restrict);

        taskBuilder
            .HasIndex(t => t.TaskType);
    }

    private static void ConfigureTaskTypeMetadata(ModelBuilder modelBuilder)
    {
        var taskTypeBuilder = modelBuilder.Entity<TaskTypeMetadata>();

        taskTypeBuilder.HasKey(t => t.Id);

        taskTypeBuilder
            .Property(t => t.Code)
            .IsRequired()
            .HasMaxLength(100);

        taskTypeBuilder
            .Property(t => t.DisplayName)
            .IsRequired()
            .HasMaxLength(255);

        taskTypeBuilder
            .Property(t => t.IsActive)
            .HasDefaultValue(true);

        taskTypeBuilder
            .Property(t => t.Version)
            .HasDefaultValue(1);

        taskTypeBuilder
            .Property(t => t.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        taskTypeBuilder
            .Property(t => t.UpdatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        taskTypeBuilder
            .HasIndex(t => t.Code)
            .IsUnique();
    }

    private static void ConfigureTaskFieldDefinition(ModelBuilder modelBuilder)
    {
        var fieldBuilder = modelBuilder.Entity<TaskFieldDefinition>();

        fieldBuilder.HasKey(f => f.Id);

        fieldBuilder
            .Property(f => f.FieldKey)
            .IsRequired()
            .HasMaxLength(100);

        fieldBuilder
            .Property(f => f.DataType)
            .IsRequired()
            .HasMaxLength(50);

        fieldBuilder
            .Property(f => f.ElementType)
            .HasMaxLength(50);

        fieldBuilder
            .Property(f => f.RegexPattern)
            .HasMaxLength(500);

        fieldBuilder
            .Property(f => f.AllowedValuesJson)
            .HasColumnType("nvarchar(max)");

        fieldBuilder
            .Property(f => f.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        fieldBuilder
            .Property(f => f.UpdatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        fieldBuilder
            .HasOne(f => f.TaskTypeMetadata)
            .WithMany(t => t.FieldDefinitions)
            .HasForeignKey(f => f.TaskTypeMetadataId)
            .OnDelete(DeleteBehavior.Cascade);

        fieldBuilder
            .HasIndex(f => new { f.TaskTypeMetadataId, f.FieldKey })
            .IsUnique();
    }

    /// <summary>
    /// Seed baseline users, task-type metadata (Procurement, Development,
    /// Marketing), field rules, and a couple of sample tasks.
    /// </summary>
    private static void SeedData(ModelBuilder modelBuilder)
    {
        var users = new List<AppUser>
        {
            new AppUser
            {
                Id = 1,
                Name = "Dan Cohen",
                Email = "dan@example.com",
                CreatedAt = SeedTimestampUtc
            },
            new AppUser
            {
                Id = 2,
                Name = "Ruth Levi",
                Email = "ruth@example.com",
                CreatedAt = SeedTimestampUtc
            },
            new AppUser
            {
                Id = 3,
                Name = "Moshe Avraham",
                Email = "moshe@example.com",
                CreatedAt = SeedTimestampUtc
            },
            new AppUser
            {
                Id = 4,
                Name = "Noa Israeli",
                Email = "noa@example.com",
                CreatedAt = SeedTimestampUtc
            },
            new AppUser
            {
                Id = 5,
                Name = "Eitan Barak",
                Email = "eitan@example.com",
                CreatedAt = SeedTimestampUtc
            },
            new AppUser
            {
                Id = 6,
                Name = "Michal Gal",
                Email = "michal@example.com",
                CreatedAt = SeedTimestampUtc
            }
        };

        modelBuilder.Entity<AppUser>().HasData(users);

        var taskTypes = new List<TaskTypeMetadata>
        {
            new()
            {
                Id = 1,
                Code = "Procurement",
                DisplayName = "Procurement",
                FinalStatus = 3,
                IsActive = true,
                Version = 1,
                CreatedAt = SeedTimestampUtc,
                UpdatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = 2,
                Code = "Development",
                DisplayName = "Development",
                FinalStatus = 4,
                IsActive = true,
                Version = 1,
                CreatedAt = SeedTimestampUtc,
                UpdatedAt = SeedTimestampUtc
            },
            // Marketing is the canonical "third task type" demo: added without
            // touching any C# code beyond this seed block. The metadata-driven
            // rule provider picks it up automatically.
            new()
            {
                Id = 3,
                Code = "Marketing",
                DisplayName = "Marketing",
                FinalStatus = 3,
                IsActive = true,
                Version = 1,
                CreatedAt = SeedTimestampUtc,
                UpdatedAt = SeedTimestampUtc
            }
        };

        modelBuilder.Entity<TaskTypeMetadata>().HasData(taskTypes);

        var fieldDefinitions = new List<TaskFieldDefinition>
        {
            new()
            {
                Id = 1,
                TaskTypeMetadataId = 1,
                FieldKey = "prices",
                DataType = "array",
                IsRequired = true,
                ArrayLength = 2,
                ElementType = "string",
                AppliesFromStatus = 2,
                AppliesToStatus = 2,
                IsIndexed = false,
                CreatedAt = SeedTimestampUtc,
                UpdatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = 2,
                TaskTypeMetadataId = 1,
                FieldKey = "receipt",
                DataType = "string",
                IsRequired = true,
                AppliesFromStatus = 3,
                AppliesToStatus = 3,
                IsIndexed = false,
                CreatedAt = SeedTimestampUtc,
                UpdatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = 3,
                TaskTypeMetadataId = 2,
                FieldKey = "specification",
                DataType = "string",
                IsRequired = true,
                MinLength = 10,
                AppliesFromStatus = 2,
                AppliesToStatus = 2,
                IsIndexed = false,
                CreatedAt = SeedTimestampUtc,
                UpdatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = 4,
                TaskTypeMetadataId = 2,
                FieldKey = "branchName",
                DataType = "string",
                IsRequired = true,
                RegexPattern = "valid_git_branch",
                AppliesFromStatus = 3,
                AppliesToStatus = 3,
                IsIndexed = true,
                CreatedAt = SeedTimestampUtc,
                UpdatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = 5,
                TaskTypeMetadataId = 2,
                FieldKey = "versionNumber",
                DataType = "stringOrNumber",
                IsRequired = true,
                RegexPattern = "semantic_version",
                AppliesFromStatus = 4,
                AppliesToStatus = 4,
                IsIndexed = false,
                CreatedAt = SeedTimestampUtc,
                UpdatedAt = SeedTimestampUtc
            },
            // Marketing field definitions (TaskTypeMetadataId = 3). Status 2
            // requires a campaign name and a target-audience enum; status 3
            // requires an ISO-8601 launch date. Pure metadata, no handlers.
            new()
            {
                Id = 6,
                TaskTypeMetadataId = 3,
                FieldKey = "campaignName",
                DataType = "string",
                IsRequired = true,
                MinLength = 3,
                AppliesFromStatus = 2,
                AppliesToStatus = 2,
                IsIndexed = false,
                CreatedAt = SeedTimestampUtc,
                UpdatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = 7,
                TaskTypeMetadataId = 3,
                FieldKey = "targetAudience",
                DataType = "string",
                IsRequired = true,
                AllowedValuesJson = "[\"B2B\",\"B2C\",\"Internal\"]",
                AppliesFromStatus = 2,
                AppliesToStatus = 2,
                IsIndexed = true,
                CreatedAt = SeedTimestampUtc,
                UpdatedAt = SeedTimestampUtc
            },
            new()
            {
                Id = 8,
                TaskTypeMetadataId = 3,
                FieldKey = "launchDate",
                DataType = "string",
                IsRequired = true,
                RegexPattern = @"^\d{4}-\d{2}-\d{2}$",
                AppliesFromStatus = 3,
                AppliesToStatus = 3,
                IsIndexed = false,
                CreatedAt = SeedTimestampUtc,
                UpdatedAt = SeedTimestampUtc
            }
        };

        modelBuilder.Entity<TaskFieldDefinition>().HasData(fieldDefinitions);

        var tasks = new List<BaseTask>
        {
            new BaseTask
            {
                Id = 1,
                TaskType = "Procurement",
                Description = "Collect supplier quotes for new equipment",
                CurrentStatus = WorkflowConstants.CreatedStatus,
                AssignedToUserId = 1,
                CustomDataJson = "{}",
                CreatedAt = SeedTimestampUtc,
                UpdatedAt = SeedTimestampUtc
            },
            new BaseTask
            {
                Id = 2,
                TaskType = "Development",
                Description = "Develop the user management module",
                CurrentStatus = WorkflowConstants.CreatedStatus,
                AssignedToUserId = 2,
                CustomDataJson = "{}",
                CreatedAt = SeedTimestampUtc,
                UpdatedAt = SeedTimestampUtc
            }
        };

        modelBuilder.Entity<BaseTask>().HasData(tasks);
    }
}
