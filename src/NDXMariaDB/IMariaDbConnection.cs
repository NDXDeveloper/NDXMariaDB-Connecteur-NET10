using System.Data;
using MySqlConnector;

namespace NDXMariaDB;

/// <summary>
/// Interface pour la gestion des connexions MariaDB.
/// </summary>
public interface IMariaDbConnection : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Identifiant unique de la connexion.
    /// </summary>
    int Id { get; }

    /// <summary>
    /// Date de création de la connexion.
    /// </summary>
    DateTime CreatedAt { get; }

    /// <summary>
    /// État actuel de la connexion.
    /// </summary>
    ConnectionState State { get; }

    /// <summary>
    /// Indique si une transaction est en cours.
    /// </summary>
    bool IsTransactionActive { get; }

    /// <summary>
    /// Indique si c'est la connexion principale.
    /// </summary>
    bool IsPrimaryConnection { get; }

    /// <summary>
    /// Connexion MySql sous-jacente.
    /// </summary>
    MySqlConnection? Connection { get; }

    /// <summary>
    /// Transaction en cours (null si aucune).
    /// </summary>
    MySqlTransaction? Transaction { get; }

    /// <summary>
    /// Dernière action effectuée sur la connexion.
    /// </summary>
    string LastAction { get; }

    /// <summary>
    /// Historique des dernières actions (5 dernières).
    /// </summary>
    IReadOnlyList<string> ActionHistory { get; }

    /// <summary>
    /// Ouvre la connexion de manière synchrone.
    /// </summary>
    void Open();

    /// <summary>
    /// Ouvre la connexion de manière asynchrone.
    /// </summary>
    Task OpenAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Ferme la connexion de manière synchrone.
    /// </summary>
    void Close();

    /// <summary>
    /// Ferme la connexion de manière asynchrone.
    /// </summary>
    Task CloseAsync();

    /// <summary>
    /// Démarre une transaction de manière synchrone.
    /// </summary>
    bool BeginTransaction(IsolationLevel isolationLevel = IsolationLevel.ReadUncommitted);

    /// <summary>
    /// Démarre une transaction de manière asynchrone.
    /// </summary>
    Task<bool> BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadUncommitted, CancellationToken cancellationToken = default);

    /// <summary>
    /// Valide la transaction en cours de manière synchrone.
    /// </summary>
    void Commit();

    /// <summary>
    /// Valide la transaction en cours de manière asynchrone.
    /// </summary>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Annule la transaction en cours de manière synchrone.
    /// </summary>
    void Rollback();

    /// <summary>
    /// Annule la transaction en cours de manière asynchrone.
    /// </summary>
    Task RollbackAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Réinitialise le timer de fermeture automatique.
    /// </summary>
    void ResetAutoCloseTimer();

    /// <summary>
    /// Exécute une requête et retourne le nombre de lignes affectées.
    /// </summary>
    Task<int> ExecuteNonQueryAsync(string query, object? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exécute une requête et retourne une valeur scalaire.
    /// </summary>
    Task<T?> ExecuteScalarAsync<T>(string query, object? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exécute une requête et retourne un DataTable.
    /// </summary>
    Task<DataTable> ExecuteQueryAsync(string query, object? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exécute une requête et retourne un reader.
    /// </summary>
    Task<MySqlDataReader> ExecuteReaderAsync(string query, object? parameters = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Crée une nouvelle commande liée à cette connexion.
    /// </summary>
    MySqlCommand CreateCommand(string? commandText = null);
}
