﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Battlegrounds.Functional;
using Battlegrounds.Game.DataCompany;
using Battlegrounds.Game.Gameplay;
using Battlegrounds.Json;
using Battlegrounds.Locale;
using Battlegrounds.Lua;
using Battlegrounds.Util.Lists;

namespace Battlegrounds.Campaigns.Organisations {
    
    /// <summary>
    /// 
    /// </summary>
    public class Army : IJsonObject {

        private record ArmyRegimentalUnitTemplate(string BlueprintName, string TransportBlueprint, Range VetRange, double Weight, int Count);
        private record ArmyRegimentalTemplate(string RegimentType, int RegimentUnitSize, int RegimentCompanyCount, 
            ArmyRegimentalUnitTemplate[] WeightedRegimentalUnitTemplates, ArmyRegimentalUnitTemplate[] CountedRegimentalUnitTemplates = null, bool IsArtillery = false);

        private Dictionary<string, List<ArmyRegimentalTemplate>> m_regimentTemplates;

        /// <summary>
        /// Get or initialize the army group name.
        /// </summary>
        public string Name { get; init; }

        public string Description { get; init; }

        public LocaleKey ArmyName { get; set; }

        public List<Division> Divisions { get; init; }

        public Faction Faction { get; init; }

        public CampaignArmyTeam Team { get; }

        public Army(CampaignArmyTeam team) {
            this.Team = team;
            this.Divisions = new List<Division>();
            this.m_regimentTemplates = new Dictionary<string, List<ArmyRegimentalTemplate>>();
        }

        public void NewRegimentTemplate(string templateName, LuaTable templateValue) {

            // Make sure we have a valid name
            int at = templateName.IndexOf('@');
            if (at == -1) {
                Trace.WriteLine($"Regimental template name '{templateName}' is not using standard naming convention and will be skipped.", $"{nameof(Army)}::{nameof(NewRegimentTemplate)}");
                return;
            }

            // Get names
            string templName = templateName[0..at];
            string templType = templateName[(at + 1)..];

            // Register new template if not already in internals
            if (!this.m_regimentTemplates.ContainsKey(templName)) {
                Trace.WriteLine($"Registering new regimental template '{templName}'.", $"{nameof(Army)}::{nameof(NewRegimentTemplate)}");
                this.m_regimentTemplates.Add(templName, new List<ArmyRegimentalTemplate>());
            }

            // Get numerics
            int regSize = (int)(templateValue["size"] as LuaNumber);
            int comCount = (int)(templateValue["companies"] as LuaNumber);
            bool isArty = (templateValue["is_artillery"] as LuaBool)?.IsTrue ?? false;

            // Get unit table and allocate corresponding managed table
            LuaTable units = templateValue["units"] as LuaTable;
            List<ArmyRegimentalUnitTemplate> weightedTemplates = new List<ArmyRegimentalUnitTemplate>();
            List<ArmyRegimentalUnitTemplate> countFixedTemplates = new List<ArmyRegimentalUnitTemplate>();

            // Create units
            units.Pairs((k, v) => {

                // Make sure it's a table
                if (v is LuaTable vt) {

                    // Create the unit template
                    if (CreateUnitTemplate(vt, out bool weighted) is ArmyRegimentalUnitTemplate unitTmpl) {
                        if (weighted) {
                            weightedTemplates.Add(unitTmpl);
                        } else {
                            countFixedTemplates.Add(unitTmpl);
                        }
                    } else {
                        Trace.WriteLine($"Unable to add unit '{k.Str()}' to regiment template {templName}:{templType}", $"{nameof(Army)}::{nameof(NewRegimentTemplate)}");
                    }

                }

            });

            // Add template
            this.m_regimentTemplates[templName].Add(new ArmyRegimentalTemplate(templType, regSize, comCount, weightedTemplates.ToArray(), countFixedTemplates.ToArray(), isArty));

        }

        private static ArmyRegimentalUnitTemplate CreateUnitTemplate(LuaTable unitTable, out bool isWeighted) {

            string sbp = unitTable["sbp"].Str();
            string tsbp = (unitTable["transport_sbp"] as LuaString)?.Str() ?? null;

            var range = (unitTable["rank_range"] as LuaTable).IfTrue(x => x is not null)
            .ThenDo(x => new Range((int)(x[1] as LuaNumber), (int)(x[2] as LuaNumber)))
            .OrDefaultTo(() => new Range(0, 0));

            if (unitTable["weight"] is LuaNumber w) {
                isWeighted = true;
                return new ArmyRegimentalUnitTemplate(sbp, tsbp, range, w, -1);
            } else if (unitTable["count"] is LuaNumber c) {
                isWeighted = false;
                return new ArmyRegimentalUnitTemplate(sbp, tsbp, range, -1.0, (int)c.AsInteger());
            } else {
                isWeighted = false;
                return null;
            }

        }

