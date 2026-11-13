using EduCenterManagement.Data;
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using EduCenterManagement.Repositories.Admin;
using EduCenterManagement.Services.Admin;
using EduCenterManagement.ViewModels.Admin;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace AdminRegression;

// An isolated relational database per test. No application connection string is used.
public sealed class AdminTestDatabase : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    private readonly IServiceScope _scope;
    public ApplicationDbContext Db { get; }
    public IServiceProvider Services => _scope.ServiceProvider;

    private AdminTestDatabase(SqliteConnection connection, ServiceProvider provider, IServiceScope scope)
    {
        _connection = connection;
        _provider = provider;
        _scope = scope;
        Db = Services.GetRequiredService<ApplicationDbContext>();
    }

    public static async Task<AdminTestDatabase> CreateAsync(params IInterceptor[] interceptors)
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(connection)
            .AddInterceptors(interceptors).Options;
        var services = new ServiceCollection();
        services.AddScoped<ApplicationDbContext>(_ => new TestDbContext(options));
        services.AddScoped<IAdminUnitOfWork, AdminUnitOfWork>();
        services.AddScoped<IAdminSubjectRepository, AdminSubjectRepository>();
        services.AddScoped<IAdminSubjectService, AdminSubjectService>();
        services.AddScoped<IAdminTimeSlotRepository, AdminTimeSlotRepository>();
        services.AddScoped<IAdminTimeSlotService, AdminTimeSlotService>();
        services.AddScoped<IAdminTeacherRepository, AdminTeacherRepository>();
        services.AddScoped<IAdminTeacherService, AdminTeacherService>();
        services.AddScoped<IAdminClassRepository, AdminClassRepository>();
        services.AddScoped<IAdminClassService, AdminClassService>();
        services.AddScoped<IClassSessionService, ClassSessionService>();
        services.AddScoped<IClassLifecycleService, ClassLifecycleService>();
        services.AddScoped<IAdminDashboardRepository, AdminDashboardRepository>();
        services.AddScoped<IAdminDashboardService, AdminDashboardService>();
        services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        var fixture = new AdminTestDatabase(connection, provider, provider.CreateScope());
        await fixture.Db.Database.EnsureCreatedAsync();
        return fixture;
    }

    public T Get<T>() where T : notnull => Services.GetRequiredService<T>();

    public async Task<(Subject Subject, TeacherProfile Teacher, TimeSlot Slot)> SeedAsync()
    {
        var subject = new Subject { Name = "Toán" };
        var teacher = new TeacherProfile
        {
            FullName = "Nguyễn Lan", User = new AppUser { Email = "teacher@test.local", Role = UserRole.Teacher },
            TeacherSubjects = new List<TeacherSubject> { new() { Subject = subject } }
        };
        var slot = new TimeSlot { Name = "Ca tối", StartTime = new(17, 0), EndTime = new(19, 0) };
        Db.AddRange(teacher, slot);
        await Db.SaveChangesAsync();
        return (subject, teacher, slot);
    }

    public ClassViewModel ClassForm(Subject subject, TimeSlot slot, TeacherProfile? teacher = null, string name = "MAT-A1")
    {
        var start = DateOnly.FromDateTime(DateTime.Now).AddDays(14);
        return new()
        {
            Name = name, SubjectId = subject.Id, TeacherId = teacher?.Id, StartDate = start, EndDate = start.AddDays(14),
            Fee = 1000000, MaxStudents = 20,
            Schedules = new() { new() { DayOfWeek = Weekday(start), TimeSlotId = slot.Id } }
        };
    }

    public async Task<Enrollment> EnrollAsync(Class cls, EnrollmentStatus status, DateTime? reservedUntil = null)
    {
        var enrollment = new Enrollment
        {
            Class = cls, Status = status, ReservedUntil = reservedUntil,
            Student = new StudentProfile { FullName = "Học viên", User = new AppUser { Email = Guid.NewGuid() + "@student.test" } },
            RegisteredByParent = new ParentProfile
            {
                FullName = "Phụ huynh", LinkingCode = Guid.NewGuid().ToString(),
                User = new AppUser { Email = Guid.NewGuid() + "@parent.test", Role = UserRole.Parent }
            }
        };
        Db.Add(enrollment);
        await Db.SaveChangesAsync();
        return enrollment;
    }

    public static int Weekday(DateOnly date) => date.DayOfWeek == DayOfWeek.Sunday ? 8 : (int)date.DayOfWeek + 1;

    public async ValueTask DisposeAsync()
    {
        _scope.Dispose();
        await _provider.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private sealed class TestDbContext(DbContextOptions<ApplicationDbContext> options) : ApplicationDbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // SQLite cannot order TimeOnly natively; integer ticks preserve production comparison semantics.
            foreach (var entity in modelBuilder.Model.GetEntityTypes())
                foreach (var property in entity.GetProperties().ToList())
                    if (property.ClrType == typeof(TimeOnly))
                        modelBuilder.Entity(entity.ClrType).Property<TimeOnly>(property.Name)
                            .HasConversion(v => v.Ticks, v => new TimeOnly(v));
        }
    }
}
