using EduCenterManagement.ViewModels.ParentChildren;
namespace EduCenterManagement.Services;
public interface IParentChildCommentsService { Task<CommentsPageViewModel?> GetCommentsAsync(int studentId,int? classId,int page,CancellationToken ct=default); }
