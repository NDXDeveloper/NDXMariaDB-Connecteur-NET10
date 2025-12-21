// ============================================================================
// NDXMariaDB - Exemples Event Scheduler
// ============================================================================
// Ce fichier contient des exemples d'utilisation de l'Event Scheduler
// de MariaDB/MySQL pour planifier des tâches automatiques.
//
// PRÉREQUIS: L'Event Scheduler doit être activé dans MariaDB:
//   SET GLOBAL event_scheduler = ON;
//   ou dans my.cnf: event_scheduler = ON
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
/// Exemples d'utilisation de l'Event Scheduler MariaDB/MySQL.
/// </summary>
public static class EventSchedulerExamples
{
    // ========================================================================
    // Vérification et activation de l'Event Scheduler
    // ========================================================================

    /// <summary>
    /// Vérifie si l'Event Scheduler est activé.
    /// </summary>
    public static async Task<bool> IsEventSchedulerEnabledAsync(IMariaDbConnection connection)
    {
        var status = await connection.ExecuteScalarAsync<string>("SELECT @@event_scheduler");
        var isEnabled = status == "ON" || status == "1";

        Console.WriteLine($"Event Scheduler: {(isEnabled ? "ACTIVÉ" : "DÉSACTIVÉ")}");
        return isEnabled;
    }

    /// <summary>
    /// Active l'Event Scheduler (nécessite les privilèges SUPER).
    /// </summary>
    public static async Task EnableEventSchedulerAsync(IMariaDbConnection connection)
    {
        await connection.ExecuteNonQueryAsync("SET GLOBAL event_scheduler = ON");
        Console.WriteLine("Event Scheduler activé");
    }

    // ========================================================================
    // Événements ponctuels (One-Time Events)
    // ========================================================================

    /// <summary>
    /// Crée un événement qui s'exécute une seule fois dans le futur.
    /// </summary>
    public static async Task CreateOneTimeEventAsync(IMariaDbConnection connection)
    {
        var eventName = "evt_cleanup_temp_files";

        // Supprimer si existe
        await connection.ExecuteNonQueryAsync($"DROP EVENT IF EXISTS {eventName}");

        // Créer l'événement - s'exécute dans 1 heure
        var sql = $@"
            CREATE EVENT {eventName}
            ON SCHEDULE AT CURRENT_TIMESTAMP + INTERVAL 1 HOUR
            COMMENT 'Nettoyage des fichiers temporaires'
            DO
            BEGIN
                DELETE FROM fichiers_temporaires
                WHERE date_creation < NOW() - INTERVAL 24 HOUR;

                INSERT INTO logs_systeme (action, message, date_execution)
                VALUES ('CLEANUP', 'Fichiers temporaires nettoyés', NOW());
            END";

        await connection.ExecuteNonQueryAsync(sql);
        Console.WriteLine($"Événement '{eventName}' créé - exécution dans 1 heure");
    }

    /// <summary>
    /// Crée un événement qui s'exécute à une date/heure précise.
    /// </summary>
    public static async Task CreateScheduledEventAsync(IMariaDbConnection connection, DateTime scheduledTime)
    {
        var eventName = "evt_rapport_mensuel";
        var formattedDate = scheduledTime.ToString("yyyy-MM-dd HH:mm:ss");

        await connection.ExecuteNonQueryAsync($"DROP EVENT IF EXISTS {eventName}");

        var sql = $@"
            CREATE EVENT {eventName}
            ON SCHEDULE AT '{formattedDate}'
            ON COMPLETION PRESERVE
            COMMENT 'Génération du rapport mensuel'
            DO
            BEGIN
                -- Générer le rapport
                INSERT INTO rapports (type, contenu, date_generation)
                SELECT
                    'MENSUEL',
                    JSON_OBJECT(
                        'total_ventes', SUM(montant_total),
                        'nb_commandes', COUNT(*),
                        'moyenne', AVG(montant_total)
                    ),
                    NOW()
                FROM commandes
                WHERE MONTH(date_commande) = MONTH(NOW() - INTERVAL 1 MONTH)
                  AND YEAR(date_commande) = YEAR(NOW() - INTERVAL 1 MONTH);
            END";

        await connection.ExecuteNonQueryAsync(sql);
        Console.WriteLine($"Événement '{eventName}' planifié pour {formattedDate}");
    }

