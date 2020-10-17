﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using Battlegrounds.Compiler;
using Battlegrounds.Functional;
using Battlegrounds.Game;
using Battlegrounds.Game.Battlegrounds;
using Battlegrounds.Steam;

namespace Battlegrounds.Online.Services {
    
    /// <summary>
    /// An abstracted representation of a lobby. This class cannot be inherited. This class has no public constructor.
    /// </summary>
    public sealed class ManagedLobby {

        /// <summary>
        /// Send a file to all lobby members.
        /// </summary>
        public const string SEND_ALL = "ALL";

        SteamUser m_self;
        Connection m_underlyingConnection;
        string m_lobbyID;
        bool m_isHost;

        /// <summary>
        /// 
        /// </summary>
        public string LobbyFileID => this.m_lobbyID.Replace("-", "");

        /// <summary>
        /// Event triggered when a player-specific event was received.
        /// </summary>
        public event ManagedLobbyPlayerEvent OnPlayerEvent;

        /// <summary>
        /// Event triggered when a local client-side event was received.
        /// </summary>
        public event ManagedLobbyLocalEvent OnLocalEvent;

        /// <summary>
        /// Event triggered when the client receives a data request.
        /// </summary>
        public event ManagedLobbyQuery OnDataRequest;

        /// <summary>
        /// Event triggered when the host has sent the <see cref="MessageType.LOBBY_STARTMATCH"/> message.
        /// </summary>
        public event ManagedLobbyMatchStart OnStartMatchReceived;

        /// <summary>
        /// Event triggered when the host has updated an information value in the lobby.
        /// </summary>
        public event ManagedLobbyInfoChanged OnLobbyInfoChanged;

        /// <summary>
        /// Function to solve local data requests. May return requested object or filepath to load object.
        /// </summary>
        public event ManagedLobbyLocalDataRequest OnLocalDataRequested;

        /// <summary>
        /// Is the instance of <see cref="ManagedLobby"/> considered to be the host of the lobby.
        /// </summary>
        public bool IsHost => this.m_isHost;

        /// <summary>
        /// The <see cref="SteamUser"/> that's connected to the server. (The local user).
        /// </summary>
        public SteamUser Self => this.m_self;

        /// <summary>
        /// Is the underlying connection connected to the server.
        /// </summary>
        public bool IsConnectedToServer => this.m_underlyingConnection.IsConnected;

        private ManagedLobby(Connection connection, bool isHost) {

            // Assign the underlying connection and start listening for messages
            this.m_underlyingConnection = connection;
            this.m_underlyingConnection.OnMessage += this.ManagedLobbyInternal_MessageReceived;
            this.m_underlyingConnection.Start();

            // Assign hostship
            this.m_isHost = isHost;

        }

        /// <summary>
        /// Send a chat message to the server.
        /// </summary>
        /// <param name="chatMessage">The contents of the chat message.</param>
        public void SendChatMessage(string chatMessage) {
            if (m_underlyingConnection != null && m_underlyingConnection.IsConnected) {
                m_underlyingConnection.SendMessage(new Message(MessageType.LOBBY_CHATMESSAGE, chatMessage));
            }
        }

