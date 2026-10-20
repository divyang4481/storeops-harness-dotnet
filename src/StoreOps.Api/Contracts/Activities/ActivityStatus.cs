namespace StoreOps.Api.Contracts.Activities;

// TaskStatus in the capstone specification: TODO | IN_PROGRESS | DONE | BLOCKED. Done is terminal.
public enum ActivityStatus
{
    Todo,
    InProgress,
    Done,
    Blocked,
}
