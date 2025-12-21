using System.Data;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace NDXMariaDB;

/// <summary>
/// Classe de connexion MariaDB/MySQL moderne et performante.
/// Gère les connexions, transactions, timers de fermeture automatique et logging.
/// Compatible Windows et Linux.
/// </summary>
/// <remarks>
/// <para>Portée et modernisée depuis Class_MySql.vb (.NET Framework 4.7.2)</para>
/// <para>Auteur original: Nicolas DEOUX </para>
/// <para>Modernisation: Nicolas DEOUX - NDXDev 2025</para>
/// </remarks>
public sealed class MariaDbConnection : IMariaDbConnection
{
    #region Constantes

    private const int DefaultAutoCloseTimeoutMs = 60_000; // 1 minute
    private const int MaxActionHistorySize = 5;

    #endregion

    #region Champs privés

    private static int _connectionCounter;
    private readonly object _actionLock = new();
    private readonly object _disposeLock = new();
    private readonly ILogger<MariaDbConnection>? _logger;
    private readonly MariaDbConnectionOptions _options;
    private readonly Timer? _autoCloseTimer;
    private readonly List<string> _actionHistory = new(MaxActionHistorySize + 1);

    private MySqlConnection? _connection;
    private MySqlTransaction? _transaction;
    private string _lastAction = string.Empty;
    private bool _isDisposed;
    private bool _isTransactionActive;

    #endregion

    #region Propriétés

    /// <inheritdoc />
    public int Id { get; }

    /// <inheritdoc />
    public DateTime CreatedAt { get; }

    /// <inheritdoc />
    public ConnectionState State
    {
        get
        {
            if (_connection is null)
                return ConnectionState.Closed;
            return _connection.State;
        }
    }

    /// <inheritdoc />
    public bool IsTransactionActive => _isTransactionActive;

    /// <inheritdoc />
    public bool IsPrimaryConnection => _options.IsPrimaryConnection;

    /// <inheritdoc />
    public MySqlConnection? Connection => _connection;

    /// <inheritdoc />
    public MySqlTransaction? Transaction => _transaction;

    /// <inheritdoc />
    public string LastAction
    {
        get
        {
            lock (_actionLock)
            {
                return _lastAction;
            }
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> ActionHistory
    {
        get
        {
            lock (_actionLock)
            {
                return _actionHistory.ToList().AsReadOnly();
            }
        }
    }

    #endregion

    #region Constructeurs

    /// <summary>
    /// Crée une nouvelle connexion MariaDB avec les options spécifiées.
    /// </summary>
    /// <param name="options">Options de configuration de la connexion.</param>
    /// <param name="logger">Logger optionnel pour le suivi des opérations.</param>
    public MariaDbConnection(MariaDbConnectionOptions options, ILogger<MariaDbConnection>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
        _logger = logger;

        Id = Interlocked.Increment(ref _connectionCounter);
        CreatedAt = DateTime.UtcNow;

        // Création de la connexion
        _connection = new MySqlConnection(options.BuildConnectionString());
        _connection.StateChange += OnConnectionStateChanged;

        // Configuration du timer de fermeture automatique (sauf pour connexion principale)
        if (!options.IsPrimaryConnection && !options.DisableAutoClose)
        {
            _autoCloseTimer = new Timer(
                OnAutoCloseTimerElapsed,
                null,
                Timeout.Infinite,
                Timeout.Infinite);
        }

        LogAction("New", $"Nouvelle connexion {(options.IsPrimaryConnection ? "principale" : "secondaire")} créée");
    }

    /// <summary>
    /// Crée une nouvelle connexion MariaDB avec une chaîne de connexion.
    /// </summary>
    /// <param name="connectionString">Chaîne de connexion MariaDB.</param>
    /// <param name="isPrimary">Indique si c'est la connexion principale.</param>
    /// <param name="logger">Logger optionnel.</param>
    public MariaDbConnection(string connectionString, bool isPrimary = false, ILogger<MariaDbConnection>? logger = null)
        : this(new MariaDbConnectionOptions { ConnectionString = connectionString, IsPrimaryConnection = isPrimary }, logger)
    {
    }

    #endregion

    #region Méthodes de connexion

    /// <inheritdoc />
    public void Open()
    {
        ThrowIfDisposed();

        if (_connection is null)
        {
            throw new InvalidOperationException("La connexion n'est pas initialisée.");
        }

        if (_connection.State != ConnectionState.Open)
        {
            _connection.Open();
            SetSessionOptions();
            LogAction("Open", "Connexion ouverte");
        }

        ResetAutoCloseTimer();
    }

    /// <inheritdoc />
    public async Task OpenAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (_connection is null)
        {
            throw new InvalidOperationException("La connexion n'est pas initialisée.");
        }

        if (_connection.State != ConnectionState.Open)
        {
            await _connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            await SetSessionOptionsAsync(cancellationToken).ConfigureAwait(false);
            LogAction("OpenAsync", "Connexion ouverte (async)");
        }

        ResetAutoCloseTimer();
    }

    /// <inheritdoc />
    public void Close()
    {
        if (_connection is not null && _connection.State != ConnectionState.Closed)
        {
            _connection.Close();
            LogAction("Close", "Connexion fermée");
        }
    }

    /// <inheritdoc />
    public async Task CloseAsync()
    {
        if (_connection is not null && _connection.State != ConnectionState.Closed)
        {
            await _connection.CloseAsync().ConfigureAwait(false);
            LogAction("CloseAsync", "Connexion fermée (async)");
        }
    }

    #endregion

    #region Méthodes de transaction

    /// <inheritdoc />
    public bool BeginTransaction(IsolationLevel isolationLevel = IsolationLevel.ReadUncommitted)
    {
        ThrowIfDisposed();

        try
        {
            EnsureConnectionOpen();
            _transaction = _connection!.BeginTransaction(isolationLevel);
            _isTransactionActive = true;
            LogAction("BeginTransaction", $"Transaction démarrée (IsolationLevel: {isolationLevel})");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Erreur lors du démarrage de la transaction");
            _transaction = null;
            _isTransactionActive = false;
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> BeginTransactionAsync(
        IsolationLevel isolationLevel = IsolationLevel.ReadUncommitted,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        try
        {
            await EnsureConnectionOpenAsync(cancellationToken).ConfigureAwait(false);
            _transaction = await _connection!.BeginTransactionAsync(isolationLevel, cancellationToken).ConfigureAwait(false);
            _isTransactionActive = true;
            LogAction("BeginTransactionAsync", $"Transaction démarrée (IsolationLevel: {isolationLevel})");
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Erreur lors du démarrage de la transaction");
            _transaction = null;
            _isTransactionActive = false;
            throw;
        }
    }

    /// <inheritdoc />
    public void Commit()
    {
        if (_transaction is null)
        {
            return;
        }

        try
        {
            _transaction.Commit();
            LogAction("Commit", "Transaction validée");
        }
        finally
        {
            _transaction = null;
            _isTransactionActive = false;
        }
    }

    /// <inheritdoc />
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            return;
        }

        try
        {
            await _transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            LogAction("CommitAsync", "Transaction validée (async)");
        }
        finally
        {
            _transaction = null;
            _isTransactionActive = false;
        }
    }

    /// <inheritdoc />
    public void Rollback()
    {
        if (_transaction is null)
        {
            return;
        }

        try
        {
            _transaction.Rollback();
            LogAction("Rollback", "Transaction annulée");
        }
        finally
        {
            _transaction = null;
            _isTransactionActive = false;
        }
    }

    /// <inheritdoc />
    public async Task RollbackAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction is null)
        {
            return;
        }

