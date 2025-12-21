using Microsoft.Extensions.Logging;

namespace NDXMariaDB;

/// <summary>
/// Factory pour créer des instances de connexion MariaDB.
/// Permet de gérer la création centralisée des connexions avec des options par défaut.
/// </summary>
public sealed class MariaDbConnectionFactory : IMariaDbConnectionFactory
{
    private readonly MariaDbConnectionOptions _defaultOptions;
    private readonly ILoggerFactory? _loggerFactory;

    /// <summary>
    /// Crée une nouvelle factory avec les options par défaut.
    /// </summary>
    /// <param name="defaultOptions">Options de connexion par défaut.</param>
    /// <param name="loggerFactory">Factory de logger optionnelle.</param>
    public MariaDbConnectionFactory(MariaDbConnectionOptions defaultOptions, ILoggerFactory? loggerFactory = null)
    {
        _defaultOptions = defaultOptions ?? throw new ArgumentNullException(nameof(defaultOptions));
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Crée une nouvelle factory à partir d'une chaîne de connexion.
    /// </summary>
    /// <param name="connectionString">Chaîne de connexion.</param>
    /// <param name="loggerFactory">Factory de logger optionnelle.</param>
    public MariaDbConnectionFactory(string connectionString, ILoggerFactory? loggerFactory = null)
        : this(new MariaDbConnectionOptions { ConnectionString = connectionString }, loggerFactory)
    {
    }

    /// <inheritdoc />
    public IMariaDbConnection CreateConnection()
    {
        return CreateConnection(_defaultOptions);
    }

    /// <inheritdoc />
    public IMariaDbConnection CreateConnection(MariaDbConnectionOptions options)
    {
        var logger = _loggerFactory?.CreateLogger<MariaDbConnection>();
        return new MariaDbConnection(options, logger);
    }

    /// <inheritdoc />
    public IMariaDbConnection CreatePrimaryConnection()
    {
        var options = CloneOptions(_defaultOptions);
        options.IsPrimaryConnection = true;
        options.DisableAutoClose = true;
        return CreateConnection(options);
    }

    /// <inheritdoc />
    public IMariaDbConnection CreateConnection(Action<MariaDbConnectionOptions> configure)
    {
        var options = CloneOptions(_defaultOptions);
        configure(options);
        return CreateConnection(options);
    }

    private static MariaDbConnectionOptions CloneOptions(MariaDbConnectionOptions source)
    {
        return new MariaDbConnectionOptions
        {
            Server = source.Server,
            Port = source.Port,
            Database = source.Database,
            Username = source.Username,
            Password = source.Password,
            ConnectionString = source.ConnectionString,
            IsPrimaryConnection = source.IsPrimaryConnection,
            AutoCloseTimeoutMs = source.AutoCloseTimeoutMs,
            DisableAutoClose = source.DisableAutoClose,
            Pooling = source.Pooling,
            MinPoolSize = source.MinPoolSize,
            MaxPoolSize = source.MaxPoolSize,
            ConnectionTimeoutSeconds = source.ConnectionTimeoutSeconds,
            CommandTimeoutSeconds = source.CommandTimeoutSeconds,
            InnoDbLockWaitTimeout = source.InnoDbLockWaitTimeout,
            UseSsl = source.UseSsl,
            SslMode = source.SslMode
        };
    }
}

/// <summary>
/// Interface pour la factory de connexions MariaDB.
/// </summary>
public interface IMariaDbConnectionFactory
{
    /// <summary>
    /// Crée une nouvelle connexion avec les options par défaut.
    /// </summary>
    IMariaDbConnection CreateConnection();

    /// <summary>
    /// Crée une nouvelle connexion avec les options spécifiées.
    /// </summary>
    IMariaDbConnection CreateConnection(MariaDbConnectionOptions options);

    /// <summary>
    /// Crée une connexion principale (ne sera pas fermée automatiquement).
    /// </summary>
    IMariaDbConnection CreatePrimaryConnection();

    /// <summary>
    /// Crée une connexion en configurant les options à partir des options par défaut.
    /// </summary>
    IMariaDbConnection CreateConnection(Action<MariaDbConnectionOptions> configure);
}
