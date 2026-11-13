using EduCenterManagement.Data;
using EduCenterManagement.Models;
using Microsoft.EntityFrameworkCore;

namespace EduCenterManagement.Repositories.Admin;

public class AdminSubjectRepository : IAdminSubjectRepository
{
    private readonly ApplicationDbContext _context;
    public AdminSubjectRepository(ApplicationDbContext context) => _context = context;
    public Task<int> CountAsync() => _context.Subjects.CountAsync();
    public Task<List<Subject>> GetPageAsync(int offset, int pageSize) => _context.Subjects.AsNoTracking()
        .OrderBy(s => s.Name).ThenBy(s => s.Id).Skip(offset).Take(pageSize).ToListAsync();
    public Task<Subject?> GetByIdAsync(int id, bool tracking = false)
        => (tracking ? _context.Subjects : _context.Subjects.AsNoTracking()).FirstOrDefaultAsync(s => s.Id == id);
    public Task<bool> NameExistsAsync(string name, int? exceptId = null)
        => _context.Subjects.AnyAsync(s => s.Name == name && (!exceptId.HasValue || s.Id != exceptId.Value));
    public void Add(Subject subject) => _context.Subjects.Add(subject);
}
