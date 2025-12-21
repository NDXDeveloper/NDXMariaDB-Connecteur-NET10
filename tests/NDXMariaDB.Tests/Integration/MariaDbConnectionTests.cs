using System.Data;
using FluentAssertions;
using NDXMariaDB.Tests.Fixtures;
using Xunit;

namespace NDXMariaDB.Tests.Integration;

/// <summary>
/// Tests d'intégration pour MariaDbConnection.
/// Utilise Testcontainers pour lancer un conteneur MariaDB réel.
/// </summary>
[Collection("MariaDB")]
public class MariaDbConnectionTests
{
    private readonly MariaDbFixture _fixture;

    public MariaDbConnectionTests(MariaDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task OpenAsync_ShouldOpenConnection()
    {
        // Arrange
        await using var connection = _fixture.CreateConnection();

        // Act
        await connection.OpenAsync();

        // Assert
        connection.State.Should().Be(ConnectionState.Open);
    }

    [Fact]
    public async Task CloseAsync_ShouldCloseConnection()
    {
        // Arrange
        await using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();

        // Act
        await connection.CloseAsync();

        // Assert
        connection.State.Should().Be(ConnectionState.Closed);
    }

    [Fact]
    public async Task ExecuteScalarAsync_ShouldReturnValue()
    {
        // Arrange
        await using var connection = _fixture.CreateConnection();

        // Act
        var result = await connection.ExecuteScalarAsync<int>("SELECT 1 + 1");

        // Assert
        result.Should().Be(2);
    }

    [Fact]
    public async Task ExecuteNonQueryAsync_CreateTable_ShouldSucceed()
    {
        // Arrange
        await using var connection = _fixture.CreateConnection();
        var tableName = $"test_table_{Guid.NewGuid():N}";

        try
        {
            // Act
            var result = await connection.ExecuteNonQueryAsync(
                $"CREATE TABLE {tableName} (id INT PRIMARY KEY AUTO_INCREMENT, name VARCHAR(100))");

            // Assert
            result.Should().Be(0); // CREATE TABLE returns 0 affected rows
        }
        finally
        {
            // Cleanup
            await connection.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS {tableName}");
        }
    }

    [Fact]
    public async Task ExecuteNonQueryAsync_InsertData_ShouldReturnAffectedRows()
    {
        // Arrange
        await using var connection = _fixture.CreateConnection();
        var tableName = $"test_insert_{Guid.NewGuid():N}";

        try
        {
            await connection.ExecuteNonQueryAsync(
                $"CREATE TABLE {tableName} (id INT PRIMARY KEY AUTO_INCREMENT, name VARCHAR(100))");

            // Act
            var result = await connection.ExecuteNonQueryAsync(
                $"INSERT INTO {tableName} (name) VALUES (@name)",
                new { name = "Test" });

            // Assert
            result.Should().Be(1);
        }
        finally
        {
            await connection.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS {tableName}");
        }
    }

    [Fact]
    public async Task ExecuteQueryAsync_ShouldReturnDataTable()
    {
        // Arrange
        await using var connection = _fixture.CreateConnection();
        var tableName = $"test_query_{Guid.NewGuid():N}";

        try
        {
            await connection.ExecuteNonQueryAsync(
                $"CREATE TABLE {tableName} (id INT PRIMARY KEY AUTO_INCREMENT, name VARCHAR(100))");
            await connection.ExecuteNonQueryAsync(
                $"INSERT INTO {tableName} (name) VALUES ('Alice'), ('Bob'), ('Charlie')");

            // Act
            var result = await connection.ExecuteQueryAsync($"SELECT * FROM {tableName} ORDER BY id");

            // Assert
            result.Should().NotBeNull();
            result.Rows.Count.Should().Be(3);
            result.Rows[0]["name"].Should().Be("Alice");
            result.Rows[1]["name"].Should().Be("Bob");
            result.Rows[2]["name"].Should().Be("Charlie");
        }
        finally
        {
            await connection.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS {tableName}");
        }
    }

    [Fact]
    public async Task ExecuteReaderAsync_ShouldReturnReader()
    {
        // Arrange
        await using var connection = _fixture.CreateConnection();

        // Act
        await using var reader = await connection.ExecuteReaderAsync("SELECT 1 as num, 'test' as str");

        // Assert
        reader.Should().NotBeNull();
        (await reader.ReadAsync()).Should().BeTrue();
        reader.GetInt32(0).Should().Be(1);
        reader.GetString(1).Should().Be("test");
    }

    [Fact]
    public async Task Transaction_CommitAsync_ShouldPersistChanges()
    {
        // Arrange
        await using var connection = _fixture.CreateConnection();
        var tableName = $"test_tx_commit_{Guid.NewGuid():N}";

        try
        {
            await connection.ExecuteNonQueryAsync(
                $"CREATE TABLE {tableName} (id INT PRIMARY KEY AUTO_INCREMENT, value INT)");

            // Act
            await connection.BeginTransactionAsync();
            await connection.ExecuteNonQueryAsync($"INSERT INTO {tableName} (value) VALUES (100)");
            await connection.CommitAsync();

            // Assert
            var count = await connection.ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM {tableName}");
            count.Should().Be(1);
        }
        finally
        {
            await connection.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS {tableName}");
        }
    }

    [Fact]
    public async Task Transaction_RollbackAsync_ShouldRevertChanges()
    {
        // Arrange
        await using var connection = _fixture.CreateConnection();
        var tableName = $"test_tx_rollback_{Guid.NewGuid():N}";

        try
        {
            await connection.ExecuteNonQueryAsync(
                $"CREATE TABLE {tableName} (id INT PRIMARY KEY AUTO_INCREMENT, value INT)");

            // Insérer une ligne en dehors de la transaction
            await connection.ExecuteNonQueryAsync($"INSERT INTO {tableName} (value) VALUES (50)");

            // Act
            await connection.BeginTransactionAsync();
            await connection.ExecuteNonQueryAsync($"INSERT INTO {tableName} (value) VALUES (100)");
            connection.IsTransactionActive.Should().BeTrue();
            await connection.RollbackAsync();

            // Assert
            connection.IsTransactionActive.Should().BeFalse();
            var count = await connection.ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM {tableName}");
            count.Should().Be(1); // Seulement la première ligne (hors transaction)
        }
        finally
        {
            await connection.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS {tableName}");
        }
    }

    [Fact]
    public async Task BeginTransactionAsync_ShouldSetIsTransactionActive()
    {
        // Arrange
        await using var connection = _fixture.CreateConnection();

        // Act
        var result = await connection.BeginTransactionAsync();

        // Assert
        result.Should().BeTrue();
        connection.IsTransactionActive.Should().BeTrue();
        connection.Transaction.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateCommand_ShouldCreateValidCommand()
    {
        // Arrange
        await using var connection = _fixture.CreateConnection();
        await connection.OpenAsync();

        // Act
        using var command = connection.CreateCommand("SELECT 1");

        // Assert
        command.Should().NotBeNull();
        command.CommandText.Should().Be("SELECT 1");
        command.Connection.Should().Be(connection.Connection);
    }

    [Fact]
    public async Task ActionHistory_ShouldTrackActions()
    {
        // Arrange
        await using var connection = _fixture.CreateConnection();

        // Act
        await connection.OpenAsync();
        await connection.CloseAsync();

        // Assert
        connection.LastAction.Should().Contain("CloseAsync");
        connection.ActionHistory.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public async Task Connection_ShouldHaveUniqueId()
    {
        // Arrange
        await using var connection1 = _fixture.CreateConnection();
        await using var connection2 = _fixture.CreateConnection();

        // Assert
        connection1.Id.Should().NotBe(connection2.Id);
    }

    [Fact]
    public async Task CreatedAt_ShouldBeSetOnConstruction()
    {
        // Arrange
        var before = DateTime.UtcNow;
        await using var connection = _fixture.CreateConnection();
        var after = DateTime.UtcNow;

        // Assert
        connection.CreatedAt.Should().BeOnOrAfter(before);
        connection.CreatedAt.Should().BeOnOrBefore(after);
    }

    [Fact]
    public async Task DisposeAsync_ShouldCloseConnection()
    {
        // Arrange
        var connection = _fixture.CreateConnection();
        await connection.OpenAsync();

        // Act
        await connection.DisposeAsync();

        // Assert
        connection.State.Should().Be(ConnectionState.Closed);
    }

    [Fact]
    public async Task ExecuteNonQueryAsync_UpdateData_ShouldReturnAffectedRows()
    {
        // Arrange
        await using var connection = _fixture.CreateConnection();
        var tableName = $"test_update_{Guid.NewGuid():N}";

        try
        {
            await connection.ExecuteNonQueryAsync(
                $"CREATE TABLE {tableName} (id INT PRIMARY KEY AUTO_INCREMENT, name VARCHAR(100), status VARCHAR(20))");
            await connection.ExecuteNonQueryAsync(
                $"INSERT INTO {tableName} (name, status) VALUES ('Alice', 'inactive'), ('Bob', 'inactive'), ('Charlie', 'active')");

            // Act - UPDATE avec paramètres
            var result = await connection.ExecuteNonQueryAsync(
                $"UPDATE {tableName} SET status = @newStatus WHERE status = @oldStatus",
                new { newStatus = "active", oldStatus = "inactive" });

            // Assert
            result.Should().Be(2); // 2 lignes mises à jour (Alice et Bob)

            // Vérifier que les données ont bien été modifiées
            var activeCount = await connection.ExecuteScalarAsync<long>(
                $"SELECT COUNT(*) FROM {tableName} WHERE status = 'active'");
            activeCount.Should().Be(3);
        }
        finally
        {
            await connection.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS {tableName}");
        }
    }

    [Fact]
    public async Task ExecuteNonQueryAsync_DeleteData_ShouldReturnAffectedRows()
    {
        // Arrange
        await using var connection = _fixture.CreateConnection();
        var tableName = $"test_delete_{Guid.NewGuid():N}";

        try
        {
            await connection.ExecuteNonQueryAsync(
                $"CREATE TABLE {tableName} (id INT PRIMARY KEY AUTO_INCREMENT, name VARCHAR(100), is_deleted BOOLEAN DEFAULT FALSE)");
            await connection.ExecuteNonQueryAsync(
                $"INSERT INTO {tableName} (name, is_deleted) VALUES ('Keep1', FALSE), ('Delete1', TRUE), ('Delete2', TRUE), ('Keep2', FALSE)");

            // Act - DELETE avec paramètre
            var result = await connection.ExecuteNonQueryAsync(
                $"DELETE FROM {tableName} WHERE is_deleted = @isDeleted",
                new { isDeleted = true });

            // Assert
            result.Should().Be(2); // 2 lignes supprimées

            // Vérifier qu'il reste les bonnes lignes
            var remainingCount = await connection.ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM {tableName}");
            remainingCount.Should().Be(2);
        }
        finally
        {
            await connection.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS {tableName}");
        }
    }

    [Fact]
    public async Task ExecuteNonQueryAsync_InsertWithMultipleParameters_ShouldWork()
    {
        // Arrange
        await using var connection = _fixture.CreateConnection();
        var tableName = $"test_params_{Guid.NewGuid():N}";

        try
        {
            await connection.ExecuteNonQueryAsync(
                $"CREATE TABLE {tableName} (id INT PRIMARY KEY AUTO_INCREMENT, name VARCHAR(100), age INT, email VARCHAR(200), salary DECIMAL(10,2))");

            // Act - INSERT avec plusieurs paramètres de types différents
            var result = await connection.ExecuteNonQueryAsync(
                $"INSERT INTO {tableName} (name, age, email, salary) VALUES (@name, @age, @email, @salary)",
                new { name = "Jean Dupont", age = 35, email = "jean@example.com", salary = 45000.50m });

            // Assert
            result.Should().Be(1);

            // Vérifier les données insérées
            var data = await connection.ExecuteQueryAsync($"SELECT * FROM {tableName} WHERE name = @name", new { name = "Jean Dupont" });
            data.Rows.Count.Should().Be(1);
            data.Rows[0]["age"].Should().Be(35);
            data.Rows[0]["email"].Should().Be("jean@example.com");
            Convert.ToDecimal(data.Rows[0]["salary"]).Should().Be(45000.50m);
        }
        finally
        {
            await connection.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS {tableName}");
        }
    }

    [Fact]
    public async Task ExecuteStoredProcedure_CallSimpleProcedure_ShouldWork()
    {
        // Arrange
        await using var connection = _fixture.CreateConnection();
        var tableName = $"test_sp_{Guid.NewGuid():N}";
        var procName = $"sp_get_users_{Guid.NewGuid():N}".Replace("-", "_");

        try
        {
            // Créer la table et la procédure stockée
            await connection.ExecuteNonQueryAsync(
                $"CREATE TABLE {tableName} (id INT PRIMARY KEY AUTO_INCREMENT, name VARCHAR(100), is_active BOOLEAN)");
            await connection.ExecuteNonQueryAsync(
                $"INSERT INTO {tableName} (name, is_active) VALUES ('Alice', TRUE), ('Bob', FALSE), ('Charlie', TRUE)");

            await connection.ExecuteNonQueryAsync(
                $"CREATE PROCEDURE {procName}() BEGIN SELECT id, name FROM {tableName} WHERE is_active = TRUE ORDER BY name; END");

            // Act - Appeler la procédure stockée
            var result = await connection.ExecuteQueryAsync($"CALL {procName}()");

            // Assert
            result.Should().NotBeNull();
            result.Rows.Count.Should().Be(2); // Alice et Charlie
            result.Rows[0]["name"].Should().Be("Alice");
            result.Rows[1]["name"].Should().Be("Charlie");
        }
        finally
        {
            await connection.ExecuteNonQueryAsync($"DROP PROCEDURE IF EXISTS {procName}");
            await connection.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS {tableName}");
        }
    }

    [Fact]
    public async Task ExecuteStoredProcedure_WithInputParameters_ShouldWork()
    {
        // Arrange
        await using var connection = _fixture.CreateConnection();
        var tableName = $"test_sp_params_{Guid.NewGuid():N}";
        var procName = $"sp_filter_by_status_{Guid.NewGuid():N}".Replace("-", "_");

        try
        {
            // Créer la table et la procédure stockée avec paramètre
            await connection.ExecuteNonQueryAsync(
                $"CREATE TABLE {tableName} (id INT PRIMARY KEY AUTO_INCREMENT, name VARCHAR(100), status VARCHAR(20))");
            await connection.ExecuteNonQueryAsync(
                $"INSERT INTO {tableName} (name, status) VALUES ('Alice', 'active'), ('Bob', 'pending'), ('Charlie', 'active'), ('David', 'inactive')");

            await connection.ExecuteNonQueryAsync(
                $"CREATE PROCEDURE {procName}(IN p_status VARCHAR(20)) BEGIN SELECT id, name, status FROM {tableName} WHERE status = p_status ORDER BY name; END");

            // Act - Appeler la procédure stockée avec paramètre
            var result = await connection.ExecuteQueryAsync($"CALL {procName}(@status)", new { status = "active" });

            // Assert
            result.Should().NotBeNull();
            result.Rows.Count.Should().Be(2); // Alice et Charlie (status = 'active')
            result.Rows[0]["name"].Should().Be("Alice");
            result.Rows[1]["name"].Should().Be("Charlie");
        }
        finally
        {
            await connection.ExecuteNonQueryAsync($"DROP PROCEDURE IF EXISTS {procName}");
            await connection.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS {tableName}");
        }
    }

    [Fact]
    public async Task ExecuteStoredProcedure_InsertAndReturnId_ShouldWork()
    {
        // Arrange
        await using var connection = _fixture.CreateConnection();
        var tableName = $"test_sp_insert_{Guid.NewGuid():N}";
        var procName = $"sp_add_user_{Guid.NewGuid():N}".Replace("-", "_");

        try
        {
            // Créer la table et la procédure stockée d'insertion
            await connection.ExecuteNonQueryAsync(
                $"CREATE TABLE {tableName} (id INT PRIMARY KEY AUTO_INCREMENT, name VARCHAR(100), email VARCHAR(200))");

            await connection.ExecuteNonQueryAsync(
                $"CREATE PROCEDURE {procName}(IN p_name VARCHAR(100), IN p_email VARCHAR(200)) BEGIN INSERT INTO {tableName} (name, email) VALUES (p_name, p_email); SELECT LAST_INSERT_ID() AS new_id; END");

            // Act - Appeler la procédure stockée d'insertion
            var result = await connection.ExecuteQueryAsync(
                $"CALL {procName}(@name, @email)",
                new { name = "Nouveau User", email = "nouveau@example.com" });

            // Assert
            result.Should().NotBeNull();
            result.Rows.Count.Should().Be(1);
            var newId = Convert.ToInt64(result.Rows[0]["new_id"]);
            newId.Should().BeGreaterThan(0);

            // Vérifier que l'insertion a fonctionné
            var user = await connection.ExecuteQueryAsync(
                $"SELECT * FROM {tableName} WHERE id = @id",
                new { id = newId });
            user.Rows.Count.Should().Be(1);
            user.Rows[0]["name"].Should().Be("Nouveau User");
            user.Rows[0]["email"].Should().Be("nouveau@example.com");
        }
        finally
        {
            await connection.ExecuteNonQueryAsync($"DROP PROCEDURE IF EXISTS {procName}");
            await connection.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS {tableName}");
        }
    }

    [Fact]
    public async Task Transaction_UpdateAndDelete_ShouldWorkTogether()
    {
        // Arrange
        await using var connection = _fixture.CreateConnection();
        var tableName = $"test_tx_crud_{Guid.NewGuid():N}";

        try
        {
            await connection.ExecuteNonQueryAsync(
                $"CREATE TABLE {tableName} (id INT PRIMARY KEY AUTO_INCREMENT, name VARCHAR(100), balance DECIMAL(10,2))");
            await connection.ExecuteNonQueryAsync(
                $"INSERT INTO {tableName} (name, balance) VALUES ('Compte1', 1000.00), ('Compte2', 500.00), ('CompteASupprimer', 0.00)");

            // Act - Transaction avec UPDATE et DELETE
            await connection.BeginTransactionAsync();

            // Transfert de 200 de Compte1 vers Compte2
            await connection.ExecuteNonQueryAsync(
                $"UPDATE {tableName} SET balance = balance - @amount WHERE name = @from",
                new { amount = 200.00m, from = "Compte1" });
            await connection.ExecuteNonQueryAsync(
                $"UPDATE {tableName} SET balance = balance + @amount WHERE name = @to",
                new { amount = 200.00m, to = "Compte2" });

            // Supprimer le compte à 0
            await connection.ExecuteNonQueryAsync(
                $"DELETE FROM {tableName} WHERE name = @name",
                new { name = "CompteASupprimer" });

            await connection.CommitAsync();

            // Assert
            var compte1 = await connection.ExecuteScalarAsync<decimal>(
                $"SELECT balance FROM {tableName} WHERE name = 'Compte1'");
            var compte2 = await connection.ExecuteScalarAsync<decimal>(
                $"SELECT balance FROM {tableName} WHERE name = 'Compte2'");
            var totalRows = await connection.ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM {tableName}");

            compte1.Should().Be(800.00m);
            compte2.Should().Be(700.00m);
            totalRows.Should().Be(2);
        }
        finally
        {
            await connection.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS {tableName}");
        }
    }

    [Fact]
    public async Task ExecuteNonQueryAsync_BulkInsert_ShouldWork()
    {
        // Arrange
        await using var connection = _fixture.CreateConnection();
        var tableName = $"test_bulk_{Guid.NewGuid():N}";

        try
        {
            await connection.ExecuteNonQueryAsync(
                $"CREATE TABLE {tableName} (id INT PRIMARY KEY AUTO_INCREMENT, value INT)");

            // Act - Insertion en masse via transaction
            await connection.BeginTransactionAsync();
            var totalInserted = 0;
            for (int i = 1; i <= 100; i++)
            {
                totalInserted += await connection.ExecuteNonQueryAsync(
                    $"INSERT INTO {tableName} (value) VALUES (@value)",
                    new { value = i * 10 });
            }
            await connection.CommitAsync();

            // Assert
            totalInserted.Should().Be(100);
            var count = await connection.ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM {tableName}");
            count.Should().Be(100);

            var sum = await connection.ExecuteScalarAsync<decimal>($"SELECT SUM(value) FROM {tableName}");
            sum.Should().Be(50500); // Somme de 10+20+...+1000
        }
        finally
        {
            await connection.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS {tableName}");
        }
    }

    [Fact]
    public async Task ExecuteStoredProcedure_WithOutParameter_ShouldWork()
    {
        // Arrange
        await using var connection = _fixture.CreateConnection();
        var tableName = $"test_sp_out_{Guid.NewGuid():N}";
        var procName = $"sp_count_active_{Guid.NewGuid():N}".Replace("-", "_");

        try
        {
            // Créer la table
            await connection.ExecuteNonQueryAsync(
                $"CREATE TABLE {tableName} (id INT PRIMARY KEY AUTO_INCREMENT, name VARCHAR(100), is_active BOOLEAN)");
            await connection.ExecuteNonQueryAsync(
                $"INSERT INTO {tableName} (name, is_active) VALUES ('Alice', TRUE), ('Bob', FALSE), ('Charlie', TRUE), ('David', TRUE)");

            // Créer la procédure stockée avec paramètre OUT
            await connection.ExecuteNonQueryAsync(
                $"CREATE PROCEDURE {procName}(OUT p_count INT) BEGIN SELECT COUNT(*) INTO p_count FROM {tableName} WHERE is_active = TRUE; END");

            // Act - Appeler la procédure avec paramètre OUT via variable utilisateur
            await connection.OpenAsync();
            await connection.ExecuteNonQueryAsync($"CALL {procName}(@result)");
            var result = await connection.ExecuteScalarAsync<int>("SELECT @result");

            // Assert
            result.Should().Be(3); // Alice, Charlie, David sont actifs
        }
        finally
        {
            await connection.ExecuteNonQueryAsync($"DROP PROCEDURE IF EXISTS {procName}");
            await connection.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS {tableName}");
        }
    }

    [Fact]
    public async Task ExecuteStoredProcedure_WithInOutParameter_ShouldWork()
    {
        // Arrange
        await using var connection = _fixture.CreateConnection();
        var procName = $"sp_double_value_{Guid.NewGuid():N}".Replace("-", "_");

        try
        {
            // Créer la procédure stockée avec paramètre INOUT
            await connection.ExecuteNonQueryAsync(
                $"CREATE PROCEDURE {procName}(INOUT p_value INT) BEGIN SET p_value = p_value * 2; END");

            // Act - Appeler la procédure avec paramètre INOUT
            await connection.OpenAsync();
            await connection.ExecuteNonQueryAsync("SET @myvalue = 25");
            await connection.ExecuteNonQueryAsync($"CALL {procName}(@myvalue)");
            var result = await connection.ExecuteScalarAsync<int>("SELECT @myvalue");

            // Assert
            result.Should().Be(50); // 25 * 2 = 50
        }
        finally
        {
            await connection.ExecuteNonQueryAsync($"DROP PROCEDURE IF EXISTS {procName}");
        }
    }

    [Fact]
    public async Task ExecuteStoredProcedure_WithMultipleInAndOutParameters_ShouldWork()
    {
        // Arrange
        await using var connection = _fixture.CreateConnection();
        var tableName = $"test_sp_multi_{Guid.NewGuid():N}";
        var procName = $"sp_calc_stats_{Guid.NewGuid():N}".Replace("-", "_");

        try
        {
            // Créer la table avec des données
            await connection.ExecuteNonQueryAsync(
                $"CREATE TABLE {tableName} (id INT PRIMARY KEY AUTO_INCREMENT, category VARCHAR(50), amount DECIMAL(10,2))");
            await connection.ExecuteNonQueryAsync(
                $"INSERT INTO {tableName} (category, amount) VALUES ('A', 100.50), ('A', 200.25), ('A', 150.00), ('B', 300.00), ('B', 50.75)");

            // Créer la procédure stockée avec IN et plusieurs OUT
            await connection.ExecuteNonQueryAsync(
                $@"CREATE PROCEDURE {procName}(
                    IN p_category VARCHAR(50),
                    OUT p_count INT,
                    OUT p_total DECIMAL(10,2),
                    OUT p_average DECIMAL(10,2)
                )
                BEGIN
                    SELECT COUNT(*), SUM(amount), AVG(amount)
                    INTO p_count, p_total, p_average
                    FROM {tableName}
                    WHERE category = p_category;
                END");

            // Act - Appeler la procédure avec paramètres multiples
            await connection.OpenAsync();
            await connection.ExecuteNonQueryAsync($"CALL {procName}('A', @cnt, @tot, @avg)");

            var count = await connection.ExecuteScalarAsync<int>("SELECT @cnt");
            var total = await connection.ExecuteScalarAsync<decimal>("SELECT @tot");
            var average = await connection.ExecuteScalarAsync<decimal>("SELECT @avg");

            // Assert
            count.Should().Be(3);
            total.Should().Be(450.75m); // 100.50 + 200.25 + 150.00
            average.Should().BeApproximately(150.25m, 0.01m); // 450.75 / 3
        }
        finally
        {
            await connection.ExecuteNonQueryAsync($"DROP PROCEDURE IF EXISTS {procName}");
            await connection.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS {tableName}");
        }
    }

    [Fact]
    public async Task ExecuteStoredProcedure_InsertWithOutId_ShouldWork()
    {
        // Arrange
        await using var connection = _fixture.CreateConnection();
        var tableName = $"test_sp_ins_out_{Guid.NewGuid():N}";
        var procName = $"sp_create_user_{Guid.NewGuid():N}".Replace("-", "_");

        try
        {
            // Créer la table
            await connection.ExecuteNonQueryAsync(
                $"CREATE TABLE {tableName} (id INT PRIMARY KEY AUTO_INCREMENT, name VARCHAR(100), email VARCHAR(200), created_at DATETIME)");

            // Créer la procédure stockée avec IN et OUT
            await connection.ExecuteNonQueryAsync(
                $@"CREATE PROCEDURE {procName}(
                    IN p_name VARCHAR(100),
                    IN p_email VARCHAR(200),
                    OUT p_id INT,
                    OUT p_created DATETIME
                )
                BEGIN
                    SET p_created = NOW();
                    INSERT INTO {tableName} (name, email, created_at) VALUES (p_name, p_email, p_created);
                    SET p_id = LAST_INSERT_ID();
                END");

            // Act - Créer un utilisateur et récupérer l'ID et la date
            await connection.OpenAsync();
            await connection.ExecuteNonQueryAsync(
                $"CALL {procName}('Marie Curie', 'marie@science.fr', @new_id, @created_date)");

            var newId = await connection.ExecuteScalarAsync<int>("SELECT @new_id");
            var createdDate = await connection.ExecuteScalarAsync<DateTime>("SELECT @created_date");

            // Assert
            newId.Should().BeGreaterThan(0);
            createdDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));

            // Vérifier en base
            var user = await connection.ExecuteQueryAsync(
                $"SELECT * FROM {tableName} WHERE id = @id", new { id = newId });
            user.Rows.Count.Should().Be(1);
            user.Rows[0]["name"].Should().Be("Marie Curie");
            user.Rows[0]["email"].Should().Be("marie@science.fr");
        }
        finally
        {
            await connection.ExecuteNonQueryAsync($"DROP PROCEDURE IF EXISTS {procName}");
            await connection.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS {tableName}");
        }
    }

    [Fact]
    public async Task ExecuteStoredProcedure_WithInOutAndResultSet_ShouldWork()
    {
        // Arrange
        await using var connection = _fixture.CreateConnection();
        var tableName = $"test_sp_complex_{Guid.NewGuid():N}";
        var procName = $"sp_search_update_{Guid.NewGuid():N}".Replace("-", "_");

        try
        {
            // Créer la table
            await connection.ExecuteNonQueryAsync(
                $"CREATE TABLE {tableName} (id INT PRIMARY KEY AUTO_INCREMENT, product VARCHAR(100), price DECIMAL(10,2), stock INT)");
            await connection.ExecuteNonQueryAsync(
                $"INSERT INTO {tableName} (product, price, stock) VALUES ('Laptop', 999.99, 10), ('Mouse', 29.99, 50), ('Keyboard', 79.99, 30)");

            // Créer la procédure : cherche les produits, met à jour le stock, retourne le résultat et le total
            await connection.ExecuteNonQueryAsync(
                $@"CREATE PROCEDURE {procName}(
                    IN p_min_price DECIMAL(10,2),
                    INOUT p_stock_adjustment INT,
                    OUT p_total_value DECIMAL(10,2)
                )
                BEGIN
                    -- Mettre à jour le stock des produits correspondants
                    UPDATE {tableName} SET stock = stock + p_stock_adjustment WHERE price >= p_min_price;

                    -- Calculer la valeur totale du stock mis à jour
                    SELECT SUM(price * stock) INTO p_total_value FROM {tableName} WHERE price >= p_min_price;

                    -- Retourner le double de l'ajustement pour confirmer
                    SET p_stock_adjustment = p_stock_adjustment * 2;

                    -- Retourner les produits affectés
                    SELECT id, product, price, stock FROM {tableName} WHERE price >= p_min_price ORDER BY price DESC;
                END");

            // Act
            await connection.OpenAsync();
            await connection.ExecuteNonQueryAsync("SET @adj = 5");
            var results = await connection.ExecuteQueryAsync(
                $"CALL {procName}(50.00, @adj, @total)");

            var adjustmentResult = await connection.ExecuteScalarAsync<int>("SELECT @adj");
            var totalValue = await connection.ExecuteScalarAsync<decimal>("SELECT @total");

            // Assert
            // Produits >= 50€ : Laptop (999.99) et Keyboard (79.99)
            results.Rows.Count.Should().Be(2);
            results.Rows[0]["product"].Should().Be("Laptop");
            results.Rows[1]["product"].Should().Be("Keyboard");

            // Stock mis à jour : Laptop 10+5=15, Keyboard 30+5=35
            Convert.ToInt32(results.Rows[0]["stock"]).Should().Be(15);
            Convert.ToInt32(results.Rows[1]["stock"]).Should().Be(35);

            // INOUT : 5 * 2 = 10
            adjustmentResult.Should().Be(10);

            // Total : (999.99 * 15) + (79.99 * 35) = 14999.85 + 2799.65 = 17799.50
            totalValue.Should().BeApproximately(17799.50m, 0.01m);
        }
        finally
        {
            await connection.ExecuteNonQueryAsync($"DROP PROCEDURE IF EXISTS {procName}");
            await connection.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS {tableName}");
        }
    }

    #region Event Scheduler Tests

    [Fact]
    public async Task EventScheduler_ShouldBeEnabled()
    {
        // Arrange
        await using var connection = _fixture.CreateConnection();

        // Act
        var status = await connection.ExecuteScalarAsync<string>("SELECT @@event_scheduler");

        // Assert
        status.Should().BeOneOf("ON", "1"); // MariaDB peut retourner "ON" ou "1"
    }

    [Fact]
    public async Task EventScheduler_CreateAndDropEvent_ShouldWork()
    {
        // Arrange
        await using var connection = _fixture.CreateConnection();
        var eventName = $"evt_test_{Guid.NewGuid():N}";
        var tableName = $"test_evt_{Guid.NewGuid():N}";

        try
        {
            // Créer une table pour stocker les résultats de l'event
            await connection.ExecuteNonQueryAsync(
                $"CREATE TABLE {tableName} (id INT PRIMARY KEY AUTO_INCREMENT, created_at DATETIME DEFAULT CURRENT_TIMESTAMP)");

            // Act - Créer un event qui s'exécute une seule fois
            await connection.ExecuteNonQueryAsync(
                $@"CREATE EVENT {eventName}
                   ON SCHEDULE AT CURRENT_TIMESTAMP + INTERVAL 1 SECOND
                   DO INSERT INTO {tableName} (created_at) VALUES (NOW())");

            // Vérifier que l'event existe
            var eventCount = await connection.ExecuteScalarAsync<long>(
                $"SELECT COUNT(*) FROM information_schema.EVENTS WHERE EVENT_NAME = @name",
                new { name = eventName });

            // Assert
            eventCount.Should().Be(1);
        }
        finally
        {
            await connection.ExecuteNonQueryAsync($"DROP EVENT IF EXISTS {eventName}");
            await connection.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS {tableName}");
        }
    }

    [Fact]
    public async Task EventScheduler_RecurringEvent_ShouldBeCreated()
    {
        // Arrange
        await using var connection = _fixture.CreateConnection();
        var eventName = $"evt_recurring_{Guid.NewGuid():N}";
        var tableName = $"test_evt_rec_{Guid.NewGuid():N}";

        try
        {
            // Créer une table pour l'event
            await connection.ExecuteNonQueryAsync(
                $"CREATE TABLE {tableName} (id INT PRIMARY KEY AUTO_INCREMENT, value INT, recorded_at DATETIME DEFAULT CURRENT_TIMESTAMP)");

            // Act - Créer un event récurrent
            await connection.ExecuteNonQueryAsync(
                $@"CREATE EVENT {eventName}
                   ON SCHEDULE EVERY 1 MINUTE
                   STARTS CURRENT_TIMESTAMP
                   ENDS CURRENT_TIMESTAMP + INTERVAL 1 HOUR
                   COMMENT 'Test recurring event'
                   DO INSERT INTO {tableName} (value) VALUES (UNIX_TIMESTAMP())");

            // Vérifier les propriétés de l'event
            var eventInfo = await connection.ExecuteQueryAsync(
                $@"SELECT EVENT_NAME, EVENT_TYPE, INTERVAL_VALUE, INTERVAL_FIELD, STATUS, EVENT_COMMENT
                   FROM information_schema.EVENTS
                   WHERE EVENT_NAME = @name",
                new { name = eventName });

            // Assert
            eventInfo.Rows.Count.Should().Be(1);
            eventInfo.Rows[0]["EVENT_TYPE"].Should().Be("RECURRING");
            eventInfo.Rows[0]["INTERVAL_VALUE"].Should().Be("1");
            eventInfo.Rows[0]["INTERVAL_FIELD"].Should().Be("MINUTE");
            eventInfo.Rows[0]["STATUS"].Should().Be("ENABLED");
            eventInfo.Rows[0]["EVENT_COMMENT"].Should().Be("Test recurring event");
        }
        finally
        {
            await connection.ExecuteNonQueryAsync($"DROP EVENT IF EXISTS {eventName}");
            await connection.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS {tableName}");
        }
    }

    [Fact]
    public async Task EventScheduler_OneTimeEvent_ShouldExecute()
    {
        // Arrange
        await using var connection = _fixture.CreateConnection();
        var eventName = $"evt_exec_{Guid.NewGuid():N}";
        var tableName = $"test_evt_exec_{Guid.NewGuid():N}";

        try
        {
            // Créer une table pour capturer l'exécution de l'event
            await connection.ExecuteNonQueryAsync(
                $"CREATE TABLE {tableName} (id INT PRIMARY KEY AUTO_INCREMENT, message VARCHAR(100), executed_at DATETIME DEFAULT CURRENT_TIMESTAMP)");

            // Créer un event qui s'exécute immédiatement (dans 1 seconde)
            await connection.ExecuteNonQueryAsync(
                $@"CREATE EVENT {eventName}
                   ON SCHEDULE AT CURRENT_TIMESTAMP + INTERVAL 1 SECOND
                   ON COMPLETION PRESERVE
                   DO INSERT INTO {tableName} (message) VALUES ('Event executed!')");

            // Act - Attendre que l'event s'exécute
            await Task.Delay(3000); // Attendre 3 secondes pour être sûr

            // Vérifier que l'event a été exécuté
            var result = await connection.ExecuteQueryAsync($"SELECT * FROM {tableName}");

            // Assert
            result.Rows.Count.Should().Be(1);
            result.Rows[0]["message"].Should().Be("Event executed!");
        }
        finally
        {
            await connection.ExecuteNonQueryAsync($"DROP EVENT IF EXISTS {eventName}");
            await connection.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS {tableName}");
        }
    }

    [Fact]
    public async Task EventScheduler_AlterEvent_ShouldWork()
    {
        // Arrange
        await using var connection = _fixture.CreateConnection();
        var eventName = $"evt_alter_{Guid.NewGuid():N}";
        var tableName = $"test_evt_alt_{Guid.NewGuid():N}";

        try
        {
            // Créer une table et un event initial
            await connection.ExecuteNonQueryAsync(
                $"CREATE TABLE {tableName} (id INT PRIMARY KEY AUTO_INCREMENT, value INT)");

            await connection.ExecuteNonQueryAsync(
                $@"CREATE EVENT {eventName}
                   ON SCHEDULE EVERY 1 HOUR
                   DO INSERT INTO {tableName} (value) VALUES (1)");

            // Act - Modifier l'event (désactiver puis changer l'intervalle)
            await connection.ExecuteNonQueryAsync($"ALTER EVENT {eventName} DISABLE");

            var statusAfterDisable = await connection.ExecuteScalarAsync<string>(
                $"SELECT STATUS FROM information_schema.EVENTS WHERE EVENT_NAME = @name",
                new { name = eventName });

            await connection.ExecuteNonQueryAsync(
                $@"ALTER EVENT {eventName}
                   ON SCHEDULE EVERY 30 MINUTE
                   ENABLE");

            var eventInfo = await connection.ExecuteQueryAsync(
                $@"SELECT STATUS, INTERVAL_VALUE, INTERVAL_FIELD
                   FROM information_schema.EVENTS WHERE EVENT_NAME = @name",
                new { name = eventName });

            // Assert
            statusAfterDisable.Should().Be("DISABLED");
            eventInfo.Rows[0]["STATUS"].Should().Be("ENABLED");
            eventInfo.Rows[0]["INTERVAL_VALUE"].Should().Be("30");
            eventInfo.Rows[0]["INTERVAL_FIELD"].Should().Be("MINUTE");
        }
        finally
        {
            await connection.ExecuteNonQueryAsync($"DROP EVENT IF EXISTS {eventName}");
            await connection.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS {tableName}");
        }
    }

    [Fact]
    public async Task EventScheduler_EventWithStoredProcedure_ShouldWork()
    {
        // Arrange
        await using var connection = _fixture.CreateConnection();
        var eventName = $"evt_proc_{Guid.NewGuid():N}";
        var procName = $"sp_evt_{Guid.NewGuid():N}".Replace("-", "_");
        var tableName = $"test_evt_sp_{Guid.NewGuid():N}";

        try
        {
            // Créer la table et la procédure stockée
            await connection.ExecuteNonQueryAsync(
                $"CREATE TABLE {tableName} (id INT PRIMARY KEY AUTO_INCREMENT, counter INT, updated_at DATETIME)");

            await connection.ExecuteNonQueryAsync(
                $"INSERT INTO {tableName} (counter, updated_at) VALUES (0, NOW())");

            await connection.ExecuteNonQueryAsync(
                $@"CREATE PROCEDURE {procName}()
                   BEGIN
                       UPDATE {tableName} SET counter = counter + 1, updated_at = NOW() WHERE id = 1;
                   END");

            // Act - Créer un event qui appelle la procédure stockée
            await connection.ExecuteNonQueryAsync(
                $@"CREATE EVENT {eventName}
                   ON SCHEDULE AT CURRENT_TIMESTAMP + INTERVAL 1 SECOND
                   ON COMPLETION PRESERVE
                   DO CALL {procName}()");

            // Attendre l'exécution
            await Task.Delay(3000);

            // Vérifier le résultat
            var counter = await connection.ExecuteScalarAsync<int>(
                $"SELECT counter FROM {tableName} WHERE id = 1");

            // Assert
            counter.Should().Be(1);
        }
        finally
        {
            await connection.ExecuteNonQueryAsync($"DROP EVENT IF EXISTS {eventName}");
            await connection.ExecuteNonQueryAsync($"DROP PROCEDURE IF EXISTS {procName}");
            await connection.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS {tableName}");
        }
    }

    [Fact]
    public async Task EventScheduler_ListAllEvents_ShouldWork()
    {
        // Arrange
        await using var connection = _fixture.CreateConnection();
        var eventName1 = $"evt_list1_{Guid.NewGuid():N}";
        var eventName2 = $"evt_list2_{Guid.NewGuid():N}";
        var tableName = $"test_evt_list_{Guid.NewGuid():N}";

        try
        {
            await connection.ExecuteNonQueryAsync(
                $"CREATE TABLE {tableName} (id INT PRIMARY KEY AUTO_INCREMENT)");

            await connection.ExecuteNonQueryAsync(
                $@"CREATE EVENT {eventName1}
                   ON SCHEDULE EVERY 1 DAY
                   DO INSERT INTO {tableName} (id) VALUES (NULL)");

            await connection.ExecuteNonQueryAsync(
                $@"CREATE EVENT {eventName2}
                   ON SCHEDULE EVERY 2 HOUR
                   COMMENT 'Second test event'
                   DO INSERT INTO {tableName} (id) VALUES (NULL)");

            // Act - Lister tous les events de la base courante
            var events = await connection.ExecuteQueryAsync(
                @"SELECT EVENT_NAME, EVENT_TYPE, INTERVAL_VALUE, INTERVAL_FIELD, STATUS, EVENT_COMMENT
                  FROM information_schema.EVENTS
                  WHERE EVENT_SCHEMA = DATABASE() AND EVENT_NAME LIKE 'evt_list%'
                  ORDER BY EVENT_NAME");

            // Assert
            events.Rows.Count.Should().Be(2);
        }
        finally
        {
            await connection.ExecuteNonQueryAsync($"DROP EVENT IF EXISTS {eventName1}");
            await connection.ExecuteNonQueryAsync($"DROP EVENT IF EXISTS {eventName2}");
            await connection.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS {tableName}");
        }
    }

    [Fact]
    public async Task EventScheduler_EventWithCondition_ShouldWork()
    {
        // Arrange
        await using var connection = _fixture.CreateConnection();
        var eventName = $"evt_cond_{Guid.NewGuid():N}";
        var tableName = $"test_evt_cond_{Guid.NewGuid():N}";
        var logTable = $"test_evt_log_{Guid.NewGuid():N}";

        try
        {
            // Table principale et table de log
            await connection.ExecuteNonQueryAsync(
                $"CREATE TABLE {tableName} (id INT PRIMARY KEY, should_log BOOLEAN DEFAULT TRUE)");
            await connection.ExecuteNonQueryAsync(
                $"INSERT INTO {tableName} (id, should_log) VALUES (1, TRUE)");

            await connection.ExecuteNonQueryAsync(
                $"CREATE TABLE {logTable} (id INT PRIMARY KEY AUTO_INCREMENT, log_time DATETIME)");

            // Event qui vérifie une condition avant d'agir
            await connection.ExecuteNonQueryAsync(
                $@"CREATE EVENT {eventName}
                   ON SCHEDULE AT CURRENT_TIMESTAMP + INTERVAL 1 SECOND
                   ON COMPLETION PRESERVE
                   DO
                   BEGIN
                       DECLARE v_should_log BOOLEAN;
                       SELECT should_log INTO v_should_log FROM {tableName} WHERE id = 1;
                       IF v_should_log THEN
                           INSERT INTO {logTable} (log_time) VALUES (NOW());
                       END IF;
                   END");

            // Act - Attendre l'exécution
            await Task.Delay(3000);

            var logCount = await connection.ExecuteScalarAsync<long>($"SELECT COUNT(*) FROM {logTable}");

            // Assert - Le log devrait avoir été créé car should_log = TRUE
            logCount.Should().Be(1);
        }
        finally
        {
            await connection.ExecuteNonQueryAsync($"DROP EVENT IF EXISTS {eventName}");
            await connection.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS {tableName}");
            await connection.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS {logTable}");
        }
    }

    #endregion
}
