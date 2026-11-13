using EduCenterManagement.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EduCenterManagement.ViewModels.Admin;

public class AdminTeacherIndexData
{
    public List<TeacherProfile> Teachers { get; set; } = new();
    public List<Subject> Subjects { get; set; } = new();
    public PaginationViewModel Pagination { get; set; } = null!;
}

public class AdminTeacherSubjectsData
{
    public string TeacherName { get; set; } = string.Empty;
    public AssignTeacherSubjectsViewModel Model { get; set; } = new();
    public List<SelectListItem> Subjects { get; set; } = new();
}
