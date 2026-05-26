using DanTaskManager.Domain;
using Microsoft.EntityFrameworkCore;

namespace DanTaskManager.Data;

/// <summary>
/// DbContext עבור מנהל המשימות
/// מכיל את ההגדרות של כל הטבלאות בבסיס הנתונים
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// טבלת המשתמשים
    /// </summary>
    public DbSet<AppUser> Users { get; set; } = null!;

    /// <summary>
    /// טבלת המשימות
    /// </summary>
    public DbSet<BaseTask> Tasks { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureAppUser(modelBuilder);
        ConfigureBaseTask(modelBuilder);
        
        SeedData(modelBuilder);
    }

    /// <summary>
    /// הגדרת מחלקת AppUser
    /// </summary>
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
    /// הגדרת מחלקת BaseTask
    /// כולל הגדרת עמודת JSON עבור CustomDataJson
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

        // הגדרת עמודת JSON עבור CustomDataJson
        // EF Core 8 כולל תמיכה מובנית ל-JSON columns בפי SQL Server
        taskBuilder
            .Property(t => t.CustomDataJson)
            .IsRequired()
            .HasColumnType("nvarchar(max)")
            .HasDefaultValue("{}");

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

        // יצירת אינדקס לשדה TaskType להאצת החיפושים
        taskBuilder
            .HasIndex(t => t.TaskType);
    }

    /// <summary>
    /// ביצוע Seed של נתונים בסיסיים
    /// </summary>
    private static void SeedData(ModelBuilder modelBuilder)
    {
        // יצירת משתמשים בסיסיים
        var users = new List<AppUser>
        {
            new AppUser
            {
                Id = 1,
                Name = "דן כהן",
                Email = "dan@example.com",
                CreatedAt = DateTime.UtcNow
            },
            new AppUser
            {
                Id = 2,
                Name = "רות לוי",
                Email = "ruth@example.com",
                CreatedAt = DateTime.UtcNow
            },
            new AppUser
            {
                Id = 3,
                Name = "משה אברהם",
                Email = "moshe@example.com",
                CreatedAt = DateTime.UtcNow
            },
            new AppUser
            {
                Id = 4,
                Name = "נועה ישראלי",
                Email = "noa@example.com",
                CreatedAt = DateTime.UtcNow
            },
            new AppUser
            {
                Id = 5,
                Name = "איתן ברק",
                Email = "eitan@example.com",
                CreatedAt = DateTime.UtcNow
            },
            new AppUser
            {
                Id = 6,
                Name = "מיכל גל",
                Email = "michal@example.com",
                CreatedAt = DateTime.UtcNow
            }
        };

        modelBuilder.Entity<AppUser>().HasData(users);

        // יצירת כמה משימות לדוגמה
        var tasks = new List<BaseTask>
        {
            new BaseTask
            {
                Id = 1,
                TaskType = "Analysis",
                Description = "ניתוח דרישות לפרויקט החדש",
                CurrentStatus = 1, // בתהליך
                AssignedToUserId = 1,
                CustomDataJson = "{\"priority\": \"high\", \"deadline\": \"2026-06-15\", \"estimatedHours\": 8}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new BaseTask
            {
                Id = 2,
                TaskType = "Development",
                Description = "פיתוח מודול ניהול משתמשים",
                CurrentStatus = WorkflowConstants.CreatedStatus, // Created
                AssignedToUserId = 2,
                CustomDataJson = "{\"priority\": \"medium\", \"deadline\": \"2026-07-01\", \"estimatedHours\": 16}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            },
            new BaseTask
            {
                Id = 3,
                TaskType = "Testing",
                Description = "בדיקת תכונות ה-API",
                CurrentStatus = 2, // הושלמה
                AssignedToUserId = 3,
                CustomDataJson = "{\"priority\": \"high\", \"testCases\": 15, \"coverage\": \"85%\"}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            }
        };

        modelBuilder.Entity<BaseTask>().HasData(tasks);
    }
}
