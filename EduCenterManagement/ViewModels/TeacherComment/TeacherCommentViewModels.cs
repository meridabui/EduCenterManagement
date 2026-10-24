using System.ComponentModel.DataAnnotations;
namespace EduCenterManagement.ViewModels.TeacherComment;
public sealed class TeacherClassViewModel { public int Id { get; init; } public string Name { get; init; }=""; public string Subject { get; init; }=""; }
public sealed class TeacherStudentViewModel { public int Id { get; init; } public string Name { get; init; }=""; public int CommentCount { get; init; } }
public sealed class TeacherCommentFormViewModel { public int? Id { get; set; } public int ClassId { get; set; } public int StudentId { get; set; } public int? SessionId { get; set; } [Range(1,5)] public int Rating { get; set; } [Required][StringLength(2000,MinimumLength=1)] public string Content { get; set; }=""; public IReadOnlyList<TeacherSessionOptionViewModel> Sessions { get; set; }=Array.Empty<TeacherSessionOptionViewModel>(); }
public sealed class TeacherSessionOptionViewModel { public int Id { get; init; } public string Name { get; init; }=""; }
public sealed class TeacherCommentHistoryItemViewModel { public int Id { get; init; } public int Rating { get; init; } public string Content { get; init; }=""; public int? SessionNumber { get; init; } public DateTime CreatedAt { get; init; } }
