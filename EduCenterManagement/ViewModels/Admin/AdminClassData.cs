using EduCenterManagement.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EduCenterManagement.ViewModels.Admin;

public class AdminClassFormData
{
    public SelectList Subjects { get; set; } = null!;
    public SelectList Teachers { get; set; } = null!;
    public List<SelectListItem> TimeSlots { get; set; } = new();
}

public class AdminClassIndexData
{
    public List<Class> Classes { get; set; } = new();
    public AdminClassFormData Form { get; set; } = new();
    public SelectList FilterSubjects { get; set; } = null!;
    public SelectList FilterTeachers { get; set; } = null!;
    public List<SelectListItem> FilterStatuses { get; set; } = new();
    public bool HasClassFilters { get; set; }
    public PaginationViewModel Pagination { get; set; } = null!;
}

public class AdminClassDetailsData
{
    public Class Class { get; set; } = null!;
    public int SessionCount { get; set; }
    public bool HasPaidEnrollments { get; set; }
}

public class AdminClassEditData
{
    public ClassViewModel Model { get; set; } = null!;
    public AdminClassFormData Form { get; set; } = new();
}
