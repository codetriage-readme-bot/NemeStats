﻿    //Usings
    Namespace("Views.PlayedGame");

    //Initialization
    Views.PlayedGame.CreatePlayedGame = function () {
        this._playerRank = 1;
        this._playerIndex = 0;
        this.$recordPlayedGameForm = null;
        this.$rankedPlayers = null;
        this.$rankedPlayersListItem = null;
        this.$playerId = null;
        this.$players = null;
        this.$playersErrorContainer = null;
        this.$playerFormData = null;
        this.$btnAddPlayer = null;
        this.$addPlayer = null;
        this.$datePicker = null;
        this.$playerItemTemplate = null;
        this._googleAnalytics = null;
    };

    //Implementation
    Views.PlayedGame.CreatePlayedGame.prototype = {

        //Method definitions
        init: function (gaObject) {
            //Fields
            var parent = this;
            this.$recordPlayedGameForm = $("#recordPlayedGame");
            this.$playersErrorContainer = $("#players-error");
            this.$rankedPlayers = $("#rankedPlayers");
            this.$players = $("#Players");
            this.$playerFormData = $("#playerFormData");
            this.$playerDiv = $("#playerDiv");
            this.$addPlayer = $("#addPlayer");
            this.$btnAddPlayer = $("#btnAddPlayer");
            this.$anchorAddPlayer = $("#addPlayerAnchor");
            this.$datePicker = $(".date-picker").datepicker({
                showOn: "button",
                buttonText: "<i class='fa fa-calendar'></i>",
                showButtonPanel: true,
                maxDate: new Date(),
                minDate: new Date(2014, 1, 1)
            }).datepicker("setDate", new Date());
            this._googleAnalytics = gaObject;

            this.$recordPlayedGameForm.on("submit", $.proxy(parent.validatePlayers, parent));

            //Event handlers
            this.$players.change(function () { parent.addPlayer(); });
            this.$rankedPlayers.sortable({
                stop: function() {
                    parent.onReorder();
                }
            });

            this.$btnAddPlayer.on("click", function() {
                if (parent.$addPlayer.hasClass("hidden")) {
                    parent.$addPlayer.removeClass("hidden");
                } else {
                    parent.$addPlayer.addClass("hidden");
                }
                document.location = parent.$anchorAddPlayer.attr("href");
                parent._googleAnalytics.trackGAEvent("PlayedGame", "AddNewPlayerClicked", "AddNewPlayerClicked");
            });

            this.$playerItemTemplate = $("#player-item-template");
        },
        onReorder: function () {
            var parent = this;

            this.$rankedPlayersListItem = $("#rankedPlayers li");
            this.$rankedPlayersListItem.each(function (index, value) {
                var listItem = $(value);
                var playerId = listItem.data("playerid");
                var rank = index + 1;
                $("#" + playerId).val(rank);

                var playerName = listItem.data("playername");
                listItem.html(parent.generatePlayerRankListItemString(index, playerId, playerName, rank));
            });
            this._googleAnalytics.trackGAEvent("PlayedGame", "PlayersReordered", "PlayersReordered");
        },
        generatePlayerRankListItemString: function (playerIndex, playerId, playerName, playerRank) {

            var template = Handlebars.compile(this.$playerItemTemplate.html());
            var context = { playerIndex: playerIndex, playerId: playerId, playerName: playerName, playerRank: playerRank };

            return template(context);
        },
        addPlayer: function () {
            var parent = this;
            var selectedOption = this.$players.find(":selected");
            this.$playersErrorContainer.addClass("hidden");

            if (selectedOption.text() == "Add A Player") {
                return alert("You must pick a player.");
            }

            var playerId = selectedOption.val();
            var playerName = selectedOption.text();
            var playerItem = "<li id='li" + playerId +
                              "' data-playerid='" + playerId +
                              "' data-playername='" + playerName +
                              "'>" + this.generatePlayerRankListItemString(this._playerIndex, playerId, playerName, this._playerRank) + "</li>";

            this.$rankedPlayers.append(playerItem);
            this._playerIndex++;
            this._playerRank++;
            selectedOption.remove();

            var removePlayerButtons = $(".btnRemovePlayer");
            removePlayerButtons.off('click').on("click", function () {
                parent.removePlayer(this);
            });

            return null;
        },
        removePlayer: function (data) {
        	var playerId = $(data).data("playerid");
        	$("#li" + playerId).remove();
            this._playerRank--;
        	this._googleAnalytics.trackGAEvent("PlayedGame", "PlayerRemoved", "PlayerRemoved");
        },
        onPlayerCreated: function (player) {
            var newPlayer = $('<option value="' + player.Id + '">' + player.Name + '</option>');
            this.$players.append(newPlayer);

            this._googleAnalytics.trackGAEvent("PlayedGame", "NewPlayerAdded", "NewPlayerAdded");
        },

        validatePlayers: function (event) {
            if (this.$rankedPlayers.children().length < 2) {
                this.$playersErrorContainer.removeClass("hidden");
                return false;
            }

            return true;
        },

        //Properties
        set_playerIndex: function (value) {
            this._playerIndex = value;
        },
        get_playerIndex: function () {
            return this._playerIndex;
        },
        set_playerRank: function (value) {
            this._playerRank = value;
        },
        get_playerRank: function () {
            return this._playerRank;
        }
    }; //end prototypes

