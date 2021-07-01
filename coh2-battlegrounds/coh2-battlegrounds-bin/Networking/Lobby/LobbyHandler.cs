﻿using Battlegrounds.Networking.Communication;
using Battlegrounds.Networking.Remoting;
using Battlegrounds.Networking.Requests;
using Battlegrounds.Networking.Server;

namespace Battlegrounds.Networking.Lobby {

    /// <summary>
    /// 
    /// </summary>
    public class LobbyHandler {
    
        /// <summary>
        /// 
        /// </summary>
        public ILobby Lobby { get; init; }

        /// <summary>
        /// 
        /// </summary>
        public IObjectID LobbyID { get; init; }

        /// <summary>
        /// 
        /// </summary>
        public ILobbyMember Self { get; init; }

        /// <summary>
        /// 
        /// </summary>
        public IRequestHandler RequestHandler { get; init; }

        /// <summary>
        /// 
        /// </summary>
        public IObjectPool ObjectPool { get; init; }

        /// <summary>
        /// 
        /// </summary>
        public ISingleInstanceHandler InstanceHandler { get; init; }

        /// <summary>
        /// 
        /// </summary>
        public IStaticInterface StaticInterface { get; init; }

        /// <summary>
        /// 
        /// </summary>
        public IConnection Connection { get; init; }

        /// <summary>
        /// 
        /// </summary>
        public ServerAPI API { get; }

        /// <summary>
        /// 
        /// </summary>
        public bool IsHost { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="api"></param>
        /// <param name="lobby"></param>
        public LobbyHandler(ServerAPI api, bool isHost) {
            this.API = api;
            this.IsHost = isHost;
        }

    }

}