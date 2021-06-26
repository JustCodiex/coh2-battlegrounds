﻿using System.Diagnostics;

using Battlegrounds.Networking.Communication;
using Battlegrounds.Networking.Server;

namespace Battlegrounds.Networking {

    /// <summary>
    /// 
    /// </summary>
    public static class NetworkingInstance {

        private static string BestAddress;

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static bool HasLocalServer()
            => Process.GetProcessesByName("bg-server").Length > 0;

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static string GetBestAddress() {
            if (BestAddress is null) {
                if (HasLocalServer()) {
                    BestAddress = "localhost";
                }
#if DEBUG
                try {
                    var connection = TcpConnection.EstablishConnectionTo("192.168.1.107", 11000, 1);
                    BestAddress = connection.Ping() >= 0 ? "192.168.1.107" : "194.37.80.249";
                    connection.Shutdown();
                } catch { }
#else
                BestAddress = "194.37.80.249";
#endif
            }
            return BestAddress;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static ServerAPI GetServerAPI()
            => new ServerAPI(GetBestAddress());

    }

}
