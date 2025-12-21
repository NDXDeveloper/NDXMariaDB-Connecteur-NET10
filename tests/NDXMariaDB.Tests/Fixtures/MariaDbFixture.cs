using Microsoft.Extensions.Logging;
using Testcontainers.MariaDb;
using Xunit;

namespace NDXMariaDB.Tests.Fixtures;

/// <summary>
/// Fixture pour les tests d'intégration MariaDB utilisant Testcontainers.
/// </summary>
public sealed class MariaDbFixture : IAsyncLifetime
{
    private readonly MariaDbContainer _container;

    public MariaDbFixture()
    {
        _container = new MariaDbBuilder()
            .WithImage("mariadb:11.8")
            .WithDatabase("ndxmariadb_test")
            .WithUsername("testuser")
            .WithPassword("testpassword")
            .WithCommand("--event-scheduler=ON")
            .Build();
    }

    public string ConnectionString => _container.GetConnectionString();

    public MariaDbConnectionOptions GetOptions() => new()
    {
        ConnectionString = ConnectionString,
        Pooling = true,
        MinPoolSize = 0,
        MaxPoolSize = 10
    };

    public IMariaDbConnectionFactory CreateFactory(ILoggerFactory? loggerFactory = null)
    {
        return new MariaDbConnectionFactory(GetOptions(), loggerFactory);
    }

    public IMariaDbConnection CreateConnection(ILogger<MariaDbConnection>? logger = null)
    {
        return new MariaDbConnection(GetOptions(), logger);
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

/// <summary>
/// Collection de tests pour le partage de fixture MariaDB.
/// </summary>
[CollectionDefinition("MariaDB")]
public class MariaDbCollection : ICollectionFixture<MariaDbFixture>
{
}