    // ========================================================================
    // Événements récurrents (Recurring Events)
    // ========================================================================

    /// <summary>
    /// Crée un événement qui s'exécute toutes les minutes.
    /// </summary>
    public static async Task CreateMinutelyEventAsync(IMariaDbConnection connection)
    {
        var eventName = "evt_heartbeat";

        await connection.ExecuteNonQueryAsync($"DROP EVENT IF EXISTS {eventName}");

        var sql = $@"
            CREATE EVENT {eventName}
            ON SCHEDULE EVERY 1 MINUTE
            COMMENT 'Mise à jour du heartbeat système'
            DO
            BEGIN
                UPDATE parametres_systeme
                SET valeur = NOW()
                WHERE cle = 'last_heartbeat';
            END";

        await connection.ExecuteNonQueryAsync(sql);
        Console.WriteLine($"Événement '{eventName}' créé - exécution chaque minute");
    }

    /// <summary>
    /// Crée un événement horaire pour le nettoyage des sessions expirées.
    /// </summary>
    public static async Task CreateHourlyCleanupEventAsync(IMariaDbConnection connection)
    {
        var eventName = "evt_cleanup_sessions";

        await connection.ExecuteNonQueryAsync($"DROP EVENT IF EXISTS {eventName}");

        var sql = $@"
            CREATE EVENT {eventName}
            ON SCHEDULE EVERY 1 HOUR
            STARTS CURRENT_TIMESTAMP
            COMMENT 'Nettoyage des sessions expirées'
            DO
            BEGIN
                DECLARE v_deleted INT DEFAULT 0;

                DELETE FROM sessions
                WHERE date_expiration < NOW();

                SET v_deleted = ROW_COUNT();

                IF v_deleted > 0 THEN
                    INSERT INTO logs_systeme (action, message, date_execution)
                    VALUES ('SESSION_CLEANUP', CONCAT(v_deleted, ' sessions expirées supprimées'), NOW());
                END IF;
            END";

        await connection.ExecuteNonQueryAsync(sql);
        Console.WriteLine($"Événement '{eventName}' créé - exécution chaque heure");
    }

    /// <summary>
    /// Crée un événement quotidien avec heure de démarrage spécifique.
    /// </summary>
    public static async Task CreateDailyReportEventAsync(IMariaDbConnection connection)
    {
        var eventName = "evt_daily_report";

        await connection.ExecuteNonQueryAsync($"DROP EVENT IF EXISTS {eventName}");

        // Exécution chaque jour à 2h00 du matin
        var sql = $@"
            CREATE EVENT {eventName}
            ON SCHEDULE EVERY 1 DAY
            STARTS (CURRENT_DATE + INTERVAL 1 DAY + INTERVAL 2 HOUR)
            COMMENT 'Rapport quotidien des ventes'
            DO
            BEGIN
                INSERT INTO rapports_quotidiens (
                    date_rapport,
                    nb_commandes,
                    total_ventes,
                    nb_nouveaux_clients,
                    produit_plus_vendu
                )
                SELECT
                    CURRENT_DATE - INTERVAL 1 DAY,
                    COUNT(DISTINCT c.id),
                    COALESCE(SUM(c.montant_total), 0),
                    (SELECT COUNT(*) FROM clients WHERE DATE(date_inscription) = CURRENT_DATE - INTERVAL 1 DAY),
                    (SELECT p.nom FROM produits p
                     JOIN lignes_commande lc ON p.id = lc.produit_id
                     JOIN commandes cmd ON lc.commande_id = cmd.id
                     WHERE DATE(cmd.date_commande) = CURRENT_DATE - INTERVAL 1 DAY
                     GROUP BY p.id ORDER BY SUM(lc.quantite) DESC LIMIT 1)
                FROM commandes c
                WHERE DATE(c.date_commande) = CURRENT_DATE - INTERVAL 1 DAY;
            END";

        await connection.ExecuteNonQueryAsync(sql);
        Console.WriteLine($"Événement '{eventName}' créé - exécution chaque jour à 2h00");
    }

