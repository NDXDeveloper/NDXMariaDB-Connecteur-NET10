# ============================================================================
# NDXMariaDB - Dockerfile pour MariaDB 11.8 LTS
# ============================================================================
# Ce Dockerfile crée une image MariaDB personnalisée pour les tests
# de développement de la bibliothèque NDXMariaDB.
#
# Auteur: Nicolas DEOUX <NDXDev@gmail.com>
# Version: 1.0.0
# ============================================================================

FROM mariadb:11.8

# Métadonnées de l'image
LABEL maintainer="Nicolas DEOUX <NDXDev@gmail.com>"
LABEL description="MariaDB 11.8 LTS pour tests NDXMariaDB"
LABEL version="1.0.0"

# Variables d'environnement par défaut
ENV MARIADB_ROOT_PASSWORD=rootpassword
ENV MARIADB_DATABASE=ndxmariadb_test
ENV MARIADB_USER=testuser
ENV MARIADB_PASSWORD=testpassword

# Configuration MariaDB optimisée pour les tests
ENV MARIADB_AUTO_UPGRADE=1
ENV MARIADB_INITDB_SKIP_TZINFO=1

# Copier les scripts d'initialisation (optionnel)
COPY init/ /docker-entrypoint-initdb.d/

# Port exposé
EXPOSE 3306

# Volume pour la persistance des données
VOLUME ["/var/lib/mysql"]

# Healthcheck pour vérifier que MariaDB est prêt
HEALTHCHECK --interval=10s --timeout=5s --start-period=30s --retries=5 \
    CMD healthcheck.sh --connect --innodb_initialized || exit 1
