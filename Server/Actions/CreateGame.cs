using FluentResults;

using FluentValidation;

using Server.Actions.Contracts;
using Server.Models;
using Server.Persistence.Contracts;

namespace Server.Actions;

public sealed record CreateGameParams(string GameName, string PlayerName, string CompanyName, int Rounds = 15);

public class CreateGameValidator : AbstractValidator<CreateGameParams>
{
    public CreateGameValidator()
    {
        RuleFor(p => p.GameName).NotEmpty();
        RuleFor(p => p.PlayerName).NotEmpty();
        RuleFor(p => p.CompanyName).NotEmpty();
        RuleFor(p => p.Rounds).NotNull().GreaterThanOrEqualTo(15);
    }
}

public class CreateGame(
  IGamesRepository gamesRepository,
  IAction<CreatePlayerParams, Result<Player>> createPlayerAction
) : IAction<CreateGameParams, Result<Game>>
{
    public async Task<Result<Game>> PerformAsync(CreateGameParams actionParams)
    {
        var actionValidator = new CreateGameValidator();
        var actionValidationResult = await actionValidator.ValidateAsync(actionParams);

        if (actionValidationResult.Errors.Count != 0)
        {
            return Result.Fail(actionValidationResult.Errors.Select(e => e.ErrorMessage));
        }

        var (gameName, playerName, companyName, rounds) = actionParams;

        var isGameNameAvailable = await gamesRepository.IsGameNameAvailable(gameName);

        if (!isGameNameAvailable)
        {
            return Result.Fail("'Game Name' is already in use.");
        }

        var game = new Game(gameName, rounds);

        await gamesRepository.SaveGame(game);

        var createPlayerParams = new CreatePlayerParams(playerName, companyName, Game: game);
        var createPlayerResult = await createPlayerAction.PerformAsync(createPlayerParams);

        if (createPlayerResult.IsFailed)
        {
            return Result.Fail(createPlayerResult.Errors);
        }

        return Result.Ok(game);
    }
}
