using EduCenterManagement.ViewModels.Timetable;
namespace EduCenterManagement.Repositories;
public interface ITimetableRepository { Task<IReadOnlyList<TimetableItemViewModel>> GetWeekAsync(int studentId,DateOnly start,DateOnly end,CancellationToken ct=default); }
