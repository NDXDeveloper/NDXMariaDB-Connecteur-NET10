using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NDXMariaDB.Extensions;

/// <summary>
/// Extensions pour l'injection de dépendances avec Microsoft.Extensions.DependencyInjection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Ajoute les services NDXMariaDB au conteneur d'injection de dépendances.
    /// </summary>
    /// <param name="services">Collection de services.</param>
    /// <param name="configure">Action de configuration des options.</param>
    /// <returns>La collection de services pour le chaînage.</returns>
    public static IServiceCollection AddNDXMariaDB(
        this IServiceCollection services,
        Action<MariaDbConnectionOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new MariaDbConnectionOptions();
        configure(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton<IMariaDbConnectionFactory, MariaDbConnectionFactory>();
        services.TryAddTransient<IMariaDbConnection>(sp =>
        {
            var factory = sp.GetRequiredService<IMariaDbConnectionFactory>();
            return factory.CreateConnection();
        });

        return services;
    }

    /// <summary>
    /// Ajoute les services NDXMariaDB avec une chaîne de connexion.
    /// </summary>
    /// <param name="services">Collection de services.</param>
    /// <param name="connectionString">Chaîne de connexion MariaDB.</param>
    /// <returns>La collection de services pour le chaînage.</returns>
    public static IServiceCollection AddNDXMariaDB(
        this IServiceCollection services,
        string connectionString)
    {
        return services.AddNDXMariaDB(options =>
        {
            options.ConnectionString = connectionString;
        });
    }
}
