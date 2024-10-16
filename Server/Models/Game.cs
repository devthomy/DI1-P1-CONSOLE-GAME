using Server.Hubs.Records;

namespace Server.Models;

public enum GameStatus
{
    Waiting,
    InProgress,
    Finished
}

public class Game(string name, int rounds = 15)
{
    public int? Id { get; private set; }
    public string Name { get; set; } = name;
    public int Rounds { get; set; } = rounds;
    public GameStatus Status { get; set; } = GameStatus.Waiting;
    public ICollection<Player> Players { get; } = new List<Player>();
    public ICollection<Round> RoundsCollection { get; } = new List<Round>();
    public ICollection<Consultant> Consultants { get; } = new List<Consultant>();

    // Check if the game can be joined by more players
    public bool CanBeJoined()
    {
        return Status == GameStatus.Waiting && Players.Count < 3;
    }

    // Check if the game can be started
    public bool CanBeStarted()
    {
        return Status == GameStatus.Waiting;
    }

    // Check if a new round can start
    public bool CanStartANewRound()
    {
        return Status == GameStatus.InProgress && RoundsCollection.Count < Rounds;
    }

    // Start a new round and deduct salaries from companies
    public bool StartNewRound()
    {
        if (!CanStartANewRound()) return false;

        var newRound = new Round(gameId: (int)Id!, order: RoundsCollection.Count + 1);
        RoundsCollection.Add(newRound);

        // Deduct salaries for all players' companies
        foreach (var player in Players)
        {
            player.Company?.DeductSalaries();
        }

        return true;
    }

    // Convert Game to its overview form
    public GameOverview ToOverview()
    {
        return new GameOverview(
            Id is null ? 0 : (int)Id, Name, Players.Select(p => p.ToOverview()).ToList(),
            Players.Count, 3, Rounds, RoundsCollection.Count,
            Status.ToString(), RoundsCollection.Select(r => r.ToOverview()).ToList(),
            Consultants.Select(c => c.ToOverview()).ToList()
        );
    }
}
