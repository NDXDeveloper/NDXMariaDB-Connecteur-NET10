// ============================================================================
// NDXMariaDB - Exemples CRUD de base
// ============================================================================
// Ce fichier contient des exemples d'opérations CRUD (Create, Read, Update, Delete)
// avec la bibliothèque NDXMariaDB.
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
/// Exemples d'opérations CRUD de base.
/// </summary>
public static class BasicCrudExamples
{
    // ========================================================================
    // Configuration de la connexion
    // ========================================================================

    /// <summary>
    /// Exemple de configuration avec propriétés individuelles.
    /// </summary>
    public static MariaDbConnectionOptions GetOptionsWithProperties()
    {
        return new MariaDbConnectionOptions
        {
            Server = "localhost",
            Port = 3306,
            Database = "ma_base",
            Username = "mon_utilisateur",
            Password = "mon_mot_de_passe",
            Pooling = true,
            MinPoolSize = 5,
            MaxPoolSize = 100,
            ConnectionTimeoutSeconds = 30,
            CommandTimeoutSeconds = 60,
            InnoDbLockWaitTimeout = 120
        };
    }

    /// <summary>
    /// Exemple de configuration avec chaîne de connexion.
    /// </summary>
    public static MariaDbConnectionOptions GetOptionsWithConnectionString()
    {
        return new MariaDbConnectionOptions
        {
            ConnectionString = "Server=localhost;Port=3306;Database=ma_base;User ID=mon_utilisateur;Password=mon_mot_de_passe;Pooling=true"
        };
    }

    // ========================================================================
    // CREATE - Insertions
    // ========================================================================

    /// <summary>
    /// Insertion simple d'un enregistrement.
    /// </summary>
    public static async Task InsertSimpleAsync(IMariaDbConnection connection)
    {
        var sql = "INSERT INTO clients (nom, email) VALUES (@nom, @email)";

        var rowsAffected = await connection.ExecuteNonQueryAsync(sql, new
        {
            nom = "Jean Dupont",
            email = "jean.dupont@example.com"
        });

        Console.WriteLine($"Lignes insérées: {rowsAffected}");
    }

    /// <summary>
    /// Insertion avec récupération de l'ID auto-incrémenté.
    /// </summary>
    public static async Task<long> InsertAndGetIdAsync(IMariaDbConnection connection)
    {
        var sql = "INSERT INTO clients (nom, email) VALUES (@nom, @email)";

        await connection.ExecuteNonQueryAsync(sql, new
        {
            nom = "Marie Martin",
            email = "marie.martin@example.com"
        });

        // Récupérer le dernier ID inséré
        var newId = await connection.ExecuteScalarAsync<long>("SELECT LAST_INSERT_ID()");

        Console.WriteLine($"Nouveau client créé avec l'ID: {newId}");
        return newId;
    }

