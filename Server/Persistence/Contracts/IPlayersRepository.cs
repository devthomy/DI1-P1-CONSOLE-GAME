﻿using Server.Models;

namespace Server.Persistence.Contracts;

public interface IPlayersRepository
{
  Task<bool> IsPlayerNameAvailable(string playerName, int gameId);

  Task SavePlayer(Player player);
}
