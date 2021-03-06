﻿using System.Collections.Generic;
using System.Diagnostics;
using Battlegrounds.Game.DataCompany;
using Battlegrounds.Game.Gameplay;
using Battlegrounds.Game.Match.Analyze;

namespace Battlegrounds.Game.Match.Finalizer {

    /// <summary>
    /// Finalize strategy for singleplayer matches (but capable of handling multiple non-AI players). Implementation of <see cref="IFinalizeStrategy"/>.
    /// </summary>
    public class SingleplayerFinalizer : IFinalizeStrategy {

        protected Dictionary<Player, Company> m_companies;
        protected IAnalyzedMatch m_matchData;

        /// <summary>
        /// Get or set if finalizer should also notify AI company changes. Default value is <see langword="false"/>.
        /// </summary>
        public bool NotifyAI { get; set; } = false;

        public FinalizedCompanyHandler CompanyHandler { get; set; }

        public SingleplayerFinalizer() {
            this.m_companies = null;
        }

        public virtual void Finalize(IAnalyzedMatch analyzedMatch) {

            // Save match data (for potential use in inheriting classes
            this.m_matchData = analyzedMatch;

            // Get the session
            var session = analyzedMatch.Session;

            // Get the units
            var units = analyzedMatch.Units;

            // Get the players
            var players = analyzedMatch.Players;

            // Create company dictionary
            this.m_companies = new();

            // Assign player companies
            foreach (Player player in players) {
                var company = session.GetPlayerCompany(player.SteamID);
                if (company is not null) {
                    if (analyzedMatch.IsWinner(player)) {
                        company.UpdateStatistics(x => { x.TotalMatchWinCount++; return x; });
                    } else {
                        company.UpdateStatistics(x => { x.TotalMatchLossCount++; return x; });
                    }
                    this.m_companies.Add(player, company);
                } else {
                    Trace.WriteLine($"Failed to find a company for {player.SteamID} ({player.Name})", "SingleplayerFinalizer");
                    // TODO: Handle
                }
            }

            // Run through all units
            foreach (UnitStatus status in units) {

                // Ignore AI player data
                if (status.PlayerOwner.IsAIPlayer || this.NotifyAI) {
                    continue;
                }

                // Get the relevant company
                var company = this.m_companies[status.PlayerOwner];

                // Get the squad
                Squad squad = company.GetSquadByIndex(status.UnitID);

                // If the unit is dead, remove it.
                if (status.IsDead) {

                    // Remove the squad
                    if (!company.RemoveSquad(status.UnitID)) {
                        Trace.WriteLine($"Failed to remove company unit '{status.UnitID}' from company '{company.Name}'.", nameof(SingleplayerFinalizer));
                    }

                    // Update losses
                    company.UpdateStatistics(x => UpdateLosses(x, !squad.SBP.IsVehicle, 0)); // TODO: Get proper loss sizes

                } else {

                    // Update veterancy
                    if (status.VetChange >= 0) {
                        squad.IncreaseVeterancy(status.VetChange, status.VetExperience);
                    }

                    // Update combat time
                    squad.IncreaseCombatTime(status.CombatTime);

                    // TODO: Apply pickups

                }

            }

            // TODO: Save captured items etc.

        }

        protected static CompanyStatistics UpdateLosses(CompanyStatistics original, bool isInfantry, uint amount) {
            if (isInfantry) {
                original.TotalInfantryLosses += amount;
            } else {
                original.TotalVehicleLosses += amount;
            }
            return original;
        }

        public virtual void Synchronize(object synchronizeObject) {

            // Make sure we log this unfortunate event
            if (this.CompanyHandler is null) {
                Trace.WriteLine("{Warning} -- The company handler is NULL and changes will therefore not be handled further!", "SingleplayerFinalizer");
            }

            // Loop through all companies and save
            foreach (var pair in this.m_companies) {
                if (!pair.Key.IsAIPlayer || this.NotifyAI) {
                    this.CompanyHandler?.Invoke(pair.Value);
                }
            }

        }

    }

}
