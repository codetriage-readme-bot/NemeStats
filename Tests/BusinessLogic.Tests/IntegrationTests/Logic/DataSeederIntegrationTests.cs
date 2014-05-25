﻿using BusinessLogic.DataAccess;
using BusinessLogic.Logic;
using BusinessLogic.Models;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Core;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLogic.Tests.IntegrationTests.Logic.DataSeederIntegrationTests
{
    [TestFixture]
    public class NerdScorekeeperInitializerIntegrationTests
    {
        private NerdScorekeeperDbContext dbContext;

        [TestFixtureSetUp]
        public void SetUp()
        {
            dbContext = new NerdScorekeeperDbContext();
            new DataSeeder(dbContext).SeedData();
        }

        [Test]
        public void ItCreatesThreeGameDefinitions()
        {
            Assert.IsTrue(dbContext.GameDefinitions.Count() >= 3, "No GameDefinitions were created.");
        }

        [Test]
        public void ItCreatesThreePlayedGames()
        {
            List<PlayedGame> playedGames = dbContext.PlayedGames.ToList<PlayedGame>();
            Assert.GreaterOrEqual(dbContext.PlayedGames.Count(), 3, "Not enough played games were created.");
        }

        [Test]
        public void ItCreatesFivePlayers()
        {
            Assert.IsTrue(dbContext.Players.Count() >= 5, "No players were created.");
        }

        [Test]
        [ExpectedException(typeof(DbUpdateException))]
        public void ItDoesntAllowDuplicatePlayerNames()
        {
            dbContext.Players.Add(new Player() { Name = DataSeeder.DAVE_PLAYER_NAME });
            dbContext.SaveChanges();
        }

        [Test]

        [TestFixtureTearDown]
        public void TearDown()
        {
            dbContext.Dispose();
        }
    }
}
