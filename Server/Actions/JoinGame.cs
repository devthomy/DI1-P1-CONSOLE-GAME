using FluentResults;

using Server.Actions.Contracts;
using Server.Hubs.Contracts;
using Server.Models;

namespace Server.Actions;

public sealed record JoinGameParams(
    string PlayerName,
    string CompanyName,
    int? GameId = null,
    Game? Game = null
) : CreatePlayerParams(PlayerName, CompanyName, GameId, Game);

public class JoinGameValidator : CreatePlayerValidator;

public class JoinGame(
    IAction<CreatePlayerParams,
    Result<Player>> createPlayerAction,
    IGameHubService gameHubService
) : IAction<JoinGameParams, Result<Player>>
{
    public async Task<Result<Player>> PerformAsync(JoinGameParams actionParams)
    {
        var createPlayerActionResult = await createPlayerAction.PerformAsync(actionParams);

        if (createPlayerActionResult.IsSuccess)
        {
            var player = createPlayerActionResult.Value;
            await gameHubService.UpdateCurrentGame(gameId: player.GameId);
        }

        return createPlayerActionResult;
    }
}
