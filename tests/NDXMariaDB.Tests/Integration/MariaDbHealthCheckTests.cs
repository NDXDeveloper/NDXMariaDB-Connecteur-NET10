using FluentAssertions;
using NDXMariaDB.Tests.Fixtures;
using Xunit;

namespace NDXMariaDB.Tests.Integration;

/// <summary>
/// Tests d'intégration pour MariaDbHealthCheck.
/// </summary>
[Collection("MariaDB")]
public class MariaDbHealthCheckTests
{
    private readonly MariaDbFixture _fixture;

    public MariaDbHealthCheckTests(MariaDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CheckHealthAsync_WhenHealthy_ShouldReturnHealthyResult()
    {
        // Arrange
        var factory = _fixture.CreateFactory();
        var healthCheck = new MariaDbHealthCheck(factory);

        // Act
        var result = await healthCheck.CheckHealthAsync();

        // Assert
        result.Should().NotBeNull();
        result.IsHealthy.Should().BeTrue();
        result.Message.Should().Contain("fonctionnelle");
        result.ResponseTime.Should().BeGreaterThan(TimeSpan.Zero);
        result.Exception.Should().BeNull();
    }

    [Fact]
    public async Task CheckHealthAsync_WhenUnhealthy_ShouldReturnUnhealthyResult()
    {
        // Arrange
        var badOptions = new MariaDbConnectionOptions
        {
            Server = "nonexistent-server-that-does-not-exist",
            Port = 9999,
            Database = "fake",
            Username = "fake",
            Password = "fake",
            ConnectionTimeoutSeconds = 1
        };
        var factory = new MariaDbConnectionFactory(badOptions);
        var healthCheck = new MariaDbHealthCheck(factory);

        // Act
        var result = await healthCheck.CheckHealthAsync();

        // Assert
        result.Should().NotBeNull();
        result.IsHealthy.Should().BeFalse();
        result.Message.Should().Contain("Erreur");
        result.Exception.Should().NotBeNull();
    }

    [Fact]
    public async Task GetServerInfoAsync_ShouldReturnServerInformation()
    {
        // Arrange
        var factory = _fixture.CreateFactory();
        var healthCheck = new MariaDbHealthCheck(factory);

        // Act
        var info = await healthCheck.GetServerInfoAsync();

        // Assert
        info.Should().NotBeNull();
        info.Version.Should().NotBeNullOrEmpty();
        info.Version.Should().Contain("MariaDB");
        info.CurrentDatabase.Should().Be("ndxmariadb_test");
        info.CurrentUser.Should().Contain("testuser");
        info.ConnectionId.Should().BeGreaterThan(0);
    }
}
