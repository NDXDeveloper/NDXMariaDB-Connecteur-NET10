# Sommaire - Documentation NDXMariaDB

## Guide de démarrage

- [README.md](README.md) - Présentation et démarrage rapide

## Documentation technique

### Bibliothèque source

- [Documentation du projet source](docs/source/README.md)
  - Architecture de la bibliothèque
  - Classes principales
  - Exemples d'utilisation avancés
  - Migration depuis .NET Framework 4.7.2

### Tests

- [Documentation des tests](docs/tests/README.md)
  - Structure des tests (55 tests)
  - Tests unitaires (17)
  - Tests d'intégration (38)
  - Exécution et couverture

### Docker

- [Documentation Docker](docs/docker/README.md)
  - Installation et configuration
  - Commandes essentielles
  - Configuration personnalisée
  - Dépannage

### Exemples

- [Exemples d'utilisation](examples/README.md)
  - [CRUD de base](examples/BasicCrudExamples.cs) - INSERT, SELECT, UPDATE, DELETE
  - [Procédures stockées](examples/StoredProcedureExamples.cs) - IN, OUT, INOUT
  - [Event Scheduler](examples/EventSchedulerExamples.cs) - Tâches planifiées
  - [Transactions](examples/TransactionExamples.cs) - Transactions et bulk operations
  - [Avancé](examples/AdvancedExamples.cs) - Health checks, DI, monitoring

## Références

### Classes principales

| Classe | Description |
|--------|-------------|
| `MariaDbConnection` | Connexion principale avec gestion async |
| `MariaDbConnectionOptions` | Options de configuration |
| `MariaDbConnectionFactory` | Factory pour créer des connexions |
| `MariaDbHealthCheck` | Vérification de l'état de la base |

### Interfaces

| Interface | Description |
|-----------|-------------|
| `IMariaDbConnection` | Interface de connexion |
| `IMariaDbConnectionFactory` | Interface de factory |

### Options de connexion

| Propriété | Type | Défaut | Description |
|-----------|------|--------|-------------|
| `Server` | string | localhost | Serveur MariaDB |
| `Port` | int | 3306 | Port de connexion |
| `Database` | string | - | Nom de la base |
| `Username` | string | - | Utilisateur |
| `Password` | string | - | Mot de passe |
| `ConnectionString` | string | null | Chaîne complète (surcharge) |
| `Pooling` | bool | true | Activer le pool |
| `MinPoolSize` | int | 0 | Taille min du pool |
| `MaxPoolSize` | int | 100 | Taille max du pool |
| `ConnectionTimeoutSeconds` | int | 30 | Timeout connexion |
| `CommandTimeoutSeconds` | int | 30 | Timeout commande |
| `InnoDbLockWaitTimeout` | int | 120 | Timeout verrous InnoDB |
| `UseSsl` | bool | false | Activer SSL |
| `SslMode` | string | Preferred | Mode SSL |
| `AllowUserVariables` | bool | true | Variables @ (pour OUT/INOUT) |
| `IsPrimaryConnection` | bool | false | Connexion principale |
| `AutoCloseTimeoutMs` | int | 60000 | Fermeture auto (ms) |
| `DisableAutoClose` | bool | false | Désactiver fermeture auto |

## Fonctionnalités

### CRUD

- INSERT avec paramètres nommés
- SELECT vers DataTable ou DataReader
- SELECT scalaire typé
- UPDATE avec conditions
- DELETE avec paramètres

### Transactions

- BeginTransaction / BeginTransactionAsync
- Commit / CommitAsync
- Rollback / RollbackAsync
- Niveaux d'isolation (ReadUncommitted, ReadCommitted, RepeatableRead, Serializable)

### Procédures stockées

- Paramètres IN (entrée)
- Paramètres OUT (sortie)
- Paramètres INOUT (entrée/sortie)
- Résultats multiples
- Variables utilisateur (@variable)

### Event Scheduler

- Événements ponctuels (AT)
- Événements récurrents (EVERY)
- Dates de début/fin (STARTS/ENDS)
- Gestion (ENABLE/DISABLE/DROP)
- Appel de procédures stockées

### Autres

- Fermeture automatique des connexions inactives
- Historique des actions
- Health checks intégrés
- Logging avec ILogger
- Injection de dépendances

## Tests couverts

### Tests unitaires (17)

- MariaDbConnectionOptions
  - Valeurs par défaut
  - Construction de chaîne de connexion
  - Modes SSL
  - AllowUserVariables
- MariaDbConnectionFactory
  - Création de connexions
  - IDs uniques
  - Connexion principale

### Tests d'intégration (38)

- Connexions
  - Open/Close async
  - Dispose async
  - États et historique
- CRUD
  - INSERT simple et multiple
  - SELECT (scalaire, query, reader)
  - UPDATE avec paramètres
  - DELETE conditionnel
  - Opérations en masse
- Transactions
  - Commit
  - Rollback
  - Isolation levels
- Procédures stockées
  - Appel simple
  - Paramètres IN
  - Paramètres OUT
  - Paramètres INOUT
  - Combinaisons multiples
- Event Scheduler
  - Vérification activation
  - Création/suppression
  - Événements récurrents
  - Exécution ponctuelle
  - Modification (ALTER)
  - Avec procédures stockées
  - Liste des événements
  - Conditions

### Health Checks (3)

- Connexion saine
- Connexion défaillante
- Informations serveur

## Historique

### Version 1.0.0 (Décembre 2025)

- Portage initial depuis .NET Framework 4.7.2
- Support complet async/await
- Compatibilité Windows et Linux
- 55 tests unitaires et d'intégration
- Configuration Docker MariaDB 11.8 LTS
- Event Scheduler support
- Procédures stockées IN/OUT/INOUT
- Exemples complets documentés

## Ressources externes

- [Documentation MariaDB](https://mariadb.com/kb/en/)
- [Event Scheduler MariaDB](https://mariadb.com/kb/en/event-scheduler/)
- [MySqlConnector](https://mysqlconnector.net/)
- [.NET 10 Documentation](https://docs.microsoft.com/dotnet/)
