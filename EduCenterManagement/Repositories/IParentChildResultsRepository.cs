using EduCenterManagement.ViewModels.ParentChildren;
namespace EduCenterManagement.Repositories;
public interface IParentChildResultsRepository { Task<QuizResultsPageViewModel?> GetQuizResultsAsync(int parentProfileId,int studentId,int? classId,int page,int pageSize,CancellationToken ct=default); }
