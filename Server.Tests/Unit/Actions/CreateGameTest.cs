using FluentAssertions;

using FluentResults;

using Moq;

using Server.Actions;
using Server.Actions.Contracts;
using Server.Hubs;
using Server.Hubs.Contracts;
using Server.Hubs.Records;
using Server.Models;
using Server.Persistence.Contracts;

namespace Server.Tests.Unit.Actions;

public class CreateGameTest
{
    private readonly Mock<IGamesRepository> _gamesRepositoryMock;
    private readonly Mock<IAction<CreatePlayerParams, Result<Player>>> _createPlayerMock;
    private readonly Mock<IMainHubService> _mainHubServiceMock;

    public CreateGameTest()
    {
        _gamesRepositoryMock = new Mock<IGamesRepository>();
        _createPlayerMock = new Mock<IAction<CreatePlayerParams, Result<Player>>>();
        _mainHubServiceMock = new Mock<IMainHubService>();

        _gamesRepositoryMock
            .Setup(r => r.IsGameNameAvailable(It.IsAny<string>()))
            .Returns(Task.Run(() => true));

        _gamesRepositoryMock
            .Setup(r => r.SaveGame(It.IsAny<Game>()))
            .Returns(Task.Run(() => { }));

        _createPlayerMock
            .Setup(a => a.PerformAsync(It.IsAny<CreatePlayerParams>()))
            .Returns(Task.Run(() => Result.Ok(It.IsAny<Player>())));

        _mainHubServiceMock
            .Setup(s => s.UpdateJoinableGamesList(It.IsAny<IMainHubClient>()))
            .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task ItShouldNotCreateGameWithoutAGameName()
    {
        var actionParams = new CreateGameParams("", "Player 1", "Company 1");
        var action = new CreateGame(
            _gamesRepositoryMock.Object,
            _createPlayerMock.Object,
            _mainHubServiceMock.Object
        );

        var actionResult = await action.PerformAsync(actionParams);

        actionResult.IsFailed.Should().BeTrue();
        actionResult.Errors.Count.Should().Be(1);
        actionResult.Errors.First().Message.Should().Be("'Game Name' must not be empty.");
    }

    [Fact]
    public async Task ItShouldNotCreateGameWithoutAPlayerName()
    {
        var actionParams = new CreateGameParams("Game 1", "", "Company 1");
        var action = new CreateGame(
            _gamesRepositoryMock.Object,
            _createPlayerMock.Object,
            _mainHubServiceMock.Object
        );

        var actionResult = await action.PerformAsync(actionParams);

        actionResult.IsFailed.Should().BeTrue();
        actionResult.Errors.Count.Should().Be(1);
        actionResult.Errors.First().Message.Should().Be("'Player Name' must not be empty.");
    }

    [Fact]
    public async Task ItShouldNotCreateGameWithTooFewRounds()
    {
        var actionParams = new CreateGameParams("Game 1", "Player 1", "Company 1", 14);
        var action = new CreateGame(
            _gamesRepositoryMock.Object,
            _createPlayerMock.Object,
            _mainHubServiceMock.Object
        );

        var actionResult = await action.PerformAsync(actionParams);

        actionResult.IsFailed.Should().BeTrue();
        actionResult.Errors.Count.Should().Be(1);
        actionResult.Errors.First().Message.Should().Be("'Rounds' must be greater than or equal to '15'.");
    }

    [Fact]
    public async Task ItShouldNotCreateGameWithATakenGameName()
    {
        _gamesRepositoryMock
            .Setup(r => r.IsGameNameAvailable(It.IsAny<string>()))
            .Returns(Task.Run(() => false));

        var actionParams = new CreateGameParams("Game 1", "Player 1", "Company 1");
        var action = new CreateGame(
            _gamesRepositoryMock.Object,
            _createPlayerMock.Object,
            _mainHubServiceMock.Object
        );

        var actionResult = await action.PerformAsync(actionParams);

        actionResult.IsFailed.Should().BeTrue();
        actionResult.Errors.Count.Should().Be(1);
        actionResult.Errors.First().Message.Should().Be("'Game Name' is already in use.");
    }

    [Fact]
    public async Task ItShouldNotCreateGameIfCreatePlayerFails()
    {
        _createPlayerMock
            .Setup(a => a.PerformAsync(It.IsAny<CreatePlayerParams>()))
            .Returns(Task.Run(() => Result.Fail<Player>("CreatePlayer ERROR")));

        var actionParams = new CreateGameParams("Game 1", "Player 1", "Company 1");
        var action = new CreateGame(
            _gamesRepositoryMock.Object,
            _createPlayerMock.Object,
            _mainHubServiceMock.Object
        );

        var actionResult = await action.PerformAsync(actionParams);

        actionResult.IsFailed.Should().BeTrue();
        actionResult.Errors.Count.Should().Be(1);
        actionResult.Errors.First().Message.Should().Be("CreatePlayer ERROR");
    }

    [Fact]
    public async Task ItShouldCreateGameWithValidData()
    {
        var actionParams = new CreateGameParams("Game 1", "Player 1", "Company 1");
        var action = new CreateGame(
            _gamesRepositoryMock.Object,
            _createPlayerMock.Object,
            _mainHubServiceMock.Object
        );

        var actionResult = await action.PerformAsync(actionParams);

        actionResult.IsSuccess.Should().BeTrue();
        actionResult.Errors.Count.Should().Be(0);
    }
}