    /// <summary>
    /// Insertion avec plusieurs champs de types différents.
    /// </summary>
    public static async Task InsertWithMultipleTypesAsync(IMariaDbConnection connection)
    {
        var sql = @"
            INSERT INTO produits (nom, description, prix, quantite, actif, date_creation)
            VALUES (@nom, @description, @prix, @quantite, @actif, @dateCreation)";

        await connection.ExecuteNonQueryAsync(sql, new
        {
            nom = "Laptop Pro",
            description = "Ordinateur portable haute performance",
            prix = 1299.99m,
            quantite = 50,
            actif = true,
            dateCreation = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Insertion multiple en une seule requête.
    /// </summary>
    public static async Task InsertMultipleRowsAsync(IMariaDbConnection connection)
    {
        var sql = @"
            INSERT INTO tags (nom) VALUES
            ('Électronique'),
            ('Informatique'),
            ('Bureau'),
            ('Accessoires')";

        var rowsAffected = await connection.ExecuteNonQueryAsync(sql);
        Console.WriteLine($"Tags créés: {rowsAffected}");
    }

    // ========================================================================
    // READ - Lectures
    // ========================================================================

    /// <summary>
    /// Lecture d'une valeur scalaire.
    /// </summary>
    public static async Task<int> GetCountAsync(IMariaDbConnection connection)
    {
        var count = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM clients");
        Console.WriteLine($"Nombre de clients: {count}");
        return count;
    }

    /// <summary>
    /// Lecture d'un enregistrement unique.
    /// </summary>
    public static async Task<DataTable> GetClientByIdAsync(IMariaDbConnection connection, int clientId)
    {
        var sql = "SELECT * FROM clients WHERE id = @id";
        var result = await connection.ExecuteQueryAsync(sql, new { id = clientId });

        if (result.Rows.Count > 0)
        {
            var row = result.Rows[0];
            Console.WriteLine($"Client trouvé: {row["nom"]} ({row["email"]})");
        }

        return result;
    }

    /// <summary>
    /// Lecture de plusieurs enregistrements avec filtres.
    /// </summary>
    public static async Task<DataTable> GetActiveClientsAsync(IMariaDbConnection connection)
    {
        var sql = @"
            SELECT id, nom, email, date_inscription
            FROM clients
            WHERE actif = @actif
            ORDER BY nom ASC
            LIMIT @limit";

        var result = await connection.ExecuteQueryAsync(sql, new
        {
            actif = true,
            limit = 100
        });

        Console.WriteLine($"Clients actifs trouvés: {result.Rows.Count}");
        return result;
    }

    /// <summary>
    /// Lecture avec jointures.
    /// </summary>
    public static async Task<DataTable> GetOrdersWithClientInfoAsync(IMariaDbConnection connection)
    {
        var sql = @"
            SELECT
                c.id AS commande_id,
                c.date_commande,
                c.montant_total,
                cl.nom AS client_nom,
                cl.email AS client_email
            FROM commandes c
            INNER JOIN clients cl ON c.client_id = cl.id
            WHERE c.date_commande >= @dateDebut
            ORDER BY c.date_commande DESC";

        var result = await connection.ExecuteQueryAsync(sql, new
        {
            dateDebut = DateTime.UtcNow.AddMonths(-1)
        });

        return result;
    }

    /// <summary>
    /// Lecture avec agrégations.
    /// </summary>
    public static async Task GetSalesStatisticsAsync(IMariaDbConnection connection)
    {
        var sql = @"
            SELECT
                COUNT(*) AS nombre_commandes,
                SUM(montant_total) AS total_ventes,
                AVG(montant_total) AS moyenne_commande,
                MIN(montant_total) AS plus_petite_commande,
                MAX(montant_total) AS plus_grande_commande
            FROM commandes
            WHERE date_commande >= @dateDebut";

        var result = await connection.ExecuteQueryAsync(sql, new
        {
            dateDebut = DateTime.UtcNow.AddMonths(-1)
        });

        if (result.Rows.Count > 0)
        {
            var row = result.Rows[0];
            Console.WriteLine($"Statistiques du mois:");
            Console.WriteLine($"  - Nombre de commandes: {row["nombre_commandes"]}");
            Console.WriteLine($"  - Total des ventes: {row["total_ventes"]:C}");
            Console.WriteLine($"  - Moyenne par commande: {row["moyenne_commande"]:C}");
        }
    }

    /// <summary>
    /// Utilisation du DataReader pour un traitement ligne par ligne.
    /// </summary>
    public static async Task ProcessLargeDatasetAsync(IMariaDbConnection connection)
    {
        var sql = "SELECT id, nom, email FROM clients WHERE actif = TRUE";

        await using var reader = await connection.ExecuteReaderAsync(sql);

        while (await reader.ReadAsync())
        {
            var id = reader.GetInt32("id");
            var nom = reader.GetString("nom");
            var email = reader.GetString("email");

            // Traitement de chaque ligne...
            Console.WriteLine($"Traitement du client {id}: {nom}");
        }
    }

    // ========================================================================
    // UPDATE - Mises à jour
    // ========================================================================

    /// <summary>
    /// Mise à jour simple d'un enregistrement.
    /// </summary>
    public static async Task<int> UpdateClientEmailAsync(IMariaDbConnection connection, int clientId, string newEmail)
    {
        var sql = "UPDATE clients SET email = @email, date_modification = @dateMod WHERE id = @id";

        var rowsAffected = await connection.ExecuteNonQueryAsync(sql, new
        {
            id = clientId,
            email = newEmail,
            dateMod = DateTime.UtcNow
        });

        Console.WriteLine($"Client {clientId} mis à jour: {rowsAffected} ligne(s) affectée(s)");
        return rowsAffected;
    }

    /// <summary>
    /// Mise à jour de plusieurs enregistrements.
    /// </summary>
    public static async Task<int> DeactivateInactiveClientsAsync(IMariaDbConnection connection)
    {
        var sql = @"
            UPDATE clients
            SET actif = FALSE, date_desactivation = @dateDesact
            WHERE derniere_connexion < @dateLimite AND actif = TRUE";

        var rowsAffected = await connection.ExecuteNonQueryAsync(sql, new
        {
            dateDesact = DateTime.UtcNow,
            dateLimite = DateTime.UtcNow.AddYears(-1)
        });

        Console.WriteLine($"Clients désactivés: {rowsAffected}");
        return rowsAffected;
    }

    /// <summary>
    /// Mise à jour conditionnelle avec CASE.
    /// </summary>
    public static async Task UpdateProductPricesAsync(IMariaDbConnection connection)
    {
        var sql = @"
            UPDATE produits
            SET prix = CASE
                WHEN categorie = 'Électronique' THEN prix * 1.05
                WHEN categorie = 'Accessoires' THEN prix * 1.02
                ELSE prix
            END
            WHERE actif = TRUE";

        var rowsAffected = await connection.ExecuteNonQueryAsync(sql);
        Console.WriteLine($"Prix mis à jour pour {rowsAffected} produits");
    }

    // ========================================================================
    // DELETE - Suppressions
    // ========================================================================

    /// <summary>
    /// Suppression d'un enregistrement par ID.
    /// </summary>
    public static async Task<int> DeleteClientAsync(IMariaDbConnection connection, int clientId)
    {
        var sql = "DELETE FROM clients WHERE id = @id";

        var rowsAffected = await connection.ExecuteNonQueryAsync(sql, new { id = clientId });
        Console.WriteLine($"Client {clientId} supprimé: {rowsAffected} ligne(s)");
        return rowsAffected;
    }

    /// <summary>
    /// Suppression conditionnelle de plusieurs enregistrements.
    /// </summary>
    public static async Task<int> DeleteOldLogsAsync(IMariaDbConnection connection)
    {
        var sql = "DELETE FROM logs WHERE date_creation < @dateLimite";

        var rowsAffected = await connection.ExecuteNonQueryAsync(sql, new
        {
            dateLimite = DateTime.UtcNow.AddMonths(-6)
        });

        Console.WriteLine($"Anciens logs supprimés: {rowsAffected}");
        return rowsAffected;
    }

    /// <summary>
    /// Soft delete (suppression logique).
    /// </summary>
    public static async Task<int> SoftDeleteClientAsync(IMariaDbConnection connection, int clientId)
    {
        var sql = @"
            UPDATE clients
            SET
                supprime = TRUE,
                date_suppression = @dateSup,
                supprime_par = @userId
            WHERE id = @id AND supprime = FALSE";

        var rowsAffected = await connection.ExecuteNonQueryAsync(sql, new
        {
            id = clientId,
            dateSup = DateTime.UtcNow,
            userId = 1 // ID de l'utilisateur courant
        });

        return rowsAffected;
    }

    // ========================================================================
    // Exemple d'utilisation complète
    // ========================================================================

    /// <summary>
    /// Exemple complet d'utilisation avec cycle de vie de la connexion.
    /// </summary>
    public static async Task FullExampleAsync()
    {
        var options = GetOptionsWithProperties();

        // Utilisation avec using pour garantir la fermeture
        await using var connection = new MariaDbConnection(options);

        // La connexion s'ouvre automatiquement à la première requête
        // Mais on peut l'ouvrir explicitement si nécessaire
        await connection.OpenAsync();

        try
        {
            // CREATE
            var newId = await InsertAndGetIdAsync(connection);

            // READ
            var client = await GetClientByIdAsync(connection, (int)newId);

            // UPDATE
            await UpdateClientEmailAsync(connection, (int)newId, "nouveau.email@example.com");

            // DELETE (soft delete)
            await SoftDeleteClientAsync(connection, (int)newId);
        }
        finally
        {
            // La connexion sera fermée automatiquement par le using
            // Mais on peut la fermer explicitement
            await connection.CloseAsync();
        }
    }
}