    /// <summary>
    /// Crée un événement hebdomadaire.
    /// </summary>
    public static async Task CreateWeeklyMaintenanceEventAsync(IMariaDbConnection connection)
    {
        var eventName = "evt_weekly_maintenance";

        await connection.ExecuteNonQueryAsync($"DROP EVENT IF EXISTS {eventName}");

        // Exécution chaque dimanche à 3h00
        var sql = $@"
            CREATE EVENT {eventName}
            ON SCHEDULE EVERY 1 WEEK
            STARTS (CURRENT_DATE + INTERVAL (6 - WEEKDAY(CURRENT_DATE)) DAY + INTERVAL 3 HOUR)
            COMMENT 'Maintenance hebdomadaire'
            DO
            BEGIN
                -- Optimiser les tables principales
                OPTIMIZE TABLE commandes, clients, produits;

                -- Archiver les anciennes commandes
                INSERT INTO commandes_archive
                SELECT * FROM commandes
                WHERE date_commande < NOW() - INTERVAL 1 YEAR
                  AND archivee = FALSE;

                UPDATE commandes SET archivee = TRUE
                WHERE date_commande < NOW() - INTERVAL 1 YEAR;

                -- Mettre à jour les statistiques
                ANALYZE TABLE commandes, clients, produits;

                -- Logger l'opération
                INSERT INTO logs_systeme (action, message, date_execution)
                VALUES ('WEEKLY_MAINTENANCE', 'Maintenance hebdomadaire terminée', NOW());
            END";

        await connection.ExecuteNonQueryAsync(sql);
        Console.WriteLine($"Événement '{eventName}' créé - exécution chaque dimanche à 3h00");
    }

    /// <summary>
    /// Crée un événement mensuel pour la facturation.
    /// </summary>
    public static async Task CreateMonthlyBillingEventAsync(IMariaDbConnection connection)
    {
        var eventName = "evt_monthly_billing";

        await connection.ExecuteNonQueryAsync($"DROP EVENT IF EXISTS {eventName}");

        // Exécution le 1er de chaque mois à 1h00
        var sql = $@"
            CREATE EVENT {eventName}
            ON SCHEDULE EVERY 1 MONTH
            STARTS (DATE_FORMAT(CURRENT_DATE + INTERVAL 1 MONTH, '%Y-%m-01') + INTERVAL 1 HOUR)
            COMMENT 'Génération des factures mensuelles'
            DO
            BEGIN
                DECLARE v_client_id INT;
                DECLARE v_done INT DEFAULT FALSE;
                DECLARE cur_clients CURSOR FOR
                    SELECT DISTINCT client_id FROM abonnements WHERE actif = TRUE;
                DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_done = TRUE;

                OPEN cur_clients;

                read_loop: LOOP
                    FETCH cur_clients INTO v_client_id;
                    IF v_done THEN
                        LEAVE read_loop;
                    END IF;

                    -- Générer la facture pour ce client
                    INSERT INTO factures (client_id, periode, montant, date_creation, statut)
                    SELECT
                        v_client_id,
                        DATE_FORMAT(NOW() - INTERVAL 1 MONTH, '%Y-%m'),
                        SUM(a.prix_mensuel),
                        NOW(),
                        'PENDING'
                    FROM abonnements a
                    WHERE a.client_id = v_client_id AND a.actif = TRUE;
                END LOOP;

                CLOSE cur_clients;

                INSERT INTO logs_systeme (action, message, date_execution)
                VALUES ('MONTHLY_BILLING', 'Facturation mensuelle générée', NOW());
            END";

        await connection.ExecuteNonQueryAsync(sql);
        Console.WriteLine($"Événement '{eventName}' créé - exécution le 1er de chaque mois à 1h00");
    }

    // ========================================================================
    // Événements avec dates de fin
    // ========================================================================

