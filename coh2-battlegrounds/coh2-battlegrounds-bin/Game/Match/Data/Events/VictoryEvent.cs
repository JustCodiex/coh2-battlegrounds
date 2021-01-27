﻿using System;

namespace Battlegrounds.Game.Match.Data.Events {

    /// <summary>
    /// <see cref="IMatchEvent"/> implementation for the event of a victory.
    /// </summary>
    public class VictoryEvent : IMatchEvent {
        
        public char Identifier => 'V';
        public uint Uid { get; }

        /// <summary>
        /// Get the ID of the victor
        /// </summary>
        public byte VictorID { get; }

        /// <summary>
        /// Create a new <see cref="VictoryEvent"/>.
        /// </summary>
        /// <param name="values">String arguments containing victor ID</param>
        public VictoryEvent(uint id, string[] values) {
            this.Uid = id;
            if (byte.TryParse(values[0], out byte victor)) {
                this.VictorID = victor;
            } else {
                throw new FormatException();
            }
        }

    }

}