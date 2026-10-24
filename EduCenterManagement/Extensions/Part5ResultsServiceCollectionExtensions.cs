using EduCenterManagement.Repositories;
using EduCenterManagement.Services;

namespace Microsoft.Extensions.DependencyInjection;

public static class Part5ResultsServiceCollectionExtensions
{
    public static IServiceCollection AddPart5Results(this IServiceCollection services)
    {
        services.AddScoped<IParentChildResultsRepository, ParentChildResultsRepository>();
        services.AddScoped<IParentChildResultsService, ParentChildResultsService>();
        return services;
    }
}
