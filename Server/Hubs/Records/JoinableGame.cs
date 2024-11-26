namespace Server.Hubs.Records;

public sealed record JoinableGame(int Id, string Name, int PlayersCount, int MaximumPlayersCount);
