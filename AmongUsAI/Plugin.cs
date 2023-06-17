using AmongUs.Data.Player;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppSystem;
using Il2CppSystem.Collections.Generic;
using Reactor;
using Reactor.Utilities;
using Rewired;
using Sentry.Protocol;
using System.IO;
using System.Linq;
using xCloud;

namespace AmongUsAI;

[BepInAutoPlugin]
[BepInProcess("Among Us.exe")]
[BepInDependency(ReactorPlugin.Id)]
public partial class Plugin : BasePlugin
{
    public Harmony Harmony { get; } = new(Id);

    public ConfigEntry<string> ConfigName { get; private set; }

    public override void Load()
    {
        ConfigName = Config.Bind("Fake", "Name", ":>");

        Harmony.PatchAll();
    }


    [HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
    public static class Data_Pipe_Patch
    {
        public static void Postfix(PlayerControl __instance)
        {
            string file = "sendData2.txt";
            bool areLightsOff = false;
            if (__instance == PlayerControl.LocalPlayer)
            {


                // Is in game?
                File.WriteAllText("inGameData.txt", __instance.CanMove ? "1" : "0");

                // Player position
                File.WriteAllText(file, __instance.GetTruePosition().x.ToString() + " " + __instance.GetTruePosition().y.ToString() + "\n");

                // Role
                File.AppendAllText(file, isImposter(__instance) ? "impostor\n" : "crewmate\n");

                var currentTasks = __instance.myTasks.ToArray();

                // Task List
                File.AppendAllText(file, "[");
                foreach (var task in currentTasks)
                {
                    if (task != currentTasks.Last())
                        File.AppendAllText(file, TranslateTaskTypes(task.TaskType) + ", ");
                    else
                        File.AppendAllText(file, TranslateTaskTypes(task.TaskType));

                    if (TranslateTaskTypes(task.TaskType).Equals("Fix Lights"))
                        areLightsOff = true;
                }
                File.AppendAllText(file, "]");
                addNewLine(file);

                // Task Locations
                File.AppendAllText(file, "[");
                foreach (var task in currentTasks)
                {
                    if (task != currentTasks.Last())
                        File.AppendAllText(file, TranslateSystemTypes(task.StartAt) + ", ");
                    else
                        File.AppendAllText(file, TranslateSystemTypes(task.StartAt));

                }
                File.AppendAllText(file, "]");
                addNewLine(file);

                
                // Task Steps
                File.AppendAllText(file, "[");
                foreach (var task in currentTasks)
                {
                    NormalPlayerTask normTask = task.Cast<NormalPlayerTask>();
                    if (task != currentTasks.Last())
                        File.AppendAllText(file, normTask.taskStep + "/" + normTask.MaxStep + ", ");
                    else
                        File.AppendAllText(file, normTask.taskStep + "/" + normTask.MaxStep);

                }
                File.AppendAllText(file, "]");
                addNewLine(file);


            }
        }

        public static void addNewLine(string file)
        {
            File.AppendAllText(file, "\n");
        }

        public static bool isImposter(PlayerControl player)
        {
            return player.Data.Role.name[0] == 'I';
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



    }

}
