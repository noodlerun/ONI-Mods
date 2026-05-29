using HarmonyLib;
using KMod;
using PeterHan.PLib.Core;
using PeterHan.PLib.Options;
using System;
using UnityEngine.Diagnostics;
using Newtonsoft.Json;

namespace Interstellar
{
    [RestartRequired]
    [JsonObject(MemberSerialization.OptIn)]
    [ConfigFile("Controler.json", true, false)]

    public class InterstellarModConsole : SingletonOptions<InterstellarModConsole>
    {
        private const string DebugOptionName = "Interstellar.BUILDINGS.PREFABS.OPTIONS.DEBUGTEXT1";
        private const string DebugOptionTooltip = "Interstellar.BUILDINGS.PREFABS.OPTIONS.DEBUGTEXT2";

        [Option(DebugOptionName, DebugOptionTooltip, null)][JsonProperty] public bool OptionsDebugMode { get; set; } = false;
    }
}