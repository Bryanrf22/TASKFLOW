using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Core.Domain.Entities;
using TaskFlow.Data;
using TaskFlow.Web.Models.Projects;
using TaskFlow.Web.Models.Tasks;

namespace TaskFlow.UnitTests.Models;

public class LengthParityTests
{
    private static int? DbMaxLength(Type entityType, string propertyName)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;

        using var db = new AppDbContext(options);
        return db.Model.FindEntityType(entityType)?.FindProperty(propertyName)?.GetMaxLength();
    }

    private static int? VmMaxLength(Type vmType, string propertyName)
        => vmType.GetProperty(propertyName)?.GetCustomAttribute<StringLengthAttribute>()?.MaximumLength;

    [Theory]
    [InlineData(typeof(Project), nameof(Project.Name), typeof(CreateProjectViewModel), nameof(CreateProjectViewModel.Name))]
    [InlineData(typeof(Project), nameof(Project.Name), typeof(EditProjectViewModel), nameof(EditProjectViewModel.Name))]
    [InlineData(typeof(TaskItem), nameof(TaskItem.Description), typeof(CreateTaskViewModel), nameof(CreateTaskViewModel.Description))]
    [InlineData(typeof(TaskItem), nameof(TaskItem.Description), typeof(EditTaskViewModel), nameof(EditTaskViewModel.Description))]
    public void ViewModel_StringLength_MatchesEntityConfiguration(
        Type entityType,
        string entityProperty,
        Type vmType,
        string vmProperty)
    {
        var dbMaxLength = DbMaxLength(entityType, entityProperty);
        var vmMaxLength = VmMaxLength(vmType, vmProperty);

        Assert.NotNull(dbMaxLength);
        Assert.Equal(dbMaxLength, vmMaxLength);
    }
}