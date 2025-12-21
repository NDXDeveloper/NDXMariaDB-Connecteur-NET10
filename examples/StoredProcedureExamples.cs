// ============================================================================
// NDXMariaDB - Exemples de Procédures Stockées
// ============================================================================
// Ce fichier contient des exemples d'utilisation de procédures stockées
// avec paramètres IN, OUT et INOUT.
//
// NOTE: Ces exemples sont fournis à titre de documentation.
//       Ils ne sont pas exécutés par les tests unitaires.
//
// Auteur: Nicolas DEOUX <NDXDev@gmail.com>
// ============================================================================

using System.Data;
using NDXMariaDB;

namespace NDXMariaDB.Examples;

/// <summary>
/// Exemples d'utilisation des procédures stockées MariaDB/MySQL.
/// </summary>
public static class StoredProcedureExamples
{
    // ========================================================================
    // Création de procédures stockées (SQL)
    // ========================================================================

    /// <summary>
    /// Scripts SQL pour créer les procédures stockées de démonstration.
    /// À exécuter une fois pour configurer la base de données.
    /// </summary>
    public static class SqlScripts
    {
        // Procédure simple sans paramètres
        public const string CreateGetAllClients = @"
            CREATE PROCEDURE sp_get_all_clients()
            BEGIN
                SELECT id, nom, email, date_inscription
                FROM clients
                WHERE actif = TRUE
                ORDER BY nom;
            END";

        // Procédure avec paramètre IN
        public const string CreateGetClientById = @"
            CREATE PROCEDURE sp_get_client_by_id(
                IN p_client_id INT
            )
            BEGIN
                SELECT id, nom, email, telephone, adresse, date_inscription
                FROM clients
                WHERE id = p_client_id;
            END";

        // Procédure avec paramètres IN multiples
        public const string CreateSearchClients = @"
            CREATE PROCEDURE sp_search_clients(
                IN p_nom VARCHAR(100),
                IN p_email VARCHAR(200),
                IN p_limit INT
            )
            BEGIN
                SELECT id, nom, email, date_inscription
                FROM clients
                WHERE
                    (p_nom IS NULL OR nom LIKE CONCAT('%', p_nom, '%'))
                    AND (p_email IS NULL OR email LIKE CONCAT('%', p_email, '%'))
                    AND actif = TRUE
                ORDER BY nom
                LIMIT p_limit;
            END";

        // Procédure avec paramètre OUT
        public const string CreateCountActiveClients = @"
            CREATE PROCEDURE sp_count_active_clients(
                OUT p_count INT
            )
            BEGIN
                SELECT COUNT(*) INTO p_count
                FROM clients
                WHERE actif = TRUE;
            END";

        // Procédure avec IN et OUT
        public const string CreateAddClient = @"
            CREATE PROCEDURE sp_add_client(
                IN p_nom VARCHAR(100),
                IN p_email VARCHAR(200),
                IN p_telephone VARCHAR(20),
                OUT p_new_id INT,
                OUT p_success BOOLEAN
            )
            BEGIN
                DECLARE EXIT HANDLER FOR SQLEXCEPTION
                BEGIN
                    SET p_success = FALSE;
                    SET p_new_id = 0;
                END;

                INSERT INTO clients (nom, email, telephone, date_inscription, actif)
                VALUES (p_nom, p_email, p_telephone, NOW(), TRUE);

                SET p_new_id = LAST_INSERT_ID();
                SET p_success = TRUE;
            END";

        // Procédure avec paramètre INOUT
        public const string CreateApplyDiscount = @"
            CREATE PROCEDURE sp_apply_discount(
                IN p_product_id INT,
                INOUT p_price DECIMAL(10,2)
            )
            BEGIN
                DECLARE v_discount_rate DECIMAL(5,2);

                -- Récupérer le taux de remise selon la catégorie du produit
                SELECT COALESCE(c.taux_remise, 0)
                INTO v_discount_rate
                FROM produits p
                LEFT JOIN categories c ON p.categorie_id = c.id
                WHERE p.id = p_product_id;

                -- Appliquer la remise au prix
                SET p_price = p_price * (1 - v_discount_rate / 100);
            END";

        // Procédure complexe avec IN, OUT, INOUT et résultat
        public const string CreateProcessOrder = @"
            CREATE PROCEDURE sp_process_order(
                IN p_client_id INT,
                IN p_product_ids VARCHAR(500),  -- Liste d'IDs séparés par des virgules
                INOUT p_total DECIMAL(10,2),
                OUT p_order_id INT,
                OUT p_status VARCHAR(50),
                OUT p_message VARCHAR(500)
            )
            BEGIN
                DECLARE v_product_count INT DEFAULT 0;
                DECLARE v_stock_ok BOOLEAN DEFAULT TRUE;

