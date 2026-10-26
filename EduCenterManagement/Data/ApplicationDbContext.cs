
using EduCenterManagement.Enums;
using EduCenterManagement.Models;
using Microsoft.EntityFrameworkCore;
namespace EduCenterManagement.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        //Khai báo toàn bộ DbSet trong ApplicationDbContext để EF Core nhận diện các Model và tạo bảng database.
        public DbSet<AppUser> Users { get; set; }
        public DbSet<ParentProfile> ParentProfiles { get; set; }
        public DbSet<StudentProfile> StudentProfiles { get; set; }
        public DbSet<TeacherProfile> TeacherProfiles { get; set; }

        public DbSet<Subject> Subjects { get; set; }
        public DbSet<Class> Classes { get; set; }
        public DbSet<TimeSlot> TimeSlots { get; set; }
        public DbSet<ClassSchedule> ClassSchedules { get; set; }
        public DbSet<ClassSession> ClassSessions { get; set; }

        public DbSet<ParentStudentLink> ParentStudentLinks { get; set; }
        public DbSet<Enrollment> Enrollments { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<AttendanceRecord> AttendanceRecords { get; set; }
        public DbSet<TeacherComment> TeacherComments { get; set; }

        public DbSet<Slide> Slides { get; set; }
        public DbSet<Quiz> Quizzes { get; set; }
        public DbSet<QuizQuestion> QuizQuestions { get; set; }
        public DbSet<QuizOption> QuizOptions { get; set; }
        public DbSet<QuizAttempt> QuizAttempts { get; set; }
        public DbSet<QuizAnswer> QuizAnswers { get; set; }

        public DbSet<Notification> Notifications { get; set; }
        public DbSet<EmailOutbox> EmailOutbox { get; set; }
        public DbSet<EmailVerificationToken> EmailVerificationTokens { get; set; }


        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AppUser>()
                .Property(x => x.Role)
                .HasConversion<byte>()
                .HasColumnType("tinyint");

            modelBuilder.Entity<AppUser>()
                .Property(x => x.Status)
                .HasConversion<byte>()
                .HasColumnType("tinyint");

            modelBuilder.Entity<Subject>()
                .Property(x => x.Status)
                .HasConversion<byte>()
                .HasColumnType("tinyint");

            modelBuilder.Entity<Class>()
                .Property(x => x.Status)
                .HasConversion<byte>()
                .HasColumnType("tinyint");

            modelBuilder.Entity<ClassSession>()
                .Property(x => x.Status)
                .HasConversion<byte>()
                .HasColumnType("tinyint");

            modelBuilder.Entity<ParentStudentLink>()
                .Property(x => x.Status)
                .HasConversion<byte>()
                .HasColumnType("tinyint");

            modelBuilder.Entity<Enrollment>()
                .Property(x => x.Status)
                .HasConversion<byte>()
                .HasColumnType("tinyint");

            modelBuilder.Entity<Payment>()
                .Property(x => x.Status)
                .HasConversion<byte>()
                .HasColumnType("tinyint");

            modelBuilder.Entity<AttendanceRecord>()
                .Property(x => x.Status)
                .HasConversion<byte>()
                .HasColumnType("tinyint");

            modelBuilder.Entity<Quiz>()
                .Property(x => x.Status)
                .HasConversion<byte>()
                .HasColumnType("tinyint");

            modelBuilder.Entity<QuizAttempt>()
                .Property(x => x.Status)
                .HasConversion<byte>()
                .HasColumnType("tinyint");

            modelBuilder.Entity<EmailOutbox>()
                .Property(x => x.Status)
                .HasConversion<byte>()
                .HasColumnType("tinyint");

            modelBuilder.Entity<TimeSlot>()
                .Property(x => x.Status)
                .HasConversion<byte>()
                .HasColumnType("tinyint");


            //Cấu hình UNIQUE INDEX để ngăn dữ liệu trùng lặp.

            // USERS
            modelBuilder.Entity<AppUser>()
                .HasIndex(x => x.Email)
                .IsUnique();

            // PROFILES: mỗi tài khoản chỉ có một hồ sơ cho mỗi vai trò
            modelBuilder.Entity<ParentProfile>()
                .HasIndex(x => x.UserId)
                .IsUnique();

            modelBuilder.Entity<ParentProfile>()
                .HasIndex(x => x.LinkingCode)
                .IsUnique();

            modelBuilder.Entity<StudentProfile>()
                .HasIndex(x => x.UserId)
                .IsUnique();

            modelBuilder.Entity<TeacherProfile>()
                .HasIndex(x => x.UserId)
                .IsUnique();

            // PARENT - STUDENT
            modelBuilder.Entity<ParentStudentLink>()
                .HasIndex(x => new { x.ParentId, x.StudentId })
                .IsUnique();

            // CLASS SCHEDULES
            modelBuilder.Entity<ClassSchedule>()
                .HasIndex(x => new
                {
                    x.ClassId,
                    x.DayOfWeek,
                    x.TimeSlotId
                })
                .IsUnique();

            // EMAIL VERIFICATION
            modelBuilder.Entity<EmailVerificationToken>()
                .HasIndex(x => x.TokenHash)
                .IsUnique();

            // ATTENDANCE: mỗi Student chỉ có một record mỗi Session
            modelBuilder.Entity<AttendanceRecord>()
                .HasIndex(x => new { x.SessionId, x.StudentId })
                .IsUnique();

            // QUIZ OPTIONS
            modelBuilder.Entity<QuizOption>()
                .HasIndex(x => new { x.QuestionId, x.SortOrder })
                .IsUnique();

            // QUIZ ATTEMPTS: không trùng số lần làm
            modelBuilder.Entity<QuizAttempt>()
                .HasIndex(x => new
                {
                    x.QuizId,
                    x.StudentId,
                    x.AttemptNumber
                })
                .IsUnique();

            // QUIZ ANSWERS: mỗi câu chỉ có một answer trong một attempt
            modelBuilder.Entity<QuizAnswer>()
                .HasIndex(x => new { x.AttemptId, x.QuestionId })
                .IsUnique();

            // PAYMENTS: chỉ áp dụng UNIQUE khi đã có transaction ID
            modelBuilder.Entity<Payment>()
                .HasIndex(x => x.GatewayTransactionId)
                .IsUnique()
                .HasFilter("[GatewayTransactionId] IS NOT NULL");

            // EMAIL OUTBOX: tránh tạo email trùng
            modelBuilder.Entity<EmailOutbox>()
                .HasIndex(x => x.DeduplicationKey)
                .IsUnique()
                .HasFilter("[DeduplicationKey] IS NOT NULL");



            // USER - PROFILES (1 - 1)
            modelBuilder.Entity<ParentProfile>()
                .HasOne(x => x.User)
                .WithOne()
                .HasForeignKey<ParentProfile>(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<StudentProfile>()
                .HasOne(x => x.User)
                .WithOne()
                .HasForeignKey<StudentProfile>(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TeacherProfile>()
                .HasOne(x => x.User)
                .WithOne()
                .HasForeignKey<TeacherProfile>(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // SUBJECT - CLASSES (1 - N)
            modelBuilder.Entity<Class>()
                .HasOne(x => x.Subject)
                .WithMany()
                .HasForeignKey(x => x.SubjectId)
                .OnDelete(DeleteBehavior.Restrict);

            // TEACHER - CLASSES (1 - N, optional)
            modelBuilder.Entity<Class>()
                .HasOne(x => x.Teacher)
                .WithMany()
                .HasForeignKey(x => x.TeacherId)
                .OnDelete(DeleteBehavior.Restrict);

            // CLASS - PREREQUISITE CLASS (tự tham chiếu, tùy chọn)
            modelBuilder.Entity<Class>()
                .HasOne(x => x.PrerequisiteClass)
                .WithMany()
                .HasForeignKey(x => x.PrerequisiteClassId)
                .OnDelete(DeleteBehavior.Restrict);

            // CLASS - CLASS SCHEDULES
            modelBuilder.Entity<ClassSchedule>()
                .HasOne(x => x.Class)
                .WithMany()
                .HasForeignKey(x => x.ClassId)
                .OnDelete(DeleteBehavior.Restrict);

            // TIME SLOT - CLASS SCHEDULES
            modelBuilder.Entity<ClassSchedule>()
                .HasOne(x => x.TimeSlot)
                .WithMany()
                .HasForeignKey(x => x.TimeSlotId)
                .OnDelete(DeleteBehavior.Restrict);

            // CLASS - CLASS SESSIONS
            modelBuilder.Entity<ClassSession>()
                .HasOne(x => x.Class)
                .WithMany()
                .HasForeignKey(x => x.ClassId)
                .OnDelete(DeleteBehavior.Restrict);

            // CLASS SCHEDULE - CLASS SESSIONS (optional)
            modelBuilder.Entity<ClassSession>()
                .HasOne(x => x.ClassSchedule)
                .WithMany()
                .HasForeignKey(x => x.ClassScheduleId)
                .OnDelete(DeleteBehavior.Restrict);

            // PARENT - STUDENT LINKS
            modelBuilder.Entity<ParentStudentLink>()
                .HasOne(x => x.Parent)
                .WithMany()
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<ParentStudentLink>()
                .HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            // ENROLLMENTS
            modelBuilder.Entity<Enrollment>()
                .HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Enrollment>()
                .HasOne(x => x.Class)
                .WithMany()
                .HasForeignKey(x => x.ClassId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Enrollment>()
                .HasOne(x => x.RegisteredByParent)
                .WithMany()
                .HasForeignKey(x => x.RegisteredByParentId)
                .OnDelete(DeleteBehavior.Restrict);

            // ENROLLMENT - PAYMENTS (1 - N)
            modelBuilder.Entity<Payment>()
                .HasOne(x => x.Enrollment)
                .WithMany()
                .HasForeignKey(x => x.EnrollmentId)
                .OnDelete(DeleteBehavior.Restrict);

            // ATTENDANCE RECORDS
            modelBuilder.Entity<AttendanceRecord>()
                .HasOne(x => x.Session)
                .WithMany()
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AttendanceRecord>()
                .HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AttendanceRecord>()
                .HasOne(x => x.MarkedByTeacher)
                .WithMany()
                .HasForeignKey(x => x.MarkedByTeacherId)
                .OnDelete(DeleteBehavior.Restrict);

            // SLIDES
            modelBuilder.Entity<Slide>()
                .HasOne(x => x.Session)
                .WithMany()
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Slide>()
                .HasOne(x => x.UploadedByTeacher)
                .WithMany()
                .HasForeignKey(x => x.UploadedByTeacherId)
                .OnDelete(DeleteBehavior.Restrict);

            // QUIZZES
            modelBuilder.Entity<Quiz>()
                .HasOne(x => x.Session)
                .WithMany()
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Quiz>()
                .HasOne(x => x.CreatedByTeacher)
                .WithMany()
                .HasForeignKey(x => x.CreatedByTeacherId)
                .OnDelete(DeleteBehavior.Restrict);

            // QUIZ - QUESTIONS
            modelBuilder.Entity<QuizQuestion>()
                .HasOne(x => x.Quiz)
                .WithMany()
                .HasForeignKey(x => x.QuizId)
                .OnDelete(DeleteBehavior.Restrict);

            // QUESTION - OPTIONS
            modelBuilder.Entity<QuizOption>()
                .HasOne(x => x.Question)
                .WithMany()
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);

            // QUIZ ATTEMPTS
            modelBuilder.Entity<QuizAttempt>()
                .HasOne(x => x.Quiz)
                .WithMany()
                .HasForeignKey(x => x.QuizId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<QuizAttempt>()
                .HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            // QUIZ ANSWERS
            modelBuilder.Entity<QuizAnswer>()
                .HasOne(x => x.Attempt)
                .WithMany()
                .HasForeignKey(x => x.AttemptId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<QuizAnswer>()
                .HasOne(x => x.Question)
                .WithMany()
                .HasForeignKey(x => x.QuestionId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<QuizAnswer>()
                .HasOne(x => x.SelectedOption)
                .WithMany()
                .HasForeignKey(x => x.SelectedOptionId)
                .OnDelete(DeleteBehavior.Restrict);

            // TEACHER COMMENTS
            modelBuilder.Entity<TeacherComment>()
                .HasOne(x => x.Teacher)
                .WithMany()
                .HasForeignKey(x => x.TeacherId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TeacherComment>()
                .HasOne(x => x.Student)
                .WithMany()
                .HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TeacherComment>()
                .HasOne(x => x.Class)
                .WithMany()
                .HasForeignKey(x => x.ClassId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TeacherComment>()
                .HasOne(x => x.Session)
                .WithMany()
                .HasForeignKey(x => x.SessionId)
                .OnDelete(DeleteBehavior.Restrict);

            // NOTIFICATIONS
            modelBuilder.Entity<Notification>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // EMAIL OUTBOX
            modelBuilder.Entity<EmailOutbox>()
                .HasOne(x => x.RecipientUser)
                .WithMany()
                .HasForeignKey(x => x.RecipientUserId)
                .OnDelete(DeleteBehavior.Restrict);

            // EMAIL VERIFICATION TOKENS
            modelBuilder.Entity<EmailVerificationToken>()
                .HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);


            // CHECK CONSTRAINTS

            // TIME SLOTS
            modelBuilder.Entity<TimeSlot>()
                .ToTable(t => t.HasCheckConstraint(
                    "CK_TimeSlots_TimeRange",
                    "[StartTime] < [EndTime]"));

            // CLASSES
            modelBuilder.Entity<Class>()
                .ToTable(t =>
                {
                    t.HasCheckConstraint(
                        "CK_Classes_DateRange",
                        "[StartDate] <= [EndDate]");

                    t.HasCheckConstraint(
                        "CK_Classes_MaxStudents",
                        "[MaxStudents] > 0");

                    t.HasCheckConstraint(
                        "CK_Classes_Fee",
                        "[Fee] >= 0");
                });

            // CLASS SESSIONS
            modelBuilder.Entity<ClassSession>()
                .ToTable(t =>
                {
                    t.HasCheckConstraint(
                        "CK_ClassSessions_TimeRange",
                        "[StartsAt] < [EndsAt]");

                    t.HasCheckConstraint(
                        "CK_ClassSessions_SessionNumber",
                        "[SessionNumber] > 0");
                });

            // PAYMENTS
            modelBuilder.Entity<Payment>()
                .ToTable(t => t.HasCheckConstraint(
                    "CK_Payments_Amount",
                    "[Amount] > 0"));

            // QUIZZES
            modelBuilder.Entity<Quiz>()
                .ToTable(t =>
                {
                    t.HasCheckConstraint(
                        "CK_Quizzes_Duration",
                        "[DurationMinutes] > 0");

                    t.HasCheckConstraint(
                        "CK_Quizzes_AvailableRange",
                        "[AvailableFrom] < [AvailableUntil]");

                    t.HasCheckConstraint(
                        "CK_Quizzes_MaxAttempts",
                        "[MaxAttempts] > 0");
                });

            // QUIZ QUESTIONS
            modelBuilder.Entity<QuizQuestion>()
                .ToTable(t =>
                {
                    t.HasCheckConstraint(
                        "CK_QuizQuestions_Points",
                        "[Points] > 0");

                    t.HasCheckConstraint(
                        "CK_QuizQuestions_SortOrder",
                        "[SortOrder] > 0");
                });

            // QUIZ OPTIONS
            modelBuilder.Entity<QuizOption>()
                .ToTable(t => t.HasCheckConstraint(
                    "CK_QuizOptions_SortOrder",
                    "[SortOrder] > 0"));

            // QUIZ ATTEMPTS
            modelBuilder.Entity<QuizAttempt>()
                .ToTable(t =>
                {
                    t.HasCheckConstraint(
                        "CK_QuizAttempts_AttemptNumber",
                        "[AttemptNumber] > 0");

                    t.HasCheckConstraint(
                        "CK_QuizAttempts_Score",
                        "[Score] IS NULL OR [Score] >= 0");
                });

            // QUIZ ANSWERS
            modelBuilder.Entity<QuizAnswer>()
                .ToTable(t => t.HasCheckConstraint(
                    "CK_QuizAnswers_EarnedPoints",
                    "[EarnedPoints] >= 0"));

        }

    }
}
