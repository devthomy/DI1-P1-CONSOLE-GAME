using FluentResults;

using FluentValidation;

using Server.Actions.Contracts;
using Server.Hubs.Contracts;
using Server.Models;
using Server.Persistence.Contracts;

using static Server.Models.GenerateNewConsultantRoundAction;

namespace Server.Actions;

public sealed record FinishRoundParams(int? RoundId = null, Round? Round = null);

public class FinishRoundValidator : AbstractValidator<FinishRoundParams>
{
    public FinishRoundValidator()
    {
        RuleFor(p => p.RoundId).NotEmpty().When(p => p.Round is null);
        RuleFor(p => p.Round).NotEmpty().When(p => p.RoundId is null);
    }
}
public class FinishRound(
    IRoundsRepository roundsRepository,
    ICompaniesRepository companiesRepository,
    IAction<ApplyRoundActionParams, Result> applyRoundActionAction,
    IAction<StartRoundParams, Result<Round>> startRoundAction,
    IAction<FinishGameParams, Result<Game>> finishGameAction,
    IGameHubService gameHubService
) : IAction<FinishRoundParams, Result<Round>>
{
    public async Task<Result<Round>> PerformAsync(FinishRoundParams actionParams)
    {
        // Step 1: Validate the action parameters
        var actionValidator = new FinishRoundValidator();
        var actionValidationResult = await actionValidator.ValidateAsync(actionParams);

        if (!actionValidationResult.IsValid)
        {
            return Result.Fail(actionValidationResult.Errors.Select(e => e.ErrorMessage));
        }

        // Step 2: Retrieve the round
        var (roundId, round) = actionParams;
        round ??= await roundsRepository.GetById(roundId!.Value);

        if (round is null)
        {
            return Result.Fail<Round>($"Round with Id \"{roundId}\" not found.");
        }

        // Step 3: Randomly decide if a new consultant should be generated
        var rnd = new Random();
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

        // Step 4: Apply all actions in the round
        foreach (var action in round.Actions)
        {
            var applyRoundActionParams = new ApplyRoundActionParams(RoundAction: action, Game: round.Game);
            var applyRoundActionResult = await applyRoundActionAction.PerformAsync(applyRoundActionParams);

            if (applyRoundActionResult.IsFailed)
            {
                return Result.Fail<Round>(applyRoundActionResult.Errors);
            }
        }



        // Step 6: Check if a new round can be started or if the game should finish
        if (round.Game.CanStartANewRound())
        {
            var startRoundActionParams = new StartRoundParams(Game: round.Game);
            var startRoundActionResult = await startRoundAction.PerformAsync(startRoundActionParams);


            // Step 5: Deduct salaries from all companies and save the changes
            foreach (var player in round.Game.Players)
            {
                if (player.Company != null)
                {
                    player.Company.DeductSalaries();
                    Console.WriteLine($"Deducted salaries for company {player.Company.Name}. Updated treasury: {player.Company.Treasury}");

                    // Save updated company information
                    await companiesRepository.SaveCompany(player.Company);
                }
            }
            
            if (startRoundActionResult.IsFailed)
            {
                return Result.Fail<Round>(startRoundActionResult.Errors);
            }

            var newRound = startRoundActionResult.Value;
            await gameHubService.UpdateCurrentGame(gameId: round.GameId);

            return Result.Ok(newRound);
        }
        else
        {
            var finishGameActionParams = new FinishGameParams(Game: round.Game);
            var finishGameActionResult = await finishGameAction.PerformAsync(finishGameActionParams);

            if (finishGameActionResult.IsFailed)
            {
                return Result.Fail<Round>(finishGameActionResult.Errors);
            }

            await gameHubService.UpdateCurrentGame(gameId: round.GameId);
            return Result.Ok(round);
        }
    }
}
