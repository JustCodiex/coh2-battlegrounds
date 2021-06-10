﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

using Battlegrounds;
using Battlegrounds.Game;
using Battlegrounds.Game.DataCompany;
using Battlegrounds.Game.Gameplay;
using Battlegrounds.Game.Match;
using Battlegrounds.Networking.Lobby;
using Battlegrounds.Online.Lobby;

using BattlegroundsApp.LocalData;
using BattlegroundsApp.Views.ViewComponent;

namespace BattlegroundsApp.Models {

    public enum LobbyTeamType {
        Observers = 0,
        Allies = 1,
        Axis = 2
    }

    public class LobbyTeamManagementModel {

        public const int MAXTEAMPLAYERCOUNT = 4;

        private Dictionary<LobbyTeamType, TeamPlayerCard[]> m_teamSetup;

        private LobbyHandler m_handler;
        private TeamPlayerCard m_selfCard;

        public event Action<LobbyTeamType, TeamPlayerCard, object, string> OnTeamEvent;

        public LobbyTeamManagementModel(TeamPlayerCard[][] teamPlayerCards, LobbyHandler lobbyHandler) {

            // Set the handler
            this.m_handler = lobbyHandler;

            // Prepare team grid
            this.m_teamSetup = new Dictionary<LobbyTeamType, TeamPlayerCard[]>() {
                [LobbyTeamType.Observers] = teamPlayerCards[0],
                [LobbyTeamType.Allies] = teamPlayerCards[1],
                [LobbyTeamType.Axis] = teamPlayerCards[2],
            };

            // Init cards
            foreach (KeyValuePair<LobbyTeamType, TeamPlayerCard[]> kvp in this.m_teamSetup) {
                for (int i = 0; i < kvp.Value.Length; i++) {
                    kvp.Value[i].Init(this.m_handler, kvp.Key, i);
                    kvp.Value[i].RequestFullRefresh = x => this.RefreshCard(x, x.TeamSlot, x.TeamType);
                }
            }

        }

        public void SetMaxPlayers(int count) {

            if (count is < 0 or > MAXTEAMPLAYERCOUNT) {
                return;
            }

            for (int i = 0; i < MAXTEAMPLAYERCOUNT; i++) {
                this.m_teamSetup[LobbyTeamType.Allies][i].Visibility = i < (count / 2) ? Visibility.Visible : Visibility.Hidden;
                this.m_teamSetup[LobbyTeamType.Axis][i].Visibility = i < (count / 2) ? Visibility.Visible : Visibility.Hidden;
            }

        }

        public void SetMaxObservers(int count) {

            if (count is < 0 or > MAXTEAMPLAYERCOUNT) {
                return;
            }

            for (int i = 0; i < MAXTEAMPLAYERCOUNT; i++) {
                this.m_teamSetup[LobbyTeamType.Observers][i].Visibility = i < count ? Visibility.Visible : Visibility.Hidden;
            }

        }

        private ILobbyTeam GetLobbyTeamFromType(LobbyTeamType lobbyTeamType) => lobbyTeamType switch {
            LobbyTeamType.Allies => this.m_handler.Lobby.AlliesTeam,
            LobbyTeamType.Axis => this.m_handler.Lobby.AxisTeam,
            LobbyTeamType.Observers => this.m_handler.Lobby.SpectatorTeam,
            _ => throw new Exception()
        };

        public void RefreshAll(bool refreshObservers) {
            this.RefreshTeam(LobbyTeamType.Allies);
            this.RefreshTeam(LobbyTeamType.Axis);
            if (refreshObservers) {
                this.RefreshTeam(LobbyTeamType.Observers);
            }
        }

        public void RefreshTeam(LobbyTeamType teamType) {
            ILobbyTeam team = this.GetLobbyTeamFromType(teamType);
            for (int i = 0; i < MAXTEAMPLAYERCOUNT; i++) {
                this.RefreshCard(this.m_teamSetup[teamType][i], team.GetSlotAt(i), teamType);
            }
        }

