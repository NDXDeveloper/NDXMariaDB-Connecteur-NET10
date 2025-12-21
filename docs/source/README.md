# Documentation du projet source NDXMariaDB

## Vue d'ensemble

NDXMariaDB est une bibliothèque de connexion MariaDB/MySQL moderne pour .NET 10, portée et modernisée depuis une implémentation .NET Framework 4.7.2.

## Architecture

```
NDXMariaDB/
├── MariaDbConnection.cs           # Classe de connexion principale
├── MariaDbConnectionOptions.cs    # Options de configuration
├── MariaDbConnectionFactory.cs    # Factory pour créer des connexions
├── IMariaDbConnection.cs          # Interface de connexion
├── MariaDbHealthCheck.cs          # Vérification de santé
└── Extensions/
    └── ServiceCollectionExtensions.cs  # Extensions DI
```

## Classes principales

### MariaDbConnection

La classe principale qui gère les connexions à MariaDB/MySQL.

**Fonctionnalités :**
- Gestion synchrone et asynchrone des connexions
- Support des transactions avec niveaux d'isolation
- Timer de fermeture automatique pour les connexions inactives
- Historique des actions pour le débogage
- Pattern IDisposable et IAsyncDisposable
- Support des procédures stockées (IN, OUT, INOUT)
- Compatible Event Scheduler

**Exemple complet :**

```csharp
var options = new MariaDbConnectionOptions
{
    Server = "localhost",
    Port = 3306,
    Database = "ma_base",
    Username = "user",
    Password = "pass",
    AutoCloseTimeoutMs = 60000,  // Fermeture auto après 1 min d'inactivité
    InnoDbLockWaitTimeout = 120,
    AllowUserVariables = true     // Pour les paramètres OUT/INOUT
};

await using var connection = new MariaDbConnection(options);

// Ouvrir la connexion
await connection.OpenAsync();

// Exécuter des requêtes
var count = await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM users");

// Transaction
await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted);
try
{
    await connection.ExecuteNonQueryAsync("UPDATE users SET status = 'active' WHERE id = 1");
    await connection.CommitAsync();
}
catch
{
    await connection.RollbackAsync();
    throw;
}
```

### MariaDbConnectionOptions

Options de configuration pour personnaliser le comportement de la connexion.

| Propriété | Type | Par défaut | Description |
|-----------|------|------------|-------------|
| `Server` | string | "localhost" | Serveur MariaDB |
| `Port` | int | 3306 | Port de connexion |
| `Database` | string | "" | Nom de la base |
| `Username` | string | "" | Utilisateur |
| `Password` | string | "" | Mot de passe |
| `ConnectionString` | string? | null | Chaîne complète (surcharge autres props) |
| `IsPrimaryConnection` | bool | false | Connexion principale (pas de fermeture auto) |
| `AutoCloseTimeoutMs` | int | 60000 | Timeout de fermeture auto (ms) |
| `DisableAutoClose` | bool | false | Désactive la fermeture automatique |
| `Pooling` | bool | true | Active le pooling |
| `MinPoolSize` | int | 0 | Taille min du pool |
| `MaxPoolSize` | int | 100 | Taille max du pool |
| `ConnectionTimeoutSeconds` | int | 30 | Timeout de connexion |
| `CommandTimeoutSeconds` | int | 30 | Timeout des commandes |
| `InnoDbLockWaitTimeout` | int | 120 | Timeout verrou InnoDB |
| `UseSsl` | bool | false | Active SSL |
| `SslMode` | string | "Preferred" | Mode SSL |
| `AllowUserVariables` | bool | true | Variables utilisateur @ (pour OUT/INOUT) |

### MariaDbConnectionFactory

Factory pour créer des instances de connexion avec des options centralisées.

```csharp
// Création de la factory
var factory = new MariaDbConnectionFactory(defaultOptions, loggerFactory);

// Créer une connexion standard
await using var conn1 = factory.CreateConnection();

// Créer une connexion principale
await using var mainConn = factory.CreatePrimaryConnection();

// Créer avec configuration personnalisée
await using var customConn = factory.CreateConnection(opts =>
{
    opts.CommandTimeoutSeconds = 60;
    opts.DisableAutoClose = true;
});
```

### MariaDbHealthCheck

Utilitaire pour vérifier l'état de la base de données.

```csharp
var healthCheck = new MariaDbHealthCheck(factory);

// Vérifier la santé
var result = await healthCheck.CheckHealthAsync();
Console.WriteLine($"Healthy: {result.IsHealthy}");
Console.WriteLine($"Message: {result.Message}");
Console.WriteLine($"Response Time: {result.ResponseTime.TotalMilliseconds}ms");

// Obtenir les infos serveur
var info = await healthCheck.GetServerInfoAsync();
Console.WriteLine($"Version: {info.Version}");
Console.WriteLine($"Database: {info.CurrentDatabase}");
```

## Procédures stockées

### Paramètres IN

```csharp
var result = await connection.ExecuteQueryAsync(
    "CALL sp_get_client(@clientId)",
    new { clientId = 1 });
```

### Paramètres OUT

```csharp
// Appeler la procédure avec une variable utilisateur
await connection.ExecuteNonQueryAsync("CALL sp_count_clients(@count)");

// Récupérer la valeur
var count = await connection.ExecuteScalarAsync<int>("SELECT @count");
```

### Paramètres INOUT

```csharp
// Initialiser la variable
await connection.ExecuteNonQueryAsync("SET @price = 100.00");

// Appeler la procédure
await connection.ExecuteNonQueryAsync(
    "CALL sp_apply_discount(@productId, @price)",
    new { productId = 1 });

// Récupérer la valeur modifiée
var discountedPrice = await connection.ExecuteScalarAsync<decimal>("SELECT @price");
```