        public void NewDivision(uint divisionID, LocaleKey divisionName, string template, LuaValue speed, LuaTable regiments, ref ushort campaignUnitID) {

            // Make sure it's a valid division
            if (!this.m_regimentTemplates.ContainsKey(template)) {
                Trace.WriteLine(
                    $"Army '{this.ArmyName.LocaleID}' contains no template '{template}' and will therefore skip creating '{divisionName.LocaleID}'", 
                    $"{nameof(Army)}::{nameof(NewDivision)}");
                return;
            }

            // Create division
            Division div = new Division(divisionID, this, divisionName) {
                TemplateName = template,
                MaxMove = (speed as LuaNumber)?.ToInt() ?? 1
            };

            // ID buffer
            ushort squadIDCounter = campaignUnitID;

            // Loop table for regiments
            regiments.Pairs((k, v) => {

                // Get template type
                string templType = k.Str();

                // Get template
                var regimentalTemplate = this.m_regimentTemplates[template].FirstOrDefault(x => x.RegimentType == templType);

                // Get regimental names
                List<string> regimentNames = new();

                // Loop over all entries
                (v as LuaTable).Pairs((_, entry) => {
                    if (entry is LuaString lstr) {
                        if (regimentalTemplate is not null) {
                            if (GenerateRegiment(lstr.Str(), div, regimentalTemplate, ref squadIDCounter) is Regiment simpleRegiment) {
                                div.Regiments.Add(simpleRegiment);
                            } else {
                                return new LuaBool(false);
                            }
                        } else {
                            Trace.WriteLine(
                                $"Army '{this.ArmyName.LocaleID}' contains no template sub-type '{templType}' in {template} and will therefore skip creating '{divisionName.LocaleID}'",
                                $"{nameof(Army)}::{nameof(NewDivision)}");
                            return new LuaBool(false);
                        }
                    } else if (entry is LuaTable entryTable) {
                        string instName = entryTable["name"].Str();
                        string tmplName = (entryTable["tmpl"] as LuaString)?.Str() ?? $"{template}@{templType}";
                        string[] tmplLookup = tmplName.Split('@');
                        if (this.m_regimentTemplates[tmplLookup[0]].FirstOrDefault(x => x.RegimentType == tmplLookup[1]) is ArmyRegimentalTemplate otherRegimentalTemplate) {
                            if (entryTable["custom_units"] is LuaTable customUnits) {
                                customUnits.Pairs((uk, unit) => {
                                    if (unit is LuaTable unitTable) {
                                        if (CreateUnitTemplate(unitTable, out bool weighted) is ArmyRegimentalUnitTemplate unitTmpl) {
                                            if (weighted) {
                                                otherRegimentalTemplate = otherRegimentalTemplate with
                                                { // Why this ugly style...
                                                    WeightedRegimentalUnitTemplates = otherRegimentalTemplate.WeightedRegimentalUnitTemplates.Append(unitTmpl)
                                                };
                                            } else {
                                                otherRegimentalTemplate = otherRegimentalTemplate with
                                                {
                                                    CountedRegimentalUnitTemplates = otherRegimentalTemplate.CountedRegimentalUnitTemplates.Append(unitTmpl)
                                                };
                                            }
                                        } else {
                                            Trace.WriteLine($"Unable to add unit '{uk.Str()}' to regiment template {tmplLookup[0]}:{tmplLookup[1]}", $"{nameof(Army)}::{nameof(NewRegimentTemplate)}");
                                        }
                                    }
                                });
                            }
                            if (GenerateRegiment(instName, div, otherRegimentalTemplate, ref squadIDCounter) is Regiment mutatedRegiment) {
                                div.Regiments.Add(mutatedRegiment);
                            }
                        } else {
                            Trace.WriteLine(
                                $"Army '{this.ArmyName.LocaleID}' contains no template sub-type '{tmplLookup[0]}' in {tmplLookup[1]} and will therefore skip creating '{instName}'",
                                $"{nameof(Army)}::{nameof(NewDivision)}");
                        }
                    }
                    return new LuaNil();
                });

            });

            // Update counter
            campaignUnitID = squadIDCounter;

            // Add division
            this.Divisions.Add(div);

        }

