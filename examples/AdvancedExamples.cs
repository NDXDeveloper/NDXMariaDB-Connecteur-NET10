// ============================================================================
// NDXMariaDB - Exemples Avancés
// ============================================================================
// Ce fichier contient des exemples avancés: health checks, connexions multiples,
// injection de dépendances, logging et monitoring.
//
// NOTE: Ces exemples sont fournis à titre de documentation.
//       Ils ne sont pas exécutés par les tests unitaires.
//
// Auteur: Nicolas DEOUX <NDXDev@gmail.com>
// ============================================================================

using System.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NDXMariaDB;

namespace NDXMariaDB.Examples;

/// <summary>
/// Exemples avancés d'utilisation de NDXMariaDB.
/// </summary>
public static class AdvancedExamples
{
    // ========================================================================
    // Health Checks
    // ========================================================================

    /// <summary>
    /// Vérification basique de la santé de la connexion.
    /// </summary>
    public static async Task<bool> BasicHealthCheckAsync(IMariaDbConnection connection)
    {
        try
        {
            var result = await connection.ExecuteScalarAsync<int>("SELECT 1");
            return result == 1;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Health check complet avec informations détaillées.
    /// </summary>
    public static async Task<HealthCheckResult> DetailedHealthCheckAsync(IMariaDbConnection connection)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // Test de connexion basique
            await connection.ExecuteScalarAsync<int>("SELECT 1");

            // Récupérer les informations du serveur
            var version = await connection.ExecuteScalarAsync<string>("SELECT VERSION()");
            var uptime = await connection.ExecuteScalarAsync<long>("SELECT VARIABLE_VALUE FROM information_schema.GLOBAL_STATUS WHERE VARIABLE_NAME = 'Uptime'");
            var connections = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM information_schema.PROCESSLIST");

            var responseTime = DateTime.UtcNow - startTime;

            return new HealthCheckResult
            {
                IsHealthy = true,
                Message = "Connexion OK",
                ResponseTime = responseTime,
                ServerVersion = version ?? "Unknown",
                UptimeSeconds = uptime,
                ActiveConnections = connections
            };
        }
        catch (Exception ex)
        {
            return new HealthCheckResult
            {
                IsHealthy = false,
                Message = ex.Message,
                ResponseTime = DateTime.UtcNow - startTime,
                Exception = ex
            };
        }
    }

    /// <summary>
    /// Résultat d'un health check.
    /// </summary>
    public class HealthCheckResult
    {
        public bool IsHealthy { get; set; }
        public string Message { get; set; } = string.Empty;
        public TimeSpan ResponseTime { get; set; }
        public string? ServerVersion { get; set; }
        public long UptimeSeconds { get; set; }
        public int ActiveConnections { get; set; }
        public Exception? Exception { get; set; }

        public void Print()
        {
            Console.WriteLine($"=== Health Check ===");
            Console.WriteLine($"Status: {(IsHealthy ? "HEALTHY" : "UNHEALTHY")}");
            Console.WriteLine($"Message: {Message}");
            Console.WriteLine($"Response Time: {ResponseTime.TotalMilliseconds:F2}ms");

            if (IsHealthy)
            {
                Console.WriteLine($"Server Version: {ServerVersion}");
                Console.WriteLine($"Uptime: {TimeSpan.FromSeconds(UptimeSeconds)}");
                Console.WriteLine($"Active Connections: {ActiveConnections}");
            }
            else if (Exception != null)
            {
                Console.WriteLine($"Error: {Exception.GetType().Name}");
            }
        }
    }

    // ========================================================================
    // Injection de dépendances
    // ========================================================================

    /// <summary>
    /// Configuration de l'injection de dépendances avec IServiceCollection.
    /// </summary>
    public static IServiceCollection ConfigureDependencyInjection(
        IServiceCollection services,
        MariaDbConnectionOptions options)
    {
        // Enregistrer les options
        services.AddSingleton(options);

        // Enregistrer la factory
        services.AddSingleton<IMariaDbConnectionFactory, MariaDbConnectionFactory>();

        // Enregistrer un service qui utilise la connexion
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IOrderService, OrderService>();

        // Ajouter le logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });

        return services;
    }

