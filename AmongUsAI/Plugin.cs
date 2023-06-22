using AmongUs.Data.Player;
using AmongUs.Data.Settings;
using AmongUs.GameOptions;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using Discord;
using GameCore;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Runtime;
using Il2CppSystem;
using Il2CppSystem.Collections.Generic;
using Il2CppSystem.Configuration;
using Il2CppSystem.Reflection;
using InnerNet;
using Reactor;
using Reactor.Utilities;
using Rewired;
using Sentry.Protocol;
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using xCloud;

namespace AmongUsAI;

[BepInAutoPlugin]
[BepInProcess("Among Us.exe")]
[BepInDependency(ReactorPlugin.Id)]
public partial class Plugin : BasePlugin
{
    public Harmony Harmony { get; } = new(Id);

    public ConfigEntry<string> ConfigName { get; private set; }

    public enum MapType : int
    {
        Ship = 0,
        Hq = 1,
        Pb = 2,
        Airship = 3
    }

    public static MapType map;
    public static bool inMeeting = false;

    public override void Load()
    {
        ConfigName = Config.Bind("Fake", "Name", ":>");

        Harmony.PatchAll();
    }

    // MAPS
    // Skeld and HQ
    [HarmonyPatch(typeof(ShipStatus), nameof(ShipStatus.OnEnable))]
    public static class ShipStatusUpdate
    {
        public static void Prefix(ShipStatus __instance)
        {
            map = (MapType)__instance.Type;
        }
    }

    // Polus
    [HarmonyPatch(typeof(PolusShipStatus), nameof(PolusShipStatus.OnEnable))]
    public static class PolusShipStatusUpdate
    {
        public static void Prefix(PolusShipStatus __instance)
        {
            map = MapType.Pb;
        }
    }

    // Airship
    [HarmonyPatch(typeof(AirshipStatus), nameof(AirshipStatus.OnEnable))]
    public static class AirshipStatusUpdate
    {
        public static void Prefix(AirshipStatus __instance)
        {
            map = MapType.Airship;
        }
    }

