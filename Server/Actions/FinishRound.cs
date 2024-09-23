using FluentResults;

using FluentValidation;

using Server.Actions.Contracts;
using Server.Hubs.Contracts;
using Server.Models;
using Server.Persistence.Contracts;

using static Server.Models.GenerateNewConsultantRoundAction;

namespace Server.Actions;

// Parameters required to finish a round
public sealed record FinishRoundParams(int? RoundId = null, Round? Round = null);

// Validator for FinishRoundParams
public class FinishRoundValidator : AbstractValidator<FinishRoundParams>
{
    public FinishRoundValidator()
    {
        RuleFor(p => p.RoundId).NotEmpty().When(p => p.Round is null);
        RuleFor(p => p.Round).NotEmpty().When(p => p.RoundId is null);
    }
}

// Action class to finish a round
public class FinishRound(
    IRoundsRepository roundsRepository,
    IAction<ApplyRoundActionParams, Result> applyRoundActionAction,
    IAction<StartRoundParams, Result<Round>> startRoundAction,
    IAction<FinishGameParams, Result<Game>> finishGameAction,
    IGameHubService gameHubService
) : IAction<FinishRoundParams, Result<Round>>
{
    // Method to perform the action
    public async Task<Result<Round>> PerformAsync(FinishRoundParams actionParams)
    {
        // Validate the action parameters
        var actionValidator = new FinishRoundValidator();
        var actionValidationResult = await actionValidator.ValidateAsync(actionParams);

        // Return validation errors if any
        if (actionValidationResult.Errors.Count != 0)
        {
            return Result.Fail(actionValidationResult.Errors.Select(e => e.ErrorMessage));
        }

        var (roundId, round) = actionParams;

        // Retrieve the round if not provided
        round ??= await roundsRepository.GetById(roundId!.Value);

        if (round is null)
        {
            return Result.Fail($"Round with Id \"{roundId}\" not found.");
        }

        var rnd = new Random();

        // Determine if a new consultant should be generated
        var newConsultantShouldBeGenerated = rnd.Next(2) == 1;

        if (newConsultantShouldBeGenerated)
        {
            var action = RoundAction.CreateForType(
                RoundActionType.GenerateNewConsultant,
                0,
                new GenerateNewConsultantPayload { GameId = round.GameId }
            );

            round.Actions.Add(action);

            await roundsRepository.SaveRound(round);
        }

        // Apply each action in the round
        foreach (var action in round.Actions)
        {
            var applyRoundActionParams = new ApplyRoundActionParams(RoundAction: action, Game: round.Game);
            var applyRoundActionResult = await applyRoundActionAction.PerformAsync(applyRoundActionParams);

            if (applyRoundActionResult.IsFailed)
            {
                return Result.Fail(applyRoundActionResult.Errors);
            }
        }



        // Check if a new round can be started
        if (round.Game.CanStartANewRound())
        {
            var startRoundActionParams = new StartRoundParams(Game: round.Game);
            var startRoundActionResult = await startRoundAction.PerformAsync(startRoundActionParams);
            var newRound = startRoundActionResult.Value;

            foreach (var gamePlayer in round.Game.Players)
            {
                gamePlayer.Company?.DeductSalaries();
            }

            await gameHubService.UpdateCurrentGame(gameId: round.GameId);

            return Result.Ok(newRound);
        }
        else
        {
            var finishGameActionParams = new FinishGameParams(Game: round.Game);
            var finishGameActionResult = await finishGameAction.PerformAsync(finishGameActionParams);

            if (finishGameActionResult.IsFailed)
            {
                return Result.Fail(finishGameActionResult.Errors);
            }

            await gameHubService.UpdateCurrentGame(gameId: round.GameId);

            return Result.Ok(round);
        }
    }
}
