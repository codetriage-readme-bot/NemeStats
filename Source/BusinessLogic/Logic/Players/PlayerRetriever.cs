﻿#region LICENSE
// NemeStats is a free website for tracking the results of board games.
//     Copyright (C) 2015 Jacob Gordon
// 
//     This program is free software: you can redistribute it and/or modify
//     it under the terms of the GNU General Public License as published by
//     the Free Software Foundation, either version 3 of the License, or
//     (at your option) any later version.
// 
//     This program is distributed in the hope that it will be useful,
//     but WITHOUT ANY WARRANTY; without even the implied warranty of
//     MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//     GNU General Public License for more details.
// 
//     You should have received a copy of the GNU General Public License
//     along with this program.  If not, see <http://www.gnu.org/licenses/>
#endregion

using BusinessLogic.DataAccess;
using BusinessLogic.DataAccess.Repositories;
using BusinessLogic.Models;
using BusinessLogic.Models.Players;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using BusinessLogic.Exceptions;
using BusinessLogic.Logic.PlayerAchievements;
using BusinessLogic.Models.Achievements;
using BusinessLogic.Models.Points;
using BusinessLogic.Models.User;
using BusinessLogic.Models.Utility;
using BusinessLogic.Paging;

namespace BusinessLogic.Logic.Players
{
    public class PlayerRetriever : IPlayerRetriever
    {
        private readonly IDataContext _dataContext;
        private readonly IPlayerRepository _playerRepository;
        private readonly IRecentPlayerAchievementsUnlockedRetriever _recentPlayerAchievementsUnlockedRetriever;
        public const string ExceptionMessagePlayerCouldNotBeFound = "Could not find player with Id: {0}";
        public const int MAX_NUMBER_OF_RECENT_PLAYERS = 5;

        public PlayerRetriever(
            IDataContext dataContext, 
            IPlayerRepository playerRepository, 
            IRecentPlayerAchievementsUnlockedRetriever recentPlayerAchievementsUnlockedRetriever)
        {
            _dataContext = dataContext;
            _playerRepository = playerRepository;
            _recentPlayerAchievementsUnlockedRetriever = recentPlayerAchievementsUnlockedRetriever;
        }

        internal IQueryable<Player> GetAllPlayersInGamingGroupQueryable(int gamingGroupId)
        {
            return _dataContext.GetQueryable<Player>()
                .Where(player => player.GamingGroupId == gamingGroupId);
        }

        public List<Player> GetAllPlayers(int gamingGroupId, bool includeInactive = true)
        {
            return GetAllPlayersInGamingGroupQueryable(gamingGroupId)
                .Where(x => x.Active || x.Active != includeInactive)
                .OrderByDescending(player => player.Active)
                .ThenBy(player => player.Name)
                .ToList();
        }

        public List<PlayerWithNemesis> GetAllPlayersWithNemesisInfo(int gamingGroupId, IDateRangeFilter dateRangeFilter = null)
        {
            if (dateRangeFilter == null)
            {
                dateRangeFilter = new BasicDateRangeFilter();
            }

            var playersWithNemesis = (from Player player in GetAllPlayersInGamingGroupQueryable(gamingGroupId)
                                          .Include(player => player.PlayerGameResults)
                                      select new PlayerWithNemesis
                                      {
                                          PlayerId = player.Id,
                                          ApplicationUserId = player.ApplicationUserId,
                                          PlayerName = player.Name,
                                          PlayerActive = player.Active,
                                          PlayerRegistered = !string.IsNullOrEmpty(player.ApplicationUserId),
                                          NemesisPlayerId = player.Nemesis.NemesisPlayerId,
                                          NemesisPlayerName = player.Nemesis.NemesisPlayer.Name,
                                          PreviousNemesisPlayerId =  player.PreviousNemesis.NemesisPlayerId,
                                          PreviousNemesisPlayerName = player.PreviousNemesis.NemesisPlayer.Name,
                                          GamingGroupId = player.GamingGroupId,
                                          GamesWon =
                                              player.PlayerGameResults.Where(
                                                                             x =>
                                                                             x.PlayedGame.DatePlayed >= dateRangeFilter.FromDate &&
                                                                             x.PlayedGame.DatePlayed <= dateRangeFilter.ToDate).Count(x => x.GameRank == 1),
                                          GamesLost =
                                              player.PlayerGameResults.Where(
                                                                             x =>
                                                                             x.PlayedGame.DatePlayed >= dateRangeFilter.FromDate &&
                                                                             x.PlayedGame.DatePlayed <= dateRangeFilter.ToDate).Count(x => x.GameRank > 1),
                                          //only get championed games where this player is the current champion
                                          TotalChampionedGames =
                                              player.ChampionedGames.Count(
                                                                           champion =>
                                                                           champion.GameDefinition.ChampionId != null &&
                                                                           champion.GameDefinition.ChampionId.Value == champion.Id)
                                      }
                                     ).ToList();

            PopulateNemePointsSummary(gamingGroupId, playersWithNemesis, dateRangeFilter);
            PopulateAchivements(playersWithNemesis);

            return playersWithNemesis//--deliberately ToList() first since Linq To Entities cannot support ordering by NemePointsSummary.TotalPoints
                                      .OrderByDescending(x => x.PlayerActive)
                                      .ThenByDescending(pwn => pwn.NemePointsSummary?.TotalPoints ?? 0)
                                      .ThenByDescending(pwn => pwn.GamesWon)
                                      .ThenBy(pwn => pwn.PlayerName)
                                      .ToList();
        }

