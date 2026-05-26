using DanTaskManager.Services;
using Microsoft.AspNetCore.Mvc;

namespace DanTaskManager.Controllers;

[ApiController]
[Route("api/task-types")]
public class TaskTypesController : ControllerBase
{
    private readonly ITaskTypeMetadataService _metadataService;

    public TaskTypesController(ITaskTypeMetadataService metadataService)
    {
        _metadataService = metadataService;
    }

    /// <summary>
    /// קבלת כל הסכמות של סוגי המשימות.
    /// </summary>
    [HttpGet]
    public ActionResult<IReadOnlyCollection<TaskTypeSchemaDto>> GetTaskTypes()
    {
        return Ok(_metadataService.GetTaskTypes());
    }

    /// <summary>
    /// קבלת סכימה מלאה לסוג משימה בודד.
    /// </summary>
    [HttpGet("{taskType}")]
    public ActionResult<TaskTypeSchemaDto> GetTaskTypeSchema(string taskType)
    {
        var schema = _metadataService.GetTaskType(taskType);
        if (schema == null)
        {
            return NotFound();
        }

        return Ok(schema);
    }

    [HttpGet("{taskType}/schema")]
    public ActionResult<TaskTypeSchemaDto> GetTaskTypeSchemaAlias(string taskType)
    {
        return GetTaskTypeSchema(taskType);
    }

    /// <summary>
    /// יצירה/עדכון של metadata עבור סוג משימה.
    /// </summary>
    [HttpPost]
    public ActionResult<TaskTypeSchemaDto> UpsertTaskType(UpsertTaskTypeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TaskType))
        {
            return BadRequest(new { error = "TaskType נדרש" });
        }

        var result = _metadataService.UpsertTaskType(
            new UpsertTaskTypeCommand(
                request.TaskType,
                request.DisplayName,
                request.FinalStatus,
                request.IsActive));

        if (!result.Success)
        {
            return BadRequest(new { error = result.Message });
        }

        return Ok(result.TaskType);
    }

    /// <summary>
    /// יצירה/עדכון חוקיות של שדה מותאם אישית לסוג משימה.
    /// </summary>
    [HttpPost("{taskType}/fields")]
    public ActionResult<TaskTypeSchemaDto> UpsertTaskTypeField(
        string taskType,
        UpsertTaskTypeFieldRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Field))
        {
            return BadRequest(new { error = "Field נדרש" });
        }

        var result = _metadataService.UpsertFieldDefinition(taskType, new UpsertFieldDefinitionCommand
        {
            Field = request.Field,
            Type = request.Type,
            Required = request.Required,
            MinLength = request.MinLength,
            MaxLength = request.MaxLength,
            MinValue = request.MinValue,
            MaxValue = request.MaxValue,
            ArrayLength = request.ArrayLength,
            MinItems = request.MinItems,
            MaxItems = request.MaxItems,
            ElementType = request.ElementType,
            Pattern = request.Pattern,
            AppliesFromStatus = request.AppliesFromStatus,
            AppliesToStatus = request.AppliesToStatus,
            AllowedValues = request.AllowedValues,
            IsIndexed = request.IsIndexed
        });

        if (!result.Success)
        {
            return BadRequest(new { error = result.Message });
        }

        return Ok(result.TaskType);
    }

    /// <summary>
    /// יצירה/עדכון חוקיות שדה לפי מזהה שדה בנתיב.
    /// </summary>
    [HttpPut("{taskType}/fields/{field}")]
    public ActionResult<TaskTypeSchemaDto> PutTaskTypeField(
        string taskType,
        string field,
        UpsertTaskTypeFieldRequest request)
    {
        var normalizedRequest = request with { Field = field };
        return UpsertTaskTypeField(taskType, normalizedRequest);
    }
}

public record UpsertTaskTypeRequest(
    string TaskType,
    string? DisplayName,
    int? FinalStatus,
    bool IsActive = true);

public record UpsertTaskTypeFieldRequest(
    string Field,
    string Type = "string",
    bool Required = true,
    int? MinLength = null,
    int? MaxLength = null,
    decimal? MinValue = null,
    decimal? MaxValue = null,
    int? ArrayLength = null,
    int? MinItems = null,
    int? MaxItems = null,
    string? ElementType = null,
    string? Pattern = null,
    int? AppliesFromStatus = null,
    int? AppliesToStatus = null,
    List<string>? AllowedValues = null,
    bool IsIndexed = false);
