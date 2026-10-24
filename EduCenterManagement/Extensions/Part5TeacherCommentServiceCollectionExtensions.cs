using EduCenterManagement.Repositories;using EduCenterManagement.Services;
namespace Microsoft.Extensions.DependencyInjection;
public static class Part5TeacherCommentServiceCollectionExtensions { public static IServiceCollection AddPart5TeacherComments(this IServiceCollection services){services.AddScoped<ITeacherCommentRepository,TeacherCommentRepository>();services.AddScoped<ITeacherCommentService,TeacherCommentService>();return services;} }
