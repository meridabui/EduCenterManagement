using EduCenterManagement.ViewModels.ParentChildren;
namespace EduCenterManagement.Services;
public interface IParentChildResultsService { Task<QuizResultsPageViewModel?> GetQuizResultsAsync(int studentId, int? classId, int page, CancellationToken ct = default); }