        private static Regiment GenerateRegiment(string regimentName, Division division, ArmyRegimentalTemplate regimentalTemplate, ref ushort campaignUnitID) {

            // Create regiment
            var nameKey = new LocaleKey(regimentName, division.Name.LocaleSource);
            Regiment regiment = new Regiment(division, nameKey, regimentalTemplate.RegimentType, regimentalTemplate.IsArtillery, regimentalTemplate.RegimentCompanyCount);

            // Create companies
            for (int i = 0; i < regimentalTemplate.RegimentCompanyCount; i++) {

                // Handle squads
                List<Squad> squadss = new List<Squad>();

                // Add all
                if (regimentalTemplate.CountedRegimentalUnitTemplates is not null) {
                    foreach (var counted in regimentalTemplate.CountedRegimentalUnitTemplates) {
                        for (int j = 0; j < counted.Count; j++) {
                            squadss.Add(CreateSquad(counted, ref campaignUnitID));
                        }
                    }
                }

                // Set remaining
                int remaining = (regimentalTemplate.RegimentUnitSize / regimentalTemplate.RegimentCompanyCount) - squadss.Count;

                // Get weighted list
                var weighted = new WeightedList<ArmyRegimentalUnitTemplate>(regimentalTemplate.WeightedRegimentalUnitTemplates, x => x.Weight);

                // Loop through and pick
                for (int j = 0; j < remaining; j++) {
                    squadss.Add(CreateSquad(weighted.Pick(BattlegroundsInstance.RNG), ref campaignUnitID));
                }
                /*
                
                This code will print the distribution of squads, use for testing purposes!

                // Get counts in pairs
                var bps = squadss.Select(x => x.SBP.Name).Distinct().OrderBy(x => x).Select(x => (x, squadss.Count(y => y.SBP.Name == x)));

                // Log counts
                Trace.WriteLine($"Generated regiment: {string.Join(", ", bps.Select(x => $"{x.x}[{x.Item2}]"))}", $"{nameof(Army)}::{nameof(GenerateRegiment)}");
                */

                // Create company
                regiment.CreateCompany(i, squadss);

            }

            return regiment;

        }

        private static Squad CreateSquad(ArmyRegimentalUnitTemplate unitTemplate, ref ushort campaignUnitID) {
            
            // Create unit builder
            UnitBuilder ub = new UnitBuilder();
            ub.SetModGUID(BattlegroundsInstance.BattleGroundsTuningMod.Guid);
            ub.SetBlueprint(unitTemplate.BlueprintName);

            // Build squad
            Squad squadResult = ub.Build(campaignUnitID++);
            if (squadResult.Crew is not null) {
                campaignUnitID++;
            }

            // Return squad
            return squadResult;
        
        }

        public void DeployDivision(uint divisionID, List<string> deployLocations, CampaignMap map) {
            if (this.Divisions.FirstOrDefault(x => x.DivisionUid == divisionID) is Division div) {
                
                // Create formation from division
                Formation form = new Formation();
                form.FromDivision(div);
                
                // Determine method to spawn (just spawn or split to fit)
                if (deployLocations.Count == 1) {
                    map.SpawnFormationAt(deployLocations[0], form);
                } else {
                    if (form.CanSplit) { // Make sure we can split
                        Formation[] split = form.Split(deployLocations.Count);
                        for (int i = 0; i < deployLocations.Count; i++) {
                            map.SpawnFormationAt(deployLocations[i], split[i]);
                        }
                    } else {
                        Trace.WriteLine($"Army '{this.ArmyName.LocaleID}' contains deploy division with ID {div.Name.LocaleID}, as there are too few regiments to distribute.", $"{nameof(Army)}::{nameof(DeployDivision)}");
                    }
                }

                // Log that we're deploying some unit
                Trace.WriteLine($"Army '{this.ArmyName.LocaleID}' deploying {div.Name.LocaleID} at {string.Join(", ", deployLocations)}", $"{nameof(Army)}::{nameof(DeployDivision)}");

            } else {
                Trace.WriteLine($"Army '{this.ArmyName.LocaleID}' contains no division with ID {divisionID}", $"{nameof(Army)}::{nameof(DeployDivision)}");
            }            
        }

        public string ToJsonReference() => this.ArmyName.LocaleID;

    }

}
