namespace TaskFlow.Web.Authorization;

public static class Policies
{
    public const string AdminOnly = "AdminOnly";
    public const string ProjectMember = "ProjectMember";
    public const string ProjectManager = "ProjectManager";
}