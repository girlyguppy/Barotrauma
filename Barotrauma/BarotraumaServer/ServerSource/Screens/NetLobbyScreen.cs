﻿using Barotrauma.Extensions;
using Barotrauma.Networking;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Barotrauma
{
    partial class NetLobbyScreen : Screen
    {
        private SubmarineInfo selectedSub;
        private SubmarineInfo selectedEnemySub;
        private SubmarineInfo selectedShuttle;

        public bool RadiationEnabled = true;

        public SubmarineInfo SelectedSub
        {
            get { return selectedSub; }
            set
            {
                selectedSub = value;
                lastUpdateID++;
                if (GameMain.NetworkMember?.ServerSettings != null)
                {
                    GameMain.NetworkMember.ServerSettings.ServerDetailsChanged = true;
                }
            }
        }

        [MaybeNull, AllowNull]
        public SubmarineInfo SelectedEnemySub
        {
            get => selectedEnemySub;
            set
            {
                selectedEnemySub = value;
                lastUpdateID++;
            }
        }

        public SubmarineInfo SelectedShuttle
        {
            get { return selectedShuttle; }
            set { selectedShuttle = value; lastUpdateID++; }
        }

        public GameModePreset[] GameModes { get; }

        private int selectedModeIndex;
        public int SelectedModeIndex
        {
            get { return selectedModeIndex; }
            set
            {
                lastUpdateID++;
                selectedModeIndex = MathHelper.Clamp(value, 0, GameModes.Length - 1);
                if (SelectedMode != GameModePreset.MultiPlayerCampaign && GameMain.GameSession?.GameMode is CampaignMode && Selected == this)
                {
                    GameMain.GameSession = null;
                }
                if (GameMain.NetworkMember?.ServerSettings != null)
                {
                    GameMain.NetworkMember.ServerSettings.GameModeIdentifier = SelectedModeIdentifier;
                    GameMain.NetworkMember.ServerSettings.ServerDetailsChanged = true;
                }
            }
        }

        public Identifier SelectedModeIdentifier
        {
            get { return GameModes[SelectedModeIndex].Identifier; }
            set
            {
                if (SelectedModeIdentifier == value) { return; }
                for (int i = 0; i < GameModes.Length; i++)
                {
                    if (GameModes[i].Identifier == value)
                    {
                        SelectedModeIndex = i;
                        break;
                    }
                }
                if (GameMain.NetworkMember?.ServerSettings != null)
                {
                    GameMain.NetworkMember.ServerSettings.GameModeIdentifier = SelectedModeIdentifier;
                    GameMain.NetworkMember.ServerSettings.ServerDetailsChanged = true;
                }
            }
        }

        public GameModePreset SelectedMode
        {
            get { return GameModes[SelectedModeIndex]; }
        }

        public IEnumerable<Identifier> MissionTypes
        {
            get { return GameMain.NetworkMember.ServerSettings.AllowedRandomMissionTypes; }
            set
            {
                lastUpdateID++;
                if (GameMain.NetworkMember?.ServerSettings != null)
                {
                    GameMain.NetworkMember.ServerSettings.MissionTypes = string.Join(",", value.Select(t => t.ToIdentifier()));
                }
            }
        }

        public NetLobbyScreen()
        {
            LevelSeed = ToolBox.RandomSeed(8);

            subs = SubmarineInfo.SavedSubmarines
                .Where(s => s.Type == SubmarineType.Player && !s.HasTag(SubmarineTag.HideInMenus))
                .ToList();

            if (subs == null || subs.Count == 0)
            {
                throw new Exception("No submarines are available.");
            }

            selectedSub = subs.FirstOrDefault(s => !s.HasTag(SubmarineTag.Shuttle));
            if (selectedSub == null)
            {
                //no subs available, use a shuttle
                DebugConsole.ThrowError("No full-size submarines available - choosing a shuttle as the main submarine.");
                selectedSub = subs[0];
            }

            selectedShuttle = subs.First(s => s.HasTag(SubmarineTag.Shuttle));
            if (selectedShuttle == null)
            {
                //no shuttles available, use a sub
                DebugConsole.ThrowError("No shuttles available - choosing a full-size submarine as the shuttle.");
                selectedShuttle = subs[0];
            }

            DebugConsole.NewMessage("Selected sub: " + SelectedSub.Name, Color.White);
            DebugConsole.NewMessage("Selected shuttle: " + SelectedShuttle.Name, Color.White);

            GameModes = GameModePreset.List.ToArray();
        }
        
        private readonly List<SubmarineInfo> subs;
        public IReadOnlyList<SubmarineInfo> GetSubList() => subs;

        public string LevelSeed
        {
            get
            {
                return levelSeed;
            }
            set
            {
                if (levelSeed == value) { return; }

                lastUpdateID++;
                levelSeed = value;
                LocationType.Random(new MTRandom(ToolBox.StringToInt(levelSeed))); //call to sync up with clients
            }
        }
        
        public void ToggleCampaignMode(bool enabled)
        {
            for (int i = 0; i < GameModes.Length; i++)
            {
                if ((GameModes[i] == GameModePreset.MultiPlayerCampaign) == enabled)
                {
                    selectedModeIndex = i;
                    break;
                }
            }

            lastUpdateID++;
        }

        public override void Select()
        {
            base.Select();
            Voting.ResetVotes(GameMain.Server.ConnectedClients, resetKickVotes: false);
            if (SelectedMode != GameModePreset.MultiPlayerCampaign && GameMain.GameSession?.GameMode is CampaignMode && Selected == this)
            {
                GameMain.GameSession = null;
            }
            if (GameMain.Server.ServerSettings.SelectedSubmarine.IsNullOrEmpty())
            {
                //if no sub is selected in the settings,
                //select the random sub we selected in the constructor
                GameMain.Server.ServerSettings.SelectedSubmarine = SelectedSub?.Name;
            }
        }

        public void RandomizeSettings()
        {
            if (GameMain.Server.ServerSettings.RandomizeSeed) { LevelSeed = ToolBox.RandomSeed(8); }

            //don't touch any of these settings if a campaign is running!
            if (GameMain.GameSession?.Campaign == null)
            {
                if (GameMain.Server.ServerSettings.SubSelectionMode == SelectionMode.Random)
                {
                    var nonShuttles = SubmarineInfo.SavedSubmarines.Where(c => !c.HasTag(SubmarineTag.Shuttle) && !c.HasTag(SubmarineTag.HideInMenus) && c.IsPlayer).ToList();
                    SelectedSub = nonShuttles[Rand.Range(0, nonShuttles.Count)];
                }
                if (GameMain.Server.ServerSettings.ModeSelectionMode == SelectionMode.Random)
                {
                    var allowedGameModes = Array.FindAll(GameModes, m => !m.IsSinglePlayer && m != GameModePreset.MultiPlayerCampaign);
                    SelectedModeIdentifier = allowedGameModes[Rand.Range(0, allowedGameModes.Length)].Identifier;
                }

                GameMain.Server.ServerSettings.SelectNonHiddenSubmarine();
            }
        }
    }
}
