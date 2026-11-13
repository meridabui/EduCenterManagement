namespace EduCenterManagement.Services.Admin;

// Services return business outcomes; controllers decide how to display them.
public enum AdminResultStatus { Success, Invalid, NotFound, Conflict, BadRequest }
public record AdminValidationError(string Field, string Message);

public class AdminResult
{
    public AdminResultStatus Status { get; set; } = AdminResultStatus.Success;
    public bool Success => Status == AdminResultStatus.Success;
    public string Message { get; set; } = string.Empty;
    public List<AdminValidationError> Errors { get; set; } = new();
    public static AdminResult Ok(string message = "") => new() { Message = message };
    public static AdminResult Missing() => new() { Status = AdminResultStatus.NotFound };
    public static AdminResult Invalid(string message, string field = "") => new()
    {
        Status = AdminResultStatus.Invalid, Message = message, Errors = new() { new(field, message) }
    };
}

public class AdminResult<T> : AdminResult
{
    public T? Data { get; set; }
}
