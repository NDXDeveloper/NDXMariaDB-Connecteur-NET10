namespace NDXMariaDB;

/// <summary>
/// Options de configuration pour la connexion MariaDB.
/// </summary>
public sealed class MariaDbConnectionOptions
{
    /// <summary>
    /// Serveur de base de données (hostname ou IP).
    /// </summary>
    public string Server { get; set; } = "localhost";

    /// <summary>
    /// Port de connexion (par défaut 3306).
    /// </summary>
    public int Port { get; set; } = 3306;

    /// <summary>
    /// Nom de la base de données.
    /// </summary>
    public string Database { get; set; } = string.Empty;

    /// <summary>
    /// Nom d'utilisateur pour la connexion.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Mot de passe pour la connexion.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Chaîne de connexion complète (optionnelle, surcharge les autres propriétés si définie).
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Indique si c'est une connexion principale (ne sera pas fermée automatiquement).
    /// </summary>
    public bool IsPrimaryConnection { get; set; }

    /// <summary>
    /// Délai d'inactivité avant fermeture automatique en millisecondes (par défaut 60000ms = 1 minute).
    /// </summary>
    public int AutoCloseTimeoutMs { get; set; } = 60_000;

    /// <summary>
    /// Désactive la fermeture automatique de la connexion.
    /// </summary>
    public bool DisableAutoClose { get; set; }

    /// <summary>
    /// Active le pooling de connexions (par défaut true).
    /// </summary>
    public bool Pooling { get; set; } = true;

    /// <summary>
    /// Taille minimale du pool de connexions.
    /// </summary>
    public int MinPoolSize { get; set; } = 0;

    /// <summary>
    /// Taille maximale du pool de connexions.
    /// </summary>
    public int MaxPoolSize { get; set; } = 100;

    /// <summary>
    /// Timeout de connexion en secondes.
    /// </summary>
    public int ConnectionTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Timeout de commande en secondes.
    /// </summary>
    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Timeout pour le verrou InnoDB en secondes.
    /// </summary>
    public int InnoDbLockWaitTimeout { get; set; } = 120;

    /// <summary>
    /// Active les opérations SSL.
    /// </summary>
    public bool UseSsl { get; set; }

    /// <summary>
    /// Mode SSL (None, Preferred, Required, VerifyCA, VerifyFull).
    /// </summary>
    public string SslMode { get; set; } = "Preferred";

    /// <summary>
    /// Autorise les variables utilisateur MySQL (@variable) dans les requêtes.
    /// Nécessaire pour les procédures stockées avec paramètres OUT/INOUT.
    /// Par défaut true.
    /// </summary>
    public bool AllowUserVariables { get; set; } = true;

    /// <summary>
    /// Génère la chaîne de connexion à partir des options.
    /// </summary>
    public string BuildConnectionString()
    {
        if (!string.IsNullOrWhiteSpace(ConnectionString))
        {
            // Si une chaîne de connexion est fournie, ajouter AllowUserVariables si non présent
            if (AllowUserVariables && !ConnectionString.Contains("AllowUserVariables", StringComparison.OrdinalIgnoreCase))
            {
                return ConnectionString.TrimEnd(';') + ";AllowUserVariables=true";
            }
            return ConnectionString;
        }

        var builder = new MySqlConnector.MySqlConnectionStringBuilder
        {
            Server = Server,
            Port = (uint)Port,
            Database = Database,
            UserID = Username,
            Password = Password,
            Pooling = Pooling,
            MinimumPoolSize = (uint)MinPoolSize,
            MaximumPoolSize = (uint)MaxPoolSize,
            ConnectionTimeout = (uint)ConnectionTimeoutSeconds,
            DefaultCommandTimeout = (uint)CommandTimeoutSeconds,
            AllowUserVariables = AllowUserVariables
        };

        if (UseSsl)
        {
            builder.SslMode = Enum.Parse<MySqlConnector.MySqlSslMode>(SslMode, ignoreCase: true);
        }

        return builder.ConnectionString;
    }
}
