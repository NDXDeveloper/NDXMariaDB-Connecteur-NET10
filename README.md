# NDXMariaDB

**Bibliothèque de connexion MariaDB/MySQL moderne et performante pour .NET 10**

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![MariaDB](https://img.shields.io/badge/MariaDB-11.8%20LTS-003545?style=flat-square&logo=mariadb)](https://mariadb.org/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](LICENSE)
[![Platform](https://img.shields.io/badge/Platform-Windows%20%7C%20Linux-blue?style=flat-square)]()
[![Tests](https://img.shields.io/badge/Tests-55%20passed-brightgreen?style=flat-square)]()

---

## Pourquoi NDXMariaDB ?

Vous cherchez une bibliothèque de connexion MariaDB/MySQL qui soit **simple**, **moderne** et **performante** ? NDXMariaDB est faite pour vous !

- **Async/Await natif** : Toutes les opérations supportent l'asynchrone
- **Cross-platform** : Fonctionne sur Windows et Linux sans modification
- **Gestion automatique** : Fermeture automatique des connexions inactives
- **Transactions simplifiées** : API intuitive pour gérer vos transactions
- **Procédures stockées** : Support complet IN, OUT, INOUT
- **Event Scheduler** : Création et gestion des tâches planifiées
- **Logging intégré** : Compatible avec Microsoft.Extensions.Logging
- **Injection de dépendances** : S'intègre parfaitement avec DI

---

## Installation rapide

```bash
# Cloner le dépôt
git clone https://github.com/NDXDeveloper/NDXMariaDB-Connecteur-NET10.git

# Ajouter une référence au projet dans votre solution
dotnet add reference chemin/vers/src/NDXMariaDB/NDXMariaDB.csproj
```

Ou simplement copier le dossier `src/NDXMariaDB` dans votre solution.
```

---

## Démarrage en 30 secondes

```csharp
using NDXMariaDB;

// Création d'une connexion
var options = new MariaDbConnectionOptions
{
    Server = "localhost",
    Database = "ma_base",
    Username = "utilisateur",
    Password = "mot_de_passe"
};

await using var connection = new MariaDbConnection(options);

// Exécuter une requête
var result = await connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM users");
Console.WriteLine($"Nombre d'utilisateurs: {result}");
```

---

## Fonctionnalités principales

### Opérations CRUD asynchrones

```csharp
// INSERT avec paramètres
await connection.ExecuteNonQueryAsync(
    "INSERT INTO users (name, email) VALUES (@name, @email)",
    new { name = "Jean", email = "jean@example.com" });

// SELECT avec DataTable
var users = await connection.ExecuteQueryAsync(
    "SELECT * FROM users WHERE active = @active",
    new { active = true });

// SELECT scalaire
var count = await connection.ExecuteScalarAsync<long>("SELECT COUNT(*) FROM users");

// UPDATE
var rows = await connection.ExecuteNonQueryAsync(
    "UPDATE users SET email = @email WHERE id = @id",
    new { email = "nouveau@example.com", id = 1 });

// DELETE
await connection.ExecuteNonQueryAsync(
    "DELETE FROM users WHERE id = @id",
    new { id = 1 });
```

### Gestion des transactions

```csharp
await connection.BeginTransactionAsync();
try
{
    await connection.ExecuteNonQueryAsync("UPDATE accounts SET balance = balance - 100 WHERE id = 1");
    await connection.ExecuteNonQueryAsync("UPDATE accounts SET balance = balance + 100 WHERE id = 2");
    await connection.CommitAsync();
}
catch
{
    await connection.RollbackAsync();
    throw;
}
```

### Procédures stockées avec IN, OUT, INOUT

```csharp
// Procédure avec paramètres IN
var result = await connection.ExecuteQueryAsync(
    "CALL sp_get_clients_by_status(@status)",
    new { status = "active" });

// Procédure avec paramètre OUT
await connection.ExecuteNonQueryAsync("CALL sp_count_clients(@result)");
var count = await connection.ExecuteScalarAsync<int>("SELECT @result");

// Procédure avec paramètre INOUT
await connection.ExecuteNonQueryAsync("SET @price = 100.00");
await connection.ExecuteNonQueryAsync("CALL sp_apply_discount(@productId, @price)", new { productId = 1 });
var discountedPrice = await connection.ExecuteScalarAsync<decimal>("SELECT @price");
```

### Event Scheduler (tâches planifiées)

```csharp
// Vérifier que l'Event Scheduler est activé
var status = await connection.ExecuteScalarAsync<string>("SELECT @@event_scheduler");

// Créer un événement récurrent
await connection.ExecuteNonQueryAsync(@"
    CREATE EVENT evt_cleanup_logs
    ON SCHEDULE EVERY 1 DAY
    STARTS (CURRENT_DATE + INTERVAL 1 DAY + INTERVAL 2 HOUR)
    DO DELETE FROM logs WHERE date_creation < NOW() - INTERVAL 30 DAY");

// Créer un événement ponctuel
await connection.ExecuteNonQueryAsync(@"
    CREATE EVENT evt_send_report
    ON SCHEDULE AT CURRENT_TIMESTAMP + INTERVAL 1 HOUR
    DO CALL sp_generate_report()");

// Gérer les événements
await connection.ExecuteNonQueryAsync("ALTER EVENT evt_cleanup_logs DISABLE");
await connection.ExecuteNonQueryAsync("DROP EVENT IF EXISTS evt_cleanup_logs");
```

### Factory et injection de dépendances

```csharp
// Configuration avec DI
services.AddNDXMariaDB(options =>
{
    options.Server = "localhost";
    options.Database = "ma_base";
    options.Username = "user";
    options.Password = "pass";
});

// Utilisation dans un service
public class UserService
{
    private readonly IMariaDbConnectionFactory _factory;

    public UserService(IMariaDbConnectionFactory factory)
    {
        _factory = factory;
    }

    public async Task<DataTable> GetUsersAsync()
    {
        await using var conn = _factory.CreateConnection();
        return await conn.ExecuteQueryAsync("SELECT * FROM users");
    }
}
```

### Health Check intégré

```csharp
var healthCheck = new MariaDbHealthCheck(connectionFactory);
var result = await healthCheck.CheckHealthAsync();

if (result.IsHealthy)
{
    Console.WriteLine($"Connexion OK - Temps: {result.ResponseTime.TotalMilliseconds}ms");
}
```

---

## Exemples complets

Le dossier `examples/` contient des exemples détaillés :

| Fichier | Description |
|---------|-------------|
| `BasicCrudExamples.cs` | Opérations CRUD complètes |
| `StoredProcedureExamples.cs` | Procédures stockées IN/OUT/INOUT |
| `EventSchedulerExamples.cs` | Tâches planifiées Event Scheduler |
| `TransactionExamples.cs` | Transactions et opérations en masse |
| `AdvancedExamples.cs` | Health checks, DI, monitoring |

---

## Configuration Docker pour les tests

Le projet inclut une configuration Docker complète pour MariaDB 11.8 LTS :

```bash
# Démarrer MariaDB
cd docker
docker-compose up -d

# Vérifier l'état
docker-compose ps

# Voir les logs
docker-compose logs -f

# Arrêter
docker-compose down

# Supprimer tout (données incluses)
docker-compose down -v --rmi all
```

**Paramètres de connexion par défaut :**

| Paramètre | Valeur |
|-----------|--------|
| Hôte | localhost |
| Port | 3306 |
| Base | ndxmariadb_test |
| Utilisateur | testuser |
| Mot de passe | testpassword |

**Configuration MariaDB incluse :**
- Event Scheduler activé (`event_scheduler = ON`)
- UTF8MB4 par défaut
- InnoDB optimisé pour les tests
- Slow query log activé

---

## Structure du projet

```
NDXMariaDB/
├── src/
│   └── NDXMariaDB/              # Bibliothèque principale
│       ├── MariaDbConnection.cs
│       ├── MariaDbConnectionOptions.cs
│       ├── MariaDbConnectionFactory.cs
│       ├── MariaDbHealthCheck.cs
│       └── Extensions/
├── tests/
│   └── NDXMariaDB.Tests/        # 55 tests (unitaires + intégration)
│       ├── Unit/
│       └── Integration/
├── examples/                     # Exemples d'utilisation
│   ├── BasicCrudExamples.cs
│   ├── StoredProcedureExamples.cs
│   ├── EventSchedulerExamples.cs
│   ├── TransactionExamples.cs
│   └── AdvancedExamples.cs
├── docs/                         # Documentation
└── docker/                       # Configuration Docker
    ├── docker-compose.yml
    ├── config/my.cnf
    └── init/

```

---

## Tests

Le projet inclut **55 tests** couvrant :

- **Tests unitaires (17)** : Options, Factory, Configuration
- **Tests d'intégration (38)** :
  - Connexions et cycle de vie
  - Opérations CRUD
  - Transactions (commit, rollback, isolation levels)
  - Procédures stockées (IN, OUT, INOUT)
  - Event Scheduler (création, modification, exécution)
  - Health checks

```bash
# Lancer tous les tests
dotnet test

# Avec verbosité
dotnet test --verbosity normal

# Avec couverture
dotnet test --collect:"XPlat Code Coverage"
```

---

## Documentation

La documentation complète est disponible dans le fichier [SOMMAIRE.md](/SOMMAIRE.md).

---

## Prérequis

- **.NET 10.0** ou supérieur
- **MariaDB 10.6+** ou **MySQL 8.0+**
- **Docker** (optionnel, pour les tests)

---

## Auteur

**Nicolas DEOUX**
- Email: [NDXDev@gmail.com](mailto:NDXDev@gmail.com)
- LinkedIn: [nicolas-deoux-ab295980](https://www.linkedin.com/in/nicolas-deoux-ab295980/)

---

## Licence

Ce projet est sous licence MIT. Voir le fichier [LICENSE](/LICENSE) pour plus de détails.

---

<p align="center">
  <b>Fait avec passion en France</b>
</p>
