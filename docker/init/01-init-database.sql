-- ============================================================================
-- NDXMariaDB - Script d'initialisation de la base de données
-- ============================================================================
-- Ce script est exécuté automatiquement au premier démarrage du conteneur.
-- Il crée les tables et données de test nécessaires.
--
-- Auteur: Nicolas DEOUX <NDXDev@gmail.com>
-- ============================================================================

-- Utilisation de la base de données de test
USE ndxmariadb_test;

-- ============================================================================
-- Table de test simple pour les tests unitaires
-- ============================================================================
CREATE TABLE IF NOT EXISTS test_table (
    id INT PRIMARY KEY AUTO_INCREMENT,
    name VARCHAR(100) NOT NULL,
    value DECIMAL(10, 2) DEFAULT 0.00,
    is_active BOOLEAN DEFAULT TRUE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    INDEX idx_name (name),
    INDEX idx_created_at (created_at)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ============================================================================
-- Données de test initiales
-- ============================================================================
INSERT INTO test_table (name, value, is_active) VALUES
    ('Test Alpha', 100.50, TRUE),
    ('Test Beta', 200.75, TRUE),
    ('Test Gamma', 300.00, FALSE),
    ('Test Delta', 450.25, TRUE);

-- ============================================================================
-- Table pour tests de transactions
-- ============================================================================
CREATE TABLE IF NOT EXISTS transaction_test (
    id INT PRIMARY KEY AUTO_INCREMENT,
    operation VARCHAR(50) NOT NULL,
    amount DECIMAL(10, 2) NOT NULL,
    status ENUM('pending', 'completed', 'cancelled') DEFAULT 'pending',
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ============================================================================
-- Procédure stockée de test
-- ============================================================================
DELIMITER //

CREATE PROCEDURE IF NOT EXISTS sp_get_active_items()
BEGIN
    SELECT id, name, value, created_at
    FROM test_table
    WHERE is_active = TRUE
    ORDER BY name;
END //

CREATE PROCEDURE IF NOT EXISTS sp_add_item(
    IN p_name VARCHAR(100),
    IN p_value DECIMAL(10, 2),
    OUT p_id INT
)
BEGIN
    INSERT INTO test_table (name, value) VALUES (p_name, p_value);
    SET p_id = LAST_INSERT_ID();
END //

DELIMITER ;

-- ============================================================================
-- Vue de test
-- ============================================================================
CREATE OR REPLACE VIEW v_active_items AS
SELECT id, name, value, created_at
FROM test_table
WHERE is_active = TRUE;

-- ============================================================================
-- Affichage de confirmation
-- ============================================================================
SELECT 'Base de données NDXMariaDB initialisée avec succès!' AS message;
SELECT COUNT(*) AS total_test_items FROM test_table;
