﻿using System;
using System.Diagnostics;
using Battlegrounds.Game.DataCompany;
using Battlegrounds.Game.Match;
using Battlegrounds.Game.Match.Analyze;
using Battlegrounds.Game.Match.Composite;
using Battlegrounds.Game.Match.Finalizer;
using Battlegrounds.Game.Match.Play;
using Battlegrounds.Game.Match.Startup;
using Battlegrounds.Online;
using Battlegrounds.Online.Lobby;

using BattlegroundsApp.Views;

namespace BattlegroundsApp.Models {

    /// <summary>
    /// Play model for handling match startup and playthrough and for handling feedback from underlying systems.
    /// </summary>
    public class LobbyHostPlayModel : ILobbyPlayModel {

        /// <summary>
        /// Amount of seconds that players will have to stop a match from starting.
        /// </summary>
        public const uint WAIT_TIME = 15;

        private bool m_shouldStop = false;
        private bool m_canStop = false;

        private GameLobbyView m_view;
        private ManagedLobby m_lobby;
        private MatchController m_controller;
        private PlayCancelHandler m_cancelHandler;

        public bool CanCancel => this.m_canStop;

        public LobbyHostPlayModel(GameLobbyView gameLobby, ManagedLobby lobby) {
            this.m_view = gameLobby;
            this.m_lobby = lobby;
        }

        public void PlayGame(PlayCancelHandler cancelHandler) {

            // Set cancel handler
            this.m_cancelHandler = cancelHandler;

            // Inform local host that game is about to be started.
            Trace.WriteLine("Start game button was clicked -- Picking startup strategy.", "GameLobbyView");

            // The strategies to use
            IStartupStrategy startupStrategy = null;
            IAnalyzeStrategy matchAnalyzer = null;
            IFinalizeStrategy finalizeStrategy = null;

            // Pick and initialize proper startup strategy
            if (this.m_view.TeamManager.TotalHumanCount == 1) {

                // Startup strategy
                startupStrategy = new SingleplayerStartupStrategy {
                    LocalCompanyCollector = this.GetLocalCompany,
                    SessionInfoCollector = this.GetSessionInfo,
                };

                // Analysis strategy
                matchAnalyzer = new SingleplayerMatchAnalyzer {

                };

                // Finalizer strategy
                finalizeStrategy = new SingleplayerFinalizer {

                };

                // Log strategy choice
                Trace.WriteLine("Using singleplayer strategy (1 human player)", "GameLobbyView");

                // Trigger handler for self-purposes
                this.HandleStartupInformation(null, null, "Starting match");

            } else {

                // Create standard online setup strategy.
                startupStrategy = new OnlineStartupStrategy {
                    LocalCompanyCollector = this.GetLocalCompany,
                    SessionInfoCollector = this.GetSessionInfo,
                    StartMatchWait = this.StopMatchPulse,
                    StopMatchSeconds = WAIT_TIME,
                };

                // Create standard online match strategy.
                matchAnalyzer = new OnlineMatchAnalyzer { 
                    
                };

                // Create standard online finalizer
                finalizeStrategy = new MultiplayerFinalizer {

                };

                // Log strategy choice
                Trace.WriteLine($"Using multiplayer strategy ({this.m_view.TeamManager.TotalHumanCount} human players)", "GameLobbyView");

            }

            // Add listeners
            startupStrategy.StartupFeedback += this.HandleStartupInformation;
            startupStrategy.StartupCancelled += this.HandleStartupCancel;

            // Create session
            MultiplayerSession session = new MultiplayerSession(this.m_lobby);

            // Create and initialize controller
            this.m_controller = new MatchController();
            this.m_controller.SetStartupObjects(session, startupStrategy);
            this.m_controller.SetAnalysisObjects(session, matchAnalyzer);
            this.m_controller.SetFinalizerObjects(session, finalizeStrategy);
            this.m_controller.Complete += this.OnComplete;
            this.m_controller.Error += this.OnError;

            // Log state
            Trace.WriteLine($"The multiplayer session has been created and will now begin.", "GameLobbyView");

            // Set flags
            this.m_shouldStop = false;
            this.m_canStop = true;

            // Play the match
            this.m_controller.Control();

        }

        private Company GetLocalCompany() => this.m_view.Dispatcher.Invoke(() => this.m_view.GetLocalCompany());

        private SessionInfo GetSessionInfo() => this.m_view.Dispatcher.Invoke(() => this.m_view.CreateSessionInfo());

        private void OnError(object reason, string message) {

            // Write to console
            Trace.WriteLine($"[{reason}] -- {message} (OnError)", "GameLobbyView");

            // Report the rest on the UI thread
            this.m_view.UpdateGUI(() => {

                // If the reason is from the play strategy
                if (reason is IPlayStrategy playStrategy) {

                    // Append to lobby chat
                    this.m_view.lobbyChat.AppendText($"[System] A fatal error was detected while playing.\n");

                }

                // Invoke the handler for "cancelling"
                this.m_cancelHandler.Invoke();

            });

        }

        private void OnComplete() {
            this.m_view.UpdateGUI(() => {

                // Append to lobby chat
                this.m_view.lobbyChat.AppendText($"[System] Match has completed and been logged.\n");

                // Invoke the handler for "cancelling"
                this.m_cancelHandler.Invoke();

            });
        }

        public void CancelGame() {

            // Tell the match to stop
            this.m_shouldStop = true;

            // Tell startup to cancel
            this.m_lobby.GetConnection().SendSelfMessage(new Message(MessageType.LOBBY_CANCEL));

            // Invoke the cancel handler
            this.m_cancelHandler?.Invoke();

        }

        private bool StopMatchPulse(int pulseNumber) {

            this.m_view.UpdateGUI(() => {
                if (pulseNumber >= WAIT_TIME) {
                    this.m_canStop = false;
                    this.m_view.StartGameBttn.Content = $"Starting Match";
                } else {
                    this.m_view.StartGameBttn.Content = $"Stop Match ({WAIT_TIME - pulseNumber}s)";
                }
            });

            return this.m_shouldStop;

        }

        private void HandleStartupCancel(IStartupStrategy strategy, object caller, string message) => this.m_cancelHandler?.Invoke();

        private void HandleStartupInformation(IStartupStrategy strategy, object caller, string message) {
            if (caller == this.m_lobby && strategy is OnlineStartupStrategy) {
                this.m_lobby.TriggerSystemMessage(message);
            } else {
                this.m_view.UpdateGUI(() => {
                    this.m_view.lobbyChat.AppendText($"[System] {message}{Environment.NewLine}");
                });
            }
        }

    }

}