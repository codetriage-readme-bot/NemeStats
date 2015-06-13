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
using BusinessLogic.Models;
using BusinessLogic.Models.Players;
using BusinessLogic.Models.User;
using NUnit.Framework;
using Rhino.Mocks;
using System;
using System.Collections.Generic;
using System.Linq;
using UI.Models;
using UI.Models.Players;
using UI.Transformations;
using UI.Transformations.PlayerTransformations;

namespace UI.Tests.UnitTests.TransformationsTests.PlayerTransformationTests.PlayerDetailsViewModelBuilderTests
{
    [TestFixture]
    public class BuildTests
    {
        private IGameResultViewModelBuilder gameResultViewModelBuilder;
        private IMinionViewModelBuilder minionViewModelBuilderMock;
        private IChampionViewModelBuilder championViewModelBuilderMock;
        private PlayerDetails playerDetails;
        private PlayerDetailsViewModel playerDetailsViewModel;
        private PlayerDetailsViewModelBuilder builder;
        private ApplicationUser currentUser;
        private PlayerVersusPlayerStatistics normalPlayer;
        private PlayerVersusPlayerStatistics nemesisPlayer;
        private PlayerVersusPlayerStatistics minionPlayer;
        private PlayerVersusPlayerStatistics playerWithNoGamesPlayed;
        private string twitterMinionBraggingUrl = "some url";
        private int gamingGroupId = 123;

        [SetUp]
        public void TestFixtureSetUp()
        {
            minionViewModelBuilderMock = MockRepository.GenerateMock<IMinionViewModelBuilder>();

            currentUser = new ApplicationUser()
            {
                CurrentGamingGroupId = gamingGroupId,
                Id = "application user id"
            };

            var champion = new Champion();
            var gameDefinition1 = new GameDefinition()
            {
                Name = "test game 1",
                Id = 1,
                Champion = champion
            };
            var playedGame1 = new PlayedGame()
            {
                Id = 1,
                GameDefinition = gameDefinition1
            };
            var gameDefinition2 = new GameDefinition()
            {
                Name = "test game 2",
                Id = 2
            };
            var playedGame2 = new PlayedGame()
            {
                Id = 2,
                GameDefinition = gameDefinition2
            };
            var playerGameResults = new List<PlayerGameResult>()
            {
                new PlayerGameResult(){ PlayedGameId = 12, PlayedGame = playedGame1 },
                new PlayerGameResult(){ PlayedGameId = 13, PlayedGame = playedGame2 }
            };

            normalPlayer = new PlayerVersusPlayerStatistics
            {
                OpposingPlayerName = "Jim",
                OpposingPlayerId = 1,
                NumberOfGamesWonVersusThisPlayer = 10,
                NumberOfGamesPlayedVersusThisPlayer = 20
            };

            playerWithNoGamesPlayed = new PlayerVersusPlayerStatistics
            {
                OpposingPlayerId = 2,
                NumberOfGamesPlayedVersusThisPlayer = 0
            };

            minionPlayer = new PlayerVersusPlayerStatistics
            {
                OpposingPlayerName = "Minion Player",
                OpposingPlayerId = 5,
                NumberOfGamesWonVersusThisPlayer = 20,
                NumberOfGamesPlayedVersusThisPlayer = 20
            };

            nemesisPlayer = new PlayerVersusPlayerStatistics
            {
                OpposingPlayerName = "nemesis player",
                OpposingPlayerId = 3,
                NumberOfGamesWonVersusThisPlayer = 0,
                NumberOfGamesPlayedVersusThisPlayer = 100
            };

            var nemesis = new Nemesis()
            {
                NemesisPlayerId = nemesisPlayer.OpposingPlayerId,
                NumberOfGamesLost = 3,
                LossPercentage = 75,
                NemesisPlayer = new Player()
                {
                    Name = "Ace Nemesis",
                }
            };

            playerDetails = new PlayerDetails()
            {
                Id = 134,
                ApplicationUserId = currentUser.Id,
                Active = true,
                Name = "Skipper",
                PlayerGameResults = playerGameResults,
                PlayerStats = new PlayerStatistics() 
                    { 
                        TotalGames = 71,
                        TotalPoints = 150,
                        AveragePlayersPerGame = 3
                    },
                CurrentNemesis = nemesis,
                Minions = new List<Player>
                {
                    new Player
                    {
                        Id = minionPlayer.OpposingPlayerId
                    }
                },
                GamingGroupId = gamingGroupId,
                GamingGroupName = "gaming group name",
                PlayerVersusPlayersStatistics = new List<PlayerVersusPlayerStatistics>
                {
                    normalPlayer,
                    playerWithNoGamesPlayed,
                    nemesisPlayer,
                    minionPlayer
                }
            };

            gameResultViewModelBuilder
                = MockRepository.GenerateMock<IGameResultViewModelBuilder>();
            gameResultViewModelBuilder.Expect(build => build.Build(playerGameResults[0]))
                    .Repeat
                    .Once()
                    .Return(new Models.PlayedGame.GameResultViewModel() { PlayedGameId = playerGameResults[0].PlayedGameId });
            gameResultViewModelBuilder.Expect(build => build.Build(playerGameResults[1]))
                    .Repeat
                    .Once()
                    .Return(new Models.PlayedGame.GameResultViewModel() { PlayedGameId = playerGameResults[1].PlayedGameId });
            foreach (var player in playerDetails.Minions)
            {
                minionViewModelBuilderMock.Expect(mock => mock.Build(player))
                    .Return(new MinionViewModel() { MinionPlayerId = player.Id });
            }

            championViewModelBuilderMock = MockRepository.GenerateMock<IChampionViewModelBuilder>();
            var championedGames = new List<Champion>
            {
                new Champion { GameDefinition = gameDefinition1 }
            };
            playerDetails.ChampionedGames = championedGames;

            championViewModelBuilderMock.Expect(mock => mock.Build(Arg<Champion>.Is.Anything))
                .Return(new ChampionViewModel { GameDefinitionName = gameDefinition1.Name });

            builder = new PlayerDetailsViewModelBuilder(gameResultViewModelBuilder, minionViewModelBuilderMock, championViewModelBuilderMock);

            playerDetailsViewModel = builder.Build(playerDetails, twitterMinionBraggingUrl, currentUser);
        }

