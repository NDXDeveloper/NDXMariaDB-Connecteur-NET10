using System.Data;

namespace NDXMariaDB;

/// <summary>
/// Utilitaire pour vérifier l'état de santé de la connexion MariaDB.
/// </summary>
public sealed class MariaDbHealthCheck
{
    private readonly IMariaDbConnectionFactory _connectionFactory;

    /// <summary>
    /// Crée une nouvelle instance du health check.
    /// </summary>
    /// <param name="connectionFactory">Factory de connexions.</param>
    public MariaDbHealthCheck(IMariaDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    /// <summary>
    /// Vérifie si la connexion à la base de données est fonctionnelle.
    /// </summary>
    /// <param name="cancellationToken">Token d'annulation.</param>
    /// <returns>Résultat du health check.</returns>
    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            await using var connection = _connectionFactory.CreateConnection();
            await connection.OpenAsync(cancellationToken);

            // Test simple avec SELECT 1
            var result = await connection.ExecuteScalarAsync<int>("SELECT 1", cancellationToken: cancellationToken);

            var duration = DateTime.UtcNow - startTime;

            if (result == 1)
            {
                return new HealthCheckResult(
                    IsHealthy: true,
                    Message: "Connexion MariaDB fonctionnelle",
                    ResponseTime: duration);
            }

            return new HealthCheckResult(
                IsHealthy: false,
                Message: "Réponse inattendue du serveur",
                ResponseTime: duration);
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            return new HealthCheckResult(
                IsHealthy: false,
                Message: $"Erreur de connexion: {ex.Message}",
                ResponseTime: duration,
                Exception: ex);
        }
    }

    /// <summary>
    /// Récupère les informations sur le serveur MariaDB.
    /// </summary>
    /// <param name="cancellationToken">Token d'annulation.</param>
    /// <returns>Informations sur le serveur.</returns>
    public async Task<ServerInfo> GetServerInfoAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync(cancellationToken);

        var version = await connection.ExecuteScalarAsync<string>("SELECT VERSION()", cancellationToken: cancellationToken);
        var database = await connection.ExecuteScalarAsync<string>("SELECT DATABASE()", cancellationToken: cancellationToken);
        var user = await connection.ExecuteScalarAsync<string>("SELECT USER()", cancellationToken: cancellationToken);
        var connectionId = await connection.ExecuteScalarAsync<long>("SELECT CONNECTION_ID()", cancellationToken: cancellationToken);

        return new ServerInfo
        {
            Version = version ?? "Unknown",
            CurrentDatabase = database ?? "Unknown",
            CurrentUser = user ?? "Unknown",
            ConnectionId = connectionId
        };
    }
}

/// <summary>
/// Résultat d'un health check.
/// </summary>
public sealed record HealthCheckResult(
    bool IsHealthy,
    string Message,
    TimeSpan ResponseTime,
    Exception? Exception = null);

/// <summary>
/// Informations sur le serveur MariaDB.
/// </summary>
public sealed record ServerInfo
{
    /// <summary>
    /// Version du serveur MariaDB.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Base de données actuelle.
    /// </summary>
    public required string CurrentDatabase { get; init; }

    /// <summary>
    /// Utilisateur actuel.
    /// </summary>
    public required string CurrentUser { get; init; }

    /// <summary>
    /// ID de connexion.
    /// </summary>
    public required long ConnectionId { get; init; }
}