                DECLARE EXIT HANDLER FOR SQLEXCEPTION
                BEGIN
                    ROLLBACK;
                    SET p_status = 'ERROR';
                    SET p_message = 'Une erreur est survenue lors du traitement';
                    SET p_order_id = 0;
                END;

                START TRANSACTION;

                -- Vérifier le stock (simplifié)
                SELECT COUNT(*) INTO v_product_count
                FROM produits
                WHERE FIND_IN_SET(id, p_product_ids) > 0 AND stock > 0;

                IF v_product_count = 0 THEN
                    SET p_status = 'NO_STOCK';
                    SET p_message = 'Produits non disponibles en stock';
                    SET p_order_id = 0;
                    ROLLBACK;
                ELSE
                    -- Créer la commande
                    INSERT INTO commandes (client_id, montant_total, date_commande, statut)
                    VALUES (p_client_id, p_total, NOW(), 'PENDING');

                    SET p_order_id = LAST_INSERT_ID();

                    -- Appliquer les taxes (exemple: 20%)
                    SET p_total = p_total * 1.20;

                    -- Mettre à jour le montant avec taxes
                    UPDATE commandes SET montant_total = p_total WHERE id = p_order_id;

                    SET p_status = 'SUCCESS';
                    SET p_message = CONCAT('Commande #', p_order_id, ' créée avec succès');

                    COMMIT;
                END IF;

                -- Retourner les détails de la commande
                SELECT * FROM commandes WHERE id = p_order_id;
            END";

        // Procédure avec calculs statistiques et multiples OUT
        public const string CreateGetClientStats = @"
            CREATE PROCEDURE sp_get_client_stats(
                IN p_client_id INT,
                OUT p_total_orders INT,
                OUT p_total_spent DECIMAL(12,2),
                OUT p_average_order DECIMAL(10,2),
                OUT p_first_order DATE,
                OUT p_last_order DATE,
                OUT p_loyalty_level VARCHAR(20)
            )
            BEGIN
                -- Calculer les statistiques
                SELECT
                    COUNT(*),
                    COALESCE(SUM(montant_total), 0),
                    COALESCE(AVG(montant_total), 0),
                    MIN(date_commande),
                    MAX(date_commande)
                INTO
                    p_total_orders,
                    p_total_spent,
                    p_average_order,
                    p_first_order,
                    p_last_order
                FROM commandes
                WHERE client_id = p_client_id AND statut = 'COMPLETED';

                -- Déterminer le niveau de fidélité
                SET p_loyalty_level = CASE
                    WHEN p_total_spent >= 10000 THEN 'PLATINUM'
                    WHEN p_total_spent >= 5000 THEN 'GOLD'
                    WHEN p_total_spent >= 1000 THEN 'SILVER'
                    ELSE 'BRONZE'
                END;
            END";
    }

    // ========================================================================
    // Appels de procédures stockées depuis C#
    // ========================================================================

    /// <summary>
    /// Appel d'une procédure simple sans paramètres.
    /// </summary>
    public static async Task<DataTable> CallGetAllClientsAsync(IMariaDbConnection connection)
    {
        var result = await connection.ExecuteQueryAsync("CALL sp_get_all_clients()");
        Console.WriteLine($"Clients récupérés: {result.Rows.Count}");
        return result;
    }

    /// <summary>
    /// Appel d'une procédure avec paramètre IN.
    /// </summary>
    public static async Task<DataTable> CallGetClientByIdAsync(IMariaDbConnection connection, int clientId)
    {
        var result = await connection.ExecuteQueryAsync(
            "CALL sp_get_client_by_id(@clientId)",
            new { clientId });

        if (result.Rows.Count > 0)
        {
            Console.WriteLine($"Client trouvé: {result.Rows[0]["nom"]}");
        }

        return result;
    }

    /// <summary>
    /// Appel d'une procédure avec plusieurs paramètres IN.
    /// </summary>
    public static async Task<DataTable> CallSearchClientsAsync(
        IMariaDbConnection connection,
        string? nom = null,
        string? email = null,
        int limit = 50)
    {
        var result = await connection.ExecuteQueryAsync(
            "CALL sp_search_clients(@nom, @email, @limit)",
            new { nom, email, limit });

        Console.WriteLine($"Résultats de recherche: {result.Rows.Count} client(s)");
        return result;
    }