        [Test]
        public void PlayerDetailsCannotBeNull()
        {
            var builder = new PlayerDetailsViewModelBuilder(null, null, null);

            var exception = Assert.Throws<ArgumentNullException>(() =>
                    builder.Build(null, twitterMinionBraggingUrl, currentUser));

            Assert.AreEqual("playerDetails", exception.ParamName);
        }

        [Test]
        public void ItRequiresPlayerGameResults()
        {
            var builder = new PlayerDetailsViewModelBuilder(null, null, null);

            var exception = Assert.Throws<ArgumentException>(() =>
                    builder.Build(new PlayerDetails(), twitterMinionBraggingUrl, currentUser));

            Assert.AreEqual(PlayerDetailsViewModelBuilder.EXCEPTION_PLAYER_GAME_RESULTS_CANNOT_BE_NULL, exception.Message);
        }

        [Test]
        public void ItRequiresPlayerStatistics()
        {
            var builder = new PlayerDetailsViewModelBuilder(null, null, null);
            var playerDetailsWithNoStatistics = new PlayerDetails() { PlayerGameResults = new List<PlayerGameResult>() };
            var exception = Assert.Throws<ArgumentException>(() =>
                    builder.Build(playerDetailsWithNoStatistics, twitterMinionBraggingUrl, currentUser));

            Assert.AreEqual(PlayerDetailsViewModelBuilder.EXCEPTION_PLAYER_STATISTICS_CANNOT_BE_NULL, exception.Message);
        }

        [Test]
        public void MinionsCannotBeNull()
        {
            var builder = new PlayerDetailsViewModelBuilder(null, null, null);
            var playerDetailsWithNoMinions = new PlayerDetails() { PlayerGameResults = new List<PlayerGameResult>() };
            playerDetailsWithNoMinions.PlayerStats = new PlayerStatistics();

            var exception = Assert.Throws<ArgumentException>(() =>
                    builder.Build(playerDetailsWithNoMinions, twitterMinionBraggingUrl, currentUser));

            Assert.AreEqual(PlayerDetailsViewModelBuilder.EXCEPTION_MINIONS_CANNOT_BE_NULL, exception.Message);
        }