        private void PopulateAchivements(List<PlayerWithNemesis> playersWithNemesis)
        {
            var playerAchievements = _dataContext.GetQueryable<PlayerAchievement>();
            if (playerAchievements != null)
            {

                foreach (var player in playersWithNemesis)
                {
                    player.AchievementsPerLevel.Add(AchievementLevel.Bronze,
                        playerAchievements.Count(
                            pa => pa.PlayerId == player.PlayerId && pa.AchievementLevel == AchievementLevel.Bronze));
                    player.AchievementsPerLevel.Add(AchievementLevel.Silver,
                        playerAchievements.Count(
                            pa => pa.PlayerId == player.PlayerId && pa.AchievementLevel == AchievementLevel.Silver));
                    player.AchievementsPerLevel.Add(AchievementLevel.Gold,
                        playerAchievements.Count(
                            pa => pa.PlayerId == player.PlayerId && pa.AchievementLevel == AchievementLevel.Gold));
                }
            }
        }


        internal virtual void PopulateNemePointsSummary(int gamingGroupId, List<PlayerWithNemesis> playersWithNemesis, IDateRangeFilter dateRangeFilter)
        {
            var nemePointsDictionary = (from playerGameResult in _dataContext.GetQueryable<PlayerGameResult>()
                                        where playerGameResult.PlayedGame.GamingGroupId == gamingGroupId
                                        && playerGameResult.PlayedGame.DatePlayed >= dateRangeFilter.FromDate
                                        && playerGameResult.PlayedGame.DatePlayed <= dateRangeFilter.ToDate
                                        group playerGameResult by playerGameResult.PlayerId
                                        into groupedResults
                                        select
                                            new
                                            {
                                                BasePoints = groupedResults.Sum(x => x.NemeStatsPointsAwarded),
                                                GameDurationBonusPoints = groupedResults.Sum(x => x.GameDurationBonusPoints),
                                                WeightBonusPoints = groupedResults.Sum(x => x.GameWeightBonusPoints),
                                                PlayerId = groupedResults.Key
                                            }).ToDictionary(key => key.PlayerId, value => new NemePointsSummary(value.BasePoints, value.GameDurationBonusPoints, value.WeightBonusPoints));

            foreach (var player in playersWithNemesis)
            {
                player.NemePointsSummary = nemePointsDictionary.ContainsKey(player.PlayerId) ? nemePointsDictionary[player.PlayerId] : new NemePointsSummary(0, 0, 0);
            }
        }