    /// <summary>
    /// Appel d'une procédure avec paramètre OUT.
    /// </summary>
    public static async Task<int> CallCountActiveClientsAsync(IMariaDbConnection connection)
    {
        // Ouvrir la connexion si nécessaire
        await connection.OpenAsync();

        // Appeler la procédure avec une variable utilisateur
        await connection.ExecuteNonQueryAsync("CALL sp_count_active_clients(@result)");

        // Récupérer la valeur du paramètre OUT
        var count = await connection.ExecuteScalarAsync<int>("SELECT @result");

        Console.WriteLine($"Nombre de clients actifs: {count}");
        return count;
    }

    /// <summary>
    /// Appel d'une procédure avec paramètres IN et OUT.
    /// </summary>
    public static async Task<(int NewId, bool Success)> CallAddClientAsync(
        IMariaDbConnection connection,
        string nom,
        string email,
        string telephone)
    {
        await connection.OpenAsync();

        // Appeler la procédure
        await connection.ExecuteNonQueryAsync(
            "CALL sp_add_client(@nom, @email, @telephone, @newId, @success)",
            new { nom, email, telephone });

        // Récupérer les paramètres OUT
        var newId = await connection.ExecuteScalarAsync<int>("SELECT @newId");
        var success = await connection.ExecuteScalarAsync<bool>("SELECT @success");

        if (success)
        {
            Console.WriteLine($"Client créé avec l'ID: {newId}");
        }
        else
        {
            Console.WriteLine("Échec de la création du client");
        }

        return (newId, success);
    }

    /// <summary>
    /// Appel d'une procédure avec paramètre INOUT.
    /// </summary>
    public static async Task<decimal> CallApplyDiscountAsync(
        IMariaDbConnection connection,
        int productId,
        decimal originalPrice)
    {
        await connection.OpenAsync();

        // Initialiser la variable INOUT avec le prix original
        await connection.ExecuteNonQueryAsync(
            "SET @price = @originalPrice",
            new { originalPrice });

        // Appeler la procédure
        await connection.ExecuteNonQueryAsync(
            "CALL sp_apply_discount(@productId, @price)",
            new { productId });

        // Récupérer le prix après remise
        var discountedPrice = await connection.ExecuteScalarAsync<decimal>("SELECT @price");

        Console.WriteLine($"Prix original: {originalPrice:C} -> Prix remisé: {discountedPrice:C}");
        return discountedPrice;
    }

    /// <summary>
    /// Appel d'une procédure complexe avec IN, OUT, INOUT et résultat.
    /// </summary>
    public static async Task<(int OrderId, string Status, string Message, decimal Total, DataTable? OrderDetails)>
        CallProcessOrderAsync(
            IMariaDbConnection connection,
            int clientId,
            int[] productIds,
            decimal initialTotal)
    {
        await connection.OpenAsync();

        // Convertir les IDs en chaîne séparée par des virgules
        var productIdsString = string.Join(",", productIds);

        // Initialiser la variable INOUT
        await connection.ExecuteNonQueryAsync("SET @total = @initialTotal", new { initialTotal });

        // Appeler la procédure
        var orderDetails = await connection.ExecuteQueryAsync(
            "CALL sp_process_order(@clientId, @productIds, @total, @orderId, @status, @message)",
            new { clientId, productIds = productIdsString });

        // Récupérer les paramètres OUT et INOUT
        var orderId = await connection.ExecuteScalarAsync<int>("SELECT @orderId");
        var status = await connection.ExecuteScalarAsync<string>("SELECT @status") ?? "UNKNOWN";
        var message = await connection.ExecuteScalarAsync<string>("SELECT @message") ?? "";
        var finalTotal = await connection.ExecuteScalarAsync<decimal>("SELECT @total");

        Console.WriteLine($"Traitement commande - Status: {status}");
        Console.WriteLine($"Message: {message}");
        Console.WriteLine($"Total (avec taxes): {finalTotal:C}");

        return (orderId, status, message, finalTotal, orderDetails.Rows.Count > 0 ? orderDetails : null);
    }

    /// <summary>
    /// Appel d'une procédure avec multiples paramètres OUT pour statistiques.
    /// </summary>
    public static async Task<ClientStats> CallGetClientStatsAsync(IMariaDbConnection connection, int clientId)
    {
        await connection.OpenAsync();

        // Appeler la procédure
        await connection.ExecuteNonQueryAsync(
            @"CALL sp_get_client_stats(
                @clientId,
                @totalOrders,
                @totalSpent,
                @averageOrder,
                @firstOrder,
                @lastOrder,
                @loyaltyLevel)",
            new { clientId });