        [Test]
        public void ChampionedGamesCannotBeNull()
        {
            var builder = new PlayerDetailsViewModelBuilder(null, null, null);
            var playerDetailsWithNoChampionedGames = new PlayerDetails()
            {
                PlayerGameResults = new List<PlayerGameResult>(),
                PlayerStats = new PlayerStatistics(),
                Minions = new List<Player>()
            };

            var exception = Assert.Throws<ArgumentException>(() =>
                    builder.Build(playerDetailsWithNoChampionedGames, twitterMinionBraggingUrl, currentUser));

            Assert.AreEqual(PlayerDetailsViewModelBuilder.EXCEPTION_CHAMPIONED_GAMES_CANNOT_BE_NULL, exception.Message);
        }

        [Test]
        public void ItCopiesThePlayerId()
        {
            Assert.AreEqual(playerDetails.Id, playerDetailsViewModel.PlayerId);
        }

        [Test]
        public void ItCopiesThePlayerName()
        {
            Assert.AreEqual(playerDetails.Name, playerDetailsViewModel.PlayerName);
        }

        [Test]
        public void ItSetsThePlayerRegisteredFlagToTrueIfThereIsAnApplicationUserIdOnThePlayer()
        {
            Assert.AreEqual(true, playerDetailsViewModel.PlayerRegistered);
        }

        [Test]
        public void ItSetsThePlayerRegisteredFlagToFalseIfThereIsNoApplicationUserIdOnThePlayer()
        {
            playerDetails.ApplicationUserId = null;
            playerDetailsViewModel = builder.Build(playerDetails, twitterMinionBraggingUrl, currentUser);

            Assert.AreEqual(false, playerDetailsViewModel.PlayerRegistered);
        }

        [Test]
        public void ItCopiesTheActiveFlag()
        {
            Assert.AreEqual(playerDetails.Active, playerDetailsViewModel.Active);
        }

        [Test]
        public void ItCopiesTheTotalGamesPlayed()
        {
            Assert.AreEqual(playerDetails.PlayerStats.TotalGames, playerDetailsViewModel.TotalGamesPlayed);
        }

        [Test]
        public void ItCopiesTheGamingGroupName()
        {
            Assert.AreEqual(playerDetails.GamingGroupId, playerDetailsViewModel.GamingGroupId);
        }

        [Test]
        public void ItCopiesTheGamingGroupId()
        {
            Assert.AreEqual(playerDetails.GamingGroupName, playerDetailsViewModel.GamingGroupName);
        }

        [Test]
        public void ItCopiesTheTotalPoints()
        {
            Assert.AreEqual(playerDetails.PlayerStats.TotalPoints, playerDetailsViewModel.TotalPoints);
        }

        [Test]
        public void ItSetsTheAveragePointsPerGame()
        {
            var expectedPoints = (float)playerDetails.PlayerStats.TotalPoints / (float)playerDetails.PlayerStats.TotalGames;

            Assert.AreEqual(expectedPoints, playerDetailsViewModel.AveragePointsPerGame);
        }

        [Test]
        public void ItSetsTheAveragePointsPerGameToZeroIfNoGamesHaveBeenPlayed()
        {
            playerDetails.PlayerStats.TotalGames = 0;

            playerDetailsViewModel = builder.Build(playerDetails, twitterMinionBraggingUrl, currentUser);

            Assert.AreEqual(0, playerDetailsViewModel.AveragePointsPerGame);
        }

        [Test]
        public void ItSetsTheAveragePlayersPerGame()
        {
            Assert.AreEqual(playerDetails.PlayerStats.AveragePlayersPerGame, playerDetailsViewModel.AveragePlayersPerGame);
        }

        [Test]
        public void ItSetsTheAveragePointsPerPlayer()
        {
            var expectedPointsPerGame = (float)playerDetails.PlayerStats.TotalPoints / (float)playerDetails.PlayerStats.TotalGames;
            var expectedPointsPerPlayer = expectedPointsPerGame / (float)playerDetails.PlayerStats.AveragePlayersPerGame;

            Assert.AreEqual(expectedPointsPerPlayer, playerDetailsViewModel.AveragePointsPerPlayer);
        }