        /// <summary>
        /// Send a meta-message to all users in the server.
        /// </summary>
        /// <param name="metaMessage">The contents of the meta-message.</param>
        public void SendMetaMessage(string metaMessage) {
            if (m_underlyingConnection != null && m_underlyingConnection.IsConnected) {
                m_underlyingConnection.SendMessage(new Message(MessageType.LOBBY_METAMESSAGE, metaMessage));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="response"></param>
        public void GetPlayersInLobby(Action<int> response) {
            if (m_underlyingConnection != null && m_underlyingConnection.IsConnected) {
                this.GetLobbyInformation("players", (a, b) => response.Invoke(int.Parse(a)));
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<int> GetPlayersInLobbyAsync() {
            int result = 0;
            bool done = false;
            this.GetPlayersInLobby(x => { result = x; done = true; });
            while (!done) {
                await Task.Delay(1);
            }
            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="response"></param>
        public void GetLobbyCapacity(Action<int> response) {
            if (m_underlyingConnection != null && m_underlyingConnection.IsConnected) {
                this.GetLobbyInformation("capacity", (a, b) => {
                    if (int.TryParse(a, out int c)) {
                        response.Invoke(c);
                    } else {
                        response.Invoke(-1);
                    }
                });
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<int> GetLobbyCapacityAsync() {
            int result = 0;
            bool done = false;
            this.GetLobbyCapacity(x => { result = x; done = true; });
            while (!done) {
                await Task.Delay(1);
            }
            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="info"></param>
        /// <param name="value"></param>
        public void UpdateUserInformation(string info, object value) => this.m_underlyingConnection.SendMessage(new Message(MessageType.LOBBY_SETUSERDATA, info, value.ToString()));

        /// <summary>
        /// Get a specific detail from the lobby.
        /// </summary>
        /// <param name="lobbyInformation">The information that is sought.</param>
        /// <param name="reponse">The query response callback to handle the server response.</param>
        public void GetLobbyInformation(string lobbyInformation, ManagedLobbyQueryResponse response) {
            if (m_underlyingConnection != null && m_underlyingConnection.IsConnected) {
                Message queryMessage = new Message(MessageType.LOBBY_INFO, lobbyInformation);
                Message.SetIdentifier(m_underlyingConnection.ConnectionSocket, queryMessage);
                void OnResponse(Message message) {
                    m_underlyingConnection.ClearIdentifierReceiver(message.Identifier);
                    response?.Invoke(message.Argument1, message.Argument2);
                }
                m_underlyingConnection.SetIdentifierReceiver(queryMessage.Identifier, OnResponse);
                m_underlyingConnection.SendMessage(queryMessage);
            }
        }

        /// <summary>
        /// Get a specific detail from the lobby.
        /// </summary>
        /// <param name="lobbyInformation">The information that is sought from the server.</param>
        /// <returns>The first argument of the received server response.</returns>
        /// <remarks>Async method.</remarks>
        public async Task<string> GetLobbyInformation(string lobbyInformation) {
            string response = null;
            this.GetLobbyInformation(lobbyInformation, (a, b) => response = a);
            while (response is null) {
                await Task.Delay(1);
            }
            return response;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="userInformation"></param>
        /// <param name="response"></param>
        public void GetUserInformation(string userInformation, ManagedLobbyQueryResponse response, int userIndex = -1) {
            if (this.m_underlyingConnection != null && this.m_underlyingConnection.IsConnected) {
                Message queryMessage = null;
                if (userIndex == -1) {
                    queryMessage = new Message(MessageType.LOBBY_GETUSERDATA, userInformation);
                } else {
                    queryMessage = new Message(MessageType.LOBBY_GETUSERDATA, userIndex.ToString(), userInformation);
                }
                Message.SetIdentifier(this.m_underlyingConnection.ConnectionSocket, queryMessage);
                void OnResponse(Message message) {
                    this.m_underlyingConnection.ClearIdentifierReceiver(message.Identifier);
                    response?.Invoke(message.Argument1, message.Argument2);
                }
                this.m_underlyingConnection.SetIdentifierReceiver(queryMessage.Identifier, OnResponse);
                this.m_underlyingConnection.SendMessage(queryMessage);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="userInformation"></param>
        /// <param name="response"></param>
        public void GetUserInformation(string userInformation, ManagedLobbyQueryResponse response, ulong userId) {
            if (this.m_underlyingConnection != null && this.m_underlyingConnection.IsConnected) {
                Message queryMessage = null;
                queryMessage = new Message(MessageType.LOBBY_GETUSERDATA, userId.ToString(), userInformation);
                Message.SetIdentifier(this.m_underlyingConnection.ConnectionSocket, queryMessage);
                void OnResponse(Message message) {
                    this.m_underlyingConnection.ClearIdentifierReceiver(message.Identifier);
                    response?.Invoke(message.Argument1, message.Argument2);
                }
                this.m_underlyingConnection.SetIdentifierReceiver(queryMessage.Identifier, OnResponse);
                this.m_underlyingConnection.SendMessage(queryMessage);
            }
        }

        /// <summary>
        /// Get a specific detail from the lobby.
        /// </summary>
        /// <param name="userInfo">The information that is sought from the server.</param>
        /// <returns>The first argument of the received server response.</returns>
        /// <remarks>Async method.</remarks>
        public async Task<string> GetUserInformation(string userInfo) {
            string response = null;
            this.GetUserInformation(userInfo, (a, _) => response = a);
            while (response is null) {
                await Task.Delay(1);
            }
            return response;
        }

        /// <summary>
        /// Get a specific detail from the lobby.
        /// </summary>
        /// <param name="lobbyInformation">The information that is sought from the server.</param>
        /// <returns>The first argument of the received server response.</returns>
        /// <remarks>Async method.</remarks>
        public async Task<string> GetUserInformation(int lobbyUser, string userInfo) {
            string response = null;
            this.GetUserInformation(userInfo, (a, _) => response = a, lobbyUser);
            while (response is null) {
                await Task.Delay(1);
            }
            return response;
        }

        /// <summary>
        /// Get a specific detail from the lobby.
        /// </summary>
        /// <param name="lobbyInformation">The information that is sought from the server.</param>
        /// <returns>The first argument of the received server response.</returns>
        /// <remarks>Async method.</remarks>
        public async Task<string> GetUserInformation(ulong lobbyUser, string userInfo) {
            string response = null;
            this.GetUserInformation(userInfo, (a, _) => response = a, lobbyUser);
            while (response is null) {
                await Task.Delay(1);
            }
            return response;
        }

        /// <summary>
        /// Set a specific lobby detail. This will only be fully invoked if host.
        /// </summary>
        /// <param name="lobbyInformation">The information to change.</param>
        /// <param name="lobbyInformationValue">The new value to set.</param>
        public void SetLobbyInformation(string lobbyInformation, object lobbyInformationValue) {
            if (m_isHost) {
                if (lobbyInformationValue is null) {
                    lobbyInformationValue = string.Empty;
                }
                if (m_underlyingConnection is not null && m_underlyingConnection.IsConnected) {
                    m_underlyingConnection.SendMessage(new Message(MessageType.LOBBY_UPDATE, lobbyInformation, lobbyInformationValue.ToString()));
                }
            }
        }

        /// <summary>
        /// Update user-specific information for specified lobby member. Will require hostship to apply.
        /// </summary>
        /// <param name="lobbyIndex">The index of the user in lobby to update value of.</param>
        /// <param name="userinfo">The user info to change (Like, fac, tid, or com)</param>
        /// <param name="uservalue">The value to update information to.</param>
        public void SetUserInformation(int lobbyIndex, string userinfo, object uservalue) {
            if (this.m_isHost) {
                if (uservalue is null) {
                    uservalue = string.Empty;
                }
                if (this.m_underlyingConnection is not null && this.m_underlyingConnection.IsConnected) {
                    this.m_underlyingConnection.SendMessage(new Message(MessageType.LOBBY_SETUSERDATA, lobbyIndex.ToString(), userinfo, uservalue.ToString()));
                }
            }
        }

        /// <summary>
        /// Update user-specific information for local user (doesn't require hostship)
        /// </summary>
        /// <param name="userinfo">The user info to change (Like, fac, tid, or com)</param>
        /// <param name="uservalue">The value to update information to.</param>
        public void SetUserInformation(string userinfo, object uservalue) {
            if (uservalue is null) {
                uservalue = string.Empty;
            }
            if (this.m_underlyingConnection is not null && this.m_underlyingConnection.IsConnected) {
                this.m_underlyingConnection.SendMessage(new Message(MessageType.LOBBY_SETUSERDATA, userinfo, uservalue.ToString()));
            }
        }

        /// <summary>
        /// Get a random identifier to identify messages.
        /// </summary>
        /// <returns>A random identifier.</returns>
        public int GetRandomIdentifier()
            => Message.GetIdentifier(this.m_underlyingConnection.ConnectionSocket);

        /// <summary>
        /// Gracefully disconnect from the <see cref="ManagedLobby"/>.
        /// </summary>
        /// <remarks>The connection to the server will be broken and <see cref="Join(LobbyHub, string, string, ManagedLobbyConnectCallback)"/> must be used to reestablish connection.</remarks>
        public void Leave() {
            if (m_underlyingConnection != null) {
                m_underlyingConnection.SendMessage(new Message(MessageType.LOBBY_LEAVE));
                m_underlyingConnection.Stop();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<string[]> GetPlayerNamesAsync() {

            string[] playernames = null;
            bool alldone = false;

            void OnMessage(Message response) {
                Trace.WriteLine(response);
                if (response.Descriptor == MessageType.LOBBY_PLAYERNAMES) {
                    playernames = response.Argument1.Split(';', StringSplitOptions.RemoveEmptyEntries).ForEach(x => x.Replace("\"", ""));
                }
                alldone = true; // do this even if false...
            }

            Message playersQueryMessage = new Message(MessageType.LOBBY_PLAYERNAMES);
            Message.SetIdentifier(this.m_underlyingConnection.ConnectionSocket, playersQueryMessage);
            this.m_underlyingConnection.SetIdentifierReceiver(playersQueryMessage.Identifier, OnMessage);
            this.m_underlyingConnection.SendMessage(playersQueryMessage);
            this.m_underlyingConnection.Listen(); // For some reason it just stops listening here...

            while (!alldone) {
                await Task.Delay(1);
            }

            return playernames;

        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<ulong[]> GetPlayerIDsAsync() {

            ulong[] playerids = null;
            bool alldone = false;

            void OnMessage(Message response) {
                Trace.WriteLine(response);
                if (response.Descriptor == MessageType.LOBBY_PLAYERIDS) {
                    playerids = response.Argument1.Split(';', StringSplitOptions.RemoveEmptyEntries).ForEach(x => x.Replace("\"", "")).Select(x => ulong.Parse(x)).ToArray();
                }
                alldone = true;
            }

            Message playerIDQueryMessage = new Message(MessageType.LOBBY_PLAYERIDS);
            Message.SetIdentifier(m_underlyingConnection.ConnectionSocket, playerIDQueryMessage);
            m_underlyingConnection.SetIdentifierReceiver(playerIDQueryMessage.Identifier, OnMessage);
            m_underlyingConnection.SendMessage(playerIDQueryMessage);
            m_underlyingConnection.Listen();

            while (!alldone) {
                await Task.Delay(1);
            }

            return playerids;

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="player"></param>
        /// <returns></returns>
        public async Task<ulong> GetLobbyPlayerIDAsync(string player) {

            ulong id = 0; // in the case of Steam ID's, 0 should never happen
            bool alldone = false;

            void OnMessage(Message message) {
                Trace.WriteLine(message);
                if (message.Descriptor == MessageType.LOBBY_GETPLAYERID) {
                    ulong.TryParse(message.Argument1, out id);
                }
                alldone = true;
            }

            Message idQueryMessage = new Message(MessageType.LOBBY_GETPLAYERID, player);
            Message.SetIdentifier(m_underlyingConnection.ConnectionSocket, idQueryMessage);
            m_underlyingConnection.SetIdentifierReceiver(idQueryMessage.Identifier, OnMessage);
            m_underlyingConnection.SendMessage(idQueryMessage);
            m_underlyingConnection.Listen();

            while (!alldone) {
                await Task.Delay(1);
            }

            return id;

        }

        public async Task<int[]> GetLobbyPlayerDifficultiesAsync(ulong[] players) {

            int[] diffs = new int[players.Length];
            for (int i = 0; i < players.Length; i++) {
                string diff = await this.GetUserInformation(players[i], "dif");
                if (int.TryParse(diff, out int d)) {
                    diffs[i] = d;
                }
            }

            return diffs;

        }

        private Company GetLocalCompany() {
            object val = this.OnLocalDataRequested?.Invoke("CompanyData");
            if (val is Company c) {
                return c;
            } else if (val is string s) {
                return Company.ReadCompanyFromFile(s);
            }
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="companyFile"></param>
        public void UploadCompany(string companyFile) {

            // Read the company
            Company company = Company.ReadCompanyFromFile(companyFile);
            company.Owner = this.m_self.ID.ToString();

            // Upload file
            FileHub.UploadFile(company.ToBytes(), $"{this.m_self.ID}_company.json", this.LobbyFileID);

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="player"></param>
        /// <param name="destination"></param>
        /// <returns></returns>
        public async Task<bool> GetLobbyCompany(string player, string destination) 
            => this.GetLobbyCompany(await this.GetLobbyPlayerIDAsync(player), destination);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="userID"></param>
        /// <param name="destination"></param>
        /// <returns></returns>
        public bool GetLobbyCompany(ulong userID, string destination) 
            => FileHub.DownloadFile(destination, $"{userID}_company.json", this.LobbyFileID);
        
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private async Task<(List<Company>, bool)> GetLobbyCompanies() {

            ulong[] lobbyPlayers = await this.GetPlayerIDsAsync();
            int[] lobbyDifficulties = await this.GetLobbyPlayerDifficultiesAsync(lobbyPlayers);
            List<Company> companies = new List<Company>();
            bool success = false;

            int humanCount = lobbyDifficulties.Count(x => x == (int)AIDifficulty.Human);
            
            await Task.Run(() => {

                int count = 0;

                for (int i = 0; i < lobbyPlayers.Length; i++) {

                    if (lobbyDifficulties[i] != (int)AIDifficulty.Human) { // doesn't make sense (and not possible) to download company data of AI
                        Trace.WriteLine($"Skipping player {lobbyPlayers[i]} as they're not of human \"difficulty\" ({lobbyDifficulties[i]})");
                        continue;
                    }

                    string destination = BattlegroundsInstance.GetRelativePath(BattlegroundsPaths.SESSION_FOLDER, $"{lobbyPlayers[i]}_company.json");

                    if (!this.GetLobbyCompany(lobbyPlayers[i], destination)) {
                        // TODO: Try and redownload
                    } else {
                        Company company = Company.ReadCompanyFromFile(destination);
                        company.Owner = lobbyPlayers[i].ToString();
                        companies.Add(company);
                        count++;
                    }

                }

                success = count == humanCount;

            });

            return (companies, success);

        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="difficulty"></param>
        /// <param name="faction"></param>
        /// <param name="teamIndex"></param>
        /// <returns></returns>
        public async Task<int> TryCreateAIPlayer(AIDifficulty difficulty, string faction, int teamIndex) {
            string responseQuery = null;
            Message addAIMessage = new Message(MessageType.LOBBY_ADDAI, ((int)difficulty).ToString(), faction, teamIndex.ToString());
            Message.SetIdentifier(this.m_underlyingConnection.ConnectionSocket, addAIMessage);
            this.m_underlyingConnection.SetIdentifierReceiver(addAIMessage.Identifier, msg => responseQuery = msg.Argument1);
            this.m_underlyingConnection.SendMessage(addAIMessage);
            while (responseQuery is null) {
                await Task.Delay(1);
            }
            if (int.TryParse(responseQuery, out int id)) {
                return id;
            } else {
                return -1;
            }
        }

        /// <summary>
        /// Kick a player from the lobby. (If host).
        /// </summary>
        /// <param name="playerID">The unique steam ID of the player to kick.</param>
        /// <param name="message">A message to send to the user stating why they were kicked.</param>
        public void KickPlayer(ulong playerID, string message = "Kicked by Host") 
            => this.m_isHost.Then(() => this.m_underlyingConnection.SendMessage(new Message(MessageType.LOBBY_KICK, playerID.ToString(), message)));

        /// <summary>
        /// Remove the AI player with specified ID. (If host).
        /// </summary>
        /// <param name="aiID">The ID used to identify the AI to remove.</param>
        public void RemoveAI(ulong aiID)
            => this.m_isHost.Then(() => this.m_underlyingConnection.SendMessage(new Message(MessageType.LOBBY_REMOVEAI, aiID.ToString())));

        /// <summary>
        /// Compile the win condition using data from the lobby members and begin the match with all lobby members.<br/>This will start Company of Heroes 2 if completed.
        /// </summary>
        /// <remarks>The method is synchronous and make take several minutes to complete. (Use in a <see cref="Task.Run(Action)"/> context to maintain responsiveness).</remarks>
        /// <param name="operationCancelled">The <see cref="Action{T}"/> invoked if the execution of the method is cancelled. The <see cref="string"/> argument describes what caused the cancellation.</param>
        public async void CompileAndStartMatch(Action<string> operationCancelled) {

            // Make sure we're the host
            if (!this.m_isHost) {
                return;
            }

            // Get the local company
            Company ownCompany = GetLocalCompany();
            if (ownCompany == null) {
                operationCancelled?.Invoke("Failed to load own company!");
                return;
            } else {
                ownCompany.Owner = BattlegroundsInstance.LocalSteamuser.ID.ToString();
            }

            // Send a "Starting match" message to lobby members
            this.m_underlyingConnection.SendMessage(new Message(MessageType.LOBBY_STARTING));

            // Wait a bit
            await Task.Delay(240);

            // Get company lobbies
            (List<Company> lobbyCompanies, bool success) = await GetLobbyCompanies();

            // If we managed to retrieve companies
            if (success) {

                // Add our own
                lobbyCompanies.Add(ownCompany);

                // The session to be built
                Session session = null;

                try {

                    // Get match data (A SessionInfo object)
                    if (this.OnLocalDataRequested?.Invoke("MatchInfo") is SessionInfo info) {

                        // Zip/Map the companies to their respective SessionParticipant instances.
                        Session.ZipCompanies(lobbyCompanies.ToArray(), ref info);

                        // Create the session properly.
                        session = Session.CreateSession(info);

                    } else {
                        throw new Exception("Failed to get a valid SessionInfo");
                    }

                } catch (Exception e) {
                    operationCancelled?.Invoke(e.Message);
                }

                // Did we fail to create session?
                if (session is null) {
                    operationCancelled?.Invoke("Failed to create session");
                }

                // Play the session
                SessionManager.PlaySession<SessionCompiler<CompanyCompiler>, CompanyCompiler>(
                    session,
                    this.ManagedLobbyInternal_GameSessionStatusChanged,
                    this.ManagedLobbyInternal_GameMatchAnalyzed,
                    () => this.ManagedLobbyInternal_GameOnGamemodeCompiled(operationCancelled));

            } else {
                operationCancelled?.Invoke("Failed to get lobby companies");
            }

        }

        bool ManagedLobbyInternal_GameOnGamemodeCompiled(Action<string> operationCancelled) {

            string sgapath = $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}\\my games\\Company of Heroes 2\\mods\\gamemode\\coh2_battlegrounds_wincondition.sga";

            if (File.Exists(sgapath)) {

                // Upload
                if (FileHub.UploadFile(sgapath, "gamemode.sga", this.LobbyFileID)) {

                    // Sleep for 1s (TODO: Fix Upload to wait for confirmation...)
                    Thread.Sleep(1000);

                    // Notify lobby players the gamemode is available
                    this.m_underlyingConnection.SendMessage(new Message(MessageType.LOBBY_NOTIFY_GAMEMODE));

                    // Sleep for 1s
                    Thread.Sleep(1000);

                    // Send the start match...
                    this.m_underlyingConnection.SendMessage(new Message(MessageType.LOBBY_STARTMATCH));

                    // Sleep for 1s
                    Thread.Sleep(1000);

                    // Return true
                    return true;

                } else {

                    operationCancelled?.Invoke("Failed to upload gamemode!");

                }

            } else {
                operationCancelled?.Invoke("Failed to compile!");
            }

            return false;

        }

        void ManagedLobbyInternal_GameSessionStatusChanged(SessionStatus status, Session s) {
            Trace.WriteLine($"Session update: {status}");
            switch (status) {
                case SessionStatus.S_FailedCompile:
                    break;
                case SessionStatus.S_FailedPlay:
                    break;
                case SessionStatus.S_GameNotLaunched:
                    break;
                case SessionStatus.S_NoPlayback:
                    break;
                case SessionStatus.S_ScarError:
                    break;
                case SessionStatus.S_BugSplat:
                    break;
            }
        }

        void ManagedLobbyInternal_GameMatchAnalyzed(GameMatch results) {
            // TODO: Sync
        }

        private void ManagedLobbyInternal_MessageReceived(Message incomingMessage) {
            Trace.WriteLine($"Received message <<{incomingMessage}>> from server.", "ManagedLobby.cs");
            switch (incomingMessage.Descriptor) {
                case MessageType.LOBBY_CHATMESSAGE:
                    this.OnPlayerEvent?.Invoke(ManagedLobbyPlayerEventType.Message, incomingMessage.Argument2, incomingMessage.Argument1);
                    break;
                case MessageType.LOBBY_METAMESSAGE:
                    this.OnPlayerEvent?.Invoke(ManagedLobbyPlayerEventType.Meta, incomingMessage.Argument2, incomingMessage.Argument1);
                    break;
                case MessageType.LOBBY_JOIN:
                    this.OnPlayerEvent?.Invoke(ManagedLobbyPlayerEventType.Join, incomingMessage.Argument1, string.Empty);
                    break;
                case MessageType.LOBBY_LEAVE:
                    this.OnPlayerEvent?.Invoke(ManagedLobbyPlayerEventType.Leave, incomingMessage.Argument1, string.Empty);
                    break;
                case MessageType.LOBBY_KICK:
                    this.OnPlayerEvent?.Invoke(ManagedLobbyPlayerEventType.Kicked, incomingMessage.Argument1, incomingMessage.Argument2);
                    break;
                case MessageType.LOBBY_KICKED:
                    this.OnLocalEvent?.Invoke(ManagedLobbyLocalEventType.Kicked, incomingMessage.Argument1);
                    break;
                case MessageType.LOBBY_SETHOST:
                    this.OnLocalEvent?.Invoke(ManagedLobbyLocalEventType.Host, string.Empty);
                    break;
                case MessageType.LOBBY_INFO:
                    this.OnLobbyInfoChanged?.Invoke(incomingMessage.Argument1, incomingMessage.Argument2);
                    break;
                case MessageType.LOBBY_REQUEST_COMPANY:
                    this.OnDataRequest?.Invoke(true, incomingMessage.Argument1, "CompanyData", incomingMessage.Identifier);
                    break;
                case MessageType.LOBBY_REQUEST_RESULTS:
                    this.OnDataRequest?.Invoke(true, incomingMessage.Argument1, "MatchData", incomingMessage.Identifier);
                    break;
                case MessageType.LOBBY_STARTMATCH:
                    this.OnStartMatchReceived?.Invoke();
                    break;
                case MessageType.CONFIRMATION_MESSAGE:
                    break;
                case MessageType.LOBBY_NOTIFY_GAMEMODE:
                    string path = $"{Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)}\\my games\\Company of Heroes 2\\mods\\gamemode\\coh2_battlegrounds_wincondition.sga";
                    if (!FileHub.DownloadFile(path, "gamemode.sga", this.LobbyFileID)) {
                        Trace.WriteLine("Failed to download 'gamemode.sga'");
                    } else {
                        Trace.WriteLine("Successfully downloaded 'gamemode.sga'");
                    }                    
                    break;
                case MessageType.FatalMessageError:
                    break;
                default: Trace.WriteLine($"Unhandled type <<{incomingMessage.Descriptor}>>", "ManagedLobby.cs"); break;
            }
        }

        /// <summary>
        /// Host a new <see cref="ManagedLobby"/> on the central server.
        /// </summary>
        /// <param name="hub">The <see cref="LobbyHub"/> instance to use when attempting to host.</param>
        /// <param name="lobbyName">The name of the lobby.</param>
        /// <param name="lobbyPassword">The password of the lobby. Can be <see cref="string.Empty"/> if no password is desired.</param>
        /// <param name="managedCallback">The callback to invoke when the server has responded to the host request.</param>
        /// <exception cref="ArgumentException"/>
        public static void Host(LobbyHub hub, string lobbyName, string lobbyPassword, ManagedLobbyConnectCallback managedCallback) {

            // Make sure lobby name is valid
            if (string.IsNullOrEmpty(lobbyName)) {
                throw new ArgumentException("Must have a valid lobby name!");
            }

            // Just to be safe
            if (lobbyPassword is null) {
                lobbyPassword = string.Empty;
            }

            // Callback for establishing connection
            void OnConnectionEstablished(bool connected, Connection connection) {
                if (connected) {
                    void OnLobbyCreated(Socket _, Message response) {
                        if (response.Descriptor == MessageType.CONFIRMATION_MESSAGE) {
                            managedCallback?.Invoke(new ManagedLobbyStatus(true), new ManagedLobby(connection, true) { 
                                m_lobbyID = response.Argument2, 
                                m_self = hub.User 
                            });
                        } else {
                            managedCallback?.Invoke(new ManagedLobbyStatus(false, response.Argument1), null);
                        }
                    }
                    MessageSender.SendMessage(connection.ConnectionSocket, new Message(MessageType.LOBBY_CREATE, lobbyName, lobbyPassword), OnLobbyCreated);
                } else {
                    managedCallback?.Invoke(new ManagedLobbyStatus(false, "Unable to establish connection with server."), null);
                }
            }

            // Connect
            hub.Connect(OnConnectionEstablished);

        }

        /// <summary>
        /// Join an existing lobby in the <see cref="LobbyHub"/> using a <see cref="ConnectableLobby"/> to connect.
        /// </summary>
        /// <param name="hub">The <see cref="LobbyHub"/> instance to use when attempting to join.</param>
        /// <param name="lobby">The <see cref="ConnectableLobby"/> instance containing the GUID to use when attempting to join.</param>
        /// <param name="password">The password to send when trying to join.</param>
        /// <param name="managedCallback">The callback to invoke when the server has responded to the join request.</param>
        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="ArgumentOutOfRangeException"/>
        public static void Join(LobbyHub hub, ConnectableLobby lobby, string password, ManagedLobbyConnectCallback managedCallback)
            => Join(hub, lobby.lobby_guid, password, managedCallback);

        /// <summary>
        /// Join an existing lobby in the <see cref="LobbyHub"/>.
        /// </summary>
        /// <param name="hub">The <see cref="LobbyHub"/> instance to use when attempting to join.</param>
        /// <param name="lobby">The GUID to use when attempting to join.</param>
        /// <param name="password">The password to send when trying to join.</param>
        /// <param name="managedCallback">The callback to invoke when the server has responded to the join request.</param>
        /// <exception cref="ArgumentNullException"/>
        /// <exception cref="ArgumentOutOfRangeException"/>
        public static void Join(LobbyHub hub, string lobbyGUID, string password, ManagedLobbyConnectCallback managedCallback) {

            // Make sure we have a valid hub instance.
            if (hub == null) {
                throw new ArgumentNullException("The lobby hub instance was null!");
            }

            // Make sure the GUID is valid.
            if (lobbyGUID.Length != 36) {
                throw new ArgumentOutOfRangeException("The GUID was not of length 36 and is therefore not a valid GUID.");
            }

            // Callback for establishing connection
            void OnConnectionEstablished(bool connected, Connection connection) {
                if (connected) {
                    void OnLobbyJoinResponse(Socket _, Message response) {
                        if (response.Descriptor == MessageType.CONFIRMATION_MESSAGE) {
                            managedCallback?.Invoke(new ManagedLobbyStatus(true), new ManagedLobby(connection, false) { 
                                m_lobbyID = response.Argument2,
                                m_self = hub.User,
                            });
                        } else {
                            managedCallback?.Invoke(new ManagedLobbyStatus(false, response.Argument1), null);
                        }
                    }
                    MessageSender.SendMessage(connection.ConnectionSocket, new Message(MessageType.LOBBY_JOIN, lobbyGUID, password), OnLobbyJoinResponse);
                } else {
                    managedCallback?.Invoke(new ManagedLobbyStatus(false, "Unable to establish connection with server."), null);
                }
            }

            // Connect
            hub.Connect(OnConnectionEstablished);

        }

    }

}