    // MEETING UPDATE
    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Awake))]
    public static class MeetingOpenUpdate
    {
        public static void Postfix(MeetingHud __instance)
        {
            inMeeting = true;
            int time = GameOptionsManager.Instance.CurrentGameOptions.GetInt(Int32OptionNames.VotingTime) + GameOptionsManager.Instance.CurrentGameOptions.GetInt(Int32OptionNames.DiscussionTime);
            File.WriteAllText("timerData2.txt", time + "\n");
        }
        
    }

    [HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Close))]
    public static class MeetingCloseUpdate
    {
        public static void Postfix(MeetingHud __instance)
        {
            inMeeting = false;
        }

    }

    [HarmonyPatch(typeof(InnerNetClient), nameof(InnerNetClient.Update))]
    public static class ClientUpdate
    {
        public static void Postfix(InnerNetClient __instance)
        {
            if (!(__instance.GameState == InnerNetClient.GameStates.Started || __instance.GameState == InnerNetClient.GameStates.Joined))
            { 
                // Game is over
                inMeeting = false;
            }

            if (__instance.GameState == InnerNetClient.GameStates.Ended)
            {
                // No longer in game
                File.WriteAllText("inGameData.txt", "0");
            }
        }
    }

    public static int TranslateColorName(string colorName)
    {
        colorName = colorName.ToUpper().Replace("(", "").Replace(")", "");
        string[] COLOR_NAMES = {"RED", "BLUE", "GREEN", "PINK",
                "ORANGE", "YELLOW", "BLACK", "WHITE",
                "PURPLE", "BROWN", "CYAN", "LIME",
                "MAROON", "ROSE", "BANANA", "GRAY",
                "TAN", "CORAL"};

        return System.Array.IndexOf(COLOR_NAMES, colorName.ToUpper());
    }

    [HarmonyPatch(typeof(ChatController), nameof(ChatController.AddChat))]
    public static class Chat_Pipe_Patch
    {
        public static void Prefix(ChatController __instance, PlayerControl sourcePlayer, System.String chatText)
        {
            if (!sourcePlayer.Data.IsDead)
                File.AppendAllText("chatData2.txt", TranslateColorName(sourcePlayer.Data.ColorName) + ": " + chatText + "\n");
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.MurderPlayer))]
    public static class Kill_Pipe_Patch
    {
        public static void Finalizer(PlayerControl __instance, PlayerControl target)
        {
                File.AppendAllText("killData2.txt", TranslateColorName(__instance.Data.ColorName) + ", " + TranslateColorName(target.Data.ColorName) + "\n");
        }
    }

    public static List<PlayerControl> GetAllPlayerControls()
    {
        return PlayerControl.AllPlayerControls;
    }

    public static List<GameData.PlayerInfo> GetAllPlayerData()
    {
        List<GameData.PlayerInfo> playerDatas = new List<GameData.PlayerInfo>();

        var playerControls = GetAllPlayerControls();
        foreach (PlayerControl playerControl in playerControls)
        {
            playerDatas.Add(playerControl.Data);
        }

        return playerDatas;
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.StartMeeting))]
    public static class Meeting_Pipe_Patch
    {
        public static void Finalizer(PlayerControl __instance, GameData.PlayerInfo target)
        {
            File.WriteAllText("meetingData2.txt", TranslateColorName(__instance.Data.ColorName) + "\n");
            var players = GetAllPlayerData().ToArray();

            File.AppendAllText("meetingData2.txt", "[");
            foreach (GameData.PlayerInfo player in players)
            {
                if (player.IsDead)
                {
                    if (player != players.Last()) // Doesn't work since last might not be dead.
                        File.AppendAllText("meetingData2.txt", TranslateColorName(player.ColorName) + ", ");
                    else
                        File.AppendAllText("meetingData2.txt", TranslateColorName(player.ColorName).ToString());
                }
            }
            File.AppendAllText("meetingData2.txt", "]");
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.OnGameStart))]
    public static class Game_Start_Patch
    {
        public static void Finalize(PlayerControl __instance)
        {
            // Is in game?
            File.WriteAllText("inGameData.txt", "1");
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.OnGameEnd))]
    public static class Game_End_Patch
    {
        public static void Finalize(PlayerControl __instance)
        {
            // Is in game?
            File.WriteAllText("inGameData.txt", "0");
        }
    }

    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    public static class Data_Pipe_Patch
    {
        public static void Postfix(PlayerControl __instance)
        {
            string file = "sendData2.txt";
            bool areLightsOff = false;
            bool imposter = isPlayerImposter(PlayerControl.LocalPlayer.Data);
            string big_output_string = "";

            // Local Player
            if (__instance != null && __instance == PlayerControl.LocalPlayer)
            {

                // Player position
                big_output_string += __instance.GetTruePosition().x.ToString() + " " + __instance.GetTruePosition().y.ToString() + "\n";

                // Role
                big_output_string += imposter ? "impostor\n" : "crewmate\n";

                var currentTasks = __instance.myTasks.ToArray();

                // Task List
                big_output_string += "[";
                foreach (var task in currentTasks)
                {
                    if (task != currentTasks.Last())
                        big_output_string += TranslateTaskTypes(task.TaskType) + ", ";
                    else
                        big_output_string += TranslateTaskTypes(task.TaskType);

                    if (TranslateTaskTypes(task.TaskType).Equals("Fix Lights"))
                        areLightsOff = true;
                }
                big_output_string += "]";
                big_output_string += "\n";

                // Task Locations
                big_output_string += "[";
                foreach (var task in currentTasks)
                {
                    if (task != currentTasks.Last())
                        big_output_string += TranslateSystemTypes(task.StartAt) + ", ";
                    else
                        big_output_string += TranslateSystemTypes(task.StartAt);

                }
                big_output_string += "]";
                big_output_string += "\n";


                // Task Steps
                if (!imposter)
                {
                    big_output_string += "[";
                    foreach (var task in currentTasks)
                    {
                        NormalPlayerTask normTask = task.Cast<NormalPlayerTask>();
                        if (task != currentTasks.Last())
                            big_output_string += normTask.taskStep + "/" + normTask.MaxStep + ", ";
                        else
                            big_output_string += normTask.taskStep + "/" + normTask.MaxStep;

                    }
                    big_output_string += "]";
                    big_output_string += "\n";
                }
                else
                {
                    big_output_string += "[]";
                    big_output_string += "\n";
                }

                // Map ID
                big_output_string += map.ToString();
                big_output_string += "\n";

                //Is dead?
                big_output_string += __instance.Data.IsDead ? "1" : "0";
                big_output_string += "\n";

                // In meeting
                big_output_string += inMeeting ? "1" : "0";
                big_output_string += "\n";

                // Player Speed
                big_output_string += GameOptionsManager.Instance.CurrentGameOptions.GetFloat(FloatOptionNames.PlayerSpeedMod) + "\n";
                //File.AppendAllText(file, 1.5 + "\n");

                // Player color
                big_output_string += __instance.CurrentOutfit.ColorId + "\n";

                var p = __instance.GetTruePosition();

                // Room - annoying
                //big_output_string += "ERROR" + "\n";
                string output_room = TranslateSystemTypes(SystemTypes.Outside);
                var rooms = ShipStatus.Instance.AllRooms.ToArray();
                foreach (var room in rooms)
                {
                    if (room.roomArea != null && room.roomArea.OverlapPoint(p))
                    {
                        output_room = TranslateSystemTypes(room.RoomId);
                    }
                }
                big_output_string += output_room + "\n";

                // Lights
                big_output_string += areLightsOff ? "1" : "0";
                big_output_string += "\n";

                // Other players' color + position
                big_output_string += "[";
                var playerControls = GetAllPlayerControls().ToArray();
                foreach (var playerControl in playerControls)
                {
                    Vector2 p2 = playerControl.GetTruePosition();
                    if (p.x != p2.x && p.y != p2.y)
                    {
                        float dist = GetDistanceBetweenPoints_Unity(p, p2);
                        if (playerControl != playerControls.Last())
                            big_output_string += TranslateColorName(playerControl.Data.ColorName) + "/" + p2.x + "/" + p2.y + ", ";
                        else
                            big_output_string += TranslateColorName(playerControl.Data.ColorName) + "/" + p2.x + "/" + p2.y;
                    }
                }
                big_output_string += "]";
                big_output_string += "\n";

                // In vent?
                big_output_string += "[";
                foreach (var playerControl in playerControls)
                {
                    if (playerControl != null)
                    {
                        Vector2 p2 = playerControl.GetTruePosition();
                        if (p.x != p2.x && p.y != p2.y)
                        {
                            float dist = GetDistanceBetweenPoints_Unity(p, p2);
                            if (playerControl != playerControls.Last())
                                big_output_string += TranslateColorName(playerControl.Data.ColorName) + "/" + (playerControl.inVent ? "1" : "0") + ", ";
                            else
                                big_output_string += TranslateColorName(playerControl.Data.ColorName) + "/" + (playerControl.inVent ? "1" : "0");
                        }
                    }
                }
                big_output_string += "]";
                big_output_string += "\n";

                // Dead?
                big_output_string += "[";
                foreach (var playerControl in playerControls)
                {
                    if (playerControl != null)
                    {
                        Vector2 p2 = playerControl.GetTruePosition();
                        if (p.x != p2.x && p.y != p2.y)
                        {
                            float dist = GetDistanceBetweenPoints_Unity(p, p2);
                            if (playerControl != playerControls.Last())
                                big_output_string += TranslateColorName(playerControl.Data.ColorName) + "/" + (playerControl.Data.IsDead ? "1" : "0") + ", ";
                            else
                                big_output_string += TranslateColorName(playerControl.Data.ColorName) + "/" + (playerControl.Data.IsDead ? "1" : "0");
                        }
                    }
                }
                big_output_string += "]";
                big_output_string += "\n";

                // Big write to prevent incomplete send data
                File.WriteAllText(file, big_output_string);

                if (imposter)
                {
                    file = "imposterData2.txt";

                    float[] kill_dist_list = { 1f, 1.8f, 2.5f };
                    string imp_output_string = "";
                    // Fellow imposters and dead status
                    imp_output_string += "[";
                    foreach (var playerControl in playerControls)
                    {
                        if (isPlayerImposter(playerControl.Data))
                        {
                            Vector2 p2 = playerControl.GetTruePosition();
                            if (p.x != p2.x && p.y != p2.y)
                            {
                                float dist = GetDistanceBetweenPoints_Unity(p, p2);
                                if (playerControl != playerControls.Last())
                                    imp_output_string += TranslateColorName(playerControl.Data.ColorName) + "/" + (playerControl.Data.IsDead ? "1" : "0") + ", ";
                                else
                                    imp_output_string += TranslateColorName(playerControl.Data.ColorName) + "/" + (playerControl.Data.IsDead ? "1" : "0");
                            }
                        }
                    }
                    imp_output_string += "]";
                    imp_output_string += "\n";

                    if (!PlayerControl.LocalPlayer.Data.IsDead)
                        imp_output_string += PlayerControl.LocalPlayer.killTimer + "\n";
                    else
                        imp_output_string += "-1";

                    File.WriteAllText(file, imp_output_string);
                }
            }
        }

        public static void addNewLine(string file)
        {
            File.AppendAllText(file, "\n");
        }

        public static string TranslateTaskTypes(TaskTypes type)
        {
            int i = TaskTypesHelpers.AllTypes.IndexOf(type);
            string[] TASK_TRANSLATIONS = { "Submit Scan", "Prime Shields", "Fuel Engines", "Chart Course", "Start Reactor", "Swipe Card", "Clear Asteroids", "Upload Data",
        "Inspect Sample", "Empty Chute", "Empty Garbage", "Align Engine Output", "Fix Wiring", "Calibrate Distributor", "Divert Power", "Unlock Manifolds", "Reset Reactor",
        "Fix Lights", "Clean O2 Filter", "Fix Communications", "Restore Oxygen", "Stabilize Steering", "Assemble Artifact", "Sort Samples", "Measure Weather", "Enter ID Code",
        "Buy Beverage", "Process Data", "Run Diagnostics", "Water Plants", "Monitor Oxygen", "Store Artifacts", "Fill Canisters", "Activate Weather Nodes", "Insert Keys",
        "Reset Seismic Stabilizers", "Scan Boarding Pass", "Open Waterways", "Replace Water Jug", "Repair Drill", "Align Telecopse", "Record Temperature", "Reboot Wifi",
        "Polish Ruby", "Reset Breakers", "Decontaminate", "Make Burger", "Unlock Safe", "Sort Records", "Put Away Pistols", "Fix Shower", "Clean Toilet", "Dress Mannequin",
        "Pick Up Towels", "Rewind Tapes", "Start Fans", "Develop Photos", "Get Biggol Sword", "Put Away Rifles", "Stop Charles", "Vent Cleaning"};
        
            return TASK_TRANSLATIONS[i];
        }

        public static string TranslateSystemTypes(SystemTypes type)
        {
            int i = SystemTypeHelpers.AllTypes.IndexOf(type);
            string[] LOC_TRANSLATIONS = { "Hallway", "Storage", "Cafeteria", "Reactor", "Upper Engine", "Navigation", "Admin", "Electrical", "Oxygen", "Shields",
        "MedBay", "Security", "Weapons", "Lower Engine", "Communications", "Ship Tasks", "Doors", "Sabotage", "Decontamination", "Launchpad", "Locker Room", "Laboratory",
        "Balcony", "Office", "Greenhouse", "Dropship", "Decontamination", "Outside", "Specimen Room", "Boiler Room", "Vault Room", "Cockpit", "Armory", "Kitchen", "Viewing Deck",
        "Hall Of Portraits", "Cargo Bay", "Ventilation", "Showers", "Engine Room", "The Brig", "Meeting Room", "Records Room", "Lounge Room", "Gap Room", "Main Hall", "Medical",
        "Decontamination" };

            return LOC_TRANSLATIONS[i];
        }

        public static bool isPlayerImposter(GameData.PlayerInfo playerInfo)
        {
            if (playerInfo.Role == null) return false;
            RoleBehaviour role = playerInfo.Role;

            return role.TeamType == RoleTeamTypes.Impostor;
        }

        static float GetDistanceBetweenPoints_Unity(Vector2 p1, Vector2 p2)
        {
	        float dx = p1.x - p2.x, dy = p1.y - p2.y;
	        return MathF.Sqrt(dx* dx + dy* dy);
        }

}

}
