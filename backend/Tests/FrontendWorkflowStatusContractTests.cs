using DanTaskManager.Domain.Handlers;
using System.Text.RegularExpressions;

namespace DanTaskManager.Tests;

/// <summary>
/// Regression coverage for the Angular workflow status contract.
/// The frontend in this repository does not include its own test runner, so these tests
/// keep the shared status mapping aligned with the backend workflow handlers.
/// </summary>
public class FrontendWorkflowStatusContractTests
{
    [Fact]
    public void FrontendStatusConstants_ShouldMatchBackendWorkflowHandlers()
    {
        var interfaces = ReadFrontendFile("task.interfaces.ts");

        AssertStatusConstant(interfaces, "BACKLOG", 0);
        AssertStatusConstant(interfaces, "IN_PROGRESS", 1);
        AssertStatusConstant(interfaces, "READY_FOR_REVIEW", 2);
        AssertStatusConstant(interfaces, "DONE", 3);
        AssertStatusConstant(interfaces, "RELEASED", 4);
        Assert.Matches(@"export\s+const\s+CLOSED_TASK_STATUS\s*=\s*99\s*;", interfaces);
        Assert.Matches(@"CLOSED\s*:\s*CLOSED_TASK_STATUS", interfaces);

        Assert.Equal(3, new ProcurementTaskHandler().FinalStatus);
        Assert.Equal(4, new DevelopmentTaskHandler().FinalStatus);
        Assert.Matches(@"Procurement\s*:\s*TASK_STATUS\.DONE", interfaces);
        Assert.Matches(@"Development\s*:\s*TASK_STATUS\.RELEASED", interfaces);
    }

    [Fact]
    public void WorkflowBoardPayloadBranches_ShouldUseSharedStatusConstantsForBackendPayloadFields()
    {
        var component = ReadFrontendFile("task-workflow-board.component.ts");

        Assert.Contains("status === TASK_STATUS.READY_FOR_REVIEW", component);
        Assert.Contains("prices: [this.form.controls['priceA'].value, this.form.controls['priceB'].value]", component);
        Assert.Contains("specification: this.form.controls['specification'].value", component);
        Assert.Contains("status === TASK_STATUS.DONE", component);
        Assert.Contains("receipt: this.form.controls['receipt'].value", component);
        Assert.Contains("branchName: this.form.controls['branchName'].value", component);
        Assert.Contains("status === TASK_STATUS.RELEASED", component);
        Assert.Contains("versionNumber: this.form.controls['versionNumber'].value", component);
        Assert.DoesNotMatch(@"status\s*===\s*[234]\b", component);
    }

    [Fact]
    public void ProcurementFields_ShouldBindValidationAndTemplateCasesToSharedStatusConstants()
    {
        var component = ReadFrontendFile("procurement-fields.component.ts");
        var template = ReadFrontendFile("procurement-fields.component.html");

        Assert.Contains("readonly TASK_STATUS = TASK_STATUS;", component);
        Assert.Contains("this.setControlState('priceA', this.status === TASK_STATUS.READY_FOR_REVIEW", component);
        Assert.Contains("this.setControlState('priceB', this.status === TASK_STATUS.READY_FOR_REVIEW", component);
        Assert.Contains("this.setControlState('receipt', this.status === TASK_STATUS.DONE", component);
        Assert.Contains("*ngSwitchCase=\"TASK_STATUS.READY_FOR_REVIEW\"", template);
        Assert.Contains("*ngSwitchCase=\"TASK_STATUS.DONE\"", template);
        Assert.DoesNotMatch(@"\*ngSwitchCase=""[234]""", template);
    }

    [Fact]
    public void DevelopmentFields_ShouldBindValidationAndTemplateCasesToSharedStatusConstants()
    {
        var component = ReadFrontendFile("development-fields.component.ts");
        var template = ReadFrontendFile("development-fields.component.html");

        Assert.Contains("readonly TASK_STATUS = TASK_STATUS;", component);
        Assert.Contains("this.setControlState('specification', this.status === TASK_STATUS.READY_FOR_REVIEW", component);
        Assert.Contains("this.setControlState('branchName', this.status === TASK_STATUS.DONE", component);
        Assert.Contains("this.setControlState('versionNumber', this.status === TASK_STATUS.RELEASED", component);
        Assert.Contains("*ngSwitchCase=\"TASK_STATUS.READY_FOR_REVIEW\"", template);
        Assert.Contains("*ngSwitchCase=\"TASK_STATUS.DONE\"", template);
        Assert.Contains("*ngSwitchCase=\"TASK_STATUS.RELEASED\"", template);
        Assert.DoesNotMatch(@"\*ngSwitchCase=""[234]""", template);
    }

    private static void AssertStatusConstant(string source, string name, int expectedValue)
    {
        Assert.Matches($@"{name}\s*:\s*{expectedValue}\b", source);
    }

    private static string ReadFrontendFile(string fileName)
    {
        var path = Path.Combine(LocateRepositoryRoot(), "frontend", "src", "app", "tasks", fileName);
        Assert.True(File.Exists(path), $"Expected frontend workflow file to exist: {path}");
        return File.ReadAllText(path);
    }

    private static string LocateRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory != null)
        {
            var frontendTasksDirectory = Path.Combine(directory.FullName, "frontend", "src", "app", "tasks");
            var backendProject = Path.Combine(directory.FullName, "backend", "DanTaskManager.csproj");
            if (Directory.Exists(frontendTasksDirectory) && File.Exists(backendProject))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root containing frontend workflow sources.");
    }
}
