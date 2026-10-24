using EduCenterManagement.Data;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Services.Common;

// TEMP: no authentication yet. Replace QueryStringCurrentUserContext with claims-based implementation and add [Authorize] when auth is merged.
public sealed class QueryStringCurrentUserContext(IHttpContextAccessor httpContextAccessor, ApplicationDbContext db) : ICurrentUserContext
{
    private const string CookieName = "ecm_current_user";
    public int? UserId
    {
        get
        {
            var context = httpContextAccessor.HttpContext;
            if (context is null) return null;
            if (int.TryParse(context.Request.Query["userId"], out var queryId) && queryId > 0)
            {
                context.Response.Cookies.Append(CookieName, queryId.ToString(), new CookieOptions { HttpOnly = true, IsEssential = true });
                return queryId;
            }
            return int.TryParse(context.Request.Cookies[CookieName], out var cookieId) && cookieId > 0 ? cookieId : null;
        }
    }
    public async Task<int?> GetStudentProfileIdAsync(CancellationToken ct = default) => UserId is int id ? await db.StudentProfiles.AsNoTracking().Where(x => x.UserId == id).Select(x => (int?)x.Id).SingleOrDefaultAsync(ct) : null;
    public async Task<int?> GetParentProfileIdAsync(CancellationToken ct = default) => UserId is int id ? await db.ParentProfiles.AsNoTracking().Where(x => x.UserId == id).Select(x => (int?)x.Id).SingleOrDefaultAsync(ct) : null;
    public async Task<int?> GetTeacherProfileIdAsync(CancellationToken ct = default) => UserId is int id ? await db.TeacherProfiles.AsNoTracking().Where(x => x.UserId == id).Select(x => (int?)x.Id).SingleOrDefaultAsync(ct) : null;
}