        public virtual PlayerDetails GetPlayerDetails(int playerId, int numberOfRecentGamesToRetrieve)
        {
            var returnPlayer = _dataContext.GetQueryable<Player>()
                                             .Include(player => player.Nemesis)
                                             .Include(player => player.Nemesis.NemesisPlayer)
                                             .Include(player => player.PreviousNemesis)
                                             .Include(player => player.PreviousNemesis.NemesisPlayer)
                                             .Include(player => player.GamingGroup)
                                             .SingleOrDefault(player => player.Id == playerId);

            ValidatePlayerWasFound(playerId, returnPlayer);

            var playerStatistics = GetPlayerStatistics(playerId);

            var playerGameResults = GetPlayerGameResultsWithPlayedGameAndGameDefinition(playerId, numberOfRecentGamesToRetrieve);

            var minions = GetMinions(returnPlayer.Id);

            var playerGameSummaries = _playerRepository.GetPlayerGameSummaries(playerId, _dataContext);

            var championedGames = GetChampionedGames(returnPlayer.Id);

            var formerChampionedGames = GetFormerChampionedGames(returnPlayer.Id);

            var longestWinningStreak = _playerRepository.GetLongestWinningStreak(playerId, _dataContext);

            var query = new GetRecentPlayerAchievementsUnlockedQuery
            {
                PlayerId = playerId,
                Page = 1,
                PageSize = int.MaxValue
            };
            var recentAchievementsUnlocked = _recentPlayerAchievementsUnlockedRetriever.GetResults(query);

            var playerDetails = new PlayerDetails()
            {
                Active = returnPlayer.Active,
                ApplicationUserId = returnPlayer.ApplicationUserId,
                Id = returnPlayer.Id,
                Name = returnPlayer.Name,
                GamingGroupId = returnPlayer.GamingGroupId,
                GamingGroupName = returnPlayer.GamingGroup.Name,
                PlayerGameResults = playerGameResults,
                PlayerStats = playerStatistics,
                CurrentNemesis = returnPlayer.Nemesis ?? new NullNemesis(),
                PreviousNemesis = returnPlayer.PreviousNemesis ?? new NullNemesis(),
                Minions = minions,
                PlayerGameSummaries = playerGameSummaries,
                ChampionedGames = championedGames,
                PlayerVersusPlayersStatistics = _playerRepository.GetPlayerVersusPlayersStatistics(playerId, _dataContext),
                FormerChampionedGames = formerChampionedGames,
                LongestWinningStreak = longestWinningStreak,
                NemePointsSummary = playerStatistics.NemePointsSummary,
                Achievements = recentAchievementsUnlocked.ToList()
            };

            return playerDetails;
        }


        private static void ValidatePlayerWasFound(int playerId, Player returnPlayer)
        {
            if (returnPlayer == null)
            {
                throw new EntityDoesNotExistException<Player>(playerId);
            }
        }

        internal virtual List<Player> GetMinions(int nemesisPlayerId)
        {
            return (from Player player in _dataContext.GetQueryable<Player>().Include(p => p.Nemesis)
                    where player.Nemesis.NemesisPlayerId == nemesisPlayerId
                    select player).ToList();
        }

        internal virtual List<Champion> GetChampionedGames(int playerId)
        {
            return
                (from GameDefinition gameDefinition in
                     _dataContext.GetQueryable<GameDefinition>().Include(g => g.Champion)
                 where gameDefinition.Champion.PlayerId == playerId
                 select gameDefinition.Champion).Include(c => c.GameDefinition)
                 .ToList();
        }

        internal virtual List<GameDefinition> GetFormerChampionedGames(int playerId)
        {
            return
                (from Champion champion in
                     _dataContext.GetQueryable<Champion>().Include(c => c.GameDefinition)
                 where champion.PlayerId == playerId
                 select champion.GameDefinition)
                 .ToList();
        }

        internal virtual List<PlayerGameResult> GetPlayerGameResultsWithPlayedGameAndGameDefinition(
            int playerId,
            int numberOfRecentGamesToRetrieve)
        {
            var playerGameResults = _dataContext.GetQueryable<PlayerGameResult>()
                        .Where(result => result.PlayerId == playerId)
                        .OrderByDescending(result => result.PlayedGame.DatePlayed)
                        .ThenByDescending(result => result.PlayedGame.Id)
                        .Take(numberOfRecentGamesToRetrieve)
                        .Include(result => result.PlayedGame.GameDefinition.BoardGameGeekGameDefinition)
                        .ToList();
            return playerGameResults;
        }

