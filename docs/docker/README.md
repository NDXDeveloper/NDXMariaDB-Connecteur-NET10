# Documentation Docker - NDXMariaDB

## Vue d'ensemble

Ce guide explique comment utiliser la configuration Docker fournie pour exécuter MariaDB 11.8 LTS dans un environnement de développement et de test.

## Prérequis

- Docker 20.10+ ou Docker Desktop
- Docker Compose v2+

Vérifier l'installation :
```bash
docker --version
docker compose version
```

## Structure des fichiers Docker

```
docker/
├── Dockerfile              # Image personnalisée (optionnel)
├── docker-compose.yml      # Configuration principale
├── config/
│   └── my.cnf              # Configuration MariaDB
└── init/
    └── 01-init-database.sql  # Script d'initialisation
```

## Démarrage rapide

### 1. Démarrer MariaDB

```bash
cd docker
docker compose up -d
```

### 2. Vérifier l'état

```bash
docker compose ps
```

Sortie attendue :
```
NAME               STATUS          PORTS
ndxmariadb-test    Up (healthy)    0.0.0.0:3306->3306/tcp
```

### 3. Se connecter à MariaDB

```bash
# Via le client mysql dans le conteneur
docker compose exec mariadb mariadb -u testuser -p ndxmariadb_test
# Mot de passe: testpassword

# Ou avec un client externe
mariadb -h localhost -P 3306 -u testuser -p ndxmariadb_test
```

## Commandes essentielles

| Action | Commande |
|--------|----------|
| Démarrer | `docker compose up -d` |
| Arrêter | `docker compose down` |
| Redémarrer | `docker compose restart` |
| Voir les logs | `docker compose logs -f` |
| État | `docker compose ps` |
| Shell bash | `docker compose exec mariadb bash` |
| Client MariaDB | `docker compose exec mariadb mariadb -u testuser -p` |

## Arrêt et nettoyage

### Arrêter le conteneur (conserver les données)

```bash
docker compose down
```

### Arrêter et supprimer les données

```bash
docker compose down -v
```

### Supprimer tout (conteneur, volumes, images)

```bash
docker compose down -v --rmi all
```

### Nettoyage complet

```bash
# Arrêter et supprimer
docker compose down -v --rmi all

# Supprimer le volume nommé si encore présent
docker volume rm ndxmariadb-data 2>/dev/null || true

# Supprimer le réseau si encore présent
docker network rm ndxmariadb-net 2>/dev/null || true

# Vérifier qu'il ne reste rien
docker ps -a | grep ndxmariadb
docker volume ls | grep ndxmariadb
docker network ls | grep ndxmariadb
```

## Configuration

### Paramètres de connexion par défaut

| Paramètre | Valeur |
|-----------|--------|
| Hôte | localhost |
| Port | 3306 |
| Base de données | ndxmariadb_test |
| Utilisateur root | root / rootpassword |
| Utilisateur test | testuser / testpassword |

### Variables d'environnement

Modifiables dans `docker-compose.yml` :

```yaml
environment:
  MARIADB_ROOT_PASSWORD: rootpassword
  MARIADB_DATABASE: ndxmariadb_test
  MARIADB_USER: testuser
  MARIADB_PASSWORD: testpassword
```

### Configuration MariaDB personnalisée

Le fichier `config/my.cnf` contient les paramètres optimisés :

```ini
[mysqld]
# Encodage
character-set-server = utf8mb4
collation-server = utf8mb4_unicode_ci

# InnoDB
innodb_buffer_pool_size = 256M
innodb_lock_wait_timeout = 120

# Connexions
max_connections = 100
wait_timeout = 28800
```

## Script d'initialisation

Le fichier `init/01-init-database.sql` est exécuté au premier démarrage :

- Crée les tables de test
- Insère des données initiales
- Crée des procédures stockées de test
- Crée des vues de test

Pour réinitialiser la base :
```bash
docker compose down -v
docker compose up -d
```

## Dépannage

### Le conteneur ne démarre pas

```bash
# Vérifier les logs
docker compose logs mariadb

# Causes fréquentes:
# - Port 3306 déjà utilisé
# - Permissions insuffisantes sur les volumes
```

### Port 3306 déjà utilisé

```bash
# Trouver le processus
sudo lsof -i :3306

# Ou changer le port dans docker-compose.yml
ports:
  - "3307:3306"  # Utiliser le port 3307
```

### Connexion refusée

```bash
# Vérifier que le conteneur est healthy
docker compose ps

# Attendre que MariaDB soit prêt (peut prendre 30s)
docker compose logs -f mariadb | grep "ready for connections"
```

### Réinitialiser les données

```bash
docker compose down -v
docker compose up -d
```

### Problèmes de permissions (Linux)

```bash
# Donner les permissions au dossier data
sudo chown -R 1001:1001 ./data
```

## Utilisation avec les tests

### Avec Testcontainers (recommandé)

Les tests utilisent Testcontainers pour créer automatiquement un conteneur MariaDB. Aucune configuration Docker manuelle nécessaire.

### Tests manuels

```bash
# 1. Démarrer MariaDB
cd docker && docker compose up -d && cd ..

# 2. Lancer les tests
dotnet test

# 3. Arrêter MariaDB
cd docker && docker compose down && cd ..
```

## Chaîne de connexion .NET

```csharp
var connectionString = "Server=localhost;Port=3306;Database=ndxmariadb_test;User=testuser;Password=testpassword";
```

Ou avec les options :

```csharp
var options = new MariaDbConnectionOptions
{
    Server = "localhost",
    Port = 3306,
    Database = "ndxmariadb_test",
    Username = "testuser",
    Password = "testpassword"
};
```

## Ressources

- [Documentation MariaDB Docker](https://hub.docker.com/_/mariadb)
- [MariaDB 11.8 Release Notes](https://mariadb.com/kb/en/mariadb-11-8/)
- [Docker Compose Documentation](https://docs.docker.com/compose/)