        [Test]
        public void ItSetsTheAveragePointsPerPlayerToZeroIfTheAveragePlayersPerGameIsZero()
        {
            playerDetails.PlayerStats.AveragePlayersPerGame = 0;

            var viewModel = builder.Build(playerDetails, twitterMinionBraggingUrl, currentUser);

            Assert.AreEqual(0, viewModel.AveragePointsPerPlayer);
        }

        [Test]
        public void ItPopulatesThePlayerGameSummaries()
        {
            var numberOfPlayerGameResults = playerDetails.PlayerGameResults.Count();
            int expectedPlayedGameId;
            int actualPlayedGameId;
            for(var i = 0; i < numberOfPlayerGameResults; i++)
            {
                expectedPlayedGameId = playerDetails.PlayerGameResults[i].PlayedGameId;
                actualPlayedGameId = playerDetailsViewModel.PlayerGameResultDetails[i].PlayedGameId;
                Assert.AreEqual(expectedPlayedGameId, actualPlayedGameId);
            }
        }

        [Test]
        public void ItPopulatesTheHasNemesisFlagIfTheNemesisIsNotNull()
        {
            Assert.IsTrue(playerDetailsViewModel.HasNemesis);
        }

        [Test]
        public void ItPopulatesTheNemesisPlayerId()
        {
            Assert.AreEqual(playerDetails.CurrentNemesis.NemesisPlayerId, playerDetailsViewModel.NemesisPlayerId);
        }

        [Test]
        public void ItPopulatesTheNemesisName()
        {
            Assert.AreEqual(playerDetails.CurrentNemesis.NemesisPlayer.Name, playerDetailsViewModel.NemesisName);
        }

        [Test]
        public void ItPopulatesTheGamesLostVersusTheNemesis()
        {
            Assert.AreEqual(playerDetails.CurrentNemesis.NumberOfGamesLost, playerDetailsViewModel.NumberOfGamesLostVersusNemesis);
        }

        [Test]
        public void ItPopulatesTheLostPercentageVersusTheNemesis()
        {
            Assert.AreEqual(playerDetails.CurrentNemesis.LossPercentage, playerDetailsViewModel.LossPercentageVersusPlayer);
        }

        [Test]
        public void ItSetsTheMinions()
        {
            foreach(var player in playerDetails.Minions)
            {
                Assert.True(playerDetailsViewModel.Minions.Any(minion => minion.MinionPlayerId == player.Id));
            }
        }

        [Test]
        public void TheUserCanEditViewModelIfTheyShareGamingGroups()
        {
            var viewModel = builder.Build(playerDetails, twitterMinionBraggingUrl, currentUser);

            Assert.True(viewModel.UserCanEdit);
        }

        [Test]
        public void TheUserCanNotEditViewModelIfTheyDoNotShareGamingGroups()
        {
            currentUser.CurrentGamingGroupId = -1;
            var viewModel = builder.Build(playerDetails, twitterMinionBraggingUrl, currentUser);

            Assert.False(viewModel.UserCanEdit);
        }

        [Test]
        public void TheUserCanNotEditViewModelIfTheUserIsUnknown()
        {
            var viewModel = builder.Build(playerDetails, twitterMinionBraggingUrl, null);

            Assert.False(viewModel.UserCanEdit);
        }

        [Test]
        public void ItCopiesThePlayerGameSummaries()
        {
            Assert.AreEqual(playerDetails.PlayerGameSummaries, playerDetailsViewModel.PlayerGameSummaries);
        }

        [Test]
        public void ItSetsTheChampionedGames()
        {
            for (var i = 0; i < playerDetailsViewModel.ChampionedGames.Count; i++)
            {
                Assert.That(playerDetailsViewModel.ChampionedGames[i].GameDefinitionName, 
                    Is.EqualTo(playerDetails.ChampionedGames[i].GameDefinition.Name));
            }
        }

