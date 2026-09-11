namespace TaskFlow.Core.Domain.Enums;

public enum ChangeType
{
    Created = 0,
    StatusChanged = 1,
    PriorityChanged = 2,
    DueDateChanged = 3,
    AssigneeChanged = 4,
    DescriptionChanged = 5,
    LabelChanged = 6
}