        try
        {
            await _transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            LogAction("RollbackAsync", "Transaction annulée (async)");
        }
        finally
        {
            _transaction = null;
            _isTransactionActive = false;
        }
    }

    #endregion

    #region Méthodes d'exécution de requêtes

    /// <inheritdoc />
    public async Task<int> ExecuteNonQueryAsync(
        string query,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureConnectionOpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = CreateCommandInternal(query, parameters);
        var result = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        ResetAutoCloseTimer();
        return result;
    }

    /// <inheritdoc />
    public async Task<T?> ExecuteScalarAsync<T>(
        string query,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureConnectionOpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = CreateCommandInternal(query, parameters);
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);

        ResetAutoCloseTimer();

        if (result is null || result == DBNull.Value)
        {
            return default;
        }

        return (T)Convert.ChangeType(result, typeof(T));
    }

    /// <inheritdoc />
    public async Task<DataTable> ExecuteQueryAsync(
        string query,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureConnectionOpenAsync(cancellationToken).ConfigureAwait(false);

        await using var command = CreateCommandInternal(query, parameters);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        var dataTable = new DataTable();
        dataTable.Load(reader);

        ResetAutoCloseTimer();
        return dataTable;
    }

    /// <inheritdoc />
    public async Task<MySqlDataReader> ExecuteReaderAsync(
        string query,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        await EnsureConnectionOpenAsync(cancellationToken).ConfigureAwait(false);

        var command = CreateCommandInternal(query, parameters);
        var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        ResetAutoCloseTimer();
        return reader;
    }

    /// <inheritdoc />
    public MySqlCommand CreateCommand(string? commandText = null)
    {
        ThrowIfDisposed();

        var command = _connection!.CreateCommand();
        command.Transaction = _transaction;

        if (!string.IsNullOrEmpty(commandText))
        {
            command.CommandText = commandText;
        }

        return command;
    }

    #endregion

    #region Gestion du timer de fermeture automatique

    /// <inheritdoc />
    public void ResetAutoCloseTimer()
    {
        if (_autoCloseTimer is null || _isTransactionActive)
        {
            return;
        }

        _autoCloseTimer.Change(_options.AutoCloseTimeoutMs, Timeout.Infinite);
        _logger?.LogDebug("Timer de fermeture automatique réinitialisé pour la connexion {ConnectionId}", Id);
    }

    private void OnAutoCloseTimerElapsed(object? state)
    {
        if (_isTransactionActive || _options.DisableAutoClose || _options.IsPrimaryConnection)
        {
            return;
        }

        try
        {
            if (_connection?.State == ConnectionState.Open)
            {
                Close();
                LogAction("AutoClose", "Connexion fermée automatiquement (timeout)");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Erreur lors de la fermeture automatique de la connexion {ConnectionId}", Id);
        }
    }

    #endregion

    #region Configuration de session

    private void SetSessionOptions()
    {
        if (_connection?.State != ConnectionState.Open)
        {
            return;
        }

        using var command = _connection.CreateCommand();
        command.CommandText = $"SET @@session.innodb_lock_wait_timeout = {_options.InnoDbLockWaitTimeout}";
        command.ExecuteNonQuery();
    }

    private async Task SetSessionOptionsAsync(CancellationToken cancellationToken = default)
    {
        if (_connection?.State != ConnectionState.Open)
        {
            return;
        }

        await using var command = _connection.CreateCommand();
        command.CommandText = $"SET @@session.innodb_lock_wait_timeout = {_options.InnoDbLockWaitTimeout}";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    #endregion

    #region Méthodes utilitaires privées

    private void EnsureConnectionOpen()
    {
        if (_connection?.State != ConnectionState.Open)
        {
            Open();
        }
    }

    private async Task EnsureConnectionOpenAsync(CancellationToken cancellationToken = default)
    {
        if (_connection?.State != ConnectionState.Open)
        {
            await OpenAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private MySqlCommand CreateCommandInternal(string query, object? parameters)
    {
        var command = CreateCommand(query);

        if (parameters is not null)
        {
            AddParameters(command, parameters);
        }

        return command;
    }

    private static void AddParameters(MySqlCommand command, object parameters)
    {
        var properties = parameters.GetType().GetProperties();

        foreach (var property in properties)
        {
            var value = property.GetValue(parameters);
            var paramName = property.Name.StartsWith('@') ? property.Name : $"@{property.Name}";
            command.Parameters.AddWithValue(paramName, value ?? DBNull.Value);
        }
    }

    private void OnConnectionStateChanged(object sender, StateChangeEventArgs e)
    {
        if (e.CurrentState == ConnectionState.Open)
        {
            ResetAutoCloseTimer();
        }

        _logger?.LogDebug(
            "État de la connexion {ConnectionId} changé: {OldState} -> {NewState}",
            Id, e.OriginalState, e.CurrentState);
    }

    private void LogAction(string action, string description, [CallerMemberName] string? callerName = null)
    {
        lock (_actionLock)
        {
            var logEntry = $"[{DateTime.UtcNow:HH:mm:ss.fff}] [{action}] {description}";

            // Décalage de l'historique
            if (_actionHistory.Count >= MaxActionHistorySize)
            {
                _actionHistory.RemoveAt(_actionHistory.Count - 1);
            }

            if (!string.IsNullOrEmpty(_lastAction))
            {
                _actionHistory.Insert(0, _lastAction);
            }

            _lastAction = logEntry;
        }

        _logger?.LogDebug("Connexion {ConnectionId} - {Action}: {Description}", Id, action, description);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
    }

    #endregion

    #region IDisposable / IAsyncDisposable

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        Dispose(disposing: false);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        lock (_disposeLock)
        {
            if (_isDisposed)
            {
                return;
            }

            if (disposing)
            {
                _autoCloseTimer?.Dispose();

                if (_connection is not null)
                {
                    if (_connection.State != ConnectionState.Closed)
                    {
                        Close();
                    }

                    if (_options.IsPrimaryConnection || !_options.Pooling)
                    {
                        MySqlConnection.ClearPool(_connection);
                    }

                    _connection.StateChange -= OnConnectionStateChanged;
                    _connection.Dispose();
                    _connection = null;
                }

                LogAction("Dispose", "Ressources libérées");
            }

            _isDisposed = true;
        }
    }

    private async ValueTask DisposeAsyncCore()
    {
        if (_autoCloseTimer is not null)
        {
            await _autoCloseTimer.DisposeAsync().ConfigureAwait(false);
        }

        if (_transaction is not null)
        {
            await _transaction.DisposeAsync().ConfigureAwait(false);
        }

        if (_connection is not null)
        {
            if (_connection.State != ConnectionState.Closed)
            {
                await CloseAsync().ConfigureAwait(false);
            }

            if (_options.IsPrimaryConnection || !_options.Pooling)
            {
                await MySqlConnection.ClearPoolAsync(_connection).ConfigureAwait(false);
            }

            _connection.StateChange -= OnConnectionStateChanged;
            await _connection.DisposeAsync().ConfigureAwait(false);
            _connection = null;
        }

        LogAction("DisposeAsync", "Ressources libérées (async)");
    }

    #endregion
}
