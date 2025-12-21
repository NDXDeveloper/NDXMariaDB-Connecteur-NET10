// ============================================================================
// NDXMariaDB - Exemples de Transactions
// ============================================================================
// Ce fichier contient des exemples d'utilisation des transactions
// avec différents niveaux d'isolation et cas d'usage.
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
/// Exemples d'utilisation des transactions.
/// </summary>
public static class TransactionExamples
{
    // ========================================================================
    // Transactions de base
    // ========================================================================

    /// <summary>
    /// Transaction simple avec commit.
    /// </summary>
    public static async Task SimpleTransactionAsync(IMariaDbConnection connection)
    {
        // Démarrer la transaction
        await connection.BeginTransactionAsync();

        try
        {
            // Effectuer les opérations
            await connection.ExecuteNonQueryAsync(
                "INSERT INTO clients (nom, email) VALUES (@nom, @email)",
                new { nom = "Client Test", email = "test@example.com" });

            await connection.ExecuteNonQueryAsync(
                "INSERT INTO logs (action, message) VALUES (@action, @message)",
                new { action = "CREATE_CLIENT", message = "Nouveau client créé" });

            // Valider la transaction
            await connection.CommitAsync();
            Console.WriteLine("Transaction validée avec succès");
        }
        catch (Exception ex)
        {
            // Annuler en cas d'erreur
            await connection.RollbackAsync();
            Console.WriteLine($"Transaction annulée: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Transaction avec rollback explicite.
    /// </summary>
    public static async Task TransactionWithRollbackAsync(IMariaDbConnection connection)
    {
        await connection.BeginTransactionAsync();

        try
        {
            // Première opération
            await connection.ExecuteNonQueryAsync(
                "UPDATE comptes SET solde = solde - @montant WHERE id = @id",
                new { montant = 100.00m, id = 1 });

            // Vérifier une condition
            var solde = await connection.ExecuteScalarAsync<decimal>(
                "SELECT solde FROM comptes WHERE id = @id",
                new { id = 1 });

            if (solde < 0)
            {
                // Solde insuffisant, annuler
                await connection.RollbackAsync();
                Console.WriteLine("Transaction annulée: solde insuffisant");
                return;
            }

            // Deuxième opération
            await connection.ExecuteNonQueryAsync(
                "UPDATE comptes SET solde = solde + @montant WHERE id = @id",
                new { montant = 100.00m, id = 2 });

            await connection.CommitAsync();
            Console.WriteLine("Transfert effectué avec succès");
        }
        catch
        {
            await connection.RollbackAsync();
            throw;
        }
    }

    // ========================================================================
    // Niveaux d'isolation
    // ========================================================================

    /// <summary>
    /// Transaction avec niveau d'isolation READ UNCOMMITTED.
    /// Permet les lectures sales (dirty reads).
    /// </summary>
    public static async Task ReadUncommittedTransactionAsync(IMariaDbConnection connection)
    {
        await connection.BeginTransactionAsync(IsolationLevel.ReadUncommitted);

        try
        {
            // Cette lecture peut voir des données non committées d'autres transactions
            var data = await connection.ExecuteQueryAsync("SELECT * FROM produits WHERE stock > 0");
            Console.WriteLine($"Produits en stock (dirty read possible): {data.Rows.Count}");

            await connection.CommitAsync();
        }
        catch
        {
            await connection.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Transaction avec niveau d'isolation READ COMMITTED.
    /// Évite les lectures sales, mais pas les lectures non-répétables.
    /// </summary>
    public static async Task ReadCommittedTransactionAsync(IMariaDbConnection connection)
    {
        await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);

        try
        {
            // Lecture 1
            var stock1 = await connection.ExecuteScalarAsync<int>(
                "SELECT stock FROM produits WHERE id = @id",
                new { id = 1 });

            // ... traitement ...

            // Lecture 2 - peut retourner une valeur différente si une autre
            // transaction a modifié et committé entre-temps
            var stock2 = await connection.ExecuteScalarAsync<int>(
                "SELECT stock FROM produits WHERE id = @id",
                new { id = 1 });

            Console.WriteLine($"Stock initial: {stock1}, Stock actuel: {stock2}");

            await connection.CommitAsync();
        }
        catch
        {
            await connection.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Transaction avec niveau d'isolation REPEATABLE READ (par défaut InnoDB).
    /// Garantit des lectures répétables.
    /// </summary>
    public static async Task RepeatableReadTransactionAsync(IMariaDbConnection connection)
    {
        await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead);

        try
        {
            // Les lectures suivantes retourneront toujours les mêmes valeurs
            // même si d'autres transactions modifient les données

            var stock1 = await connection.ExecuteScalarAsync<int>(
                "SELECT stock FROM produits WHERE id = @id",
                new { id = 1 });

            // Simulation d'un traitement long
            await Task.Delay(1000);

            var stock2 = await connection.ExecuteScalarAsync<int>(
                "SELECT stock FROM produits WHERE id = @id",
                new { id = 1 });

            // stock1 == stock2 est garanti
            Console.WriteLine($"Stock (lecture répétable garantie): {stock1} == {stock2}");

            await connection.CommitAsync();
        }
        catch
        {
            await connection.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Transaction avec niveau d'isolation SERIALIZABLE.
    /// Plus haut niveau d'isolation, évite les lectures fantômes.
    /// </summary>
    public static async Task SerializableTransactionAsync(IMariaDbConnection connection)
    {
        await connection.BeginTransactionAsync(IsolationLevel.Serializable);

        try
        {
            // Les requêtes sont exécutées de manière sérialisée
            // Aucune autre transaction ne peut modifier les données lues

            var commandes = await connection.ExecuteQueryAsync(
                "SELECT * FROM commandes WHERE statut = 'PENDING'");

            foreach (DataRow row in commandes.Rows)
            {
                await connection.ExecuteNonQueryAsync(
                    "UPDATE commandes SET statut = 'PROCESSING' WHERE id = @id",
                    new { id = row["id"] });
            }

            await connection.CommitAsync();
            Console.WriteLine($"Traité {commandes.Rows.Count} commandes en mode sérialisé");
        }
        catch
        {
            await connection.RollbackAsync();
            throw;
        }
    }

    // ========================================================================
    // Cas d'usage courants
    // ========================================================================

    /// <summary>
    /// Transfert d'argent entre deux comptes (exemple classique).
    /// </summary>
    public static async Task<bool> TransferMoneyAsync(
        IMariaDbConnection connection,
        int fromAccountId,
        int toAccountId,
        decimal amount)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Le montant doit être positif", nameof(amount));
        }

        await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead);

        try
        {
            // Vérifier le solde du compte source
            var sourceBalance = await connection.ExecuteScalarAsync<decimal>(
                "SELECT solde FROM comptes WHERE id = @id FOR UPDATE",
                new { id = fromAccountId });

            if (sourceBalance < amount)
            {
                await connection.RollbackAsync();
                Console.WriteLine("Transfert refusé: solde insuffisant");
                return false;
            }

            // Débiter le compte source
            await connection.ExecuteNonQueryAsync(
                "UPDATE comptes SET solde = solde - @amount, date_modification = NOW() WHERE id = @id",
                new { amount, id = fromAccountId });

            // Créditer le compte destination
            await connection.ExecuteNonQueryAsync(
                "UPDATE comptes SET solde = solde + @amount, date_modification = NOW() WHERE id = @id",
                new { amount, id = toAccountId });

            // Enregistrer la transaction financière
            await connection.ExecuteNonQueryAsync(
                @"INSERT INTO transactions_financieres
                  (compte_source, compte_destination, montant, date_transaction, type)
                  VALUES (@from, @to, @amount, NOW(), 'TRANSFER')",
                new { from = fromAccountId, to = toAccountId, amount });

            await connection.CommitAsync();
            Console.WriteLine($"Transfert de {amount:C} effectué avec succès");
            return true;
        }
        catch (Exception ex)
        {
            await connection.RollbackAsync();
            Console.WriteLine($"Erreur lors du transfert: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Création d'une commande avec mise à jour du stock (transaction complète).
    /// </summary>
    public static async Task<int> CreateOrderWithStockUpdateAsync(
        IMariaDbConnection connection,
        int clientId,
        List<(int ProductId, int Quantity)> items)
    {
        await connection.BeginTransactionAsync();

        try
        {
            // 1. Vérifier la disponibilité du stock
            foreach (var (productId, quantity) in items)
            {
                var stock = await connection.ExecuteScalarAsync<int>(
                    "SELECT stock FROM produits WHERE id = @id FOR UPDATE",
                    new { id = productId });

                if (stock < quantity)
                {
                    await connection.RollbackAsync();
                    throw new InvalidOperationException(
                        $"Stock insuffisant pour le produit {productId}");
                }
            }

            // 2. Créer la commande
            await connection.ExecuteNonQueryAsync(
                @"INSERT INTO commandes (client_id, date_commande, statut, montant_total)
                  VALUES (@clientId, NOW(), 'PENDING', 0)",
                new { clientId });

            var orderId = await connection.ExecuteScalarAsync<int>("SELECT LAST_INSERT_ID()");

            // 3. Ajouter les lignes de commande et calculer le total
            decimal total = 0;

            foreach (var (productId, quantity) in items)
            {
                // Récupérer le prix du produit
                var price = await connection.ExecuteScalarAsync<decimal>(
                    "SELECT prix FROM produits WHERE id = @id",
                    new { id = productId });

                var lineTotal = price * quantity;
                total += lineTotal;

                // Ajouter la ligne de commande
                await connection.ExecuteNonQueryAsync(
                    @"INSERT INTO lignes_commande
                      (commande_id, produit_id, quantite, prix_unitaire, total_ligne)
                      VALUES (@orderId, @productId, @quantity, @price, @lineTotal)",
                    new { orderId, productId, quantity, price, lineTotal });

                // Décrémenter le stock
                await connection.ExecuteNonQueryAsync(
                    "UPDATE produits SET stock = stock - @quantity WHERE id = @id",
                    new { quantity, id = productId });
            }

            // 4. Mettre à jour le total de la commande
            await connection.ExecuteNonQueryAsync(
                "UPDATE commandes SET montant_total = @total WHERE id = @orderId",
                new { total, orderId });

            // 5. Valider la transaction
            await connection.CommitAsync();

            Console.WriteLine($"Commande #{orderId} créée - Total: {total:C}");
            return orderId;
        }
        catch
        {
            await connection.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Annulation d'une commande avec restauration du stock.
    /// </summary>
    public static async Task<bool> CancelOrderAsync(IMariaDbConnection connection, int orderId)
    {
        await connection.BeginTransactionAsync();

        try
        {
            // Vérifier que la commande existe et n'est pas déjà annulée
            var status = await connection.ExecuteScalarAsync<string>(
                "SELECT statut FROM commandes WHERE id = @id FOR UPDATE",
                new { id = orderId });

            if (status == null)
            {
                await connection.RollbackAsync();
                Console.WriteLine("Commande non trouvée");
                return false;
            }

            if (status == "CANCELLED")
            {
                await connection.RollbackAsync();
                Console.WriteLine("Commande déjà annulée");
                return false;
            }

            if (status == "SHIPPED" || status == "DELIVERED")
            {
                await connection.RollbackAsync();
                Console.WriteLine("Impossible d'annuler une commande expédiée");
                return false;
            }

            // Récupérer les lignes de commande
            var lines = await connection.ExecuteQueryAsync(
                "SELECT produit_id, quantite FROM lignes_commande WHERE commande_id = @id",
                new { id = orderId });

            // Restaurer le stock pour chaque produit
            foreach (DataRow line in lines.Rows)
            {
                await connection.ExecuteNonQueryAsync(
                    "UPDATE produits SET stock = stock + @quantity WHERE id = @productId",
                    new
                    {
                        quantity = Convert.ToInt32(line["quantite"]),
                        productId = Convert.ToInt32(line["produit_id"])
                    });
            }

            // Marquer la commande comme annulée
            await connection.ExecuteNonQueryAsync(
                @"UPDATE commandes
                  SET statut = 'CANCELLED', date_annulation = NOW()
                  WHERE id = @id",
                new { id = orderId });

            await connection.CommitAsync();
            Console.WriteLine($"Commande #{orderId} annulée, stock restauré");
            return true;
        }
        catch
        {
            await connection.RollbackAsync();
            throw;
        }
    }

    // ========================================================================
    // Opérations en masse (Bulk Operations)
    // ========================================================================

    /// <summary>
    /// Insertion en masse avec transaction.
    /// </summary>
    public static async Task<int> BulkInsertAsync(
        IMariaDbConnection connection,
        List<(string Nom, string Email)> clients)
    {
        await connection.BeginTransactionAsync();

        try
        {
            var inserted = 0;

            foreach (var (nom, email) in clients)
            {
                var result = await connection.ExecuteNonQueryAsync(
                    "INSERT INTO clients (nom, email, date_inscription) VALUES (@nom, @email, NOW())",
                    new { nom, email });

                inserted += result;
            }

            await connection.CommitAsync();
            Console.WriteLine($"{inserted} clients insérés");
            return inserted;
        }
        catch
        {
            await connection.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Insertion en masse optimisée avec VALUES multiples.
    /// </summary>
    public static async Task<int> BulkInsertOptimizedAsync(
        IMariaDbConnection connection,
        List<(string Nom, string Email)> clients,
        int batchSize = 100)
    {
        await connection.BeginTransactionAsync();

        try
        {
            var totalInserted = 0;

            for (int i = 0; i < clients.Count; i += batchSize)
            {
                var batch = clients.Skip(i).Take(batchSize).ToList();

                // Construire la requête INSERT avec valeurs multiples
                var values = string.Join(", ",
                    batch.Select((c, idx) => $"(@nom{idx}, @email{idx}, NOW())"));

                var sql = $"INSERT INTO clients (nom, email, date_inscription) VALUES {values}";

                // Construire les paramètres
                var parameters = new Dictionary<string, object>();
                for (int j = 0; j < batch.Count; j++)
                {
                    parameters[$"nom{j}"] = batch[j].Nom;
                    parameters[$"email{j}"] = batch[j].Email;
                }

                // Note: Cette approche nécessite une adaptation de la méthode
                // ExecuteNonQueryAsync pour accepter un dictionnaire de paramètres
                // Pour l'instant, on utilise l'approche standard

                foreach (var (nom, email) in batch)
                {
                    await connection.ExecuteNonQueryAsync(
                        "INSERT INTO clients (nom, email, date_inscription) VALUES (@nom, @email, NOW())",
                        new { nom, email });
                    totalInserted++;
                }

                Console.WriteLine($"Batch {i / batchSize + 1}: {batch.Count} enregistrements");
            }

            await connection.CommitAsync();
            Console.WriteLine($"Total inséré: {totalInserted}");
            return totalInserted;
        }
        catch
        {
            await connection.RollbackAsync();
            throw;
        }
    }

    /// <summary>
    /// Mise à jour en masse avec transaction.
    /// </summary>
    public static async Task<int> BulkUpdateAsync(
        IMariaDbConnection connection,
        decimal percentageIncrease,
        string category)
    {
        await connection.BeginTransactionAsync();

        try
        {
            // Sauvegarder les anciens prix pour l'historique
            await connection.ExecuteNonQueryAsync(
                @"INSERT INTO historique_prix (produit_id, ancien_prix, nouveau_prix, date_modification)
                  SELECT id, prix, prix * @factor, NOW()
                  FROM produits
                  WHERE categorie = @category",
                new { factor = 1 + percentageIncrease / 100, category });

            // Appliquer l'augmentation
            var result = await connection.ExecuteNonQueryAsync(
                "UPDATE produits SET prix = prix * @factor WHERE categorie = @category",
                new { factor = 1 + percentageIncrease / 100, category });

            await connection.CommitAsync();
            Console.WriteLine($"{result} produits mis à jour (+{percentageIncrease}%)");
            return result;
        }
        catch
        {
            await connection.RollbackAsync();
            throw;
        }
    }

    // ========================================================================
    // Gestion des erreurs et retry
    // ========================================================================

    /// <summary>
    /// Transaction avec retry en cas de deadlock.
    /// </summary>
    public static async Task<T> ExecuteWithRetryAsync<T>(
        IMariaDbConnection connection,
        Func<Task<T>> operation,
        int maxRetries = 3)
    {
        var retryCount = 0;
        var delay = TimeSpan.FromMilliseconds(100);

        while (true)
        {
            try
            {
                return await operation();
            }
            catch (MySqlConnector.MySqlException ex) when (ex.Number == 1213) // Deadlock
            {
                retryCount++;
                if (retryCount >= maxRetries)
                {
                    Console.WriteLine($"Deadlock - abandon après {maxRetries} tentatives");
                    throw;
                }

                Console.WriteLine($"Deadlock détecté - tentative {retryCount}/{maxRetries}");
                await Task.Delay(delay);
                delay *= 2; // Backoff exponentiel
            }
        }
    }

    /// <summary>
    /// Exemple d'utilisation du retry avec deadlock.
    /// </summary>
    public static async Task TransferWithRetryAsync(IMariaDbConnection connection)
    {
        await ExecuteWithRetryAsync(connection, async () =>
        {
            return await TransferMoneyAsync(connection, 1, 2, 100.00m);
        });
    }

    // ========================================================================
    // Exemple complet
    // ========================================================================

    /// <summary>
    /// Exemple complet d'utilisation des transactions.
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

        await using var connection = new MariaDbConnection(options);

        // Transaction simple
        await SimpleTransactionAsync(connection);

        // Transfert d'argent
        await TransferMoneyAsync(connection, 1, 2, 500.00m);

        // Création de commande
        var items = new List<(int, int)>
        {
            (1, 2),  // Produit 1, quantité 2
            (3, 1),  // Produit 3, quantité 1
            (5, 3)   // Produit 5, quantité 3
        };
        var orderId = await CreateOrderWithStockUpdateAsync(connection, 1, items);

        // Insertion en masse
        var newClients = new List<(string, string)>
        {
            ("Client A", "a@example.com"),
            ("Client B", "b@example.com"),
            ("Client C", "c@example.com")
        };
        await BulkInsertAsync(connection, newClients);

        Console.WriteLine("Toutes les opérations terminées avec succès!");
    }
}