        public virtual PlayerStatistics GetPlayerStatistics(int playerId)
        {
            var playerStatistics = new PlayerStatistics();
            var gameDefinitionTotals = GetGameDefinitionTotals(playerId);
            playerStatistics.GameDefinitionTotals = gameDefinitionTotals;

            var topLevelTotals = GetTopLevelTotals(gameDefinitionTotals);

            playerStatistics.TotalGames = topLevelTotals.TotalGames;
            playerStatistics.TotalGamesLost = topLevelTotals.TotalGamesLost;
            playerStatistics.TotalGamesWon = topLevelTotals.TotalGamesWon;

            if (playerStatistics.TotalGames > 0)
            {
                playerStatistics.WinPercentage = (int)((decimal)playerStatistics.TotalGamesWon / (playerStatistics.TotalGames) * 100);
            }

            playerStatistics.NemePointsSummary = GetNemePointsSummary(playerId);

            //had to cast to handle the case where there is no data:
            //http://stackoverflow.com/questions/6864311/the-cast-to-value-type-int32-failed-because-the-materialized-value-is-null
            playerStatistics.AveragePlayersPerGame = (float?)_dataContext.GetQueryable<PlayedGame>()
                .Where(playedGame => playedGame.PlayerGameResults.Any(result => result.PlayerId == playerId))
                    .Average(game => (int?)game.NumberOfPlayers) ?? 0F;

            return playerStatistics;
        }

        internal virtual TopLevelTotals GetTopLevelTotals(GameDefinitionTotals gameDefinitionTotals)
        {
            var returnResult = new TopLevelTotals
            {
                TotalGamesLost = gameDefinitionTotals.SummariesOfGameDefinitionTotals.Sum(x => x.GamesLost),
                TotalGamesWon = gameDefinitionTotals.SummariesOfGameDefinitionTotals.Sum(x => x.GamesWon)
            };
            returnResult.TotalGames = returnResult.TotalGamesLost + returnResult.TotalGamesWon;

            return returnResult;
        }

        internal virtual GameDefinitionTotals GetGameDefinitionTotals(int playerId)
        {
            var playerGameSummaries = _playerRepository.GetPlayerGameSummaries(playerId, _dataContext);
            var gameDefinitionTotals = new GameDefinitionTotals
            {
                SummariesOfGameDefinitionTotals = playerGameSummaries.Select(playerGameSummary => new GameDefinitionTotal
                {
                    GameDefinitionId = playerGameSummary.GameDefinitionId,
                    GameDefinitionName = playerGameSummary.GameDefinitionName,
                    GamesLost = playerGameSummary.NumberOfGamesLost,
                    GamesWon = playerGameSummary.NumberOfGamesWon
                }).ToList()
            };
            return gameDefinitionTotals;
        }

        internal class TopLevelTotals
        {
            public int TotalGames { get; internal set; }
            public int TotalGamesLost { get; internal set; }
            public int TotalGamesWon { get; internal set; }
        }

        internal virtual NemePointsSummary GetNemePointsSummary(int playerId)
        {
            var nemePointsSummary = _dataContext.GetQueryable<PlayerGameResult>()
                                          .Where(result => result.PlayerId == playerId)
                                          .GroupBy(x => x.PlayerId)
                                          .Select(
                                                  g =>
                                                  new NemePointsSummary
                                                  {
                                                      //had to cast to handle the case where there is no data:
                                                      //http://stackoverflow.com/questions/6864311/the-cast-to-value-type-int32-failed-because-the-materialized-value-is-null
                                                      BaseNemePoints = g.Sum(x => (int?)x.NemeStatsPointsAwarded) ?? 0,
                                                      GameDurationBonusNemePoints = g.Sum(x => (int?)x.GameDurationBonusPoints) ?? 0,
                                                      WeightBonusNemePoints = g.Sum(x => (int?)x.GameWeightBonusPoints) ?? 0
                                                  })
                                .SingleOrDefault();
            return nemePointsSummary ?? new NemePointsSummary(0, 0, 0);
        }

        public virtual Player GetPlayerForCurrentUser(string applicationUserId, int gamingGroupId)
        {
            return (from player in _dataContext.GetQueryable<Player>()
                    where player.GamingGroupId == gamingGroupId
                     && player.ApplicationUserId == applicationUserId
                    select player)
                    .FirstOrDefault();
        }