    /// <summary>
    /// Crée un événement avec date de fin.
    /// </summary>
    public static async Task CreateLimitedEventAsync(IMariaDbConnection connection)
    {
        var eventName = "evt_promo_noel";

        await connection.ExecuteNonQueryAsync($"DROP EVENT IF EXISTS {eventName}");

        // Événement actif du 1er au 31 décembre
        var sql = $@"
            CREATE EVENT {eventName}
            ON SCHEDULE EVERY 1 HOUR
            STARTS '2025-12-01 00:00:00'
            ENDS '2025-12-31 23:59:59'
            COMMENT 'Mise à jour des promotions de Noël'
            DO
            BEGIN
                -- Appliquer les réductions de Noël
                UPDATE produits
                SET prix_promo = prix * 0.80
                WHERE categorie = 'Jouets' AND promotion_noel = TRUE;
            END";

        await connection.ExecuteNonQueryAsync(sql);
        Console.WriteLine($"Événement '{eventName}' créé - actif du 1er au 31 décembre");
    }

    // ========================================================================
    // Gestion des événements
    // ========================================================================

    /// <summary>
    /// Liste tous les événements de la base de données.
    /// </summary>
    public static async Task<DataTable> ListAllEventsAsync(IMariaDbConnection connection)
    {
        var sql = @"
            SELECT
                EVENT_NAME AS nom,
                EVENT_TYPE AS type,
                EXECUTE_AT AS execution_unique,
                INTERVAL_VALUE AS intervalle_valeur,
                INTERVAL_FIELD AS intervalle_unite,
                STARTS AS debut,
                ENDS AS fin,
                STATUS AS statut,
                LAST_EXECUTED AS derniere_execution,
                EVENT_COMMENT AS commentaire
            FROM information_schema.EVENTS
            WHERE EVENT_SCHEMA = DATABASE()
            ORDER BY EVENT_NAME";

        var result = await connection.ExecuteQueryAsync(sql);

        Console.WriteLine($"\n=== Événements planifiés ({result.Rows.Count}) ===");
        foreach (DataRow row in result.Rows)
        {
            var type = row["type"].ToString();
            var status = row["statut"].ToString();

            Console.WriteLine($"\n{row["nom"]} [{status}]");
            Console.WriteLine($"  Type: {type}");

            if (type == "ONE TIME")
            {
                Console.WriteLine($"  Exécution: {row["execution_unique"]}");
            }
            else
            {
                Console.WriteLine($"  Intervalle: {row["intervalle_valeur"]} {row["intervalle_unite"]}");
                if (row["debut"] != DBNull.Value)
                    Console.WriteLine($"  Début: {row["debut"]}");
                if (row["fin"] != DBNull.Value)
                    Console.WriteLine($"  Fin: {row["fin"]}");
            }

            if (row["derniere_execution"] != DBNull.Value)
                Console.WriteLine($"  Dernière exécution: {row["derniere_execution"]}");

            if (row["commentaire"] != DBNull.Value && !string.IsNullOrEmpty(row["commentaire"].ToString()))
                Console.WriteLine($"  Commentaire: {row["commentaire"]}");
        }

        return result;
    }

    /// <summary>
    /// Active un événement.
    /// </summary>
    public static async Task EnableEventAsync(IMariaDbConnection connection, string eventName)
    {
        await connection.ExecuteNonQueryAsync($"ALTER EVENT {eventName} ENABLE");
        Console.WriteLine($"Événement '{eventName}' activé");
    }

    /// <summary>
    /// Désactive un événement.
    /// </summary>
    public static async Task DisableEventAsync(IMariaDbConnection connection, string eventName)
    {
        await connection.ExecuteNonQueryAsync($"ALTER EVENT {eventName} DISABLE");
        Console.WriteLine($"Événement '{eventName}' désactivé");
    }

    /// <summary>
    /// Modifie l'intervalle d'un événement récurrent.
    /// </summary>
    public static async Task ChangeEventIntervalAsync(
        IMariaDbConnection connection,
        string eventName,
        int intervalValue,
        string intervalUnit)
    {
        var sql = $@"
            ALTER EVENT {eventName}
            ON SCHEDULE EVERY {intervalValue} {intervalUnit}";

        await connection.ExecuteNonQueryAsync(sql);
        Console.WriteLine($"Événement '{eventName}' modifié - nouvel intervalle: {intervalValue} {intervalUnit}");
    }

