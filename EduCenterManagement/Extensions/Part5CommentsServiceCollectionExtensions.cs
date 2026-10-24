using EduCenterManagement.Repositories; using EduCenterManagement.Services;
namespace Microsoft.Extensions.DependencyInjection;
public static class Part5CommentsServiceCollectionExtensions { public static IServiceCollection AddPart5Comments(this IServiceCollection services){services.AddScoped<IParentChildCommentsRepository,ParentChildCommentsRepository>();services.AddScoped<IParentChildCommentsService,ParentChildCommentsService>();return services;} }