        public virtual PlayerQuickStats GetPlayerQuickStatsForUser(string applicationUserId, int gamingGroupId)
        {
            var playerForCurrentUser = GetPlayerForCurrentUser(applicationUserId, gamingGroupId);

            var returnValue = new PlayerQuickStats();

            if (playerForCurrentUser != null)
            {
                returnValue.PlayerId = playerForCurrentUser.Id;
                returnValue.NemePointsSummary = GetNemePointsSummary(playerForCurrentUser.Id);

                var gameDefinitionTotals = GetGameDefinitionTotals(playerForCurrentUser.Id);
                var topLevelTotals = GetTopLevelTotals(gameDefinitionTotals);
                returnValue.TotalGamesPlayed = topLevelTotals.TotalGames;
                returnValue.TotalGamesWon = topLevelTotals.TotalGamesWon;
            }

            return returnValue;
        }

        public PlayersToCreateModel GetPlayersToCreate(string currentUserId, int currentGamingGroupId)
        {
            var allPlayersOrderedByLastDatePlayedThenName = _dataContext.GetQueryable<Player>()
                .Where(player => player.GamingGroupId == currentGamingGroupId && player.Active)

                .OrderByDescending(
                    p => p.PlayerGameResults
                        .Select(pgr => pgr.PlayedGame.DatePlayed)
                        .OrderByDescending(d => d)
                        .FirstOrDefault())
                .ThenBy(p => p.Name)
                .Select(x => new
                {
                    PlayerInfo = new PlayerInfoForUser
                    {
                        GamingGroupId = currentGamingGroupId,
                        PlayerId = x.Id,
                        PlayerName = x.Name
                    },
                    IsCurrentUserPlayer = x.ApplicationUserId == currentUserId
                })
                .ToList();

            var currentUserPlayerResult = allPlayersOrderedByLastDatePlayedThenName.FirstOrDefault(x => x.IsCurrentUserPlayer);

            var recentPlayers = new List<PlayerInfoForUser>();
            var otherPlayers = new List<PlayerInfoForUser>();

            int numberOfPlayers = 0;
            foreach (var playerResult in allPlayersOrderedByLastDatePlayedThenName)
            {
                if (playerResult.IsCurrentUserPlayer)
                {
                    continue;
                }

                if (numberOfPlayers < MAX_NUMBER_OF_RECENT_PLAYERS)
                {
                    recentPlayers.Add(playerResult.PlayerInfo);
                }
                else
                {
                    otherPlayers.Add(playerResult.PlayerInfo);
                }
                numberOfPlayers++;
            }

            var result = new PlayersToCreateModel
            {
                UserPlayer = currentUserPlayerResult?.PlayerInfo,
                OtherPlayers = otherPlayers.OrderBy(x => x.PlayerName).ToList(),
                RecentPlayers = recentPlayers
            };

            return result;
        }

        public PlayersToCreateModel GetPlayersForEditingPlayedGame(int playedGameId, ApplicationUser currentUser)
        {
            var recentPlayers = _dataContext.GetQueryable<PlayerGameResult>()
                .Where(x => x.PlayedGameId == playedGameId)
                .Select(x => new PlayerInfoForUser
                {
                    PlayerId = x.PlayerId,
                    GamingGroupId = x.PlayedGame.GamingGroupId,
                    PlayerName = x.Player.Name
                })
                .OrderBy(x => x.PlayerName)
                .ToList();

            var gamingGroupId = recentPlayers.First().GamingGroupId;
            var playerIdsInGame = recentPlayers.Select(x => x.PlayerId).ToList();

            var otherPlayers = _dataContext.GetQueryable<Player>()
                .Where(x => x.Active
                            && x.GamingGroupId == gamingGroupId
                            && !playerIdsInGame.Contains(x.Id))
                .Select(x => new PlayerInfoForUser
                {
                    PlayerId = x.Id,
                    GamingGroupId = x.GamingGroupId,
                    PlayerName = x.Name
                })
                .OrderBy(x => x.PlayerName)
                .ToList();

            var result = new PlayersToCreateModel
            {
                RecentPlayers = recentPlayers,
                OtherPlayers = otherPlayers,
                UserPlayer = null
            };

            return result;
        }
    }
}
