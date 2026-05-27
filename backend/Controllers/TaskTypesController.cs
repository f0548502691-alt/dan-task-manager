using DanTaskManager.Contracts.Requests.TaskTypes;
using DanTaskManager.Domain;
using DanTaskManager.Services;
using Microsoft.AspNetCore.Mvc;

namespace DanTaskManager.Controllers;

[ApiController]
[Route("api/task-types")]
public class TaskTypesController : ControllerBase
{
    private readonly ITaskTypeMetadataService _metadataService;
    private readonly ITaskTypeCatalog _taskTypeCatalog;

    public TaskTypesController(
        ITaskTypeMetadataService metadataService,
        ITaskTypeCatalog taskTypeCatalog)
    {
        _metadataService = metadataService;
        _taskTypeCatalog = taskTypeCatalog;
    }

    /// <summary>Return every active task-type schema, merged from metadata and code-backed sources.</summary>
    [HttpGet]
    public ActionResult<IReadOnlyCollection<TaskTypeSchemaDto>> GetTaskTypes()
    {
        return Ok(GetMergedTaskTypeSchemas());
    }

    /// <summary>Return the full schema for a single task type.</summary>
    [HttpGet("{taskType}")]
    public ActionResult<TaskTypeSchemaDto> GetTaskTypeSchema(string taskType)
    {
        var schema = _metadataService.GetTaskType(taskType);
        if (schema == null)
        {
            var descriptor = _taskTypeCatalog.Find(taskType);
            if (descriptor == null)
            {
                return NotFound();
            }

            schema = CreateHandlerBackedSchema(descriptor);
        }

        return Ok(schema);
    }

    [HttpGet("{taskType}/schema")]
    public ActionResult<TaskTypeSchemaDto> GetTaskTypeSchemaAlias(string taskType)
    {
        return GetTaskTypeSchema(taskType);
    }

    /// <summary>Create or update task-type metadata.</summary>
    [HttpPost]
    public ActionResult<TaskTypeSchemaDto> UpsertTaskType(UpsertTaskTypeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TaskType))
        {
            return BadRequest(new { error = "TaskType is required" });
        }

        var result = _metadataService.UpsertTaskType(
            new UpsertTaskTypeCommand(
                request.TaskType,
                request.DisplayName,
                request.FinalStatus,
                request.IsActive));

        if (!result.Success)
        {
            throw new ApiValidationException(result.Message, "task_type_validation_failed");
        }

        return Ok(result.TaskType);
    }

    private IReadOnlyCollection<TaskTypeSchemaDto> GetMergedTaskTypeSchemas()
    {
        var schemas = _metadataService.GetTaskTypes()
            .Where(schema => schema.IsActive)
            .ToDictionary(schema => schema.TaskType, StringComparer.OrdinalIgnoreCase);

        foreach (var descriptor in _taskTypeCatalog.GetTaskTypes())
        {
            if (!schemas.ContainsKey(descriptor.TaskType))
            {
                schemas[descriptor.TaskType] = CreateHandlerBackedSchema(descriptor);
            }
        }

        return schemas.Values
            .OrderBy(schema => schema.TaskType, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static TaskTypeSchemaDto CreateHandlerBackedSchema(TaskTypeDescriptor descriptor)
    {
        return new TaskTypeSchemaDto
        {
            TaskType = descriptor.TaskType,
            DisplayName = descriptor.DisplayName,
            FinalStatus = descriptor.FinalStatus,
            IsActive = true,
            Version = 1,
            Fields = []
        };
    }

    /// <summary>Create or update a single field-validation rule for a task type.</summary>
    [HttpPost("{taskType}/fields")]
    public ActionResult<TaskTypeSchemaDto> UpsertTaskTypeField(
        string taskType,
        UpsertTaskTypeFieldRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Field))
        {
            throw new ApiValidationException("Field is required");
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
            throw new ApiValidationException(result.Message, "task_type_field_validation_failed");
        }

        return Ok(result.TaskType);
    }

    /// <summary>Same as <see cref="UpsertTaskTypeField"/> but with the field key supplied in the URL.</summary>
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
