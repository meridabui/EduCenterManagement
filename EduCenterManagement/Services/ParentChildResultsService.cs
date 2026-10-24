using EduCenterManagement.Repositories;
using EduCenterManagement.Services.Common;
using EduCenterManagement.ViewModels.ParentChildren;

namespace EduCenterManagement.Services;

public sealed class ParentChildResultsService(ICurrentUserContext currentUserContext, IParentChildResultsRepository repository) : IParentChildResultsService
{
    public async Task<QuizResultsPageViewModel?> GetQuizResultsAsync(int studentId, int? classId, int page, CancellationToken ct = default)
    {
        var parentProfileId = await currentUserContext.GetParentProfileIdAsync(ct);
        return parentProfileId is null ? null : await repository.GetQuizResultsAsync(parentProfileId.Value, studentId, classId, Math.Max(1, page), 10, ct);
    }
}
