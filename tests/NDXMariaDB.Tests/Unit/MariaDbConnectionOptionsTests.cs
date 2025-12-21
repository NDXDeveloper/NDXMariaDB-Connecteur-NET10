using FluentAssertions;
using Xunit;

namespace NDXMariaDB.Tests.Unit;

/// <summary>
/// Tests unitaires pour MariaDbConnectionOptions.
/// </summary>
public class MariaDbConnectionOptionsTests
{
    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var options = new MariaDbConnectionOptions();

        // Assert
        options.Server.Should().Be("localhost");
        options.Port.Should().Be(3306);
        options.Database.Should().BeEmpty();
        options.Username.Should().BeEmpty();
        options.Password.Should().BeEmpty();
        options.ConnectionString.Should().BeNull();
        options.IsPrimaryConnection.Should().BeFalse();
        options.AutoCloseTimeoutMs.Should().Be(60_000);
        options.DisableAutoClose.Should().BeFalse();
        options.Pooling.Should().BeTrue();
        options.MinPoolSize.Should().Be(0);
        options.MaxPoolSize.Should().Be(100);
        options.ConnectionTimeoutSeconds.Should().Be(30);
        options.CommandTimeoutSeconds.Should().Be(30);
        options.InnoDbLockWaitTimeout.Should().Be(120);
        options.UseSsl.Should().BeFalse();
        options.SslMode.Should().Be("Preferred");
        options.AllowUserVariables.Should().BeTrue();
    }

    [Fact]
    public void BuildConnectionString_WithConnectionString_ShouldReturnConnectionString()
    {
        // Arrange
        var inputConnString = "Server=myserver;Database=mydb;User=myuser;Password=mypass";
        var options = new MariaDbConnectionOptions
        {
            ConnectionString = inputConnString
        };

        // Act
        var result = options.BuildConnectionString();

        // Assert - AllowUserVariables=true est ajouté automatiquement
        result.Should().Contain(inputConnString);
        result.Should().Contain("AllowUserVariables=true");
    }

    [Fact]
    public void BuildConnectionString_WithConnectionStringContainingAllowUserVariables_ShouldNotDuplicate()
    {
        // Arrange
        var inputConnString = "Server=myserver;Database=mydb;AllowUserVariables=true";
        var options = new MariaDbConnectionOptions
        {
            ConnectionString = inputConnString
        };

        // Act
        var result = options.BuildConnectionString();

        // Assert - Pas de duplication
        result.Should().Be(inputConnString);
    }

    [Fact]
    public void BuildConnectionString_WithProperties_ShouldBuildCorrectString()
    {
        // Arrange
        var options = new MariaDbConnectionOptions
        {
            Server = "testserver",
            Port = 3307,
            Database = "testdb",
            Username = "testuser",
            Password = "testpass",
            Pooling = true,
            MinPoolSize = 5,
            MaxPoolSize = 50,
            ConnectionTimeoutSeconds = 60,
            CommandTimeoutSeconds = 120
        };

        // Act
        var result = options.BuildConnectionString();

        // Assert
        result.Should().Contain("Server=testserver");
        result.Should().Contain("Port=3307");
        result.Should().Contain("Database=testdb");
        result.Should().Contain("User ID=testuser");
        result.Should().Contain("Password=testpass");
        result.Should().Contain("Pooling=True");
        result.Should().Contain("Minimum Pool Size=5");
        result.Should().Contain("Maximum Pool Size=50");
    }

    [Theory]
    [InlineData("None")]
    [InlineData("Preferred")]
    [InlineData("Required")]
    [InlineData("VerifyCA")]
    [InlineData("VerifyFull")]
    public void BuildConnectionString_WithSslMode_ShouldSetCorrectSslMode(string sslMode)
    {
        // Arrange
        var options = new MariaDbConnectionOptions
        {
            Server = "localhost",
            Database = "test",
            Username = "user",
            Password = "pass",
            UseSsl = true,
            SslMode = sslMode
        };

        // Act
        var result = options.BuildConnectionString();

        // Assert
        result.Should().Contain($"SSL Mode={sslMode}");
    }
}
