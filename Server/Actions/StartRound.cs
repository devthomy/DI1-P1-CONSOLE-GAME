using FluentResults;
using FluentValidation;
using Microsoft.AspNetCore.SignalR;
using Server.Actions.Contracts;
using Server.Hubs;
using Server.Hubs.Contracts;
using Server.Models;
using Server.Persistence.Contracts;

namespace Server.Actions;

public sealed record StartRoundParams(int? GameId = null, Game? Game = null);

public class StartRoundValidator : AbstractValidator<StartRoundParams>
{
    public StartRoundValidator()
    {
        RuleFor(p => p.GameId).NotEmpty().When(p => p.Game is null);
        RuleFor(p => p.Game).NotEmpty().When(p => p.GameId is null);
    }
}

public class StartRound(
    IGamesRepository gamesRepository,
    IRoundsRepository roundsRepository,
    IGameHubService gameHubService,
    IAction<CreateConsultantParams, Result<Consultant>> createConsultantAction  // Injecte ici l'action CreateConsultant
) : IAction<StartRoundParams, Result<Round>>
{
    public async Task<Result<Round>> PerformAsync(StartRoundParams actionParams)
    {
        // Validation des paramètres
        var actionValidator = new StartRoundValidator();
        var actionValidationResult = await actionValidator.ValidateAsync(actionParams);

        if (actionValidationResult.Errors.Count != 0)
        {
            return Result.Fail(actionValidationResult.Errors.Select(e => e.ErrorMessage));
        }

        // Récupération ou création du jeu
        var (gameId, game) = actionParams;
        game ??= await gamesRepository.GetById(gameId!.Value);

        if (game is null)
        {
            return Result.Fail($"Game with Id \"{gameId}\" not found.");
        }

        if (!game!.CanStartANewRound())
        {
            return Result.Fail("Game cannot start a new round.");
        }

        // Création du round
        var round = new Round(game.Id!.Value, game.RoundsCollection.Count + 1);
        await roundsRepository.SaveRound(round);

        // Mise à jour du jeu
        await gameHubService.UpdateCurrentGame(gameId: round.GameId);

        // Création de 3 consultants après le début du round
        foreach (var index in Enumerable.Range(1, 3))  // Par exemple, 3 consultants
        {
            var createConsultantParams = new CreateConsultantParams("Consultant " + index, GameId: game.Id);
            var createConsultantResult = await createConsultantAction.PerformAsync(createConsultantParams);

            if (createConsultantResult.IsFailed)
            {
                return Result.Fail(createConsultantResult.Errors);
            }
        }

        return Result.Ok(round);
    }
}
