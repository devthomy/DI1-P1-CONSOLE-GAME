using FluentResults;

using FluentValidation;

using Server.Actions.Contracts;
using Server.Hubs.Contracts;
using Server.Models;
using Server.Persistence.Contracts;

using static Server.Models.RoundAction;

namespace Server.Actions;

// Paramètres requis pour effectuer une action dans un tour
public sealed record ActInRoundParams(
    RoundActionType ActionType, // Type d'action du tour
    RoundActionPayload ActionPayload, // Charge utile de l'action du tour
    int? RoundId = null, // Id du tour (optionnel)
    Round? Round = null, // Tour (optionnel)
    int? PlayerId = null, // Id du joueur (optionnel)
    Player? Player = null // Joueur (optionnel)
);

// Validateur pour ActInRoundParams
public class ActInRoundValidator : AbstractValidator<ActInRoundParams>
{
    public ActInRoundValidator()
    {
        RuleFor(p => p.ActionType).NotEmpty(); // Règle : ActionType ne doit pas être vide
        RuleFor(p => p.ActionPayload).NotEmpty(); // Règle : ActionPayload ne doit pas être vide
        RuleFor(p => p.RoundId).NotEmpty().When(p => p.Round is null); // Règle : RoundId ne doit pas être vide si Round est null
        RuleFor(p => p.Round).NotEmpty().When(p => p.RoundId is null); // Règle : Round ne doit pas être vide si RoundId est null
        RuleFor(p => p.PlayerId).NotEmpty().When(p => p.Player is null); // Règle : PlayerId ne doit pas être vide si Player est null
        RuleFor(p => p.Player).NotEmpty().When(p => p.PlayerId is null); // Règle : Player ne doit pas être vide si PlayerId est null
    }
}

// Classe d'action pour effectuer une action dans un tour
public class ActInRound(
    IRoundsRepository roundsRepository, // Référentiel des tours
    IPlayersRepository playersRepository, // Référentiel des joueurs
    IAction<FinishRoundParams, Result<Round>> finishRoundAction, // Action pour terminer un tour
    IGameHubService gameHubService // Service de hub de jeu
) : IAction<ActInRoundParams, Result<Round>>
{
    // Méthode pour effectuer l'action
    public async Task<Result<Round>> PerformAsync(ActInRoundParams actionParams)
    {
        // Valider les paramètres de l'action
        var actionValidator = new ActInRoundValidator();
        var actionValidationResult = await actionValidator.ValidateAsync(actionParams);

        // Retourner les erreurs de validation s'il y en a
        if (actionValidationResult.Errors.Count != 0)
        {
            return Result.Fail(actionValidationResult.Errors.Select(e => e.ErrorMessage));
        }

        var (actionType, actionPayload, roundId, round, playerId, player) = actionParams;

        // Récupérer le tour s'il n'est pas fourni
        round ??= await roundsRepository.GetById(roundId!.Value);

        if (round is null)
        {
            Result.Fail($"Round with Id \"{roundId}\" not found."); // Retourner une erreur si le tour n'est pas trouver
        }

        // Récupérer le joueur s'il n'est pas fourni
        player ??= await playersRepository.GetById(playerId!.Value);

        if (player is null)
        {
            Result.Fail($"Player with Id \"{playerId}\" not found."); // Retourner une erreur si le joueur n'est pas trouver
        }

        // Vérifier si le joueur peut agir dans le tour
        if (!round!.CanPlayerActIn(player!.Id!.Value))
        {
            return Result.Fail("Player cannot act in this round."); 
        }

        var roundAction = CreateForType(actionType, player.Id.Value, actionPayload);

        round.Actions.Add(roundAction); 

        // Sauvegarder le tour
        await roundsRepository.SaveRound(round);

        // Terminer le tour si tout le monde a joué
        if (round.EverybodyPlayed())
        {
            var finishRoundParams = new FinishRoundParams(Round: round);
            var finishRoundResult = await finishRoundAction.PerformAsync(finishRoundParams);

            foreach (var currentPlayer in round.Game.Players)
            {
                currentPlayer.Company?.DeductSalaries();
            }

            if (finishRoundResult.IsFailed)
            {
                return Result.Fail(finishRoundResult.Errors); // Retourner une erreur si la fin du tour échoue
            }
        }

        // Mettre à jour l'état actuel du jeu
        await gameHubService.UpdateCurrentGame(gameId: round.GameId);

        return Result.Ok(round); // Retourner le tour
    }
}