        [Test]
        public void ItSetsTheTwitterBraggingUrlIfTCurrentUserIsLookingAtThemself()
        {
            Assert.That(twitterMinionBraggingUrl, Is.EqualTo(playerDetailsViewModel.MinionBraggingTweetUrl));
        }

        [Test]
        public void ItDoesNotSetTheTwitterBraggingUrlIfTCurrentUserIsNotThePlayerBeingTransformed()
        {
            currentUser.Id = "some different user id";
            var viewModel = builder.Build(playerDetails, twitterMinionBraggingUrl, currentUser);

            Assert.That(null, Is.EqualTo(viewModel.MinionBraggingTweetUrl));
        }

        [Test]
        public void ItPopulatesTheOpposingPlayerId()
        {
            Assert.That(playerDetailsViewModel.PlayerVersusPlayers.OpposingPlayers.Any(opposingPlayer => opposingPlayer.PlayerId == normalPlayer.OpposingPlayerId));
        }

        [Test]
        public void ItPopulatesTheOpposingPlayerName()
        {
            var expectedPlayer = playerDetails.PlayerVersusPlayersStatistics.First(x => x.OpposingPlayerId == normalPlayer.OpposingPlayerId);
            var actualPlayer = playerDetailsViewModel.PlayerVersusPlayers.OpposingPlayers.First(x => x.PlayerId == normalPlayer.OpposingPlayerId);

            Assert.That(actualPlayer.Name,
                Is.EqualTo(expectedPlayer.OpposingPlayerName));
        }

        [Test]
        public void ItPopulatesTheNumberOfGamesPlayedVersusThisPlayer()
        {
            var expectedPlayer = playerDetails.PlayerVersusPlayersStatistics.First(x => x.OpposingPlayerId == normalPlayer.OpposingPlayerId);
            var actualPlayer = playerDetailsViewModel.PlayerVersusPlayers.OpposingPlayers.First(x => x.PlayerId == normalPlayer.OpposingPlayerId);

            Assert.That(actualPlayer.NumberOfGamesPlayedVersusThisPlayer,
                Is.EqualTo(expectedPlayer.NumberOfGamesPlayedVersusThisPlayer));
        }

        [Test]
        public void ItPopulatesTheNumberOfGamesWonVersusThisPlayer()
        {
            var expectedPlayer = playerDetails.PlayerVersusPlayersStatistics.First(x => x.OpposingPlayerId == normalPlayer.OpposingPlayerId);
            var actualPlayer = playerDetailsViewModel.PlayerVersusPlayers.OpposingPlayers.First(x => x.PlayerId == normalPlayer.OpposingPlayerId);

            Assert.That(actualPlayer.NumberOfGamesWonVersusThisPlayer, Is.EqualTo(expectedPlayer.NumberOfGamesWonVersusThisPlayer));
        }

        [Test]
        public void ItPopulatesTheWinPercentageVersusThisPlayer()
        {
            var actualPlayer = playerDetailsViewModel.PlayerVersusPlayers.OpposingPlayers.First(x => x.PlayerId == normalPlayer.OpposingPlayerId);

            Assert.That(actualPlayer.WinPercentageVersusThisPlayer, Is.EqualTo(50));
        }

        [Test]
        public void TheWinPercentageIsZeroIfThereAreNoPlayedGamesVersusThisPlayer()
        {
            var actualPlayer = playerDetailsViewModel.PlayerVersusPlayers.OpposingPlayers.First(x => x.PlayerId == playerWithNoGamesPlayed.OpposingPlayerId);

            Assert.That(actualPlayer.WinPercentageVersusThisPlayer, Is.EqualTo(0));
        }

        [Test]
        public void ItSetsTheNemesisFlag()
        {
            var actualPlayer = playerDetailsViewModel.PlayerVersusPlayers.OpposingPlayers.First(x => x.PlayerId == nemesisPlayer.OpposingPlayerId);

            Assert.That(actualPlayer.IsNemesis, Is.True);
        }


        [Test]
        public void ItSetsTheMinionFlag()
        {
            var actualPlayer = playerDetailsViewModel.PlayerVersusPlayers.OpposingPlayers.First(x => x.PlayerId == minionPlayer.OpposingPlayerId);

            Assert.That(actualPlayer.IsMinion, Is.True);
        }
    }
}
