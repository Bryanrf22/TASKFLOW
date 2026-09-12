using System.ComponentModel.DataAnnotations;
using TaskFlow.Core.Domain.Enums;
using TaskFlow.Web.Models.Accounts;
using TaskFlow.Web.Models.Projects;
using TaskFlow.Web.Models.Tasks;

namespace TaskFlow.UnitTests.Models;

public class ViewModelValidationTests
{
    private static IReadOnlyList<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(model);
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }

    private static void AssertValid(object model)
        => Assert.Empty(Validate(model));

    private static void AssertInvalidAt(object model, string member)
        => Assert.Contains(Validate(model), r => r.MemberNames.Contains(member));

    [Theory]
    [InlineData("INFR")]
    [InlineData("AB")]
    [InlineData("MKT2026")]
    [InlineData("a1b2c3d4e9")]
    public void CreateProject_AcceptValidKeys(string key)
    {
        AssertValid(new CreateProjectViewModel { Name = "Proyecto", Key = key });
    }

    [Theory]
    [InlineData("")]
    [InlineData("1ABC")]
    [InlineData(".ABC")]
    [InlineData("A B")]
    [InlineData("A!B")]
    [InlineData("ABCDEFGHIJK")]
    [InlineData("A")]
    [InlineData("ABC-")]
    public void CreateProject_RejectInvalidKeys(string key)
    {
        AssertInvalidAt(new CreateProjectViewModel { Name = "Proyecto", Key = key }, nameof(CreateProjectViewModel.Key));
    }

    [Fact]
    public void CreateProject_Description_OverLimit_IsRejected()
    {
        var vm = new CreateProjectViewModel
        {
            Name = "Proyecto",
            Key = "ABC",
            Description = new string('x', 2001)
        };

        AssertInvalidAt(vm, nameof(CreateProjectViewModel.Description));
    }

    [Fact]
    public void CreateProject_MissingName_IsRejected()
    {
        AssertInvalidAt(new CreateProjectViewModel { Key = "ABC" }, nameof(CreateProjectViewModel.Name));
    }

    [Fact]
    public void Register_MatchingPasswords_IsValid()
    {
        AssertValid(new RegisterViewModel
        {
            DisplayName = "Ana",
            Email = "ana@example.com",
            Password = "Clave#12345",
            ConfirmPassword = "Clave#12345"
        });
    }

    [Fact]
    public void Register_MismatchedPasswords_IsInvalid()
    {
        AssertInvalidAt(new RegisterViewModel
        {
            DisplayName = "Ana",
            Email = "ana@example.com",
            Password = "Clave#12345",
            ConfirmPassword = "Clave#54321"
        }, nameof(RegisterViewModel.ConfirmPassword));
    }

    [Fact]
    public void Register_ShortPassword_IsInvalid()
    {
        AssertInvalidAt(new RegisterViewModel
        {
            DisplayName = "Ana",
            Email = "ana@example.com",
            Password = "Corta1!",
            ConfirmPassword = "Corta1!"
        }, nameof(RegisterViewModel.Password));
    }

    [Fact]
    public void Register_InvalidEmail_IsInvalid()
    {
        AssertInvalidAt(new RegisterViewModel
        {
            DisplayName = "Ana",
            Email = "no-es-un-email",
            Password = "Clave#12345",
            ConfirmPassword = "Clave#12345"
        }, nameof(RegisterViewModel.Email));
    }

    [Fact]
    public void Login_RequiresEmailAndPassword()
    {
        var vm = new LoginViewModel();

        var results = Validate(vm);

        Assert.Contains(results, r => r.MemberNames.Contains(nameof(LoginViewModel.Email)));
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(LoginViewModel.Password)));
    }

    [Fact]
    public void CreateMember_OutOfRangeRole_IsRejected()
    {
        AssertInvalidAt(new CreateMemberViewModel { Email = "x@example.com", Role = (ProjectRole)99 }, nameof(CreateMemberViewModel.Role));
    }

    [Fact]
    public void CreateTask_OutOfRangeStatus_IsRejected()
    {
        AssertInvalidAt(new CreateTaskViewModel { Title = "Tarea", Status = (TaskStatus)99 }, nameof(CreateTaskViewModel.Status));
    }

    [Fact]
    public void CreateTask_OutOfRangePriority_IsRejected()
    {
        AssertInvalidAt(new CreateTaskViewModel { Title = "Tarea", Priority = (TaskPriority)99 }, nameof(CreateTaskViewModel.Priority));
    }

    [Fact]
    public void EditTask_OutOfRangeStatus_IsRejected()
    {
        AssertInvalidAt(new EditTaskViewModel { Title = "Tarea", Status = (TaskStatus)99 }, nameof(EditTaskViewModel.Status));
    }

    [Fact]
    public void CreateTask_MissingTitle_IsRejected()
    {
        AssertInvalidAt(new CreateTaskViewModel(), nameof(CreateTaskViewModel.Title));
    }

    [Fact]
    public void EditTask_Title_OverLimit_IsRejected()
    {
        AssertInvalidAt(
            new EditTaskViewModel { Title = new string('t', 201) },
            nameof(EditTaskViewModel.Title));
    }

    [Fact]
    public void AddComment_EmptyBody_IsRejected()
    {
        AssertInvalidAt(new AddTaskCommentViewModel(), nameof(AddTaskCommentViewModel.Body));
    }

    [Fact]
    public void AddComment_OverLimit_IsRejected()
    {
        AssertInvalidAt(
            new AddTaskCommentViewModel { Body = new string('c', 4001) },
            nameof(AddTaskCommentViewModel.Body));
    }
}