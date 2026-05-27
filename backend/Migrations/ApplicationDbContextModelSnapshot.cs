using System;
using DanTaskManager.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace DanTaskManager.Migrations;

[DbContext(typeof(ApplicationDbContext))]
partial class ApplicationDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
#pragma warning disable 612, 618
        modelBuilder
            .HasAnnotation("ProductVersion", "8.0.0")
            .HasAnnotation("Relational:MaxIdentifierLength", 128);

        SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder);

        modelBuilder.Entity("DanTaskManager.Domain.AppUser", b =>
        {
            b.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("int");

            SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

            b.Property<DateTime>("CreatedAt")
                .ValueGeneratedOnAdd()
                .HasColumnType("datetime2")
                .HasDefaultValueSql("GETUTCDATE()");

            b.Property<string>("Email")
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnType("nvarchar(255)");

            b.Property<string>("Name")
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnType("nvarchar(255)");

            b.HasKey("Id");

            b.HasIndex("Email")
                .IsUnique();

            b.ToTable("Users");

            b.HasData(
                new
                {
                    Id = 1,
                    CreatedAt = new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc),
                    Email = "dan@example.com",
                    Name = "Dan Cohen"
                },
                new
                {
                    Id = 2,
                    CreatedAt = new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc),
                    Email = "ruth@example.com",
                    Name = "Ruth Levi"
                },
                new
                {
                    Id = 3,
                    CreatedAt = new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc),
                    Email = "moshe@example.com",
                    Name = "Moshe Avraham"
                },
                new
                {
                    Id = 4,
                    CreatedAt = new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc),
                    Email = "noa@example.com",
                    Name = "Noa Israeli"
                },
                new
                {
                    Id = 5,
                    CreatedAt = new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc),
                    Email = "eitan@example.com",
                    Name = "Eitan Barak"
                },
                new
                {
                    Id = 6,
                    CreatedAt = new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc),
                    Email = "michal@example.com",
                    Name = "Michal Gal"
                });
        });

        modelBuilder.Entity("DanTaskManager.Domain.BaseTask", b =>
        {
            b.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("int");

            SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

            b.Property<int>("AssignedToUserId")
                .HasColumnType("int");

            b.Property<DateTime>("CreatedAt")
                .ValueGeneratedOnAdd()
                .HasColumnType("datetime2")
                .HasDefaultValueSql("GETUTCDATE()");

            b.Property<int>("CurrentStatus")
                .ValueGeneratedOnAdd()
                .HasColumnType("int")
                .HasDefaultValue(1);

            b.Property<string>("CustomDataJson")
                .IsRequired()
                .ValueGeneratedOnAdd()
                .HasColumnType("nvarchar(max)")
                .HasDefaultValue("{}");

            b.Property<string>("Description")
                .IsRequired()
                .HasMaxLength(1000)
                .HasColumnType("nvarchar(1000)");

            b.Property<string>("TaskType")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("nvarchar(100)");

            b.Property<DateTime>("UpdatedAt")
                .ValueGeneratedOnAdd()
                .HasColumnType("datetime2")
                .HasDefaultValueSql("GETUTCDATE()");

            b.HasKey("Id");

            b.HasIndex("AssignedToUserId");

            b.HasIndex("TaskType");

            b.ToTable("Tasks", t =>
            {
                t.HasCheckConstraint("CK_Tasks_CustomDataJson_IsJson", "ISJSON([CustomDataJson]) = 1");
            });

            b.HasData(
                new
                {
                    Id = 1,
                    AssignedToUserId = 1,
                    CreatedAt = new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc),
                    CurrentStatus = 1,
                    CustomDataJson = "{}",
                    Description = "Collect supplier quotes for new equipment",
                    TaskType = "Procurement",
                    UpdatedAt = new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc)
                },
                new
                {
                    Id = 2,
                    AssignedToUserId = 2,
                    CreatedAt = new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc),
                    CurrentStatus = 1,
                    CustomDataJson = "{}",
                    Description = "Develop the user management module",
                    TaskType = "Development",
                    UpdatedAt = new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc)
                });
        });

        modelBuilder.Entity("DanTaskManager.Domain.TaskFieldDefinition", b =>
        {
            b.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("int");

            SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

            b.Property<string>("AllowedValuesJson")
                .HasColumnType("nvarchar(max)");

            b.Property<int?>("AppliesFromStatus")
                .HasColumnType("int");

            b.Property<int?>("AppliesToStatus")
                .HasColumnType("int");

            b.Property<bool>("AppliesOnClose")
                .ValueGeneratedOnAdd()
                .HasColumnType("bit")
                .HasDefaultValue(false);

            b.Property<int?>("ArrayLength")
                .HasColumnType("int");

            b.Property<DateTime>("CreatedAt")
                .ValueGeneratedOnAdd()
                .HasColumnType("datetime2")
                .HasDefaultValueSql("GETUTCDATE()");

            b.Property<string>("DataType")
                .IsRequired()
                .HasMaxLength(50)
                .HasColumnType("nvarchar(50)");

            b.Property<string>("ElementType")
                .HasMaxLength(50)
                .HasColumnType("nvarchar(50)");

            b.Property<string>("FieldKey")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("nvarchar(100)");

            b.Property<bool>("IsIndexed")
                .HasColumnType("bit");

            b.Property<bool>("IsRequired")
                .HasColumnType("bit");

            b.Property<int?>("MaxItems")
                .HasColumnType("int");

            b.Property<int?>("MaxLength")
                .HasColumnType("int");

            b.Property<decimal?>("MaxValue")
                .HasColumnType("decimal(18,2)");

            b.Property<int?>("MinItems")
                .HasColumnType("int");

            b.Property<int?>("MinLength")
                .HasColumnType("int");

            b.Property<decimal?>("MinValue")
                .HasColumnType("decimal(18,2)");

            b.Property<string>("RegexPattern")
                .HasMaxLength(500)
                .HasColumnType("nvarchar(500)");

            b.Property<int>("TaskTypeMetadataId")
                .HasColumnType("int");

            b.Property<DateTime>("UpdatedAt")
                .ValueGeneratedOnAdd()
                .HasColumnType("datetime2")
                .HasDefaultValueSql("GETUTCDATE()");

            b.HasKey("Id");

            b.HasIndex("TaskTypeMetadataId", "FieldKey")
                .IsUnique();

            b.ToTable("TaskFieldDefinitions");

            b.HasData(
                new
                {
                    Id = 1,
                    AppliesFromStatus = 2,
                    AppliesToStatus = 2,
                    ArrayLength = 2,
                    CreatedAt = new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc),
                    DataType = "array",
                    ElementType = "string",
                    FieldKey = "prices",
                    IsIndexed = false,
                    IsRequired = true,
                    TaskTypeMetadataId = 1,
                    UpdatedAt = new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc)
                },
                new
                {
                    Id = 2,
                    AppliesFromStatus = 3,
                    AppliesToStatus = 3,
                    CreatedAt = new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc),
                    DataType = "string",
                    FieldKey = "receipt",
                    IsIndexed = false,
                    IsRequired = true,
                    TaskTypeMetadataId = 1,
                    UpdatedAt = new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc)
                },
                new
                {
                    Id = 3,
                    AppliesFromStatus = 2,
                    AppliesToStatus = 2,
                    CreatedAt = new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc),
                    DataType = "string",
                    FieldKey = "specification",
                    IsIndexed = false,
                    IsRequired = true,
                    MinLength = 10,
                    TaskTypeMetadataId = 2,
                    UpdatedAt = new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc)
                },
                new
                {
                    Id = 4,
                    AppliesFromStatus = 3,
                    AppliesToStatus = 3,
                    CreatedAt = new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc),
                    DataType = "string",
                    FieldKey = "branchName",
                    IsIndexed = true,
                    IsRequired = true,
                    RegexPattern = "valid_git_branch",
                    TaskTypeMetadataId = 2,
                    UpdatedAt = new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc)
                },
                new
                {
                    Id = 5,
                    AppliesFromStatus = 4,
                    AppliesToStatus = 4,
                    CreatedAt = new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc),
                    DataType = "stringOrNumber",
                    FieldKey = "versionNumber",
                    IsIndexed = false,
                    IsRequired = true,
                    RegexPattern = "semantic_version",
                    TaskTypeMetadataId = 2,
                    UpdatedAt = new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc)
                },
                new
                {
                    Id = 6,
                    AppliesFromStatus = 2,
                    AppliesToStatus = 2,
                    CreatedAt = new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc),
                    DataType = "string",
                    FieldKey = "campaignName",
                    IsIndexed = false,
                    IsRequired = true,
                    MinLength = 3,
                    TaskTypeMetadataId = 3,
                    UpdatedAt = new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc)
                },
                new
                {
                    Id = 7,
                    AllowedValuesJson = "[\"B2B\",\"B2C\",\"Internal\"]",
                    AppliesFromStatus = 2,
                    AppliesToStatus = 2,
                    CreatedAt = new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc),
                    DataType = "string",
                    FieldKey = "targetAudience",
                    IsIndexed = true,
                    IsRequired = true,
                    TaskTypeMetadataId = 3,
                    UpdatedAt = new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc)
                },
                new
                {
                    Id = 8,
                    AppliesFromStatus = 3,
                    AppliesToStatus = 3,
                    CreatedAt = new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc),
                    DataType = "string",
                    FieldKey = "launchDate",
                    IsIndexed = false,
                    IsRequired = true,
                    RegexPattern = "^\\d{4}-\\d{2}-\\d{2}$",
                    TaskTypeMetadataId = 3,
                    UpdatedAt = new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc)
                });
        });

        modelBuilder.Entity("DanTaskManager.Domain.TaskTypeMetadata", b =>
        {
            b.Property<int>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("int");

            SqlServerPropertyBuilderExtensions.UseIdentityColumn(b.Property<int>("Id"));

            b.Property<string>("Code")
                .IsRequired()
                .HasMaxLength(100)
                .HasColumnType("nvarchar(100)");

            b.Property<DateTime>("CreatedAt")
                .ValueGeneratedOnAdd()
                .HasColumnType("datetime2")
                .HasDefaultValueSql("GETUTCDATE()");

            b.Property<string>("DisplayName")
                .IsRequired()
                .HasMaxLength(255)
                .HasColumnType("nvarchar(255)");

            b.Property<int?>("FinalStatus")
                .HasColumnType("int");

            b.Property<bool>("IsActive")
                .ValueGeneratedOnAdd()
                .HasColumnType("bit")
                .HasDefaultValue(true);

            b.Property<DateTime>("UpdatedAt")
                .ValueGeneratedOnAdd()
                .HasColumnType("datetime2")
                .HasDefaultValueSql("GETUTCDATE()");

            b.Property<int>("Version")
                .ValueGeneratedOnAdd()
                .HasColumnType("int")
                .HasDefaultValue(1);

            b.HasKey("Id");

            b.HasIndex("Code")
                .IsUnique();

            b.ToTable("TaskTypes");

            b.HasData(
                new
                {
                    Id = 1,
                    Code = "Procurement",
                    CreatedAt = new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc),
                    DisplayName = "Procurement",
                    FinalStatus = 3,
                    IsActive = true,
                    UpdatedAt = new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc),
                    Version = 1
                },
                new
                {
                    Id = 2,
                    Code = "Development",
                    CreatedAt = new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc),
                    DisplayName = "Development",
                    FinalStatus = 4,
                    IsActive = true,
                    UpdatedAt = new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc),
                    Version = 1
                },
                new
                {
                    Id = 3,
                    Code = "Marketing",
                    CreatedAt = new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc),
                    DisplayName = "Marketing",
                    FinalStatus = 3,
                    IsActive = true,
                    UpdatedAt = new DateTime(2026, 5, 25, 0, 0, 0, DateTimeKind.Utc),
                    Version = 1
                });
        });

        modelBuilder.Entity("DanTaskManager.Domain.BaseTask", b =>
        {
            b.HasOne("DanTaskManager.Domain.AppUser", "AssignedToUser")
                .WithMany("Tasks")
                .HasForeignKey("AssignedToUserId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            b.Navigation("AssignedToUser");
        });

        modelBuilder.Entity("DanTaskManager.Domain.TaskFieldDefinition", b =>
        {
            b.HasOne("DanTaskManager.Domain.TaskTypeMetadata", "TaskTypeMetadata")
                .WithMany("FieldDefinitions")
                .HasForeignKey("TaskTypeMetadataId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.Navigation("TaskTypeMetadata");
        });

        modelBuilder.Entity("DanTaskManager.Domain.AppUser", b =>
        {
            b.Navigation("Tasks");
        });

        modelBuilder.Entity("DanTaskManager.Domain.TaskTypeMetadata", b =>
        {
            b.Navigation("FieldDefinitions");
        });
#pragma warning restore 612, 618
    }
}
