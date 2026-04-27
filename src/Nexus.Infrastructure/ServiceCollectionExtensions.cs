using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nexus.Application.Interfaces;
using Nexus.Infrastructure.Data;
using Nexus.Infrastructure.Options;
using Nexus.Infrastructure.Services;

namespace Nexus.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<NexusDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("NexusDb")));

        services.Configure<OpenAiOptions>(options =>
            configuration.GetSection(OpenAiOptions.SectionName).Bind(options));
        services.AddHttpClient<ISemanticDuplicateAnalyzer, OpenAiSemanticDuplicateAnalyzer>();
        services.AddScoped<ISanitizationService, SanitizationService>();
        services.AddScoped<ICommissionCalculator, CommissionCalculator>();

        return services;
    }
}
