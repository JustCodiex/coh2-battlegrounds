﻿using System.Diagnostics;
using System.Linq;

using Battlegrounds.Game.Match.Data;
using Battlegrounds.Game.Match.Data.Events;

namespace Battlegrounds.Game.Match.Analyze {

    /// <summary>
    /// Singleplayer analysis strategy for analyzing a singleplayer match. Implementation of <see cref="IAnalyzeStrategy"/>. Can be extended with custom behaviour.
    /// </summary>
    public class SingleplayerMatchAnalyzer : IAnalyzeStrategy {

        protected IMatchData m_subject;
        protected EventAnalysis m_analysisResult;

        public IAnalyzedMatch AnalysisResult => this.m_analysisResult;

        public virtual void OnPrepare(object caller, IMatchData toAnalyze) {
            this.m_analysisResult = new EventAnalysis(toAnalyze.Session);
            this.m_subject = toAnalyze;
        }

        public virtual void OnAnalyze(object caller) {

            // Analyze given playback data
            if (!this.AnalyzePlaybackData(this.m_subject)) {
                this.m_analysisResult = null; // Invalid (Should give a null-analysis when finalizing).
            }

        }

        protected virtual bool AnalyzePlaybackData(IMatchData replayMatchData) {

            // Set length of match
            this.m_analysisResult.SetLength(replayMatchData.Length);

            // Set players
            this.m_analysisResult.SetPlayers(replayMatchData.Players.ToArray());

            // Register all events
            foreach (TimeEvent timeEvent in replayMatchData) {
                var reg = this.m_analysisResult.RegisterEvent(timeEvent);
                if (!reg) {
                    if (reg.WasOutsideTime) {
                        Trace.WriteLine("Time event was after the length of the match (Out of range)", "SingleplayerMatchAnalyzer");
                        return false; // For sure some problem
                    } else if (reg.ConflictingTimes) {
                        Trace.WriteLine("Time event was conflicting in time", "SingleplayerMatchAnalyzer");
                        return false; // Event time not adding up
                    } else {
                        Trace.WriteLine("Time event found duplicate event", "SingleplayerMatchAnalyzer");
                        return false; // Duplicate event for something that doesn't allow duplicates
                    }
                }
            }

            // Log success (Not required in UI)
            Trace.WriteLine($"Successfully analyzed match data with {this.m_analysisResult.EventCount} events", "SingleplayerMatchAnalyzer");

            // Return true -> Analysis "complete" for this type
            return true;

        }

        public virtual IAnalyzedMatch OnCleanup(object caller) {

            // Compile the final result
            if (!this.m_analysisResult?.CompileResults() ?? true) {
                Trace.WriteLine($"Failed to compile analysis report. (Mismatching events)", "SingleplayerMatchAnalyzer");
                return new NullAnalysis();
            } else {
                Trace.WriteLine($"Successfully compiled match data into a finalizable match data object.", "SingleplayerMatchAnalyzer");
            }

            // Return the analysis
            return this.m_analysisResult;

        }

    }

}
