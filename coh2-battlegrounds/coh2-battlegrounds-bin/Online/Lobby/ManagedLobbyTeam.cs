﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Battlegrounds.Online.Lobby {
    
    /// <summary>
    /// Simple representation of which teams types can be represented.
    /// </summary>
    public enum ManagedLobbyTeamType {
        
        /// <summary>
        /// Spectator Team (May spectate a match).
        /// </summary>
        /// <remarks>
        /// May be used for surplus players when map size changes.
        /// </remarks>
        Spectator = 0,
        
        /// <summary>
        /// Allied Team
        /// </summary>
        Allies = 1,
        
        /// <summary>
        /// Axis Team
        /// </summary>
        Axis = 2,

    }

    /// <summary>
    /// Sealed class representing a Team in the <see cref="ManagedLobby"/> object.
    /// </summary>
    public sealed class ManagedLobbyTeam {

        private ManagedLobby m_lobby;
        private ManagedLobbyTeamSlot[] m_slots;

        /// <summary>
        /// The available <see cref="ManagedLobbyTeamSlot"/> slots in this team.
        /// </summary>
        public ManagedLobbyTeamSlot[] Slots => this.m_slots;

        /// <summary>
        /// All <see cref="ManagedLobbyMember"/> members in the team.
        /// </summary>
        public ManagedLobbyMember[] Members => this.m_slots.Where(x => x.State == ManagedLobbyTeamSlotState.Occupied).Select(x => x.Occupant).ToArray();

        /// <summary>
        /// The max capacity of this team.
        /// </summary>
        public int Capacity => this.Slots.Length;

        /// <summary>
        /// The amount of active members on the team.
        /// </summary>
        public int Count => this.Members.Length;

        /// <summary>
        /// The <see cref="ManagedLobbyTeamType"/> the team is a representation of.
        /// </summary>
        public ManagedLobbyTeamType Team { get; }

        public ManagedLobbyTeam(ManagedLobby lobby, byte teamsize, ManagedLobbyTeamType teamtype) {
            this.Team = teamtype;
            this.m_slots = new ManagedLobbyTeamSlot[teamsize];
            this.m_lobby = lobby;
        }

        public void ForEachMember(Action<ManagedLobbyMember> action) {
            foreach (ManagedLobbyMember member in this.Members) {
                action(member);
            }
        }

        public bool AllMembers(Predicate<ManagedLobbyMember> predicate) {
            foreach (ManagedLobbyMember member in this.Members) {
                if (!predicate(member)) {
                    return false;
                }
            }
            return true;
        }

        public ManagedLobbyMember[] SetCapacity(int max) {
            ManagedLobbyTeamSlot[] slots = new ManagedLobbyTeamSlot[max];
            List<ManagedLobbyMember> removed = new List<ManagedLobbyMember>();
            for (int i = 0; i < slots.Length; i++) {
                slots[i] = new ManagedLobbyTeamSlot(i);
                if (i < this.m_slots.Length && this.m_slots[i].State == ManagedLobbyTeamSlotState.Occupied) {
                    slots[i].SetOccupant(this.m_slots[i].Occupant);
                }
            }
            for (int i = slots.Length; i < this.m_slots.Length; i++) {
                if (this.m_slots[i].State == ManagedLobbyTeamSlotState.Occupied) {
                    removed.Add(this.m_slots[i].Occupant);
                }
            }
            this.m_slots = slots;
            return removed.ToArray();
        }

        public bool Join(ManagedLobbyMember member) {
            for (int i = 0; i < this.m_slots.Length; i++) {
                if (this.m_slots[i].State == ManagedLobbyTeamSlotState.Open) {
                    this.m_slots[i].SetOccupant(member);
                    return true;
                }
            }
            return false;
        }

        public void Leave(ManagedLobbyMember member) {
            for (int i = 0; i < this.m_slots.Length; i++) {
                if (this.m_slots[i].Occupant == member) {
                    this.m_slots[i].SetOccupant(null);
                }
            }
        }

        public ManagedLobbyMember GetLobbyMember(ulong playerID) => this.m_slots.FirstOrDefault(x => x.Occupant is not null && x.Occupant.ID == playerID)?.Occupant;

    }

}
