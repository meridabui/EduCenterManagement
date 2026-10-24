using EduCenterManagement.Repositories;
using EduCenterManagement.Services;
using EduCenterManagement.Services.Common;
namespace Microsoft.Extensions.DependencyInjection;
public static class Part5ServiceCollectionExtensions { public static IServiceCollection AddPart5Services(this IServiceCollection services){services.AddHttpContextAccessor();services.AddScoped<ICurrentUserContext,QueryStringCurrentUserContext>();services.AddScoped<INotificationRepository,NotificationRepository>();services.AddScoped<INotificationService,NotificationService>();services.AddScoped<ITimetableRepository,TimetableRepository>();services.AddScoped<ITimetableService,TimetableService>();services.AddScoped<IParentChildrenRepository,ParentChildrenRepository>();services.AddScoped<IParentChildrenService,ParentChildrenService>();return services;} }