### Exemple complet avec multiples paramètres

```csharp
await connection.OpenAsync();

// Initialiser les variables INOUT
await connection.ExecuteNonQueryAsync("SET @adjustment = 5");

// Appeler une procédure complexe
await connection.ExecuteNonQueryAsync(
    "CALL sp_process_inventory(@category, @count, @total, @avg, @adjustment)",
    new { category = "Electronics" });

// Récupérer tous les paramètres OUT
var count = await connection.ExecuteScalarAsync<int>("SELECT @count");
var total = await connection.ExecuteScalarAsync<decimal>("SELECT @total");
var avg = await connection.ExecuteScalarAsync<decimal>("SELECT @avg");
var adjustment = await connection.ExecuteScalarAsync<int>("SELECT @adjustment");
```

## Event Scheduler

### Vérifier l'état

```csharp
var status = await connection.ExecuteScalarAsync<string>("SELECT @@event_scheduler");
var isEnabled = status == "ON" || status == "1";
```

### Créer un événement ponctuel

```csharp
await connection.ExecuteNonQueryAsync(@"
    CREATE EVENT evt_cleanup
    ON SCHEDULE AT CURRENT_TIMESTAMP + INTERVAL 1 HOUR
    ON COMPLETION PRESERVE
    COMMENT 'Nettoyage automatique'
    DO DELETE FROM logs WHERE date_creation < NOW() - INTERVAL 30 DAY");
```

### Créer un événement récurrent

```csharp
await connection.ExecuteNonQueryAsync(@"
    CREATE EVENT evt_daily_backup
    ON SCHEDULE EVERY 1 DAY
    STARTS (CURRENT_DATE + INTERVAL 1 DAY + INTERVAL 2 HOUR)
    ENDS (CURRENT_DATE + INTERVAL 1 YEAR)
    DO CALL sp_backup_database()");
```

### Gérer les événements

```csharp
// Désactiver
await connection.ExecuteNonQueryAsync("ALTER EVENT evt_cleanup DISABLE");

// Activer
await connection.ExecuteNonQueryAsync("ALTER EVENT evt_cleanup ENABLE");

// Modifier l'intervalle
await connection.ExecuteNonQueryAsync(@"
    ALTER EVENT evt_cleanup
    ON SCHEDULE EVERY 2 HOUR");

// Supprimer
await connection.ExecuteNonQueryAsync("DROP EVENT IF EXISTS evt_cleanup");
```

### Lister les événements

```csharp
var events = await connection.ExecuteQueryAsync(@"
    SELECT EVENT_NAME, EVENT_TYPE, STATUS, INTERVAL_VALUE, INTERVAL_FIELD
    FROM information_schema.EVENTS
    WHERE EVENT_SCHEMA = DATABASE()");
```

## Injection de dépendances

Intégration avec Microsoft.Extensions.DependencyInjection :

```csharp
// Dans Program.cs ou Startup.cs
services.AddNDXMariaDB(options =>
{
    options.Server = "localhost";
    options.Database = "ma_base";
    options.Username = "user";
    options.Password = "pass";
});

// Utilisation dans un service
public class MonService
{
    private readonly IMariaDbConnectionFactory _factory;
    private readonly IMariaDbConnection _connection;

    public MonService(
        IMariaDbConnectionFactory factory,
        IMariaDbConnection connection)
    {
        _factory = factory;
        _connection = connection;
    }
}
```

## Migration depuis .NET Framework 4.7.2

### Principales différences

| Ancien (VB.NET 4.7.2) | Nouveau (C# .NET 10) |
|-----------------------|----------------------|
| `Dim oMy As New Class_MySql()` | `var conn = new MariaDbConnection()` |
| `oMy.Open()` | `await conn.OpenAsync()` |
| `oMy.BeginTransaction()` | `await conn.BeginTransactionAsync()` |
| `oMy.Commit()` | `await conn.CommitAsync()` |
| `oMy.Rollback()` | `await conn.RollbackAsync()` |
| `oMy.Close()` | `await conn.CloseAsync()` |
| `oMy.Dispose()` | `await conn.DisposeAsync()` |

### Points clés de la migration

1. **Async partout** : Toutes les opérations IO sont maintenant async
2. **IAsyncDisposable** : Utilisez `await using` au lieu de `Using`
3. **Nullable reference types** : Le code utilise les types référence nullable
4. **Records** : Utilisation de records pour les DTOs
5. **Pattern matching** : Simplification du code avec pattern matching

## Bonnes pratiques

### 1. Toujours utiliser `await using`

```csharp
// Correct
await using var connection = new MariaDbConnection(options);

// Éviter
var connection = new MariaDbConnection(options);
try { ... }
finally { connection.Dispose(); }
```

### 2. Utiliser la factory pour les services

```csharp
// Préférer
await using var conn = _factory.CreateConnection();

// Plutôt que
await using var conn = new MariaDbConnection(_options);
```

### 3. Gérer les transactions correctement

```csharp
await connection.BeginTransactionAsync();
try
{
    // Opérations...
    await connection.CommitAsync();
}
catch
{
    await connection.RollbackAsync();
    throw;
}
```

### 4. Utiliser les paramètres pour éviter les injections SQL

```csharp
// Correct
await connection.ExecuteNonQueryAsync(
    "SELECT * FROM users WHERE id = @id",
    new { id = userId });

// DANGER - Injection SQL possible
await connection.ExecuteNonQueryAsync(
    $"SELECT * FROM users WHERE id = {userId}");
```

## Performances

- Le pooling est activé par défaut
- Les connexions inactives sont fermées automatiquement
- Les opérations async évitent de bloquer les threads
- MySqlConnector est utilisé pour de meilleures performances que MySql.Data
- AllowUserVariables activé par défaut pour les procédures stockées