        public void RefreshCard(TeamPlayerCard playerCard, ILobbyTeamSlot slot, LobbyTeamType teamType) {

            playerCard.IsAllies = teamType == LobbyTeamType.Allies;

            if (slot.SlotState == LobbyTeamSlotState.OPEN) {

                playerCard.SetCardState(TeamPlayerCard.OPENSTATE);

            } else if (slot.SlotState == LobbyTeamSlotState.LOCKED) {

                playerCard.SetCardState(TeamPlayerCard.OPENSTATE);

            } else if (slot.SlotState == LobbyTeamSlotState.OCCUPIED) {

                // Get the occupant
                ILobbyMember occupant = slot.SlotOccupant;
                IAILobbyMember ai = occupant as IAILobbyMember;
                bool isAI = ai is not null;

                // Determine viewstate
                if (teamType == LobbyTeamType.Observers) {
                    playerCard.SetCardState(TeamPlayerCard.OBSERVERSTATE);
                } else {
                    if (this.m_handler.IsHost && isAI) {
                        playerCard.SetCardState(TeamPlayerCard.AISTATE);
                    } else {
                        playerCard.SetCardState(occupant.Equals(this.m_handler.Self) ? TeamPlayerCard.SELFSTATE : TeamPlayerCard.OCCUPIEDSTATE);
                    }
                }

                // Set player visual data
                playerCard.Playername = isAI ? ((AIDifficulty)ai.Difficulty).GetIngameDisplayName() : occupant.Name;
                playerCard.Playercompany = occupant.CompanyName;
                playerCard.Playerarmy = occupant.Army;

                // Triger value update
                playerCard.RefreshVisualProperty(nameof(playerCard.Playername));
                playerCard.RefreshVisualProperty(nameof(playerCard.Playercompany));

                // If self, make refresh army and company data
                if (playerCard.CardState is TeamPlayerCard.SELFSTATE or TeamPlayerCard.AISTATE && teamType != LobbyTeamType.Observers) {
                    playerCard.RefreshArmyData();
                    playerCard.OnFactionChangedHandle = this.SelfChangedArmy;
                    playerCard.OnCompanyChangedHandle = this.SelfChangedCompany;
                    this.m_selfCard = playerCard;
                } else {
                    if (this.m_handler.IsHost && isAI) {
                        playerCard.OnFactionChangedHandle = x => this.AIChangedArmy(ai, x);
                        playerCard.OnCompanyChangedHandle = x => this.AIChangedCompany(ai, x);
                    } else {
                        playerCard.OnCompanyChangedHandle = null;
                        playerCard.OnFactionChangedHandle = null;
                    }
                }

            }

        }

        private void SelfChangedCompany(TeamPlayerCompanyItem companyItem) {
            if (companyItem is not null) {
                this.m_handler.Lobby.Self.SetCompany(companyItem.Name, companyItem.Strength);
            }
        }

        private void SelfChangedArmy(TeamPlayerArmyItem armyItem) {
            if (armyItem is not null && Faction.FromName(armyItem.Name) is Faction faction) {
                this.m_handler.Lobby.Self.SetArmy(faction.Name);
                this.m_selfCard.RefreshCompanyData();
            }
        }

        private void AIChangedCompany(IAILobbyMember lobbyAIMember, TeamPlayerCompanyItem companyItem) {

        }

        private void AIChangedArmy(IAILobbyMember lobbyAIMember, TeamPlayerArmyItem armyItem) {

        }

        private PlayercardCompanyItem CreateCompanyFromOccupant(ManagedLobbyMember member) {
            if (member is HumanLobbyMember human) {
                if (!string.IsNullOrEmpty(human.CompanyName) && human.CompanyName.CompareTo("NULL") != 0) {
                    return new PlayercardCompanyItem(CompanyItemState.Company, human.CompanyName, human.CompanyStrength);
                } else {
                    return new PlayercardCompanyItem(CompanyItemState.None, "NULL");
                }
            } else if (member is AILobbyMember ai) {
                if (!string.IsNullOrEmpty(ai.CompanyName) && ai.CompanyName.CompareTo("NULL") != 0) {
                    return new PlayercardCompanyItem(CompanyItemState.Company, ai.CompanyName, ai.CompanyStrength);
                } else if (ai.CompanyName.CompareTo("AUGEN") == 0) {
                    return new PlayercardCompanyItem(CompanyItemState.Generate, "AUGEN");
                } else {
                    return new PlayercardCompanyItem(CompanyItemState.None, "NULL");
                }
            } else {
                throw new NotImplementedException();
            }
        }

        public List<SessionParticipant> GetParticipants(ManagedLobbyTeamType team) {

            List<SessionParticipant> participants = new List<SessionParticipant>();

            return participants;

        }

        private ManagedLobbyTeamType GetTeamOfCard(TeamPlayerCard playerCard) 
            => this.m_teamSetup[LobbyTeamType.Allies].Contains(playerCard) ? ManagedLobbyTeamType.Allies : ManagedLobbyTeamType.Axis;

    }

}
