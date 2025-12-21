# Exemples NDXMariaDB

Ce dossier contient des exemples complets d'utilisation de la bibliothèque NDXMariaDB.

> **Note**: Ces exemples sont fournis à titre de documentation. Ils ne sont pas exécutés par les tests unitaires.

## Structure des fichiers

| Fichier | Description |
|---------|-------------|
| `BasicCrudExamples.cs` | Opérations CRUD de base (Create, Read, Update, Delete) |
| `StoredProcedureExamples.cs` | Procédures stockées avec paramètres IN, OUT et INOUT |
| `EventSchedulerExamples.cs` | Event Scheduler pour les tâches planifiées |
| `TransactionExamples.cs` | Transactions et opérations en masse |
| `AdvancedExamples.cs` | Health checks, DI, monitoring, parallélisme |

## Exemples CRUD (`BasicCrudExamples.cs`)

### Configuration
```csharp
var options = new MariaDbConnectionOptions
{
    Server = "localhost",
    Port = 3306,
    Database = "ma_base",
    Username = "mon_utilisateur",
    Password = "mon_mot_de_passe"
};
```

### Insertion
```csharp
await connection.ExecuteNonQueryAsync(
    "INSERT INTO clients (nom, email) VALUES (@nom, @email)",
    new { nom = "Jean Dupont", email = "jean@example.com" });
```

### Lecture
```csharp
var result = await connection.ExecuteQueryAsync(
    "SELECT * FROM clients WHERE actif = @actif",
    new { actif = true });
```

### Mise à jour
```csharp
var rows = await connection.ExecuteNonQueryAsync(
    "UPDATE clients SET email = @email WHERE id = @id",
    new { email = "nouveau@example.com", id = 1 });
```

### Suppression
```csharp
await connection.ExecuteNonQueryAsync(
    "DELETE FROM clients WHERE id = @id",
    new { id = 1 });
```

## Procédures stockées (`StoredProcedureExamples.cs`)

### Paramètre IN
```csharp
var result = await connection.ExecuteQueryAsync(
    "CALL sp_get_client_by_id(@clientId)",
    new { clientId = 1 });
```

### Paramètre OUT
```csharp
await connection.ExecuteNonQueryAsync("CALL sp_count_clients(@result)");
var count = await connection.ExecuteScalarAsync<int>("SELECT @result");
```

### Paramètre INOUT
```csharp
await connection.ExecuteNonQueryAsync("SET @price = 100.00");
await connection.ExecuteNonQueryAsync("CALL sp_apply_discount(@productId, @price)", new { productId = 1 });
var discountedPrice = await connection.ExecuteScalarAsync<decimal>("SELECT @price");
```

## Event Scheduler (`EventSchedulerExamples.cs`)

### Événement ponctuel
```csharp
await connection.ExecuteNonQueryAsync(@"
    CREATE EVENT evt_cleanup
    ON SCHEDULE AT CURRENT_TIMESTAMP + INTERVAL 1 HOUR
    DO DELETE FROM logs WHERE date < NOW() - INTERVAL 30 DAY");
```

### Événement récurrent
```csharp
await connection.ExecuteNonQueryAsync(@"
    CREATE EVENT evt_daily_report
    ON SCHEDULE EVERY 1 DAY
    STARTS (CURRENT_DATE + INTERVAL 1 DAY + INTERVAL 2 HOUR)
    DO CALL sp_generate_report()");
```

### Gestion
```csharp
// Activer/Désactiver
await connection.ExecuteNonQueryAsync("ALTER EVENT evt_cleanup ENABLE");
await connection.ExecuteNonQueryAsync("ALTER EVENT evt_cleanup DISABLE");

// Supprimer
await connection.ExecuteNonQueryAsync("DROP EVENT IF EXISTS evt_cleanup");
```

## Transactions (`TransactionExamples.cs`)

### Transaction simple
```csharp
await connection.BeginTransactionAsync();
try
{
    await connection.ExecuteNonQueryAsync("INSERT INTO ...");
    await connection.ExecuteNonQueryAsync("UPDATE ...");
    await connection.CommitAsync();
}
catch
{
    await connection.RollbackAsync();
    throw;
}
```

### Avec niveau d'isolation
```csharp
await connection.BeginTransactionAsync(IsolationLevel.RepeatableRead);
```

### Transfert d'argent
```csharp
await connection.BeginTransactionAsync();
try
{
    // Vérifier le solde
    var solde = await connection.ExecuteScalarAsync<decimal>(
        "SELECT solde FROM comptes WHERE id = @id FOR UPDATE",
        new { id = fromAccount });

    if (solde < montant)
    {
        await connection.RollbackAsync();
        return false;
    }

    // Débiter
    await connection.ExecuteNonQueryAsync(
        "UPDATE comptes SET solde = solde - @montant WHERE id = @id",
        new { montant, id = fromAccount });

    // Créditer
    await connection.ExecuteNonQueryAsync(
        "UPDATE comptes SET solde = solde + @montant WHERE id = @id",
        new { montant, id = toAccount });

    await connection.CommitAsync();
    return true;
}
catch
{
    await connection.RollbackAsync();
    throw;
}
```

## Fonctionnalités avancées (`AdvancedExamples.cs`)

### Health Check
```csharp
var result = await connection.ExecuteScalarAsync<int>("SELECT 1");
var isHealthy = result == 1;
```

### Injection de dépendances
```csharp
services.AddSingleton<IMariaDbConnectionFactory, MariaDbConnectionFactory>();
services.AddScoped<IClientRepository, ClientRepository>();
```

### Requêtes parallèles
```csharp
var tasks = new List<Task<DataTable>>();
for (int i = 0; i < 5; i++)
{
    tasks.Add(Task.Run(async () =>
    {
        await using var conn = factory.CreateConnection();
        return await conn.ExecuteQueryAsync("SELECT ...");
    }));
}
var results = await Task.WhenAll(tasks);
```

### Statistiques serveur
```csharp
var stats = await connection.ExecuteQueryAsync(@"
    SELECT VARIABLE_NAME, VARIABLE_VALUE
    FROM information_schema.GLOBAL_STATUS
    WHERE VARIABLE_NAME IN ('Connections', 'Threads_connected', 'Uptime')");
```

## Prérequis

Pour utiliser ces exemples :

1. **MariaDB 11.x** ou MySQL 8.x installé et configuré
2. **Event Scheduler activé** (pour les exemples Event Scheduler) :
   ```sql
   SET GLOBAL event_scheduler = ON;
   ```
3. **Droits suffisants** pour créer des procédures et événements

## Auteur

**Nicolas DEOUX**
- Email: NDXDev@gmail.com
- LinkedIn: [nicolas-deoux-ab295980](https://www.linkedin.com/in/nicolas-deoux-ab295980/)
