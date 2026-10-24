using EduCenterManagement.ViewModels.ParentChildren;
namespace EduCenterManagement.Repositories;
public interface IParentChildCommentsRepository { Task<CommentsPageViewModel?> GetCommentsAsync(int parentProfileId, int studentId, int? classId, int page, int pageSize, CancellationToken ct = default); }
