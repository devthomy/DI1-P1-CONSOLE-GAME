
// Importation des dépendances nécessaires
using FluentResults;
using FluentValidation;
using Microsoft.AspNetCore.SignalR;
using Server.Actions.Contracts;
using Server.Hubs;
using Server.Hubs.Contracts;
using Server.Models;
using Server.Persistence.Contracts;

namespace Server.Actions;

// Définition des paramètres pour terminer un jeu
public sealed record FinishGameParams(int? GameId = null, Game? Game = null);

// Validateur pour les paramètres de fin de jeu
public class FinishGameValidator : AbstractValidator<FinishGameParams>
{
    public FinishGameValidator()
    {
        // Règle : GameId ne doit pas être vide si Game est null
        RuleFor(p => p.GameId).NotEmpty().When(p => p.Game is null);
        // Règle : Game ne doit pas être vide si GameId est null
        RuleFor(p => p.Game).NotEmpty().When(p => p.GameId is null);
    }
}

// Classe pour terminer un jeu
public class FinishGame(
    IGamesRepository gamesRepository,
    IGameHubService gameHubService
) : IAction<FinishGameParams, Result<Game>>
{
    public async Task<Result<Game>> PerformAsync(FinishGameParams actionParams)
    {
        // Validation des paramètres d'action
        var actionValidator = new FinishGameValidator();
        var actionValidationResult = await actionValidator.ValidateAsync(actionParams);

        // Si des erreurs de validation sont présentes, retourner un échec
        if (actionValidationResult.Errors.Count != 0)
        {
            return Result.Fail(actionValidationResult.Errors.Select(e => e.ErrorMessage));
        }

        var (gameId, game) = actionParams;

        // Si le jeu n'est pas fourni, le récupérer à partir de l'ID
        game ??= await gamesRepository.GetById(gameId!.Value);

        // Vérifier si le jeu existe
        if (game is null)
        {
            return Result.Fail($"Jeu avec l'Id \"{gameId}\" non trouvé.");
        }

        // Vérifier si le jeu est déjà terminé
        if (game.Status == GameStatus.Finished)
        {
            return Result.Fail("Le jeu est déjà terminé.");
        }

        // Marquer le jeu comme terminé
        game.Status = GameStatus.Finished;

        // Sauvegarder les modifications du jeu
        await gamesRepository.SaveGame(game);

        // Mettre à jour le jeu actuel via le hub
        await gameHubService.UpdateCurrentGame(gameId: game.Id);

        // Retourner le jeu mis à jour
        return Result.Ok(game);
    }
}