    /// <summary>
    /// Renomme un événement.
    /// </summary>
    public static async Task RenameEventAsync(
        IMariaDbConnection connection,
        string oldName,
        string newName)
    {
        await connection.ExecuteNonQueryAsync($"ALTER EVENT {oldName} RENAME TO {newName}");
        Console.WriteLine($"Événement renommé: '{oldName}' -> '{newName}'");
    }

    /// <summary>
    /// Supprime un événement.
    /// </summary>
    public static async Task DropEventAsync(IMariaDbConnection connection, string eventName)
    {
        await connection.ExecuteNonQueryAsync($"DROP EVENT IF EXISTS {eventName}");
        Console.WriteLine($"Événement '{eventName}' supprimé");
    }

    // ========================================================================
    // Événements avec procédures stockées
    // ========================================================================

    /// <summary>
    /// Crée un événement qui appelle une procédure stockée.
    /// </summary>
    public static async Task CreateEventWithProcedureAsync(IMariaDbConnection connection)
    {
        var procName = "sp_maintenance_auto";
        var eventName = "evt_maintenance_auto";

        // Créer la procédure
        await connection.ExecuteNonQueryAsync($"DROP PROCEDURE IF EXISTS {procName}");
        var procSql = $@"
            CREATE PROCEDURE {procName}()
            BEGIN
                -- Supprimer les logs anciens
                DELETE FROM logs_systeme WHERE date_execution < NOW() - INTERVAL 30 DAY;

                -- Mettre à jour les statistiques des clients
                UPDATE clients c
                SET
                    nb_commandes = (SELECT COUNT(*) FROM commandes WHERE client_id = c.id),
                    total_achats = (SELECT COALESCE(SUM(montant_total), 0) FROM commandes WHERE client_id = c.id)
                WHERE c.actif = TRUE;

                -- Logger l'opération
                INSERT INTO logs_systeme (action, message, date_execution)
                VALUES ('AUTO_MAINTENANCE', 'Maintenance automatique exécutée', NOW());
            END";
        await connection.ExecuteNonQueryAsync(procSql);

        // Créer l'événement
        await connection.ExecuteNonQueryAsync($"DROP EVENT IF EXISTS {eventName}");
        var eventSql = $@"
            CREATE EVENT {eventName}
            ON SCHEDULE EVERY 6 HOUR
            COMMENT 'Appel de la procédure de maintenance automatique'
            DO CALL {procName}()";

        await connection.ExecuteNonQueryAsync(eventSql);
        Console.WriteLine($"Événement '{eventName}' créé - appelle {procName}() toutes les 6 heures");
    }

    // ========================================================================
    // Exemple complet
    // ========================================================================

    /// <summary>
    /// Exemple complet d'utilisation de l'Event Scheduler.
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

        // 1. Vérifier que l'Event Scheduler est activé
        if (!await IsEventSchedulerEnabledAsync(connection))
        {
            Console.WriteLine("ATTENTION: L'Event Scheduler est désactivé!");
            Console.WriteLine("Exécutez: SET GLOBAL event_scheduler = ON;");
            return;
        }

        // 2. Créer différents types d'événements
        await CreateHourlyCleanupEventAsync(connection);
        await CreateDailyReportEventAsync(connection);
        await CreateWeeklyMaintenanceEventAsync(connection);
        await CreateEventWithProcedureAsync(connection);

        // 3. Lister tous les événements
        await ListAllEventsAsync(connection);

        // 4. Gérer les événements
        await DisableEventAsync(connection, "evt_weekly_maintenance");
        await EnableEventAsync(connection, "evt_weekly_maintenance");
        await ChangeEventIntervalAsync(connection, "evt_cleanup_sessions", 2, "HOUR");

        // 5. Nettoyer (optionnel)
        // await DropEventAsync(connection, "evt_cleanup_sessions");
    }
}
