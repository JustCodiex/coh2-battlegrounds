﻿using System;
using Battlegrounds.Game.DataCompany;

namespace Battlegrounds.Game.Match {

    /// <summary>
    /// A session that's being controlled remotely. Implements <see cref="ISession"/>.
    /// </summary>
    public sealed class RemoteSession : ISession {
        
        public Guid SessionID { get; }

        public bool AllowPersistency => true;

        public RemoteSession(string session) => this.SessionID = Guid.Parse(session);

        public Company GetPlayerCompany(ulong steamID) => null;

    }

}