    /// <summary>
    /// Exemple de repository utilisant l'injection de dépendances.
    /// </summary>
    public interface IClientRepository
    {
        Task<DataTable> GetAllAsync();
        Task<DataRow?> GetByIdAsync(int id);
        Task<int> CreateAsync(string nom, string email);
    }

    public class ClientRepository : IClientRepository
    {
        private readonly IMariaDbConnectionFactory _connectionFactory;
        private readonly ILogger<ClientRepository> _logger;

        public ClientRepository(
            IMariaDbConnectionFactory connectionFactory,
            ILogger<ClientRepository> logger)
        {
            _connectionFactory = connectionFactory;
            _logger = logger;
        }

        public async Task<DataTable> GetAllAsync()
        {
            await using var connection = _connectionFactory.CreateConnection();

            _logger.LogDebug("Récupération de tous les clients");
            return await connection.ExecuteQueryAsync("SELECT * FROM clients WHERE actif = TRUE");
        }

        public async Task<DataRow?> GetByIdAsync(int id)
        {
            await using var connection = _connectionFactory.CreateConnection();

            _logger.LogDebug("Récupération du client {ClientId}", id);
            var result = await connection.ExecuteQueryAsync(
                "SELECT * FROM clients WHERE id = @id",
                new { id });

            return result.Rows.Count > 0 ? result.Rows[0] : null;
        }

        public async Task<int> CreateAsync(string nom, string email)
        {
            await using var connection = _connectionFactory.CreateConnection();

            await connection.ExecuteNonQueryAsync(
                "INSERT INTO clients (nom, email, date_inscription) VALUES (@nom, @email, NOW())",
                new { nom, email });

            var newId = await connection.ExecuteScalarAsync<int>("SELECT LAST_INSERT_ID()");

            _logger.LogInformation("Client créé avec l'ID {ClientId}", newId);
            return newId;
        }
    }

    /// <summary>
    /// Exemple de service avec transactions.
    /// </summary>
    public interface IOrderService
    {
        Task<int> CreateOrderAsync(int clientId, List<OrderItem> items);
    }

    public class OrderItem
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

    public class OrderService : IOrderService
    {
        private readonly IMariaDbConnectionFactory _connectionFactory;
        private readonly ILogger<OrderService> _logger;

        public OrderService(
            IMariaDbConnectionFactory connectionFactory,
            ILogger<OrderService> logger)
        {
            _connectionFactory = connectionFactory;
            _logger = logger;
        }

        public async Task<int> CreateOrderAsync(int clientId, List<OrderItem> items)
        {
            await using var connection = _connectionFactory.CreateConnection();
            await connection.BeginTransactionAsync();

            try
            {
                _logger.LogInformation("Création de commande pour le client {ClientId}", clientId);

                // Créer la commande
                await connection.ExecuteNonQueryAsync(
                    "INSERT INTO commandes (client_id, date_commande, statut) VALUES (@clientId, NOW(), 'PENDING')",
                    new { clientId });

                var orderId = await connection.ExecuteScalarAsync<int>("SELECT LAST_INSERT_ID()");

                decimal total = 0;

                foreach (var item in items)
                {
                    var price = await connection.ExecuteScalarAsync<decimal>(
                        "SELECT prix FROM produits WHERE id = @id",
                        new { id = item.ProductId });

                    var lineTotal = price * item.Quantity;
                    total += lineTotal;

                    await connection.ExecuteNonQueryAsync(
                        @"INSERT INTO lignes_commande (commande_id, produit_id, quantite, prix_unitaire)
                          VALUES (@orderId, @productId, @quantity, @price)",
                        new { orderId, productId = item.ProductId, quantity = item.Quantity, price });
                }

                await connection.ExecuteNonQueryAsync(
                    "UPDATE commandes SET montant_total = @total WHERE id = @orderId",
                    new { total, orderId });

                await connection.CommitAsync();

                _logger.LogInformation("Commande {OrderId} créée - Total: {Total:C}", orderId, total);
                return orderId;
            }
            catch (Exception ex)
            {
                await connection.RollbackAsync();
                _logger.LogError(ex, "Erreur lors de la création de la commande");
                throw;
            }
        }
    }

    // ========================================================================
    // Connexions multiples et parallélisme
    // ========================================================================

