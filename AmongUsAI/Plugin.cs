using BepInEx;
using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Reactor;
using Reactor.Utilities;
using Rewired;
using Sentry.Protocol;
using System.IO;

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
    public static class ExamplePatch
    {
        public static void Postfix(PlayerControl __instance)
        {
            string file = "sendData2.txt";
            if (__instance.name.Equals("AmongUsAI"))
            {
                File.WriteAllText("inGameData.txt", __instance.CanMove ? "1" : "0");
                File.WriteAllText(file, __instance.GetTruePosition().x.ToString() + " " + __instance.GetTruePosition().y.ToString());
            }
        }
    }
}
