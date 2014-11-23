﻿using System;
using System.Collections.Generic;
using System.Linq;
using BusinessLogic.Models.Games;
using UI.Models.PlayedGame;

namespace UI.Models.GameDefinitionModels
{
    public class GameDefinitionDetailsViewModel : GameDefinitionSummaryViewModel
    {
        public IList<PlayedGameDetailsViewModel> PlayedGames { get; set; }
    }
}