    /// <summary>
    /// Exécution de requêtes en parallèle avec plusieurs connexions.
    /// </summary>
    public static async Task ParallelQueriesAsync(IMariaDbConnectionFactory factory)
    {
        var tasks = new List<Task<int>>();

        // Lancer 5 requêtes en parallèle
        for (int i = 0; i < 5; i++)
        {
            var taskId = i;
            tasks.Add(Task.Run(async () =>
            {
                await using var connection = factory.CreateConnection();

                Console.WriteLine($"Tâche {taskId}: Démarrage");

                var count = await connection.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM clients WHERE id > @min",
                    new { min = taskId * 100 });

                Console.WriteLine($"Tâche {taskId}: {count} résultats");
                return count;
            }));
        }

        var results = await Task.WhenAll(tasks);
        Console.WriteLine($"Total: {results.Sum()} enregistrements traités");
    }

    /// <summary>
    /// Pool de connexions personnalisé.
    /// </summary>
    public static async Task ConnectionPoolDemoAsync(IMariaDbConnectionFactory factory)
    {
        // Créer plusieurs connexions
        var connections = new List<IMariaDbConnection>();

        for (int i = 0; i < 10; i++)
        {
            var conn = factory.CreateConnection();
            await conn.OpenAsync();
            connections.Add(conn);
            Console.WriteLine($"Connexion {i + 1} créée (ID: {conn.Id})");
        }

        // Utiliser les connexions
        foreach (var conn in connections)
        {
            await conn.ExecuteScalarAsync<int>("SELECT 1");
        }

        // Fermer toutes les connexions
        foreach (var conn in connections)
        {
            await conn.CloseAsync();
            await conn.DisposeAsync();
        }

        Console.WriteLine("Toutes les connexions fermées");
    }

    // ========================================================================
    // Monitoring et métriques
    // ========================================================================

    /// <summary>
    /// Récupération des statistiques du serveur.
    /// </summary>
    public static async Task<ServerStats> GetServerStatsAsync(IMariaDbConnection connection)
    {
        var stats = new ServerStats();

        // Variables globales
        var variables = await connection.ExecuteQueryAsync(
            @"SELECT VARIABLE_NAME, VARIABLE_VALUE
              FROM information_schema.GLOBAL_STATUS
              WHERE VARIABLE_NAME IN (
                  'Connections',
                  'Threads_connected',
                  'Threads_running',
                  'Questions',
                  'Slow_queries',
                  'Uptime',
                  'Bytes_received',
                  'Bytes_sent'
              )");

        foreach (DataRow row in variables.Rows)
        {
            var name = row["VARIABLE_NAME"].ToString();
            var value = Convert.ToInt64(row["VARIABLE_VALUE"]);

            switch (name)
            {
                case "Connections": stats.TotalConnections = value; break;
                case "Threads_connected": stats.ActiveConnections = (int)value; break;
                case "Threads_running": stats.RunningThreads = (int)value; break;
                case "Questions": stats.TotalQueries = value; break;
                case "Slow_queries": stats.SlowQueries = value; break;
                case "Uptime": stats.UptimeSeconds = value; break;
                case "Bytes_received": stats.BytesReceived = value; break;
                case "Bytes_sent": stats.BytesSent = value; break;
            }
        }

        // Taille de la base de données
        var dbSize = await connection.ExecuteScalarAsync<decimal?>(
            @"SELECT SUM(data_length + index_length) / 1024 / 1024
              FROM information_schema.TABLES
              WHERE table_schema = DATABASE()");

        stats.DatabaseSizeMB = dbSize ?? 0;

        return stats;
    }

    /// <summary>
    /// Statistiques du serveur.
    /// </summary>
    public class ServerStats
    {
        public long TotalConnections { get; set; }
        public int ActiveConnections { get; set; }
        public int RunningThreads { get; set; }
        public long TotalQueries { get; set; }
        public long SlowQueries { get; set; }
        public long UptimeSeconds { get; set; }
        public long BytesReceived { get; set; }
        public long BytesSent { get; set; }
        public decimal DatabaseSizeMB { get; set; }

        public void Print()
        {
            Console.WriteLine("=== Statistiques Serveur ===");
            Console.WriteLine($"Uptime: {TimeSpan.FromSeconds(UptimeSeconds):g}");
            Console.WriteLine($"Connexions totales: {TotalConnections:N0}");
            Console.WriteLine($"Connexions actives: {ActiveConnections}");
            Console.WriteLine($"Threads en cours: {RunningThreads}");
            Console.WriteLine($"Total requêtes: {TotalQueries:N0}");
            Console.WriteLine($"Requêtes lentes: {SlowQueries:N0}");
            Console.WriteLine($"Données reçues: {BytesReceived / 1024.0 / 1024.0:F2} MB");
            Console.WriteLine($"Données envoyées: {BytesSent / 1024.0 / 1024.0:F2} MB");
            Console.WriteLine($"Taille base: {DatabaseSizeMB:F2} MB");
        }
    }

    /// <summary>
    /// Liste des processus actifs (SHOW PROCESSLIST).
    /// </summary>
    public static async Task<DataTable> GetProcessListAsync(IMariaDbConnection connection)
    {
        var result = await connection.ExecuteQueryAsync(
            @"SELECT
                ID as id,
                USER as utilisateur,
                HOST as hote,
                DB as base_donnees,
                COMMAND as commande,
                TIME as duree_secondes,
                STATE as etat,
                LEFT(INFO, 100) as requete
              FROM information_schema.PROCESSLIST
              ORDER BY TIME DESC");

        Console.WriteLine($"Processus actifs: {result.Rows.Count}");
        foreach (DataRow row in result.Rows)
        {
            Console.WriteLine($"  [{row["id"]}] {row["utilisateur"]}@{row["hote"]} - {row["commande"]} ({row["duree_secondes"]}s)");
        }

        return result;
    }

    // ========================================================================
    // Gestion des erreurs avancée
    // ========================================================================

    /// <summary>
    /// Wrapper pour gérer les erreurs de connexion.
    /// </summary>
    public static async Task<T> ExecuteWithErrorHandlingAsync<T>(
        IMariaDbConnection connection,
        Func<Task<T>> operation,
        T defaultValue = default!)
    {
        try
        {
            return await operation();
        }
        catch (MySqlConnector.MySqlException ex)
        {
            Console.WriteLine($"Erreur MySQL [{ex.Number}]: {ex.Message}");

            // Gérer différents codes d'erreur
            switch (ex.Number)
            {
                case 1045: // Access denied
                    Console.WriteLine("Vérifiez les credentials de connexion");
                    break;
                case 1049: // Unknown database
                    Console.WriteLine("Base de données introuvable");
                    break;
                case 1062: // Duplicate entry
                    Console.WriteLine("Violation de contrainte d'unicité");
                    break;
                case 1213: // Deadlock
                    Console.WriteLine("Deadlock détecté - réessayez l'opération");
                    break;
                case 1205: // Lock wait timeout
                    Console.WriteLine("Timeout de verrouillage - réessayez l'opération");
                    break;
                case 2002: // Connection refused
                case 2003: // Can't connect
                    Console.WriteLine("Serveur MariaDB inaccessible");
                    break;
            }

            return defaultValue;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur inattendue: {ex.Message}");
            return defaultValue;
        }
    }

    // ========================================================================
    // Exemple complet
    // ========================================================================

    /// <summary>
    /// Exemple complet d'utilisation avancée.
    /// </summary>
    public static async Task FullExampleAsync()
    {
        var options = new MariaDbConnectionOptions
        {
            Server = "localhost",
            Database = "ma_base",
            Username = "mon_user",
            Password = "mon_pass"
        };

        // Configuration DI
        var services = new ServiceCollection();
        ConfigureDependencyInjection(services, options);
        var serviceProvider = services.BuildServiceProvider();

        // Récupérer les services
        var clientRepo = serviceProvider.GetRequiredService<IClientRepository>();
        var orderService = serviceProvider.GetRequiredService<IOrderService>();
        var factory = serviceProvider.GetRequiredService<IMariaDbConnectionFactory>();

        // Health check
        await using (var connection = factory.CreateConnection())
        {
            var health = await DetailedHealthCheckAsync(connection);
            health.Print();

            if (!health.IsHealthy)
            {
                Console.WriteLine("Serveur non disponible, arrêt");
                return;
            }

            // Statistiques
            var stats = await GetServerStatsAsync(connection);
            stats.Print();
        }

        // Opérations métier
        var clients = await clientRepo.GetAllAsync();
        Console.WriteLine($"Clients trouvés: {clients.Rows.Count}");

        // Requêtes parallèles
        await ParallelQueriesAsync(factory);

        Console.WriteLine("Exemple avancé terminé!");
    }
}