        // Récupérer tous les paramètres OUT
        var stats = new ClientStats
        {
            TotalOrders = await connection.ExecuteScalarAsync<int>("SELECT @totalOrders"),
            TotalSpent = await connection.ExecuteScalarAsync<decimal>("SELECT @totalSpent"),
            AverageOrder = await connection.ExecuteScalarAsync<decimal>("SELECT @averageOrder"),
            FirstOrderDate = await connection.ExecuteScalarAsync<DateTime?>("SELECT @firstOrder"),
            LastOrderDate = await connection.ExecuteScalarAsync<DateTime?>("SELECT @lastOrder"),
            LoyaltyLevel = await connection.ExecuteScalarAsync<string>("SELECT @loyaltyLevel") ?? "BRONZE"
        };

        Console.WriteLine($"Statistiques client {clientId}:");
        Console.WriteLine($"  - Commandes: {stats.TotalOrders}");
        Console.WriteLine($"  - Total dépensé: {stats.TotalSpent:C}");
        Console.WriteLine($"  - Moyenne: {stats.AverageOrder:C}");
        Console.WriteLine($"  - Niveau fidélité: {stats.LoyaltyLevel}");

        return stats;
    }

    /// <summary>
    /// Classe pour stocker les statistiques client.
    /// </summary>
    public class ClientStats
    {
        public int TotalOrders { get; set; }
        public decimal TotalSpent { get; set; }
        public decimal AverageOrder { get; set; }
        public DateTime? FirstOrderDate { get; set; }
        public DateTime? LastOrderDate { get; set; }
        public string LoyaltyLevel { get; set; } = "BRONZE";
    }

    // ========================================================================
    // Création et suppression de procédures via code
    // ========================================================================

    /// <summary>
    /// Créer une procédure stockée dynamiquement.
    /// </summary>
    public static async Task CreateProcedureAsync(IMariaDbConnection connection)
    {
        // Supprimer si existe
        await connection.ExecuteNonQueryAsync("DROP PROCEDURE IF EXISTS sp_exemple_dynamique");

        // Créer la procédure
        var sql = @"
            CREATE PROCEDURE sp_exemple_dynamique(IN p_message VARCHAR(200))
            BEGIN
                SELECT CONCAT('Message reçu: ', p_message) AS result;
            END";

        await connection.ExecuteNonQueryAsync(sql);
        Console.WriteLine("Procédure sp_exemple_dynamique créée avec succès");
    }

    /// <summary>
    /// Supprimer une procédure stockée.
    /// </summary>
    public static async Task DropProcedureAsync(IMariaDbConnection connection, string procedureName)
    {
        await connection.ExecuteNonQueryAsync($"DROP PROCEDURE IF EXISTS {procedureName}");
        Console.WriteLine($"Procédure {procedureName} supprimée");
    }

    /// <summary>
    /// Lister toutes les procédures stockées de la base courante.
    /// </summary>
    public static async Task<DataTable> ListProceduresAsync(IMariaDbConnection connection)
    {
        var sql = @"
            SELECT
                ROUTINE_NAME AS name,
                ROUTINE_TYPE AS type,
                CREATED AS created,
                LAST_ALTERED AS last_modified,
                ROUTINE_COMMENT AS comment
            FROM information_schema.ROUTINES
            WHERE ROUTINE_SCHEMA = DATABASE()
            ORDER BY ROUTINE_NAME";

        var result = await connection.ExecuteQueryAsync(sql);

        Console.WriteLine($"Procédures stockées trouvées: {result.Rows.Count}");
        foreach (DataRow row in result.Rows)
        {
            Console.WriteLine($"  - {row["name"]} ({row["type"]})");
        }

        return result;
    }

    // ========================================================================
    // Exemple complet
    // ========================================================================

    /// <summary>
    /// Exemple complet d'utilisation des procédures stockées.
    /// </summary>
    public static async Task FullExampleAsync()
    {
        var options = new MariaDbConnectionOptions
        {
            Server = "localhost",
            Database = "ma_base",
            Username = "mon_user",
            Password = "mon_pass",
            AllowUserVariables = true  // IMPORTANT pour les paramètres OUT/INOUT
        };

        await using var connection = new MariaDbConnection(options);

        // 1. Créer les procédures (une seule fois)
        // await CreateAllProceduresAsync(connection);

        // 2. Utiliser les procédures
        var allClients = await CallGetAllClientsAsync(connection);
        var clientCount = await CallCountActiveClientsAsync(connection);

        var (newId, success) = await CallAddClientAsync(
            connection,
            "Nouveau Client",
            "nouveau@example.com",
            "+33123456789");

        if (success)
        {
            var stats = await CallGetClientStatsAsync(connection, newId);
        }
    }
}
