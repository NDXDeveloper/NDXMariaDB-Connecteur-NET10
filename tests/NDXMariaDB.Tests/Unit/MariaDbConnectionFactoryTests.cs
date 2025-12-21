using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace NDXMariaDB.Tests.Unit;

/// <summary>
/// Tests unitaires pour MariaDbConnectionFactory.
/// </summary>
public class MariaDbConnectionFactoryTests
{
    private readonly MariaDbConnectionOptions _defaultOptions;
    private readonly Mock<ILoggerFactory> _loggerFactoryMock;

    public MariaDbConnectionFactoryTests()
    {
        _defaultOptions = new MariaDbConnectionOptions
        {
            Server = "localhost",
            Database = "testdb",
            Username = "testuser",
            Password = "testpass"
        };

        _loggerFactoryMock = new Mock<ILoggerFactory>();
    }

    [Fact]
    public void Constructor_WithNullOptions_ShouldThrowArgumentNullException()
    {
        // Act
        var act = () => new MariaDbConnectionFactory((MariaDbConnectionOptions)null!, null);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("defaultOptions");
    }

    [Fact]
    public void Constructor_WithConnectionString_ShouldWork()
    {
        // Arrange
        var connectionString = "Server=localhost;Database=test;User=user;Password=pass";

        // Act
        var factory = new MariaDbConnectionFactory(connectionString);

        // Assert
        factory.Should().NotBeNull();
    }

    [Fact]
    public void CreateConnection_ShouldReturnNewConnection()
    {
        // Arrange
        var factory = new MariaDbConnectionFactory(_defaultOptions);

        // Act
        using var connection = factory.CreateConnection();

        // Assert
        connection.Should().NotBeNull();
        connection.Should().BeOfType<MariaDbConnection>();
        connection.IsPrimaryConnection.Should().BeFalse();
    }

    [Fact]
    public void CreatePrimaryConnection_ShouldReturnPrimaryConnection()
    {
        // Arrange
        var factory = new MariaDbConnectionFactory(_defaultOptions);

        // Act
        using var connection = factory.CreatePrimaryConnection();

        // Assert
        connection.Should().NotBeNull();
        connection.IsPrimaryConnection.Should().BeTrue();
    }

    [Fact]
    public void CreateConnection_WithConfigure_ShouldApplyConfiguration()
    {
        // Arrange
        var factory = new MariaDbConnectionFactory(_defaultOptions);

        // Act
        using var connection = factory.CreateConnection(opts =>
        {
            opts.IsPrimaryConnection = true;
            opts.DisableAutoClose = true;
        });

        // Assert
        connection.Should().NotBeNull();
        connection.IsPrimaryConnection.Should().BeTrue();
    }

    [Fact]
    public void CreateConnection_WithOptions_ShouldUseProvidedOptions()
    {
        // Arrange
        var factory = new MariaDbConnectionFactory(_defaultOptions);
        var customOptions = new MariaDbConnectionOptions
        {
            Server = "customserver",
            Database = "customdb",
            Username = "customuser",
            Password = "custompass",
            IsPrimaryConnection = true
        };

        // Act
        using var connection = factory.CreateConnection(customOptions);

        // Assert
        connection.Should().NotBeNull();
        connection.IsPrimaryConnection.Should().BeTrue();
    }

    [Fact]
    public void CreateConnection_ShouldGenerateUniqueIds()
    {
        // Arrange
        var factory = new MariaDbConnectionFactory(_defaultOptions);

        // Act
        using var connection1 = factory.CreateConnection();
        using var connection2 = factory.CreateConnection();
        using var connection3 = factory.CreateConnection();

        // Assert
        connection1.Id.Should().NotBe(connection2.Id);
        connection2.Id.Should().NotBe(connection3.Id);
        connection1.Id.Should().NotBe(connection3.Id);
    }
}
