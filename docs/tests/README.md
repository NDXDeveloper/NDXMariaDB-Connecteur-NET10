# Documentation des tests NDXMariaDB

## Vue d'ensemble

Le projet contient **55 tests** répartis entre tests unitaires et tests d'intégration.

| Catégorie | Nombre | Description |
|-----------|--------|-------------|
| Tests unitaires | 17 | Options, Factory, Configuration |
| Tests d'intégration | 38 | Connexions, CRUD, Transactions, Procédures, Event Scheduler |

## Structure des tests

```
tests/NDXMariaDB.Tests/
├── Unit/                          # Tests unitaires (17)
│   ├── MariaDbConnectionOptionsTests.cs
│   └── MariaDbConnectionFactoryTests.cs
├── Integration/                   # Tests d'intégration (38)
│   ├── MariaDbConnectionTests.cs
│   └── MariaDbHealthCheckTests.cs
├── Fixtures/                      # Fixtures partagées
│   └── MariaDbFixture.cs
└── appsettings.test.json         # Configuration de test
```

## Prérequis

- .NET 10 SDK
- Docker (pour les tests d'intégration)

## Frameworks de test utilisés

| Framework | Version | Usage |
|-----------|---------|-------|
| xUnit | 2.9.2 | Framework de test principal |
| FluentAssertions | 6.12.2 | Assertions lisibles |
| Moq | 4.20.72 | Mocking |
| Testcontainers.MariaDb | 4.1.0 | Conteneur MariaDB automatique |
| coverlet | 6.0.2 | Couverture de code |

## Exécuter les tests

### Tests unitaires uniquement

```bash
dotnet test --filter "FullyQualifiedName~Unit"
```

### Tests d'intégration uniquement

```bash
dotnet test --filter "FullyQualifiedName~Integration"
```

### Tous les tests

```bash
dotnet test
```

### Avec verbosité

```bash
dotnet test --verbosity normal
```

### Avec couverture de code

```bash
dotnet test --collect:"XPlat Code Coverage"

# Générer un rapport HTML (nécessite ReportGenerator)
reportgenerator \
  -reports:"**/coverage.cobertura.xml" \
  -targetdir:"coverage-report" \
  -reporttypes:Html
```

## Tests unitaires (17)

### MariaDbConnectionOptionsTests (9 tests)

| Test | Description |
|------|-------------|
| `DefaultValues_ShouldBeCorrect` | Valeurs par défaut |
| `BuildConnectionString_WithConnectionString_*` | Construction avec chaîne |
| `BuildConnectionString_WithProperties_*` | Construction avec propriétés |
| `BuildConnectionString_WithSslMode_*` | Modes SSL (5 tests) |
| `AllowUserVariables_*` | Gestion des variables utilisateur |

### MariaDbConnectionFactoryTests (8 tests)

| Test | Description |
|------|-------------|
| `Constructor_WithNullOptions_ShouldThrow` | Validation des paramètres |
| `Constructor_WithConnectionString_ShouldWork` | Création avec chaîne |
| `CreateConnection_ShouldReturnNewConnection` | Création standard |
| `CreateConnection_ShouldGenerateUniqueIds` | IDs uniques |
| `CreateConnection_WithOptions_*` | Configuration personnalisée |
| `CreatePrimaryConnection_*` | Connexion principale |

## Tests d'intégration (38)

### MariaDbConnectionTests (35 tests)

#### Connexions (5 tests)

| Test | Description |
|------|-------------|
| `OpenAsync_ShouldOpenConnection` | Ouverture async |
| `CloseAsync_ShouldCloseConnection` | Fermeture async |
| `DisposeAsync_ShouldCloseConnection` | Dispose async |
| `Connection_ShouldHaveUniqueId` | ID unique |
| `CreatedAt_ShouldBeSetOnConstruction` | Date de création |

#### CRUD (10 tests)

| Test | Description |
|------|-------------|
| `ExecuteScalarAsync_ShouldReturnValue` | SELECT scalaire |
| `ExecuteQueryAsync_ShouldReturnDataTable` | SELECT avec DataTable |
| `ExecuteReaderAsync_ShouldReturnReader` | SELECT avec Reader |
| `ExecuteNonQueryAsync_CreateTable_*` | CREATE TABLE |
| `ExecuteNonQueryAsync_InsertData_*` | INSERT simple |
| `ExecuteNonQueryAsync_InsertWithMultipleParameters_*` | INSERT paramétré |
| `ExecuteNonQueryAsync_BulkInsert_*` | INSERT multiple |
| `ExecuteNonQueryAsync_UpdateData_*` | UPDATE |
| `ExecuteNonQueryAsync_DeleteData_*` | DELETE |
| `ActionHistory_ShouldTrackActions` | Historique |

#### Transactions (4 tests)

| Test | Description |
|------|-------------|
| `BeginTransactionAsync_ShouldSetIsTransactionActive` | Démarrage transaction |
| `Transaction_CommitAsync_ShouldPersistChanges` | Commit |
| `Transaction_RollbackAsync_ShouldRevertChanges` | Rollback |
| `Transaction_UpdateAndDelete_ShouldWorkTogether` | Opérations combinées |

#### Procédures stockées (8 tests)

| Test | Description |
|------|-------------|
| `ExecuteStoredProcedure_CallSimpleProcedure_*` | Appel simple |
| `ExecuteStoredProcedure_WithInputParameters_*` | Paramètres IN |
| `ExecuteStoredProcedure_WithOutParameter_*` | Paramètres OUT |
| `ExecuteStoredProcedure_WithInOutParameter_*` | Paramètres INOUT |
| `ExecuteStoredProcedure_InsertWithOutId_*` | INSERT avec OUT |
| `ExecuteStoredProcedure_InsertAndReturnId_*` | INSERT + LAST_INSERT_ID |
| `ExecuteStoredProcedure_WithInOutAndResultSet_*` | INOUT + résultats |
| `ExecuteStoredProcedure_WithMultipleInAndOutParameters_*` | Multiples IN/OUT |

#### Event Scheduler (8 tests)

| Test | Description |
|------|-------------|
| `EventScheduler_ShouldBeEnabled` | Vérification activation |
| `EventScheduler_CreateAndDropEvent_*` | Création/suppression |
| `EventScheduler_RecurringEvent_*` | Événement récurrent |
| `EventScheduler_OneTimeEvent_ShouldExecute` | Exécution ponctuelle |
| `EventScheduler_AlterEvent_*` | Modification |
| `EventScheduler_EventWithStoredProcedure_*` | Avec procédure |
| `EventScheduler_ListAllEvents_*` | Liste événements |
| `EventScheduler_EventWithCondition_*` | Avec condition |

### MariaDbHealthCheckTests (3 tests)

| Test | Description |
|------|-------------|
| `CheckHealthAsync_WhenHealthy_*` | État sain |
| `CheckHealthAsync_WhenUnhealthy_*` | État non sain |
| `GetServerInfoAsync_*` | Informations serveur |

## Fixture Testcontainers

La fixture `MariaDbFixture` utilise Testcontainers pour créer automatiquement un conteneur MariaDB 11.8 avec Event Scheduler activé :

```csharp
public sealed class MariaDbFixture : IAsyncLifetime
{
    private readonly MariaDbContainer _container;

    public MariaDbFixture()
    {
        _container = new MariaDbBuilder()
            .WithImage("mariadb:11.8")
            .WithDatabase("ndxmariadb_test")
            .WithUsername("testuser")
            .WithPassword("testpassword")
            .WithCommand("--event-scheduler=ON")  // Event Scheduler activé
            .Build();
    }

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}
```

### Utilisation dans les tests

```csharp
[Collection("MariaDB")]
public class MonTest
{
    private readonly MariaDbFixture _fixture;

    public MonTest(MariaDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task MonTestAsync()
    {
        await using var connection = _fixture.CreateConnection();
        // ...
    }
}
```

## Configuration de test

Le fichier `appsettings.test.json` contient la configuration par défaut pour les tests manuels (sans Testcontainers) :

```json
{
  "MariaDB": {
    "Server": "localhost",
    "Port": 3306,
    "Database": "ndxmariadb_test",
    "Username": "testuser",
    "Password": "testpassword"
  }
}
```

## Meilleures pratiques

### 1. Isolation des tests

Chaque test crée et supprime ses propres données :

```csharp
var tableName = $"test_{Guid.NewGuid():N}";
try
{
    await connection.ExecuteNonQueryAsync($"CREATE TABLE {tableName} ...");
    // Tests...
}
finally
{
    await connection.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS {tableName}");
}
```

### 2. Tests parallèles

Les tests d'intégration utilisent des noms de table/procédure/événement uniques pour permettre l'exécution parallèle.

### 3. Assertions lisibles

Utilisation de FluentAssertions pour des tests lisibles :

```csharp
result.Should().NotBeNull();
result.IsHealthy.Should().BeTrue();
result.ResponseTime.Should().BeGreaterThan(TimeSpan.Zero);
```

### 4. Nettoyage dans finally

Toujours nettoyer les ressources créées :

```csharp
try
{
    await connection.ExecuteNonQueryAsync($"CREATE EVENT {eventName} ...");
    // Tests...
}
finally
{
    await connection.ExecuteNonQueryAsync($"DROP EVENT IF EXISTS {eventName}");
    await connection.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS {tableName}");
}
```

## CI/CD

Exemple de configuration GitHub Actions :

```yaml
name: Tests

on: [push, pull_request]

jobs:
  test:
    runs-on: ubuntu-latest

    services:
      mariadb:
        image: mariadb:11.8
        env:
          MARIADB_ROOT_PASSWORD: rootpassword
          MARIADB_DATABASE: ndxmariadb_test
          MARIADB_USER: testuser
          MARIADB_PASSWORD: testpassword
        ports:
          - 3306:3306
        options: >-
          --health-cmd="healthcheck.sh --connect --innodb_initialized"
          --health-interval=10s
          --health-timeout=5s
          --health-retries=5

    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet restore
      - run: dotnet build --no-restore
      - run: dotnet test --no-build --verbosity normal
```

## Couverture de code

Les tests couvrent :

- **Connexions** : Ouverture, fermeture, dispose, états
- **CRUD** : INSERT, SELECT, UPDATE, DELETE
- **Transactions** : Begin, Commit, Rollback, Isolation levels
- **Procédures stockées** : IN, OUT, INOUT, multiples paramètres
- **Event Scheduler** : Création, modification, exécution, suppression
- **Health Checks** : État sain, état non sain, informations serveur
- **Factory** : Création, configuration, IDs uniques
- **Options** : Valeurs par défaut, construction chaîne connexion, SSL
