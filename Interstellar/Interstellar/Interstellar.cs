using FMODUnity;
using HarmonyLib;
using KMod;
using KSerialization;
using Newtonsoft.Json;
using PeterHan.PLib.Core;
using PeterHan.PLib.Database;
using PeterHan.PLib.Options;
using PeterHan.PLib.UI;
using STRINGS;
using System;
using System.Collections.Generic;
using System.Reflection;
using TUNING;
using UnityEngine;
using UnityEngine.UI;
using static DetailCollapsableLabel;
using static Grid.Restriction;
using static Interstellar.AsteroidEngine;
using static KTabMenuHeader;
using static SandboxToolParameterMenu;
using static SetTextStyleSetting;
using static STRINGS.BUILDINGS.PREFABS.TEMPORALTEAROPENER;
using static STRINGS.UI.NEWBUILDCATEGORIES;

namespace Interstellar
{
    public static class StringUtils
    {
        public static void AddBuildingStrings(
          string buildingId,
          string name,
          string description,
          string effect)
        {
            Strings.Add($"STRINGS.BUILDINGS.PREFABS.{buildingId.ToUpperInvariant()}.NAME", STRINGS.UI.FormatAsLink(name, buildingId));
            Strings.Add($"STRINGS.BUILDINGS.PREFABS.{buildingId.ToUpperInvariant()}.DESC", description);
            Strings.Add($"STRINGS.BUILDINGS.PREFABS.{buildingId.ToUpperInvariant()}.EFFECT", effect);
        }

        public static void AddStatusItemStrings(string id, string prefix, string name, string tooltip)
        {
            Strings.Add($"STRINGS.{prefix.ToUpperInvariant()}.STATUSITEMS.{id.ToUpperInvariant()}.NAME", name);
            Strings.Add($"STRINGS.{prefix.ToUpperInvariant()}.STATUSITEMS.{id.ToUpperInvariant()}.TOOLTIP", tooltip);
        }

        public static void AddSideScreenStrings(string key, string title, string tooltip)
        {
            Strings.Add($"STRINGS.UI.UISIDESCREENS.{key.ToUpperInvariant()}.TITLE", title);
            Strings.Add($"STRINGS.UI.UISIDESCREENS.{key.ToUpperInvariant()}.TOOLTIP", tooltip);
        }
    }
    public static class BuildingUtils
    {
        private static PlanScreen.PlanInfo GetMenu(HashedString category)
        {
            foreach (PlanScreen.PlanInfo menu in TUNING.BUILDINGS.PLANORDER)
            {
                if (menu.category == category)
                    return menu;
            }
            throw new Exception("The plan menu was not found in TUNING.BUILDINGS.PLANORDER.");
        }
        public static void AddBuildingToPlanScreen(string buildingID, HashedString category, string addAferID = null)
        {
            List<string> data = BuildingUtils.GetMenu(category).data;
            if (data == null)
                return;
            if (addAferID != null)
            {
                int num = data.IndexOf(addAferID);
                if (num == -1 || num == data.Count - 1)
                    data.Add(buildingID);
                else
                    data.Insert(num + 1, buildingID);
            }
            else
                data.Add(buildingID);
        }

        public static void AddBuildingToTech(string buildingID, string techID)
        {
            Db.Get().Techs.Get(techID)?.unlockedItemIDs.Add(buildingID);
        }
    }
    public static class BUILDINGS
    {
        public static class PREFABS
        {
            public static class ENGINECONSOLE
            {
                public static LocString ID = "EngineConsole";
                public static LocString NAME = (LocString)STRINGS.UI.FormatAsLink("Asteroid Engine Console", ID);
                public static LocString DESC = (LocString)"It can only be built on the primary planet, and only one can be built at a time. Multiple <link=\"ASTEROIDENGINE\">asteroid engines</link> can be controlled.";
                public static LocString EFFECT = (LocString)"The <link=\"ASTEROIDENGINE\">asteroid engine</link> control panel has four settings to control the engine's output power. Settings 1: Power consumption 0, engine stops. Settings 2: Power consumption 25kW. Settings 3: Power consumption 35kW. Settings 4: Power consumption 45kW.";
                public static float POWER_CONSUMPTION_0 = 0f;
                public static float POWER_CONSUMPTION_1 = 25000f;
                public static float POWER_CONSUMPTION_2 = 35000f;
                public static float POWER_CONSUMPTION_3 = 45000f;
                // 小行星引擎基础速度。ClusterTraveler 移动 1 个星图格需要 600 movePotential；
                // 这里 1.0 表示：1 档位 * 1 个引擎时，大约 1 周期移动 1 格。最终星图飞行速度 = 基础速度 * 控制台档位 * 可用小行星引擎数量
                public static float SPEED_CONSTANS = 0.15f;
            }
            public static class ASTEROIDENGINE
            {
                public static LocString ID = "AsteroidEngine";
                public static LocString NAME = (LocString)STRINGS.UI.FormatAsLink("Asteroid Engine", ID);
                public static LocString DESC = (LocString)"It can only be built on the primary asteroid, and only on the bottom of an asteroid (its height must be less than 30 blocks from the bottom).";
                public static LocString EFFECT = (LocString)"Full control is provided by the <link=\"ENGINECONSOLE\">asteroid engine console</link>, which propels the asteroid forward.";
            }
            public static class ENGINECONSOLESIDESCREEN
            {
                public static LocString SIDESCREENTITLE = "Asteroid Engine Console";
                public static LocString DESTINATIONBUTTON = "Set Destination";
                public static LocString FRAMELABLETITLE = "Power Level";
                public static LocString POWERLEVELTIP1 = "Level 0: Engines Off";
                public static LocString POWERLEVELTIP2 = "Level 3";
                public static LocString POWERSTATUS = "Current power consumption : ";
                public static LocString ENGINESSTATUSTITLE = "Engines Status";
                public static LocString ENGINESCOUNTER = "Engines Connected";
                public static LocString ENGINESSTATUSOFF = "Engines Status : Offline";
                public static LocString ENGINESSTATUSTRANS = "Engines Status : During gear shift........";
                public static LocString ENGINESSTATUSNORMAL = "Engines Status : Normal"; 
                public static LocString ENGINESSTATUSOFFNODES = "Engines Status : Stop. No destination.";
            }
            public static class FAILREASONS
            {
                public static LocString ASTEROIDENGINEBUILDINGRULE = "Asteroid Engine can only be built on the bottom of an asteroid (its height must be less than 30 blocks from the bottom).";
                public static LocString ASTEROIDENGINECONSOLEBUILDINGRULE = "Only one control console can be built on an asteroid, and it can only be built on the primary asteroid.";
            }
            public static class CLUSTERSCREENTIPS
            {
                public static LocString TIPS1 = "About : ";
                public static LocString TIPS2 = " cycles remaining...";
                public static LocString TIPS3 = "No power";
                public static LocString TIPS4 = "Destination too close to obstacles (At least 1 grid in radius).";
                public static LocString TIPS5 = "Next cell in";
            }
            public static class NEWWORLD
            {
                public static LocString TIPS1 = "Backing up save file to: ";
                public static LocString TIPS2 = "Decomposing its own elements";
                public static LocString TIPS3 = "Traversing the wormhole";
                public static LocString TIPS4 = "Reconstructing";
            }
            public static class TEMPORALTEAR
            {
                public static LocString TIPS1 = "Temporal Tear";
                public static LocString TIPS2 = "Travel to the new world ? You won't be able to return to the old world.";
                public static LocString TIPS3 = "Yes.";
                public static LocString TIPS4 = "No, wait.";
            }
            public static class LIMITEDRESOURCE
            {
                public static LocString GEYSERSSIDESCREENTEXT1 = "Geyser Surplus Storage(%)";
                public static LocString GEYSERSSIDESCREENTEXT2 = "Storage Remaining: Unknown";
                public static LocString GEYSERSSIDESCREENTEXT3 = "Requires Geyser Research";
                public static LocString GEYSERSSIDESCREENTEXT4 = "Geyser Reservoir Depleted";
                public static LocString GEYSERSSIDESCREENTEXT5 = "This geyser's eruptible reserve has been depleted.";

                public static LocString OILWELLTEXT1 = "Oil Reservoir Storage(%)";
                public static LocString OILWELLTEXT2 = "No oil reservoir found.";
                public static LocString OILWELLTEXT3 = "Oil reservoir depleted";
                public static LocString OILWELLTEXT4 = "This oil reservoir has no remaining extractable oil.";

                public static LocString GEOTHERMALTEXT1 = "Geothermal Heat Pump Reserve(%)";
                public static LocString GEOTHERMALTEXT2 = "Geothermal heat pump reserve depleted";
                public static LocString GEOTHERMALTEXT3 = "This geothermal heat pump has no remaining output reserve.";
            }
            public static class OPTIONS
            {
                public static LocString DEBUGTEXT1 = "Debug Mode";
                public static LocString DEBUGTEXT2 = "Enable or Disable Debug Mode";

            }
        }
    }

    [HarmonyPatch(typeof(Db))]
    [HarmonyPatch("Initialize")]
    public class Db_Initialize_Patch
    {
        static bool isPatched = false;
        public static void Prefix()
        {
            if (Interstellar.dic != null)
            {
                string name, desc, effect;
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.ENGINECONSOLE.NAME", out name);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.ENGINECONSOLE.DESC", out desc);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.ENGINECONSOLE.EFFECT", out effect);

                StringUtils.AddBuildingStrings(BUILDINGS.PREFABS.ENGINECONSOLE.ID, name, desc, effect);


                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.ASTEROIDENGINE.NAME", out name);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.ASTEROIDENGINE.DESC", out desc);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.ASTEROIDENGINE.EFFECT", out effect);

                StringUtils.AddBuildingStrings(BUILDINGS.PREFABS.ASTEROIDENGINE.ID, name, desc, effect);
            }
            else
            {
                StringUtils.AddBuildingStrings(BUILDINGS.PREFABS.ENGINECONSOLE.ID, (string)BUILDINGS.PREFABS.ENGINECONSOLE.NAME, (string)BUILDINGS.PREFABS.ENGINECONSOLE.DESC, (string)BUILDINGS.PREFABS.ENGINECONSOLE.EFFECT);
                StringUtils.AddBuildingStrings(BUILDINGS.PREFABS.ASTEROIDENGINE.ID, (string)BUILDINGS.PREFABS.ASTEROIDENGINE.NAME, (string)BUILDINGS.PREFABS.ASTEROIDENGINE.DESC, (string)BUILDINGS.PREFABS.ASTEROIDENGINE.EFFECT);
            }
        }
        public static void Postfix()
        {
            if (isPatched)
            {
                return;
            }

            BuildingUtils.AddBuildingToPlanScreen(BUILDINGS.PREFABS.ENGINECONSOLE.ID, (HashedString)"Base");
            BuildingUtils.AddBuildingToTech(BUILDINGS.PREFABS.ENGINECONSOLE.ID, "CryoFuelPropulsion");

            BuildingUtils.AddBuildingToPlanScreen(BUILDINGS.PREFABS.ASTEROIDENGINE.ID, (HashedString)"Base");
            BuildingUtils.AddBuildingToTech(BUILDINGS.PREFABS.ASTEROIDENGINE.ID, "CryoFuelPropulsion");

            isPatched = true;
        }
    }
    public class Interstellar : UserMod2
    {
        public static string modPath;
        public static string EngineConsoleAssetsPath;
        public static Sprite EngineConsoleTargetIcon;
        public static Dictionary<string, string> dic = null;
        public static string Fail_AsteroidEngineBuildRule;
        public static string Fail_AsteroidEngineConsoleBuildRule;
        public static string ClusterScreenTip1, ClusterScreenTip2, ClusterScreenTip3, ClusterScreenTip4, ClusterScreenTip5;
        public static string NewWorldTip1, NewWorldTip2, NewWorldTip3, NewWorldTip4;
        public static string TemporalTearTip1, TemporalTearTip2, TemporalTearTip3, TemporalTearTip4;
        public static string GeysersText1, GeysersText2, GeysersText3, GeysersText4, GeysersText5;
        public static string OilWellText1, OilWellText2, OilWellText3, OilWellText4;
        public static string DebugText1, DebugText2;
        public static string GeothermalText1, GeothermalText2, GeothermalText3;
        public override void OnLoad(HarmonyLib.Harmony harmony)
        {
            base.OnLoad(harmony);
            PUtil.InitLibrary();

            modPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            EngineConsoleAssetsPath = System.IO.Path.Combine(modPath, @"anim\assets\engine_console");

            EngineConsoleTargetIcon = PUIUtils.LoadSpriteFile(System.IO.Path.Combine(EngineConsoleAssetsPath, "target_icon.png"));

            string templatePath = System.IO.Path.Combine(modPath, "translations");
            string langCode = Localization.GetCurrentLanguageCode();
            string poPath = System.IO.Path.Combine(modPath, "translations", langCode + ".po");
            string _po_path = poPath.Replace("_klei", "");
            if (System.IO.File.Exists(_po_path))
                dic = Localization.LoadStringsFile(_po_path, false);

            Fail_AsteroidEngineBuildRule = BUILDINGS.PREFABS.FAILREASONS.ASTEROIDENGINEBUILDINGRULE;
            Fail_AsteroidEngineConsoleBuildRule = BUILDINGS.PREFABS.FAILREASONS.ASTEROIDENGINECONSOLEBUILDINGRULE;
            ClusterScreenTip1 = BUILDINGS.PREFABS.CLUSTERSCREENTIPS.TIPS1;
            ClusterScreenTip2 = BUILDINGS.PREFABS.CLUSTERSCREENTIPS.TIPS2;
            ClusterScreenTip3 = BUILDINGS.PREFABS.CLUSTERSCREENTIPS.TIPS3;
            ClusterScreenTip4 = BUILDINGS.PREFABS.CLUSTERSCREENTIPS.TIPS4;
            ClusterScreenTip5 = BUILDINGS.PREFABS.CLUSTERSCREENTIPS.TIPS5;
            NewWorldTip1 = BUILDINGS.PREFABS.NEWWORLD.TIPS1;
            NewWorldTip2 = BUILDINGS.PREFABS.NEWWORLD.TIPS2;
            NewWorldTip3 = BUILDINGS.PREFABS.NEWWORLD.TIPS3;
            NewWorldTip4 = BUILDINGS.PREFABS.NEWWORLD.TIPS4;
            TemporalTearTip1 = BUILDINGS.PREFABS.TEMPORALTEAR.TIPS1;
            TemporalTearTip2 = BUILDINGS.PREFABS.TEMPORALTEAR.TIPS2;
            TemporalTearTip3 = BUILDINGS.PREFABS.TEMPORALTEAR.TIPS3;
            TemporalTearTip4 = BUILDINGS.PREFABS.TEMPORALTEAR.TIPS4;
            GeysersText1 = BUILDINGS.PREFABS.LIMITEDRESOURCE.GEYSERSSIDESCREENTEXT1;
            GeysersText2 = BUILDINGS.PREFABS.LIMITEDRESOURCE.GEYSERSSIDESCREENTEXT2;
            GeysersText3 = BUILDINGS.PREFABS.LIMITEDRESOURCE.GEYSERSSIDESCREENTEXT3;
            GeysersText4 = BUILDINGS.PREFABS.LIMITEDRESOURCE.GEYSERSSIDESCREENTEXT4;
            GeysersText5 = BUILDINGS.PREFABS.LIMITEDRESOURCE.GEYSERSSIDESCREENTEXT5;
            OilWellText1 = BUILDINGS.PREFABS.LIMITEDRESOURCE.OILWELLTEXT1;
            OilWellText2 = BUILDINGS.PREFABS.LIMITEDRESOURCE.OILWELLTEXT2;
            OilWellText3 = BUILDINGS.PREFABS.LIMITEDRESOURCE.OILWELLTEXT3;
            OilWellText4 = BUILDINGS.PREFABS.LIMITEDRESOURCE.OILWELLTEXT4;
            DebugText1 = BUILDINGS.PREFABS.OPTIONS.DEBUGTEXT1;
            DebugText2 = BUILDINGS.PREFABS.OPTIONS.DEBUGTEXT2;
            GeothermalText1 = BUILDINGS.PREFABS.LIMITEDRESOURCE.GEOTHERMALTEXT1;
            GeothermalText2 = BUILDINGS.PREFABS.LIMITEDRESOURCE.GEOTHERMALTEXT2;
            GeothermalText3 = BUILDINGS.PREFABS.LIMITEDRESOURCE.GEOTHERMALTEXT3;

            if (Interstellar.dic != null)
            {
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.FAILREASONS.ASTEROIDENGINEBUILDINGRULE", out Fail_AsteroidEngineBuildRule);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.FAILREASONS.ASTEROIDENGINECONSOLEBUILDINGRULE", out Fail_AsteroidEngineConsoleBuildRule);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.CLUSTERSCREENTIPS.TIPS1", out ClusterScreenTip1);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.CLUSTERSCREENTIPS.TIPS2", out ClusterScreenTip2);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.CLUSTERSCREENTIPS.TIPS3", out ClusterScreenTip3);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.CLUSTERSCREENTIPS.TIPS4", out ClusterScreenTip4);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.CLUSTERSCREENTIPS.TIPS5", out ClusterScreenTip5);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.NEWWORLD.TIPS1", out NewWorldTip1);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.NEWWORLD.TIPS2", out NewWorldTip2);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.NEWWORLD.TIPS3", out NewWorldTip3);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.NEWWORLD.TIPS4", out NewWorldTip4);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.TEMPORALTEAR.TIPS1", out TemporalTearTip1);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.TEMPORALTEAR.TIPS2", out TemporalTearTip2);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.TEMPORALTEAR.TIPS3", out TemporalTearTip3);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.TEMPORALTEAR.TIPS4", out TemporalTearTip4);

                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.LIMITEDRESOURCE.GEYSERSSIDESCREENTEXT1", out GeysersText1);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.LIMITEDRESOURCE.GEYSERSSIDESCREENTEXT2", out GeysersText2);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.LIMITEDRESOURCE.GEYSERSSIDESCREENTEXT3", out GeysersText3);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.LIMITEDRESOURCE.GEYSERSSIDESCREENTEXT4", out GeysersText4);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.LIMITEDRESOURCE.GEYSERSSIDESCREENTEXT5", out GeysersText5);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.LIMITEDRESOURCE.OILWELLTEXT1", out OilWellText1);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.LIMITEDRESOURCE.OILWELLTEXT2", out OilWellText2);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.LIMITEDRESOURCE.OILWELLTEXT3", out OilWellText3);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.LIMITEDRESOURCE.OILWELLTEXT4", out OilWellText4);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.OPTIONS.DEBUGTEXT1", out DebugText1);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.OPTIONS.DEBUGTEXT2", out DebugText2);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.LIMITEDRESOURCE.GEOTHERMALTEXT1", out GeothermalText1);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.LIMITEDRESOURCE.GEOTHERMALTEXT2", out GeothermalText2);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.LIMITEDRESOURCE.GEOTHERMALTEXT3", out GeothermalText3);

            }

            Strings.Add("Interstellar.BUILDINGS.PREFABS.OPTIONS.DEBUGTEXT1", DebugText1);
            Strings.Add("Interstellar.BUILDINGS.PREFABS.OPTIONS.DEBUGTEXT2", DebugText2);

            new POptions().RegisterOptions(this, typeof(InterstellarModConsole));
        }
    }
    public class SideScreenUtil
    {
        public static void AddCustomSideScreen<T>(string name, GameObject prefab, List<DetailsScreen.SideScreenRef> existScreens)
        {
            SideScreenContent prefab1 = prefab.AddComponent(typeof(T)) as SideScreenContent;
            existScreens.Add(SideScreenUtil.NewSideScreen(name, prefab1));
        }
        private static DetailsScreen.SideScreenRef NewSideScreen(string name, SideScreenContent prefab)
        {
            return new DetailsScreen.SideScreenRef()
            {
                name = name,
                offset = Vector2.zero,
                screenPrefab = prefab
            };
        }
        private static bool GetElements(
            out List<DetailsScreen.SideScreenRef> screens,
            out GameObject contentBody)
        {
            Traverse traverse = Traverse.Create((object)DetailsScreen.Instance);
            screens = traverse.Field("sideScreens").GetValue<List<DetailsScreen.SideScreenRef>>();
            contentBody = traverse.Field("sideScreenConfigContentBody").GetValue<GameObject>();
            return screens != null && contentBody != null;
        }
    }

    [HarmonyPatch(typeof(DetailsScreen), "OnPrefabInit")]
    public static class DetailsScreen_OnPrefabInit_Patch
    {
        public static void Postfix(List<DetailsScreen.SideScreenRef> ___sideScreens)
        {
            PUIUtils.AddSideScreenContent<EngineConsoleSideScreen>();
        }
    }
    public class EngineConsoleConfig : IBuildingConfig
    {
        public override string[] GetRequiredDlcIds() => DlcManager.EXPANSION1;

        public const float POWER_CONSUMPTION = 120f; // W
        public override BuildingDef CreateBuildingDef()
        {
            int width = 5;
            int height = 5;
            string anim = "engine_console_kanim";
            string[] MaterialCategory = new string[1] { "RefinedMetal" };

            int hitpoints = 30;
            float construction_time = 30f;
            float[] construction_mass = new float[1] { 250f };
            float melting_point = 3000f;
            BuildLocationRule build_location_rule = BuildLocationRule.OnFloor;
            EffectorValues none = NOISE_POLLUTION.NONE;
            BuildingDef buildingDef = BuildingTemplates.CreateBuildingDef(BUILDINGS.PREFABS.ENGINECONSOLE.ID, width, height, anim, hitpoints, construction_time, construction_mass, MaterialCategory, melting_point, build_location_rule, TUNING.BUILDINGS.DECOR.PENALTY.TIER1, none);
            buildingDef.Floodable = true;
            buildingDef.AudioCategory = "Metal";
            buildingDef.Overheatable = true;
            buildingDef.Repairable = true;
            buildingDef.Disinfectable = false;
            buildingDef.Invincible = false;
            buildingDef.LogicInputPorts = LogicOperationalController.CreateSingleInputPortList(new CellOffset(0, 0));
            buildingDef.PowerInputOffset = new CellOffset(0, 0);
            buildingDef.SceneLayer = Grid.SceneLayer.Building;
            buildingDef.ForegroundLayer = Grid.SceneLayer.BuildingFront;
            buildingDef.RequiresPowerInput = true;
            buildingDef.SelfHeatKilowattsWhenActive = 100;
            buildingDef.EnergyConsumptionWhenActive = POWER_CONSUMPTION;
            buildingDef.OverheatTemperature = 373.15f; //K
            buildingDef.DragBuild = true;
            buildingDef.PermittedRotations = PermittedRotations.Unrotatable;

            return buildingDef;
        }

        public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
        {
            go.AddOrGet<Operational>();
        }
        public override void DoPostConfigureComplete(GameObject go)
        {
            go.AddOrGet<EngineConsole>();
        }
    }
    public class EngineConsole : StateMachineComponent<EngineConsole.Instance>
    {
        private Action<object> m_onClusterLocationChangedDelegate;
        private Action<object> m_onClusterDestinationChangedDelegate;
        private Action<object> m_onConsoleOperationalChangedDelegate;
        private bool suppressDestinationSave;
        private bool restoringSavedDestination;
        private bool delayingPowerRestore;

        private WorldContainer masterWorld;
        private Asteroidcraft asteroidcraft;

        public RocketClusterDestinationSelector clusterSeletor;
        public ClusterTraveler clusterTraveler;

        private static readonly FieldInfo ClusterTravelerCachedPathField = AccessTools.Field(typeof(ClusterTraveler), "m_cachedPath");
        private static readonly FieldInfo ClusterTravelerCachedPathDestinationField = AccessTools.Field(typeof(ClusterTraveler), "m_cachedPathDestination");
        private static readonly FieldInfo ClusterTravelerMovePotentialField = AccessTools.Field(typeof(ClusterTraveler), "m_movePotential");
        private static readonly FieldInfo ClusterTravelerIsPathDirtyField = AccessTools.Field(typeof(ClusterTraveler), "m_isPathDirty");
        private static readonly MethodInfo ClusterTravelerUpdateAnimationTagsMethod = AccessTools.Method(typeof(ClusterTraveler), "UpdateAnimationTags");
        private static readonly FieldInfo RocketSelectorPreviousDestinationField = AccessTools.Field(typeof(RocketClusterDestinationSelector), "m_prevDestination");

        [MyCmpGet]
        public Operational operational;

        [Serialize]
        private int savedSliderValue;
        [Serialize]
        private bool hasSavedClusterDestination;
        [Serialize]
        private AxialI savedClusterDestination;
        public int sliderValue
        {
            get { return savedSliderValue; }
            private set { savedSliderValue = value; }
        }
        public KBatchedAnimController anim_controller;
        public static EngineConsole StaticInstance;
        public List<AsteroidEngine> Engines = new List<AsteroidEngine>();
        public bool SetPowerLevel(int level)
        {
            level = Mathf.Clamp(level, 0, 3);
            if (operational != null && operational.IsOperational == false)
            {
                sliderValue = level;
                SetAnimBySliderValue(sliderValue);
                StopEnginesFromConsoleState();
                return false;
            }

            if (clusterTraveler != null)
            {
                if (clusterTraveler.CurrentPath != null && clusterTraveler.CurrentPath.Count == 0 && level != 0)
                {
                    if (EngineStatusCallBack != null)
                    {
                        EngineStatusCallBack.Invoke(EngineStatus.off_no_dest);
                    }
                    return false;
                }
            }

            foreach (AsteroidEngine engine in Engines)
            {
                if (engine.IsTransitioning == true)
                {
                    return false;
                }
            }
            sliderValue = level;
            SetAnimBySliderValue(sliderValue);
            foreach (AsteroidEngine engine in Engines)
            {
                engine.SetPowerLevel(level);
            }
            return true;
        }

        private Action<EngineStatus> EngineStatusCallBack = null;
        public void SubscriptEngineStatus(Action<EngineStatus> callback)
        {
            EngineStatusCallBack = callback;
        }
        public void UnSubscriptEngineStatus(Action<EngineStatus> callback)
        {
            if (EngineStatusCallBack == callback)
            {
                EngineStatusCallBack = null;
            }
        }
        private int engine_status_report_count = 0;
        private void EngineStatusCallback(AsteroidEngine engine, AsteroidEngine.EngineStatus status)
        {
            engine_status_report_count++;

            if (engine_status_report_count == Engines.Count)
            {
                EngineStatusCallBack?.Invoke(status);
                engine_status_report_count = 0;
            }
        }
        public void RegisterEngine(AsteroidEngine engine)
        {
            if (!Engines.Contains(engine))
            {
                Engines.Add(engine);
                if (!delayingPowerRestore)
                {
                    engine.SetPowerLevel(GetEffectiveEnginePowerLevel());
                }
                engine.SubscriptEngineStatus(EngineStatusCallback);
                if (EngineCounterChangedCallBack != null)
                {
                    EngineCounterChangedCallBack.Invoke(Engines.Count);
                }
            }
        }
        private int GetEffectiveEnginePowerLevel()
        {
            if (operational != null && operational.IsOperational == false)
            {
                return 0;
            }
            if (clusterSeletor != null && clusterSeletor.IsAtDestination())
            {
                return 0;
            }
            if (clusterTraveler != null && clusterTraveler.CurrentPath != null && clusterTraveler.CurrentPath.Count == 0)
            {
                return 0;
            }
            return sliderValue;
        }
        public void UnregisterEngine(AsteroidEngine engine)
        {
            if (Engines.Contains(engine))
            {
                Engines.Remove(engine);
                if (EngineCounterChangedCallBack != null)
                {
                    EngineCounterChangedCallBack.Invoke(Engines.Count);
                }
            }
        }
        public EngineStatus GetCurrentEngineStatus()
        {
            if (operational != null && operational.IsOperational == false)
            {
                return EngineStatus.off;
            }
            if (sliderValue > 0 && clusterSeletor != null && clusterSeletor.IsAtDestination())
            {
                return EngineStatus.off_no_dest;
            }
            foreach (AsteroidEngine engine in Engines)
            {
                if (engine != null && engine.IsTransitioning)
                {
                    return engine.engine_status;
                }
            }
            foreach (AsteroidEngine engine in Engines)
            {
                if (engine != null && engine.engine_status == EngineStatus.on)
                {
                    return EngineStatus.on;
                }
            }
            if (sliderValue > 0 && Engines.Count > 0)
            {
                return EngineStatus.on;
            }
            return EngineStatus.off;
        }
        public void DoAction()
        {
            if (ClusterMapScreen.Instance == null || clusterSeletor == null)
            {
                return;
            }
            ClusterMapScreen.Instance.ShowInSelectDestinationMode(clusterSeletor);
            AxialI myWorldLocation = clusterSeletor.GetMyWorldLocation();
            AxialI destination = clusterSeletor.GetDestination();
            AxialI adjacentCellLocation = ClusterGrid.Instance.GetRandomVisibleAdjacentCellLocation(myWorldLocation, destination);
            if (adjacentCellLocation != AxialI.INVALID)
            {
                ClusterMapScreen.Instance.OnHoverHex(ClusterMapScreen.Instance.GetClusterMapHexAtLocation(adjacentCellLocation));
            }
        }
        protected override void OnSpawn()
        {
            base.OnSpawn();
            anim_controller = GetComponentInParent<KBatchedAnimController>();
            StaticInstance = this;
            SetAnimBySliderValue(sliderValue);
            this.smi.StartSM();
            SetupWorldtraveller();
            m_onConsoleOperationalChangedDelegate = new Action<object>(this.OnConsoleOperationalChanged);
            Subscribe((int)GameHashes.OperationalChanged, m_onConsoleOperationalChangedDelegate);
            delayingPowerRestore = true;
            RegisterExistingEngines();
            ScheduleRestoreSavedDestination();
        }
        protected override void OnCleanUp()
        {
            StopAndReleaseRegisteredEngines();
            CleanupWorldtravellerSubscriptions();
            if (StaticInstance == this)
            {
                StaticInstance = null;
            }
            base.OnCleanUp();
        }
        private void StopAndReleaseRegisteredEngines()
        {
            foreach (BuildingComplete building in Components.BuildingCompletes.Items)
            {
                AsteroidEngine engine = building.GetComponent<AsteroidEngine>();
                if (engine == null)
                {
                    continue;
                }

                engine.SetPowerLevel(0);
                engine.UnSubscriptEngineStatus(EngineStatusCallback);
            }

            foreach (AsteroidEngine engine in new List<AsteroidEngine>(Engines))
            {
                if (engine == null)
                {
                    continue;
                }

                engine.SetPowerLevel(0);
                engine.UnSubscriptEngineStatus(EngineStatusCallback);
            }
            Engines.Clear();
            EngineCounterChangedCallBack?.Invoke(0);
            EngineStatusCallBack?.Invoke(EngineStatus.off);
        }
        protected override void OnPrefabInit()
        {

        }
        public void SetAnimBySliderValue(int value)
        {
            if (anim_controller == null)
            {
                return;
            }
            if (value == 0)
            {
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"console_joystick_enable0", false);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"console_joystick_enable1", false);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"console_joystick_enable2", false);

                anim_controller.SetSymbolVisiblity((KAnimHashedString)"console_speed_light1_enable", false);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"console_speed_light2_enable", false);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"console_speed_light3_enable", false);

                anim_controller.SetSymbolVisiblity((KAnimHashedString)"console_joystick_disable0", true);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"console_joystick_disable1", true);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"console_joystick_disable2", true);
            }
            if (value == 1)
            {
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"console_joystick_enable0", true);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"console_joystick_enable1", false);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"console_joystick_enable2", false);

                anim_controller.SetSymbolVisiblity((KAnimHashedString)"console_speed_light1_enable", true);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"console_speed_light2_enable", false);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"console_speed_light3_enable", false);

                anim_controller.SetSymbolVisiblity((KAnimHashedString)"console_joystick_disable0", false);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"console_joystick_disable1", true);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"console_joystick_disable2", true);
            }
            if (value == 2)
            {
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"console_joystick_enable0", true);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"console_joystick_enable1", true);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"console_joystick_enable2", false);

                anim_controller.SetSymbolVisiblity((KAnimHashedString)"console_speed_light1_enable", true);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"console_speed_light2_enable", true);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"console_speed_light3_enable", false);

                anim_controller.SetSymbolVisiblity((KAnimHashedString)"console_joystick_disable0", false);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"console_joystick_disable1", false);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"console_joystick_disable2", true);
            }
            if (value == 3)
            {
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"console_joystick_enable0", true);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"console_joystick_enable1", true);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"console_joystick_enable2", true);

                anim_controller.SetSymbolVisiblity((KAnimHashedString)"console_speed_light1_enable", true);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"console_speed_light2_enable", true);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"console_speed_light3_enable", true);

                anim_controller.SetSymbolVisiblity((KAnimHashedString)"console_joystick_disable0", false);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"console_joystick_disable1", false);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"console_joystick_disable2", false);
            }
        }
        private void SetupWorldtraveller()
        {
            CleanupWorldtravellerSubscriptions();
            foreach (WorldContainer world in ClusterManager.Instance.WorldContainers)
            {
                if (world.id == 0)
                {
                    masterWorld = world;
                    break;
                }
            }
            if (masterWorld == null)
            {
                return;
            }
            RemoveDuplicateWorldTravellerComponents();
            clusterSeletor = masterWorld.gameObject.AddOrGet<RocketClusterDestinationSelector>();
            clusterTraveler = masterWorld.gameObject.AddOrGet<ClusterTraveler>();

            asteroidcraft = clusterSeletor.gameObject.AddOrGet<Asteroidcraft>();

            m_onClusterLocationChangedDelegate = new Action<object>(this.ClusterLocationChangedDelegate);
            clusterSeletor.Subscribe((int)GameHashes.ClusterLocationChanged, m_onClusterLocationChangedDelegate);

            clusterTraveler.getSpeedCB = new Func<float>(this.GetTravelerSpeed);
            clusterTraveler.getCanTravelCB = new Func<bool, bool>((_) => true);
            clusterTraveler.onTravelCB = new System.Action(this.OnTravellerMoved);

            m_onClusterDestinationChangedDelegate = new Action<object>(this.ClusterDestinationChangedDelegate);
            clusterTraveler.Subscribe((int)GameHashes.ClusterDestinationChanged, m_onClusterDestinationChangedDelegate);
        }
        private void CleanupWorldtravellerSubscriptions()
        {
            if (clusterSeletor != null && m_onClusterLocationChangedDelegate != null)
            {
                clusterSeletor.Unsubscribe((int)GameHashes.ClusterLocationChanged, m_onClusterLocationChangedDelegate);
            }
            if (clusterTraveler != null && m_onClusterDestinationChangedDelegate != null)
            {
                clusterTraveler.Unsubscribe((int)GameHashes.ClusterDestinationChanged, m_onClusterDestinationChangedDelegate);
            }
            m_onClusterLocationChangedDelegate = null;
            m_onClusterDestinationChangedDelegate = null;
        }
        private void RemoveDuplicateWorldTravellerComponents()
        {
            if (masterWorld == null)
            {
                return;
            }

            RocketClusterDestinationSelector[] selectors = masterWorld.gameObject.GetComponents<RocketClusterDestinationSelector>();
            for (int i = 1; i < selectors.Length; i++)
            {
                UnityEngine.Object.Destroy(selectors[i]);
            }

            ClusterTraveler[] travelers = masterWorld.gameObject.GetComponents<ClusterTraveler>();
            for (int i = 1; i < travelers.Length; i++)
            {
                UnityEngine.Object.Destroy(travelers[i]);
            }
        }
        private void ScheduleRestoreSavedDestination()
        {
            GameScheduler.Instance.ScheduleNextFrame("Restore Interstellar destination", _ =>
            {
                if (clusterSeletor == null || clusterTraveler == null)
                {
                    return;
                }

                RestoreSavedDestination();
                GameScheduler.Instance.ScheduleNextFrame("Restore Interstellar power level", __ =>
                {
                    if (clusterSeletor == null || clusterTraveler == null)
                    {
                        return;
                    }

                    delayingPowerRestore = false;
                    SetPowerLevel(sliderValue);
                });
            });
        }
        private void RestoreSavedDestination()
        {
            if (hasSavedClusterDestination)
            {
                restoringSavedDestination = true;
                try
                {
                    clusterSeletor.SetDestination(savedClusterDestination);
                }
                finally
                {
                    restoringSavedDestination = false;
                }
            }
        }
        public static void ClearStaticDestinationAndStopForStarmapRefresh(string reason)
        {
            if (StaticInstance != null)
            {
                StaticInstance.ClearDestinationAndStopForStarmapRefresh(reason);
            }
        }
        public void ClearDestinationAndStopForStarmapRefresh(string reason)
        {
            try
            {
                if (clusterSeletor == null || clusterTraveler == null)
                {
                    SetupWorldtraveller();
                }
                if (clusterSeletor == null)
                {
                    Debug.LogWarning($"[Interstellar] Asteroid engine console destination clear skipped. reason={reason}, selector unavailable.");
                    return;
                }

                AxialI currentLocation = clusterSeletor.GetMyWorldLocation();
                if (currentLocation == AxialI.INVALID && masterWorld != null)
                {
                    AsteroidGridEntity asteroid = masterWorld.GetComponent<AsteroidGridEntity>();
                    if (asteroid != null)
                    {
                        currentLocation = asteroid.Location;
                    }
                }
                if (currentLocation == AxialI.INVALID)
                {
                    Debug.LogWarning($"[Interstellar] Asteroid engine console destination clear skipped. reason={reason}, current location invalid.");
                    return;
                }

                hasSavedClusterDestination = true;
                savedClusterDestination = currentLocation;
                clusterSeletor.Repeat = false;
                RocketSelectorPreviousDestinationField?.SetValue(clusterSeletor, AxialI.INVALID);
                clusterSeletor.SetDestination(currentLocation);

                ClusterGridEntity craftEntity = clusterSeletor.GetComponent<ClusterGridEntity>();
                if (clusterTraveler != null && craftEntity != null)
                {
                    ResetTravelerPathCache(clusterTraveler, craftEntity, clusterSeletor, currentLocation);
                }

                SetAnimBySliderValue(0);
                foreach (AsteroidEngine engine in Engines)
                {
                    if (engine != null)
                    {
                        engine.SetPowerLevel(0);
                    }
                }
                EngineStatusCallBack?.Invoke(sliderValue != 0 ? EngineStatus.off_no_dest : EngineStatus.off);
                if (ClusterMapScreen.Instance != null)
                {
                    ClusterMapScreen.Instance.Trigger((int)GameHashes.UIRefresh, null);
                }

                Debug.Log($"[Interstellar] Asteroid engine console destination cleared for starmap refresh. reason={reason}, location={currentLocation}, sliderValue={sliderValue}, engines={Engines.Count}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Interstellar] ClearDestinationAndStopForStarmapRefresh failed. reason={reason}, error={ex}");
            }
        }
        private void RegisterExistingEngines()
        {
            Engines.Clear();
            foreach (BuildingComplete building in Components.BuildingCompletes.Items)
            {
                AsteroidEngine engine = building.GetComponent<AsteroidEngine>();
                if (engine != null && engine.operational != null && engine.operational.IsOperational == true)
                {
                    RegisterEngine(engine);
                }
            }
        }
        private float GetTravelerSpeed()
        {
            if (operational == null || operational.IsOperational == false)
            {
                return 0f;
            }
            // 最终星图飞行速度 = 基础速度 * 控制台档位 * 可用小行星引擎数量。
            return BUILDINGS.PREFABS.ENGINECONSOLE.SPEED_CONSTANS * (float)sliderValue * (float)Engines.Count;
        }
        private void OnConsoleOperationalChanged(object data)
        {
            if (operational == null || operational.IsOperational == false)
            {
                StopEnginesFromConsoleState();
            }
            else
            {
                SetPowerLevel(sliderValue);
            }
        }
        private void StopEnginesFromConsoleState()
        {
            foreach (AsteroidEngine engine in Engines)
            {
                if (engine != null)
                {
                    engine.SetPowerLevel(0);
                }
            }
            EngineStatusCallBack?.Invoke(EngineStatus.off);
        }
        private void OnTravellerMoved()
        {
            RefreshClusterMapAfterCraftSync();
        }
        private void ClusterLocationChangedDelegate(object data)
        {
            ClusterLocationChangedEvent locationChangedEvent = (ClusterLocationChangedEvent)data;
            suppressDestinationSave = true;
            try
            {
                clusterSeletor.BoxingTrigger((int)GameHashes.ClusterDestinationChanged, clusterSeletor.GetDestination());
            }
            finally
            {
                suppressDestinationSave = false;
            }

            bool movedAnyCraft = false;
            foreach (Clustercraft crafts in Components.Clustercrafts)
            {
                if (ShouldMoveCraftWithAsteroid(crafts, locationChangedEvent))
                {
                    MoveCraftWithAsteroid(crafts, locationChangedEvent.oldLocation, locationChangedEvent.newLocation);
                    movedAnyCraft = true;
                }
            }
            if (movedAnyCraft)
            {
                RefreshClusterMapAfterCraftSync();
            }

            if (clusterSeletor.IsAtDestination())
            {
                SetAnimBySliderValue(0);
                foreach (AsteroidEngine engine in Engines)
                {
                    if (engine != null)
                    {
                        engine.SetPowerLevel(0);
                    }
                }
                EngineStatusCallBack?.Invoke(sliderValue != 0 ? EngineStatus.off_no_dest : EngineStatus.off);
            }
        }
        private bool ShouldMoveCraftWithAsteroid(Clustercraft craft, ClusterLocationChangedEvent locationChangedEvent)
        {
            if (craft == null || locationChangedEvent == null || locationChangedEvent.entity == null)
            {
                return false;
            }
            if (craft.Status != Clustercraft.CraftStatus.Grounded)
            {
                return false;
            }

            ClusterGridEntity craftEntity = craft.GetComponent<ClusterGridEntity>();
            if (craftEntity == null)
            {
                return false;
            }

            WorldContainer movingWorld = locationChangedEvent.entity.GetComponent<WorldContainer>();
            CraftModuleInterface moduleInterface = craft.ModuleInterface;
            LaunchPad currentPad = moduleInterface != null ? moduleInterface.CurrentPad : null;
            if (movingWorld == null || currentPad == null)
            {
                return false;
            }

            return currentPad.GetMyWorldId() == movingWorld.id;
        }
        private void MoveCraftWithAsteroid(Clustercraft craft, AxialI oldLocation, AxialI newLocation)
        {
            ClusterGridEntity craftEntity = craft.GetComponent<ClusterGridEntity>();
            if (craftEntity == null)
            {
                return;
            }

            AxialI previousCraftLocation = craftEntity.Location;
            craftEntity.Location = newLocation;
            craftEntity.positionDirty = true;
            RefreshCraftDestinationAndPath(craft, oldLocation, previousCraftLocation, newLocation);
        }
        private void RefreshCraftDestinationAndPath(Clustercraft craft, AxialI oldLocation, AxialI previousCraftLocation, AxialI newLocation)
        {
            CraftModuleInterface moduleInterface = craft.ModuleInterface;
            RocketClusterDestinationSelector selector = moduleInterface != null ? moduleInterface.GetClusterDestinationSelector() : craft.GetComponent<RocketClusterDestinationSelector>();
            ClusterTraveler traveler = craft.GetComponent<ClusterTraveler>();
            ClusterGridEntity craftEntity = craft.GetComponent<ClusterGridEntity>();
            if (selector == null || traveler == null || craftEntity == null || ClusterGrid.Instance == null)
            {
                return;
            }

            AxialI destination = selector.GetDestination();
            if (destination == oldLocation || destination == previousCraftLocation)
            {
                selector.SetDestination(newLocation);
                destination = newLocation;
            }
            UpdateRocketPreviousDestination(selector, oldLocation, previousCraftLocation, newLocation);
            moduleInterface?.TriggerEventOnCraftAndRocket(GameHashes.ClusterDestinationChanged, destination);
            ResetTravelerPathCache(traveler, craftEntity, selector, destination);
            craft.UpdateStatusItem();
        }
        private static void UpdateRocketPreviousDestination(RocketClusterDestinationSelector selector, AxialI oldLocation, AxialI previousCraftLocation, AxialI newLocation)
        {
            if (RocketSelectorPreviousDestinationField == null)
            {
                return;
            }

            object rawPreviousDestination = RocketSelectorPreviousDestinationField.GetValue(selector);
            if (rawPreviousDestination is AxialI previousDestination && (previousDestination == oldLocation || previousDestination == previousCraftLocation))
            {
                RocketSelectorPreviousDestinationField.SetValue(selector, newLocation);
            }
        }
        private static void ResetTravelerPathCache(ClusterTraveler traveler, ClusterGridEntity craftEntity, RocketClusterDestinationSelector selector, AxialI destination)
        {
            if (ClusterTravelerCachedPathDestinationField == null || ClusterTravelerCachedPathField == null)
            {
                return;
            }

            List<AxialI> path = craftEntity.Location == destination ? new List<AxialI>() : ClusterGrid.Instance.GetPath(craftEntity.Location, destination, selector);
            ClusterTravelerCachedPathDestinationField.SetValue(traveler, destination);
            ClusterTravelerCachedPathField.SetValue(traveler, path);
            ClusterTravelerMovePotentialField?.SetValue(traveler, 0f);
            ClusterTravelerIsPathDirtyField?.SetValue(traveler, false);
            ClusterTravelerUpdateAnimationTagsMethod?.Invoke(traveler, null);
        }
        private static void RefreshClusterMapAfterCraftSync()
        {
            if (ClusterMapScreen.Instance == null)
            {
                return;
            }

            ClusterMapScreen.Instance.Trigger(1980521255, null);
            if (GameScheduler.Instance == null)
            {
                return;
            }

            GameScheduler.Instance.ScheduleNextFrame("Refresh cluster map after asteroid craft sync", _ =>
            {
                if (ClusterMapScreen.Instance != null)
                {
                    ClusterMapScreen.Instance.Trigger(1980521255, null);
                }
            });
        }
        private void ClusterDestinationChangedDelegate(object data)
        {
            AxialI destination;
            if (!suppressDestinationSave && !restoringSavedDestination && TryGetDestinationFromEvent(data, out destination))
            {
                savedClusterDestination = destination;
                hasSavedClusterDestination = true;
            }
            if (!restoringSavedDestination)
            {
                SetPowerLevel(sliderValue);
            }
        }
        private bool TryGetDestinationFromEvent(object data, out AxialI destination)
        {
            if (data is Boxed<AxialI> boxedDestination)
            {
                destination = boxedDestination.value;
                return true;
            }
            if (data is AxialI rawDestination)
            {
                destination = rawDestination;
                return true;
            }
            destination = default(AxialI);
            return false;
        }

        private Action<int> EngineCounterChangedCallBack = null;
        public void SubscriptEngineCounterChanged(Action<int> callback)
        {
            EngineCounterChangedCallBack = callback;
        }
        public void UnSubscriptEngineCounterChanged(Action<int> callback)
        {
            if (EngineCounterChangedCallBack == callback)
            {
                EngineCounterChangedCallBack = null;
            }
        }
        public class Instance(EngineConsole master) : GameStateMachine<EngineConsole.States, EngineConsole.Instance, EngineConsole, object>.GameInstance(master)
        {
        }
        public class States : GameStateMachine<EngineConsole.States, EngineConsole.Instance, EngineConsole>
        {
            public GameStateMachine<EngineConsole.States, EngineConsole.Instance, EngineConsole, object>.State off;
            public GameStateMachine<EngineConsole.States, EngineConsole.Instance, EngineConsole, object>.State on;
            public override void InitializeStates(out StateMachine.BaseState default_state)
            {
                default_state = (StateMachine.BaseState)this.off;
                this.off
                    .PlayAnim("off", KAnim.PlayMode.Loop)
                    .EventTransition(GameHashes.OperationalChanged, this.on, smi => smi.GetComponent<Operational>().IsOperational);

                this.on
                    .PlayAnim("on", KAnim.PlayMode.Loop)
                    .EventTransition(GameHashes.OperationalChanged, this.off, smi => !smi.GetComponent<Operational>().IsOperational);
            }
        }
    }
    public class EngineConsoleSideScreen : SideScreenContent
    {
        private GameObject content;
        private EngineConsole targetComp;

        private bool isInitialized = false;
        string EngineStatus, Title;
        KButton button;
        GameObject slider;
        int OldSliderValue = 0;
        bool isSettingSliderFromTarget = false;
        PLabel sliderLable;
        private LocText sliderLabelText;

        private LocText EngineCounterCmp, EngineStatusTextCmp;
        private string EngineCounter, EngineOffline, EngineTrans, EngineNormal, EngineOffNoDest;
        public override int GetSideScreenSortOrder() => -100;
        public override string GetTitle()
        {
            return Title;
        }
        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();
            if (isInitialized == true)
            {
                return;
            }

            BuildUI();

            isInitialized = true;
        }
        public override bool IsValidForTarget(GameObject target)
        {
            return target != null && target.GetComponent<EngineConsole>() != null;
        }
        public override void SetTarget(GameObject target)
        {
            targetComp = target?.GetComponent<EngineConsole>();
            if(targetComp == null)
            {
                return;
            }
            if (isInitialized == false)
            {
                OnPrefabInit();
            }
            OldSliderValue = targetComp.sliderValue;
            SetSliderValueWithoutCallback(OldSliderValue);
            UpdateSliderLabel(OldSliderValue);
            EngineCounterCmp.SetText(EngineCounter + " : " + targetComp.Engines.Count.ToString());
            targetComp.SubscriptEngineCounterChanged(onEngineCounterChanged);
            targetComp.SubscriptEngineStatus(EngineStatusChangedCallback);
            EngineStatusChangedCallback(targetComp.GetCurrentEngineStatus());
        }
        private void onEngineCounterChanged(int m_EngineCounter)
        {
            EngineCounterCmp.SetText(EngineCounter + " : " + m_EngineCounter.ToString());
            EngineCounterCmp.SetAllDirty();
        }
        private void BuildUI()
        {
            EngineStatus = BUILDINGS.PREFABS.ENGINECONSOLESIDESCREEN.POWERSTATUS;
            Title = BUILDINGS.PREFABS.ENGINECONSOLESIDESCREEN.SIDESCREENTITLE;
            EngineOffline = BUILDINGS.PREFABS.ENGINECONSOLESIDESCREEN.ENGINESSTATUSOFF;
            EngineTrans = BUILDINGS.PREFABS.ENGINECONSOLESIDESCREEN.ENGINESSTATUSTRANS;
            EngineNormal = BUILDINGS.PREFABS.ENGINECONSOLESIDESCREEN.ENGINESSTATUSNORMAL;
            EngineCounter = BUILDINGS.PREFABS.ENGINECONSOLESIDESCREEN.ENGINESCOUNTER;
            EngineOffNoDest = BUILDINGS.PREFABS.ENGINECONSOLESIDESCREEN.ENGINESSTATUSOFFNODES;
            string str2 = BUILDINGS.PREFABS.ENGINECONSOLESIDESCREEN.DESTINATIONBUTTON, str3 = BUILDINGS.PREFABS.ENGINECONSOLESIDESCREEN.POWERLEVELTIP1,
                    str4 = BUILDINGS.PREFABS.ENGINECONSOLESIDESCREEN.POWERLEVELTIP2, str5 = BUILDINGS.PREFABS.ENGINECONSOLESIDESCREEN.FRAMELABLETITLE,
                    str6 = BUILDINGS.PREFABS.ENGINECONSOLESIDESCREEN.ENGINESSTATUSTITLE;
            if (Interstellar.dic != null)
            {
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.ENGINECONSOLESIDESCREEN.POWERSTATUS", out EngineStatus);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.ENGINECONSOLESIDESCREEN.SIDESCREENTITLE", out Title);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.ENGINECONSOLESIDESCREEN.ENGINESSTATUSOFF", out EngineOffline);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.ENGINECONSOLESIDESCREEN.ENGINESSTATUSTRANS", out EngineTrans);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.ENGINECONSOLESIDESCREEN.ENGINESSTATUSNORMAL", out EngineNormal);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.ENGINECONSOLESIDESCREEN.ENGINESCOUNTER", out EngineCounter);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.ENGINECONSOLESIDESCREEN.ENGINESSTATUSOFFNODES", out EngineOffNoDest);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.ENGINECONSOLESIDESCREEN.DESTINATIONBUTTON", out str2);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.ENGINECONSOLESIDESCREEN.POWERLEVELTIP1", out str3);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.ENGINECONSOLESIDESCREEN.POWERLEVELTIP2", out str4);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.ENGINECONSOLESIDESCREEN.FRAMELABLETITLE", out str5);
                Interstellar.dic.TryGetValue("Interstellar.BUILDINGS.PREFABS.ENGINECONSOLESIDESCREEN.ENGINESSTATUSTITLE", out str6);
            }

            sliderLable = new PLabel("SliderLabel")
            {
                DynamicSize = true,
                FlexSize = Vector2.zero,
                Text = EngineStatus + "0 Kw",
                TextStyle = PUITuning.Fonts.TextDarkStyle,
            }.AddOnRealize(obj => { sliderLabelText = obj.GetComponent<LocText>() ?? obj.GetComponentInChildren<LocText>(); sliderLabelText.fontSize = 20; });

            var buttonPanel = new PPanel("MyButtonPanel")
            {
                FlexSize = new Vector2(1, 1),
                Direction = PanelDirection.Horizontal,
                Alignment = TextAnchor.MiddleCenter,
                Spacing = 25,
                Margin = new RectOffset(10, 10, 0, 0)
            }
            .AddChild(new PLabel("ButtonIcon")
            {
                Sprite = Interstellar.EngineConsoleTargetIcon,
                FlexSize = Vector2.zero,
                TextStyle = PUITuning.Fonts.TextLightStyle
            }.AddOnRealize(obj => PUIUtils.SetUISize(obj, new Vector2(50, 50))))
            .AddChild(new PButton("DoActionButton")
            {
                TextStyle = PUITuning.Fonts.TextLightStyle,
                FlexSize = new Vector2(0.9f, 0.9f),
                Text = str2,
                OnClick = OnButtonClicked
            }.SetKleiBlueStyle().AddOnRealize(obj => { button = obj.GetComponent<KButton>(); (obj.GetComponent<LocText>() ?? obj.GetComponentInChildren<LocText>()).fontSize = 15; }));

            PPanel SideUIPanel = new PPanel("MySideUIPanel")
            {
                FlexSize = new Vector2(1, 1),
                Direction = PanelDirection.Horizontal,
                Alignment = TextAnchor.MiddleCenter,
                Spacing = 10,
                Margin = new RectOffset(10, 10, 0, 0)
            }
            .AddChild(new PLabel("SideLable1")
            {
                TextAlignment = TextAnchor.UpperLeft,
                FlexSize = new Vector2(1f, 0.2f),
                Text = str3,
                TextStyle = PUITuning.Fonts.TextDarkStyle,
            })
            .AddChild(new PLabel("SideLable2")
            {
                TextAlignment = TextAnchor.UpperRight,
                FlexSize = new Vector2(1f, 0.2f),
                Text = str4,
                TextStyle = PUITuning.Fonts.TextDarkStyle,
            });

            PPanel StatusUIPanel = new PPanel("EnginesStatusUIPanel")
            {
                FlexSize = new Vector2(1, 1),
                Direction = PanelDirection.Vertical,
                Alignment = TextAnchor.MiddleCenter,
                Spacing = 10,
                Margin = new RectOffset(10, 10, 0, 0)
            }
            .AddChild(new PLabel("CounterLable1")
            {
                TextAlignment = TextAnchor.MiddleCenter,
                FlexSize = new Vector2(1f, 0.2f),
                Text = EngineCounter + " : 0",
                TextStyle = PUITuning.Fonts.TextDarkStyle,
            }.AddOnRealize(obj => { EngineCounterCmp = obj.GetComponent<LocText>() ?? obj.GetComponentInChildren<LocText>(); EngineCounterCmp.fontSize = 20; }))
            .AddChild(new PLabel("StatusLable1")
            {
                TextAlignment = TextAnchor.MiddleCenter,
                FlexSize = new Vector2(1f, 0.2f),
                Text = EngineOffline,
                TextStyle = PUITuning.Fonts.TextDarkStyle,
            }.AddOnRealize(obj => { EngineStatusTextCmp = obj.GetComponent<LocText>() ?? obj.GetComponentInChildren<LocText>(); EngineStatusTextCmp.color = Color.red; EngineStatusTextCmp.fontSize = 20; }));


            PPanel rootPanel = new PPanel("MySidePanel")
            {
                FlexSize = new Vector2(1, 1),
                Direction = PanelDirection.Vertical,
                Alignment = TextAnchor.MiddleCenter,
                Spacing = 10,
                Margin = new RectOffset(0, 0, 12, 30)
            }
            .AddChild(buttonPanel)

            .AddChild(new PLabel("TitleLable")
            {
                TextAlignment = TextAnchor.MiddleLeft,
                FlexSize = new Vector2(1f, 0.2f),
                Text = "  " + str5,
                TextStyle = PUITuning.Fonts.TextLightStyle,
            }.SetKleiPinkColor().AddOnRealize(obj => { sliderLabelText = obj.GetComponent<LocText>() ?? obj.GetComponentInChildren<LocText>(); sliderLabelText.fontSize = 15; }))

            .AddChild(sliderLable)

            .AddChild(new PSliderSingle("MySlider")
            {
                PreferredLength = 200f,
                TrackSize = 20f,
                HandleSize = 30f,
                MinValue = 0f,
                MaxValue = 3f,
                InitialValue = 0f,
                IntegersOnly = true,
                OnValueChanged = OnSliderChanged
            }.AddOnRealize(obj => slider = obj))
            .AddChild(SideUIPanel)

            .AddChild(new PLabel("StatusTitleLable")
            {
                TextAlignment = TextAnchor.MiddleLeft,
                FlexSize = new Vector2(1f, 0.2f),
                Text = "  " + str6,
                TextStyle = PUITuning.Fonts.TextLightStyle,
            }.SetKleiPinkColor().AddOnRealize(obj => { (obj.GetComponent<LocText>() ?? obj.GetComponentInChildren<LocText>()).fontSize = 14; }))
            .AddChild(StatusUIPanel);

            content = rootPanel.AddTo(gameObject, 0);
            ContentContainer = content;
        }
        private void OnButtonClicked(GameObject _)
        {
            if (targetComp != null)
                targetComp.DoAction();
        }
        private void EngineStatusChangedCallback(AsteroidEngine.EngineStatus status)
        {
            if (EngineStatusTextCmp == null)
            {
                return;
            }
            if (status == AsteroidEngine.EngineStatus.off)
            {
                EngineStatusTextCmp.SetText(EngineOffline);
                EngineStatusTextCmp.color = Color.red;
                EngineStatusTextCmp.SetAllDirty();
            }
            if (status == AsteroidEngine.EngineStatus.decelerate || status == AsteroidEngine.EngineStatus.decelerate_off || status == AsteroidEngine.EngineStatus.accelerate)
            {
                EngineStatusTextCmp.SetText(EngineTrans);
                EngineStatusTextCmp.color = Color.yellow;
                EngineStatusTextCmp.SetAllDirty();
            }
            if (status == AsteroidEngine.EngineStatus.on)
            {
                EngineStatusTextCmp.SetText(EngineNormal);
                EngineStatusTextCmp.color = Color.green;
                EngineStatusTextCmp.SetAllDirty();
            }
            if (status == AsteroidEngine.EngineStatus.off_no_dest)
            {
                EngineStatusTextCmp.SetText(EngineOffNoDest);
                EngineStatusTextCmp.color = Color.red;
                EngineStatusTextCmp.SetAllDirty();
            }

        }
        private void OnSliderChanged(GameObject _, float value)
        {
            if (isSettingSliderFromTarget)
            {
                return;
            }
            if (targetComp != null)
            {
                int sliderValue = Mathf.RoundToInt(value);
                targetComp.SubscriptEngineStatus(EngineStatusChangedCallback);
                if (targetComp.Engines.Count <= 0 || targetComp.operational.IsOperational == false || Math.Abs(sliderValue - OldSliderValue) > 1)
                {
                    SetSliderValueWithoutCallback(OldSliderValue);
                    return;
                }
                if (targetComp.SetPowerLevel(sliderValue) == false)
                {
                    SetSliderValueWithoutCallback(OldSliderValue);
                }
                else
                {
                    UpdateSliderLabel(sliderValue);
                    OldSliderValue = sliderValue;
                }
            }
        }
        private void SetSliderValueWithoutCallback(int value)
        {
            if (slider == null)
            {
                return;
            }
            isSettingSliderFromTarget = true;
            try
            {
                PSliderSingle.SetCurrentValue(slider, value);
            }
            finally
            {
                isSettingSliderFromTarget = false;
            }
        }
        private void UpdateSliderLabel(int value)
        {
            if (sliderLabelText == null)
            {
                return;
            }
            if (value == 0)
            {
                sliderLabelText.SetText(EngineStatus + Mathf.RoundToInt(BUILDINGS.PREFABS.ENGINECONSOLE.POWER_CONSUMPTION_0 / 1000f).ToString() + " Kw");
            }
            if (value == 1)
            {
                sliderLabelText.SetText(EngineStatus + Mathf.RoundToInt(BUILDINGS.PREFABS.ENGINECONSOLE.POWER_CONSUMPTION_1 / 1000f).ToString() + " Kw");
            }
            if (value == 2)
            {
                sliderLabelText.SetText(EngineStatus + Mathf.RoundToInt(BUILDINGS.PREFABS.ENGINECONSOLE.POWER_CONSUMPTION_2 / 1000f).ToString() + " Kw");
            }
            if (value == 3)
            {
                sliderLabelText.SetText(EngineStatus + Mathf.RoundToInt(BUILDINGS.PREFABS.ENGINECONSOLE.POWER_CONSUMPTION_3 / 1000f).ToString() + " Kw");
            }
        }
    }
    public class AsteroidEngineConfig : IBuildingConfig
    {
        public override string[] GetRequiredDlcIds() => DlcManager.EXPANSION1;
        public override BuildingDef CreateBuildingDef()
        {
            int width = 18;
            int height = 12;
            string anim = "AsteroidEngine_kanim";
            string[] MaterialCategory = new string[1] { SimHashes.Steel.ToString() };

            int hitpoints = 30;
            float construction_time = 600f;
            float[] construction_mass = new float[1] { 200000f };
            float melting_point = 3000f;
            BuildLocationRule build_location_rule = BuildLocationRule.Anywhere;
            EffectorValues none = NOISE_POLLUTION.NONE;
            BuildingDef buildingDef = BuildingTemplates.CreateBuildingDef(BUILDINGS.PREFABS.ASTEROIDENGINE.ID, width, height, anim, hitpoints, construction_time, construction_mass, MaterialCategory, melting_point, build_location_rule, TUNING.BUILDINGS.DECOR.PENALTY.TIER1, none);
            buildingDef.Floodable = true;
            buildingDef.AudioCategory = "Metal";
            buildingDef.Overheatable = true;
            buildingDef.Repairable = true;
            buildingDef.Disinfectable = false;
            buildingDef.Invincible = false;
            buildingDef.SceneLayer = Grid.SceneLayer.SceneMAX;
            buildingDef.ForegroundLayer = Grid.SceneLayer.BuildingFront;
            buildingDef.SelfHeatKilowattsWhenActive = 1000;
            buildingDef.EnergyConsumptionWhenActive = BUILDINGS.PREFABS.ENGINECONSOLE.POWER_CONSUMPTION_3;
            buildingDef.OverheatTemperature = 2373.15f; //K
            buildingDef.DragBuild = false;
            buildingDef.PermittedRotations = PermittedRotations.Unrotatable;
            buildingDef.RequiresPowerInput = true;
            buildingDef.PowerInputOffset = new CellOffset(-7, 5);

            return buildingDef;
        }
        public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
        {
        }
        public override void DoPostConfigureComplete(GameObject go)
        {
            go.AddOrGet<AsteroidEngine>();
            go.AddOrGet<Operational>();
        }
    }
    public class AsteroidEngine : StateMachineComponent<AsteroidEngine.Instance>
    {
        [Serialize]
        private int PowerLevel = 0;

        private Action<object> m_onOperationalChangedDelegate;
        [MyCmpGet] public Operational operational;
        public Orientation BuildingOrientation = Orientation.Neutral;
        public int originCell;
        public KBatchedAnimController anim_controller;
        [MyCmpGet]
        public EnergyConsumer powerConsumer;
        public enum EngineStatus { on, off, accelerate, decelerate, decelerate_off, off_no_dest };
        [Serialize]
        private bool isTransitioning;
        public bool IsTransitioning
        {
            get { return isTransitioning; }
            private set { isTransitioning = value; }
        }
        [Serialize]
        private EngineStatus savedEngineStatus;
        public EngineStatus engine_status
        {
            get { return savedEngineStatus; }
            private set { savedEngineStatus = value; }
        }
        private void SetEngineStatus(EngineStatus value)
        {
            engine_status = value;
            if (EngineStatusCallBack != null)
            {
                EngineStatusCallBack.Invoke(this, engine_status);
            }
        }

        private Action<AsteroidEngine, EngineStatus> EngineStatusCallBack = null;
        public void SubscriptEngineStatus(Action<AsteroidEngine, EngineStatus> callback)
        {
            EngineStatusCallBack = callback;
        }
        public void UnSubscriptEngineStatus(Action<AsteroidEngine, EngineStatus> callback)
        {
            if (EngineStatusCallBack == callback)
            {
                EngineStatusCallBack = null;
            }
        }
        protected override void OnSpawn()
        {
            base.OnSpawn();
            originCell = Grid.PosToCell(this);
            anim_controller = GetComponentInParent<KBatchedAnimController>();
            int restoredPowerLevel = PowerLevel;
            IsTransitioning = false;
            this.smi.StartSM();
            m_onOperationalChangedDelegate = new Action<object>(this.OnOperationalChanged);
            Subscribe((int)GameHashes.OperationalChanged, m_onOperationalChangedDelegate);
            if (EngineConsole.StaticInstance != null)
            {
                if (operational.IsOperational == true)
                {
                    EngineConsole.StaticInstance.RegisterEngine(this);
                }
            }
            else
            {
                RestoreStablePowerLevel(0);
            }
        }
        private void OnOperationalChanged(object data)
        {
            if (operational.IsOperational == false)
            {
                if (EngineConsole.StaticInstance != null)
                {
                    EngineConsole.StaticInstance.UnregisterEngine(this);
                }
            }
            else
            {
                if (EngineConsole.StaticInstance != null)
                {
                    EngineConsole.StaticInstance.RegisterEngine(this);
                }

            }
        }
        protected override void OnCleanUp()
        {
            if (EngineConsole.StaticInstance != null)
            {
                EngineConsole.StaticInstance.UnregisterEngine(this);
            }
            base.OnCleanUp();
        }
        protected override void OnPrefabInit()
        {

        }
        public void SetPowerLevel(int level)
        {
            level = Mathf.Clamp(level, 0, 3);
            if (PowerLevel < level)
            {
                if (level == 1 && PowerLevel == 0)
                {
                    SetAnimByPowerLevel(level);
                    this.smi.GoTo(this.smi.sm.accelerate01);
                }
                if (level == 2 && PowerLevel == 1)
                {
                    SetAnimByPowerLevel(level);
                    this.smi.GoTo(this.smi.sm.accelerate12);
                }
                if (level == 3 && PowerLevel == 2)
                {
                    SetAnimByPowerLevel(level);
                    this.smi.GoTo(this.smi.sm.accelerate23);
                }
                if (level == 2 && PowerLevel == 0)
                {
                    SetAnimByPowerLevel(level);
                    this.smi.GoTo(this.smi.sm.accelerate02);
                }
                if (level == 3 && PowerLevel == 0)
                {
                    SetAnimByPowerLevel(level);
                    this.smi.GoTo(this.smi.sm.accelerate03);
                }
            }
            if (PowerLevel > level)
            {
                if (level == 2 && PowerLevel == 3)
                {
                    this.smi.GoTo(this.smi.sm.decelerate32);
                }
                if (level == 1 && PowerLevel == 2)
                {
                    this.smi.GoTo(this.smi.sm.decelerate21);
                }
                if (level == 0 && PowerLevel == 1)
                {
                    this.smi.GoTo(this.smi.sm.decelerate10);
                }
                if (level == 0 && PowerLevel == 2)
                {
                    this.smi.GoTo(this.smi.sm.decelerate20);
                }
                if (level == 0 && PowerLevel == 3)
                {
                    this.smi.GoTo(this.smi.sm.decelerate30);
                }
            }
            SetPowerConsumptionByPowerLevel(level);
            PowerLevel = level;
        }
        private void RestoreStablePowerLevel(int level)
        {
            level = Mathf.Clamp(level, 0, 3);
            SetAnimByPowerLevel(level);
            SetPowerConsumptionByPowerLevel(level);
            PowerLevel = level;
            IsTransitioning = false;
            if (level > 0)
            {
                this.smi.GoTo(this.smi.sm.on);
            }
            else
            {
                this.smi.GoTo(this.smi.sm.off);
            }
        }
        private void SetPowerConsumptionByPowerLevel(int level)
        {
            if (powerConsumer == null)
            {
                return;
            }
            if (level == 0)
            {
                powerConsumer.BaseWattageRating = BUILDINGS.PREFABS.ENGINECONSOLE.POWER_CONSUMPTION_0;
            }
            if (level == 1)
            {
                powerConsumer.BaseWattageRating = BUILDINGS.PREFABS.ENGINECONSOLE.POWER_CONSUMPTION_1;
            }
            if (level == 2)
            {
                powerConsumer.BaseWattageRating = BUILDINGS.PREFABS.ENGINECONSOLE.POWER_CONSUMPTION_2;
            }
            if (level == 3)
            {
                powerConsumer.BaseWattageRating = BUILDINGS.PREFABS.ENGINECONSOLE.POWER_CONSUMPTION_3;
            }
        }
        public void SetAnimByPowerLevel(int level)
        {
            if (anim_controller == null)
            {
                return;
            }
            if (level == 0)
            {
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"flame", false);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"flame_effect", false);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"level_enable", false);

                anim_controller.SetSymbolVisiblity((KAnimHashedString)"flame1", false);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"flame_effect1", false);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"level_enable1", false);

                anim_controller.SetSymbolVisiblity((KAnimHashedString)"flame2", false);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"flame_effect2", false);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"level_enable2", false);

                anim_controller.SetSymbolVisiblity((KAnimHashedString)"flash", false);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"eletrc", false);
            }
            else
            {
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"flash", true);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"eletrc", true);
            }
            if (level == 1)
            {
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"flame", true);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"flame_effect", true);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"level_enable", true);

                anim_controller.SetSymbolVisiblity((KAnimHashedString)"flame1", false);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"flame_effect1", false);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"level_enable1", false);

                anim_controller.SetSymbolVisiblity((KAnimHashedString)"flame2", false);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"flame_effect2", false);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"level_enable2", false);
            }
            if (level == 2)
            {
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"flame", true);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"flame_effect", true);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"level_enable", true);

                anim_controller.SetSymbolVisiblity((KAnimHashedString)"flame1", true);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"flame_effect1", true);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"level_enable1", true);

                anim_controller.SetSymbolVisiblity((KAnimHashedString)"flame2", false);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"flame_effect2", false);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"level_enable2", false);
            }
            if (level == 3)
            {
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"flame", true);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"flame_effect", true);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"level_enable", true);

                anim_controller.SetSymbolVisiblity((KAnimHashedString)"flame1", true);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"flame_effect1", true);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"level_enable1", true);

                anim_controller.SetSymbolVisiblity((KAnimHashedString)"flame2", true);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"flame_effect2", true);
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"level_enable2", true);
            }
        }
        public class Instance(AsteroidEngine master) : GameStateMachine<AsteroidEngine.States, AsteroidEngine.Instance, AsteroidEngine, object>.GameInstance(master)
        {
            public bool ShouldRun() => master.operational.IsOperational && master.PowerLevel > 0;
        }
        public class States : GameStateMachine<AsteroidEngine.States, AsteroidEngine.Instance, AsteroidEngine>
        {
            public GameStateMachine<AsteroidEngine.States, AsteroidEngine.Instance, AsteroidEngine, object>.State off;
            public GameStateMachine<AsteroidEngine.States, AsteroidEngine.Instance, AsteroidEngine, object>.State on;
            public GameStateMachine<AsteroidEngine.States, AsteroidEngine.Instance, AsteroidEngine, object>.State accelerate01;
            public GameStateMachine<AsteroidEngine.States, AsteroidEngine.Instance, AsteroidEngine, object>.State accelerate12;
            public GameStateMachine<AsteroidEngine.States, AsteroidEngine.Instance, AsteroidEngine, object>.State accelerate23;
            public GameStateMachine<AsteroidEngine.States, AsteroidEngine.Instance, AsteroidEngine, object>.State accelerate02;
            public GameStateMachine<AsteroidEngine.States, AsteroidEngine.Instance, AsteroidEngine, object>.State accelerate03;

            public GameStateMachine<AsteroidEngine.States, AsteroidEngine.Instance, AsteroidEngine, object>.State decelerate32;
            public GameStateMachine<AsteroidEngine.States, AsteroidEngine.Instance, AsteroidEngine, object>.State decelerate21;
            public GameStateMachine<AsteroidEngine.States, AsteroidEngine.Instance, AsteroidEngine, object>.State decelerate10;
            public GameStateMachine<AsteroidEngine.States, AsteroidEngine.Instance, AsteroidEngine, object>.State decelerate30;
            public GameStateMachine<AsteroidEngine.States, AsteroidEngine.Instance, AsteroidEngine, object>.State decelerate20;

            public override void InitializeStates(out StateMachine.BaseState default_state)
            {
                default_state = (StateMachine.BaseState)this.off;
                this.off
                    .Enter(smi => { smi.master.IsTransitioning = false; smi.master.SetEngineStatus(EngineStatus.off); smi.master.operational.SetActive(false); })
                    .PlayAnim("off", KAnim.PlayMode.Loop)
                    .EventTransition(GameHashes.OperationalChanged, this.on, smi => smi.ShouldRun());

                this.on
                    .Enter(smi => { if (smi.master.engine_status == EngineStatus.decelerate) smi.master.SetAnimByPowerLevel(smi.master.PowerLevel); smi.master.IsTransitioning = false; smi.master.SetEngineStatus(EngineStatus.on); smi.master.operational.SetActive(true); })
                    .PlayAnim("on", KAnim.PlayMode.Loop)
                    .EventTransition(GameHashes.OperationalChanged, this.off, smi => !smi.ShouldRun());

                this.accelerate01
                    .Enter(smi => { smi.master.IsTransitioning = true; smi.master.SetEngineStatus(EngineStatus.accelerate); })
                    .PlayAnim("accelerate01", KAnim.PlayMode.Once)
                    .OnAnimQueueComplete(this.on);
                this.accelerate12
                    .Enter(smi => { smi.master.IsTransitioning = true; smi.master.SetEngineStatus(EngineStatus.accelerate); })
                    .PlayAnim("accelerate12", KAnim.PlayMode.Once)
                    .OnAnimQueueComplete(this.on);
                this.accelerate23
                    .Enter(smi => { smi.master.IsTransitioning = true; smi.master.SetEngineStatus(EngineStatus.accelerate); })
                    .PlayAnim("accelerate23", KAnim.PlayMode.Once)
                    .OnAnimQueueComplete(this.on);
                this.accelerate02
                    .Enter(smi => { smi.master.IsTransitioning = true; smi.master.SetEngineStatus(EngineStatus.accelerate); })
                    .PlayAnim("accelerate02", KAnim.PlayMode.Once)
                    .OnAnimQueueComplete(this.on);
                this.accelerate03
                    .Enter(smi => { smi.master.IsTransitioning = true; smi.master.SetEngineStatus(EngineStatus.accelerate); })
                    .PlayAnim("accelerate03", KAnim.PlayMode.Once)
                    .OnAnimQueueComplete(this.on);

                this.decelerate32
                    .Enter(smi => { smi.master.IsTransitioning = true; smi.master.SetEngineStatus(EngineStatus.decelerate); })
                    .PlayAnim("decelerate32", KAnim.PlayMode.Once)
                    .OnAnimQueueComplete(this.on);
                this.decelerate21
                    .Enter(smi => { smi.master.IsTransitioning = true; smi.master.SetEngineStatus(EngineStatus.decelerate); })
                    .PlayAnim("decelerate21", KAnim.PlayMode.Once)
                    .OnAnimQueueComplete(this.on);
                this.decelerate10
                    .Enter(smi => { smi.master.IsTransitioning = true; smi.master.SetEngineStatus(EngineStatus.decelerate); })
                    .PlayAnim("decelerate10", KAnim.PlayMode.Once)
                    .OnAnimQueueComplete(this.off);
                this.decelerate20
                    .Enter(smi => { smi.master.IsTransitioning = true; smi.master.SetEngineStatus(EngineStatus.decelerate); })
                    .PlayAnim("decelerate20", KAnim.PlayMode.Once)
                    .OnAnimQueueComplete(this.off);
                this.decelerate30
                    .Enter(smi => { smi.master.IsTransitioning = true; smi.master.SetEngineStatus(EngineStatus.decelerate); })
                    .PlayAnim("decelerate30", KAnim.PlayMode.Once)
                    .OnAnimQueueComplete(this.off);

            }
        }
    }
    public class AsteroidEngineBuildingRule_Patch
    {
        [HarmonyPatch(typeof(BuildingDef), nameof(BuildingDef.IsValidPlaceLocation), new System.Type[] { typeof(GameObject), typeof(int), typeof(Orientation), typeof(bool), typeof(string), typeof(bool) },
                      new ArgumentType[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal })
        ]
        public static class BuildingDef_IsValidPlaceLocation
        {
            public static void Postfix(BuildingDef __instance, GameObject source_go, int cell, Orientation orientation, bool replace_tile, ref string fail_reason, bool restrictToActiveWorld, ref bool __result)
            {
                if (__instance.PrefabID == BUILDINGS.PREFABS.ASTEROIDENGINE.ID)
                {
                    if (Grid.IsValidCell(cell))
                    {
                        if (Grid.WorldIdx[cell] != 0 || Grid.CellRow(cell) > 30)
                        {
                            fail_reason = Interstellar.Fail_AsteroidEngineBuildRule;
                            __result = false;
                        }
                    }
                }
                if (__instance.PrefabID == BUILDINGS.PREFABS.ENGINECONSOLE.ID)
                {
                    if (EngineConsole.StaticInstance != null)
                    {
                        if (Grid.IsValidCell(cell))
                        {
                            if (Grid.WorldIdx[cell] != 0 || Grid.CellRow(cell) > 30)
                            {

                                fail_reason = Interstellar.Fail_AsteroidEngineConsoleBuildRule;
                                __result = false;
                            }
                        }
                    }
                }
            }
        }

        [HarmonyPatch(typeof(BuildingDef), nameof(BuildingDef.IsValidBuildLocation), new System.Type[] { typeof(GameObject), typeof(int), typeof(Orientation), typeof(bool), typeof(string) },
              new ArgumentType[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out })
        ]
        static class BuildingDef_IsValidBuildLocation
        {
            public static void Postfix(BuildingDef __instance, GameObject source_go, int cell, Orientation orientation, bool replace_tile, ref string fail_reason, ref bool __result)
            {
                if (__instance.PrefabID == BUILDINGS.PREFABS.ASTEROIDENGINE.ID)
                {
                    if (Grid.IsValidCell(cell))
                    {
                        if (Grid.WorldIdx[cell] != 0 || Grid.CellRow(cell) > 30)
                        {
                            fail_reason = Interstellar.Fail_AsteroidEngineBuildRule;
                            __result = false;
                        }
                    }
                }
                if (__instance.PrefabID == BUILDINGS.PREFABS.ENGINECONSOLE.ID)
                {
                    if (EngineConsole.StaticInstance != null)
                    {
                        if (Grid.IsValidCell(cell))
                        {
                            if (Grid.WorldIdx[cell] != 0 || Grid.CellRow(cell) > 30)
                            {

                                fail_reason = Interstellar.Fail_AsteroidEngineConsoleBuildRule;
                                __result = false;
                            }
                        }
                    }
                }
            }
        }
    }
    public class Asteroidcraft : KMonoBehaviour, IClusterRange
    {
        protected override void OnSpawn()
        {
            base.OnSpawn();
        }
        public float GetRange()
        {
            return 100f;
        }
        public int GetRangeInTiles()
        {
            return 100;
        }
        public int GetMaxRangeInTiles()
        {
            return 100;
        }
    }

    [HarmonyPatch(typeof(ClusterTraveler), nameof(ClusterTraveler.Sim200ms))]
    public static class Patch_ClusterTraveler_Sim200ms
    {
        public static bool IgnoreAsteroidPathAssert = false;
        public static void Prefix(ClusterTraveler __instance)
        {
            if (__instance != null && __instance.GetComponentInChildren<Asteroidcraft>() != null)
                IgnoreAsteroidPathAssert = true;
        }
        static void Postfix()
        {
            IgnoreAsteroidPathAssert = false;
        }
    }

    [HarmonyPatch(typeof(Debug), nameof(Debug.Assert), new Type[] { typeof(bool), typeof(string) })]
    public static class Patch_Debug_Assert_Filter_String
    {
        public static bool Prefix(bool condition, string message)
        {
            if (Patch_ClusterTraveler_Sim200ms.IgnoreAsteroidPathAssert == true)
            {
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(RocketClusterDestinationSelector), "OnClusterLocationChanged")]
    public static class Patch_RocketClusterDestinationSelector_OnClusterLocationChanged
    {
        public static bool Prefix(RocketClusterDestinationSelector __instance, object data)
        {
            if (__instance.GetComponent<Asteroidcraft>() != null)
            {
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(ClusterGridEntity), nameof(ClusterGridEntity.SendClusterLocationChangedEvent))]
    public static class Patch_ClusterGridEntity_SendClusterLocationChangedEvent_AsteroidcraftVisual
    {
        public static void Prefix(ClusterGridEntity __instance)
        {
            if (__instance != null && __instance.GetComponentInParent<Asteroidcraft>() != null)
            {
                __instance.positionDirty = false;
            }
        }
    }

    [HarmonyPatch(typeof(ClusterGridEntity), nameof(ClusterGridEntity.SpaceOutInSameHex))]
    public static class Patch_ClusterGridEntity_SpaceOutInSameHex_Asteroidcraft
    {
        public static void Postfix(ClusterGridEntity __instance, ref bool __result)
        {
            if (__instance != null && __instance.GetComponentInParent<Asteroidcraft>() != null)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(ClusterMapVisualizer), "Init")]
    public static class Patch_ClusterMapVisualizer_Init_Smooth3
    {
        private const float AsteroidcraftVisualizerTransitionSpeed = 1f;

        public static void Postfix(ClusterMapVisualizer __instance, ClusterGridEntity entity, ClusterMapPathDrawer pathDrawer)
        {
            if (__instance == null || entity == null || entity.GetComponentInParent<Asteroidcraft>() == null)
            {
                return;
            }

            __instance.doesTransitionAnimation = false;
            __instance.SetAnimRotation(0f);
            MoveAsteroidcraftVisualizerToMobileLayer(__instance, entity);
        }

        public static void UpdateAsteroidcraftVisualizer(ClusterMapVisualizer visualizer, ClusterGridEntity entity)
        {
            if (visualizer == null || entity == null || ClusterGrid.Instance == null)
            {
                return;
            }

            MoveAsteroidcraftVisualizerToMobileLayer(visualizer, entity);
            visualizer.SetAnimRotation(0f);

            RectTransform rectTransform = visualizer.rectTransform();
            Vector3 currentPosition = rectTransform.GetLocalPosition();
            Vector3 targetPosition = ClusterGrid.Instance.GetPosition(entity);
            Vector3 delta = targetPosition - currentPosition;
            float distance = delta.magnitude;

            if (distance > 0.001f)
            {
                float step = AsteroidcraftVisualizerTransitionSpeed * Time.unscaledDeltaTime;
                rectTransform.SetLocalPosition(step < distance ? currentPosition + delta.normalized * step : targetPosition);
                visualizer.RefreshPathDrawing();
            }
        }

        private static void MoveAsteroidcraftVisualizerToMobileLayer(ClusterMapVisualizer visualizer, ClusterGridEntity entity)
        {
            if (ClusterMapScreen.Instance == null || ClusterMapScreen.Instance.mobileVisContainer == null)
            {
                return;
            }

            Transform mobileLayer = ClusterMapScreen.Instance.mobileVisContainer.transform;
            if (visualizer.transform.parent == mobileLayer)
            {
                return;
            }

            // Asteroidcraft stays an asteroid in the data model, but should render like a moving craft on the starmap.
            RectTransform rectTransform = visualizer.rectTransform();
            Vector3 localPosition = rectTransform.GetLocalPosition();
            visualizer.transform.SetParent(mobileLayer, false);
            rectTransform.SetLocalPosition(localPosition);
            rectTransform.SetAsLastSibling();
        }
    }

    [HarmonyPatch(typeof(ClusterMapScreen), nameof(ClusterMapScreen.ScreenUpdate))]
    public static class Patch_ClusterMapScreen_ScreenUpdate_Asteroidcraft
    {
        public static void Postfix(ClusterMapScreen __instance)
        {
            if (__instance == null || ClusterGrid.Instance == null)
            {
                return;
            }

            foreach (List<ClusterGridEntity> cellContents in ClusterGrid.Instance.cellContents.Values)
            {
                foreach (ClusterGridEntity entity in cellContents)
                {
                    if (entity == null || entity.GetComponentInParent<Asteroidcraft>() == null)
                    {
                        continue;
                    }

                    ClusterMapVisualizer visualizer = __instance.GetEntityVisAnim(entity);
                    if (visualizer == null || !visualizer.gameObject.activeInHierarchy)
                    {
                        continue;
                    }

                    Patch_ClusterMapVisualizer_Init_Smooth3.UpdateAsteroidcraftVisualizer(visualizer, entity);
                }
            }
        }
    }

    [HarmonyPatch(typeof(TemporalTearSideScreen), nameof(TemporalTearSideScreen.IsValidForTarget))]
    public static class Patch_TemporalTearSideScreen_IsValidForTarget
    {
        public static void Postfix(GameObject target, ref bool __result)
        {
            if (__result || target == null)
            {
                return;
            }

            TemporalTear targetTear = target.GetComponent<TemporalTear>();
            if (targetTear == null)
            {
                return;
            }

            __result = targetTear.IsOpen() &&
                       TemporalTearAsteroidcraftUtils.HasAsteroidcraftAtLocation(targetTear.Location);
        }
    }

    [HarmonyPatch(typeof(TemporalTearSideScreen), nameof(TemporalTearSideScreen.SetTarget))]
    public static class Patch_TemporalTearSideScreen_SetTarget
    {
        public static bool Prefix(TemporalTearSideScreen __instance, GameObject target)
        {
            if (target == null)
            {
                return true;
            }

            TemporalTear targetTear = target.GetComponent<TemporalTear>();
            if (targetTear == null)
            {
                return true;
            }

            if (!targetTear.IsOpen() || !TemporalTearAsteroidcraftUtils.HasAsteroidcraftAtLocation(targetTear.Location))
            {
                return false;
            }

            HierarchyReferences references = __instance.GetComponent<HierarchyReferences>();
            LocText label = references.GetReference<LocText>("label");
            KButton button = references.GetReference<KButton>("button");
            label.SetText((string)STRINGS.UI.UISIDESCREENS.TEMPORALTEARSIDESCREEN.BUTTON_OPEN);
            button.ClearOnClick();
            button.isInteractable = true;
            button.onClick += () =>
            {
                ShowConfirmDialog(targetTear);
            };
            return false;
        }

        private static void ShowConfirmDialog(TemporalTear targetTear)
        {
            GameObject parent = GameScreenManager.Instance.ssOverlayCanvas.gameObject;
            ConfirmDialogScreen dialog = GameScreenManager.Instance
                .StartScreen(ScreenPrefabs.Instance.ConfirmDialogScreen.gameObject, parent)
                .GetComponent<ConfirmDialogScreen>();

            dialog.PopupConfirmDialog(
                Interstellar.TemporalTearTip2,
                () =>
                {
                    if (targetTear == null || !targetTear.IsOpen() || !TemporalTearAsteroidcraftUtils.HasAsteroidcraftAtLocation(targetTear.Location))
                    {
                        return;
                    }

                    Mod.Switch = true;
                    Mod.CreateNewWorlds();
                },
                () => { },
                title_text: Interstellar.TemporalTearTip1,
                confirm_text: Interstellar.TemporalTearTip3,
                cancel_text: Interstellar.TemporalTearTip4);
        }
    }

    public static class TemporalTearAsteroidcraftUtils
    {
        public static bool HasAsteroidcraftAtLocation(AxialI location)
        {
            if (ClusterGrid.Instance == null || !ClusterGrid.Instance.IsValidCell(location))
            {
                return false;
            }

            foreach (ClusterGridEntity entity in ClusterGrid.Instance.GetVisibleEntitiesAtCell(location))
            {
                if (entity != null && entity.GetComponent<Asteroidcraft>() != null)
                {
                    return true;
                }
            }
            return false;
        }
    }

    public static class ClusterTravelTimeUtils
    {
        public static float GetCyclesToNextCell(ClusterTraveler traveler)
        {
            if (traveler == null || traveler.getSpeedCB == null || traveler.CurrentPath == null || traveler.CurrentPath.Count <= 0)
            {
                return 0f;
            }
            float speed = traveler.getSpeedCB();
            if (speed <= 0f)
            {
                return 0f;
            }
            float remainingCellDistance = Mathf.Max(0f, 1f - traveler.GetMoveProgress()) * 600f;
            return remainingCellDistance / speed / 600f;
        }
    }
    [HarmonyPatch(typeof(ClusterMapSelectToolHoverTextCard), "UpdateHoverElements")]
    public static class ClusterMapHoverPatch
    {
        private static readonly FieldInfo fi_iconWarning = AccessTools.Field(typeof(ClusterMapSelectToolHoverTextCard), "m_iconWarning");

        public static bool Prefix(
          List<KSelectable> hoverObjects,
          ClusterMapSelectToolHoverTextCard __instance)
        {
            if (__instance == null || hoverObjects == null || hoverObjects.Count == 0)
                return true;
            Sprite iconWarning = fi_iconWarning?.GetValue(__instance) as Sprite;
            if (iconWarning == null)
            {
                __instance.ConfigureHoverScreen();
                iconWarning = fi_iconWarning?.GetValue(__instance) as Sprite;
            }

            HoverTextDrawer hoverTextDrawer = HoverTextScreen.Instance.BeginDrawing();
            foreach (KSelectable hoverObject in hoverObjects)
            {
                if (hoverObject == null)
                {
                    continue;
                }

                hoverTextDrawer.BeginShadowBar(ClusterMapSelectTool.Instance.GetSelected() == hoverObject);
                string unitFormattedName = GameUtil.GetUnitFormattedName(hoverObject.gameObject, true);
                hoverTextDrawer.DrawText(unitFormattedName, __instance.Styles_Title.Standard);

                DrawStatusItems(hoverTextDrawer, hoverObject, __instance, iconWarning, true);
                DrawStatusItems(hoverTextDrawer, hoverObject, __instance, iconWarning, false);
                DrawAsteroidTravelInfo(hoverTextDrawer, hoverObject, __instance);

                hoverTextDrawer.EndShadowBar();
            }
            hoverTextDrawer.EndDrawing();
            return false;
        }

        private static void DrawStatusItems(HoverTextDrawer hoverTextDrawer, KSelectable hoverObject, ClusterMapSelectToolHoverTextCard hoverCard, Sprite iconWarning, bool mainStatusItems)
        {
            foreach (StatusItemGroup.Entry entry in hoverObject.GetStatusItemGroup())
            {
                bool isMainStatusItem = entry.category != null && entry.category.Id == "Main";
                if (isMainStatusItem != mainStatusItems)
                {
                    continue;
                }

                bool isWarning = IsStatusItemWarning(entry);
                TextStyleSetting style = isWarning ? hoverCard.Styles_Warning.Standard : hoverCard.Styles_BodyText.Standard;
                Sprite icon = entry.item.sprite != null ? entry.item.sprite.sprite : iconWarning;
                Color color = isWarning ? hoverCard.Styles_Warning.Standard.textColor : hoverCard.Styles_BodyText.Standard.textColor;
                hoverTextDrawer.NewLine();
                if (icon != null)
                {
                    hoverTextDrawer.DrawIcon(icon, color);
                }
                hoverTextDrawer.DrawText(entry.GetName(), style);
            }
        }

        private static bool IsStatusItemWarning(StatusItemGroup.Entry entry)
        {
            return entry.item.notificationType == NotificationType.Bad ||
                   entry.item.notificationType == NotificationType.BadMinor ||
                   entry.item.notificationType == NotificationType.DuplicantThreatening;
        }

        private static void DrawAsteroidTravelInfo(HoverTextDrawer hoverTextDrawer, KSelectable hoverObject, ClusterMapSelectToolHoverTextCard hoverCard)
        {
            Asteroidcraft component = hoverObject.gameObject.GetComponent<Asteroidcraft>();
            if (component == null)
            {
                return;
            }

            ClusterTraveler traveler = component.GetComponentInParent<ClusterTraveler>();
            if (traveler == null || !traveler.IsTraveling())
            {
                return;
            }

            hoverTextDrawer.NewLine();
            if (traveler.getSpeedCB != null && traveler.getSpeedCB() > 0)
            {
                hoverTextDrawer.DrawText(Interstellar.ClusterScreenTip1 + (traveler.TravelETA() / 600f).ToString("F2") + Interstellar.ClusterScreenTip2, hoverCard.Styles_BodyText.Standard);
                hoverTextDrawer.NewLine();
                hoverTextDrawer.DrawText(Interstellar.ClusterScreenTip5 + ClusterTravelTimeUtils.GetCyclesToNextCell(traveler).ToString("F3") + Interstellar.ClusterScreenTip2, hoverCard.Styles_BodyText.Standard);
            }
            else
            {
                hoverTextDrawer.DrawText(Interstellar.ClusterScreenTip3, hoverCard.Styles_BodyText.Standard);
            }
        }
    }

    [HarmonyPatch(typeof(ClusterGrid), nameof(ClusterGrid.GetPath), new Type[] { typeof(AxialI), typeof(AxialI), typeof(ClusterDestinationSelector), typeof(string), typeof(bool) },
        new ArgumentType[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal, }
    )]
    public static class Patch_ClusterGrid_GetPath_Replace
    {
        private static readonly AxialI[] OFFSETS_R1 = BuildRadius1Offsets();
        private static AxialI[] BuildRadius1Offsets()
        {
            var dirs = AxialI.DIRECTIONS; // 6 directions
            var list = new List<AxialI>(6);

            // dist=1
            for (int i = 0; i < 6; i++)
                list.Add(dirs[i]);

            return list.ToArray();
        }
        public static bool Prefix(ClusterGrid __instance, AxialI start, AxialI end, ClusterDestinationSelector destination_selector, ref string fail_reason, bool dodgeHiddenAsteroids, ref List<AxialI> __result)
        {
            if (destination_selector.GetComponentInParent<Asteroidcraft>() == null)
                return true;

            if (PassClearanceRadius1(end) == false || PassRevealClearanceRadius1(end) == false)
            {
                fail_reason = Interstellar.ClusterScreenTip4;
                __result = null;
                return false;
            }

            fail_reason = null;

            if (!destination_selector.canNavigateFogOfWar && !__instance.IsCellVisible(end))
            {
                fail_reason = (string)UI.CLUSTERMAP.TOOLTIP_INVALID_DESTINATION_FOG_OF_WAR;
                __result = null;
                return false;
            }

            var asteroidAtEnd = __instance.GetVisibleEntityOfLayerAtCell(end, EntityLayer.Asteroid);
            if ((UnityEngine.Object)asteroidAtEnd != null && destination_selector.requireLaunchPadOnAsteroidDestination)
            {
                bool hasPad = false;
                foreach (KMonoBehaviour launchPad in Components.LaunchPads)
                {
                    if (launchPad.GetMyWorldLocation() == asteroidAtEnd.Location)
                    {
                        hasPad = true;
                        break;
                    }
                }
                if (!hasPad)
                {
                    fail_reason = (string)UI.CLUSTERMAP.TOOLTIP_INVALID_DESTINATION_NO_LAUNCH_PAD;
                    __result = null;
                    return false;
                }
            }

            if ((UnityEngine.Object)asteroidAtEnd == null && destination_selector.requireAsteroidDestination)
            {
                fail_reason = (string)UI.CLUSTERMAP.TOOLTIP_INVALID_DESTINATION_REQUIRE_ASTEROID;
                __result = null;
                return false;
            }

            if (destination_selector.requiredEntityLayer != EntityLayer.None &&
                (UnityEngine.Object)__instance.GetVisibleEntityOfLayerAtCell(end, destination_selector.requiredEntityLayer) == null)
            {
                fail_reason = (string)UI.CLUSTERMAP.TOOLTIP_INVALID_METEOR_TARGET;
                __result = null;
                return false;
            }

            if (start == end)
            {
                __result = new List<AxialI>();
                return false;
            }

            bool PassCell(AxialI cell)
            {
                if (!__instance.IsValidCell(cell))
                    return false;

                if (!(__instance.IsCellVisible(cell) || destination_selector.canNavigateFogOfWar))
                    return false;

                // 可见陨石不可穿过（除非 start/end）
                if (__instance.HasVisibleAsteroidAtCell(cell) && cell != start && cell != end)
                    return false;

                if (dodgeHiddenAsteroids)
                {
                    var a = ClusterGrid.Instance.GetAsteroidAtCell(cell);
                    if ((UnityEngine.Object)a != null &&
                        a.IsVisibleInFOW != ClusterRevealLevel.Visible &&
                        cell != start && cell != end)
                        return false;
                }

                return true;
            }

            bool PassClearanceRadius1(AxialI center)
            {
                if (center == start)
                    return true;

                foreach (var off in OFFSETS_R1)
                {
                    AxialI c = center + off;

                    if (!__instance.IsValidCell(c))
                        continue;

                    if (c == start || c == end)
                        continue;

                    if (__instance.HasVisibleAsteroidAtCell(c))
                        return false;

                    if (dodgeHiddenAsteroids)
                    {
                        var a = ClusterGrid.Instance.GetAsteroidAtCell(c);
                        if ((UnityEngine.Object)a != null && a.IsVisibleInFOW != ClusterRevealLevel.Visible)
                            return false;
                    }
                }
                return true;
            }

            bool PassRevealClearanceRadius1(AxialI center)
            {
                if (center == start)
                    return true;

                // Asteroidcraft test rule: keep every path cell at least 1 hex away from unrevealed cluster space.
                if (__instance.IsValidCell(center) && !__instance.IsCellVisible(center))
                    return false;

                foreach (var off in OFFSETS_R1)
                {
                    AxialI c = center + off;

                    if (!__instance.IsValidCell(c))
                        continue;

                    if (!__instance.IsCellVisible(c))
                        return false;
                }

                return true;
            }

            var q = new Queue<AxialI>();
            var visited = new HashSet<AxialI>();
            var cameFrom = new Dictionary<AxialI, AxialI>();

            q.Enqueue(start);
            visited.Add(start);

            bool found = false;

            while (q.Count > 0 && !found)
            {
                var cell = q.Dequeue();

                foreach (var dir in AxialI.DIRECTIONS) 
                {
                    var neighbor = cell + dir;

                    if (visited.Contains(neighbor))
                        continue;

                    if (!PassCell(neighbor))
                        continue;

                    if (!PassClearanceRadius1(neighbor))
                        continue;

                    if (!PassRevealClearanceRadius1(neighbor))
                        continue;

                    visited.Add(neighbor);
                    cameFrom[neighbor] = cell;

                    if (neighbor == end) { found = true; break; }
                    q.Enqueue(neighbor);
                }
            }

            if (!visited.Contains(end))
            {
                fail_reason = (string)UI.CLUSTERMAP.TOOLTIP_INVALID_DESTINATION_NO_PATH;
                __result = null;
                return false;
            }

            var path = new List<AxialI>();
            for (AxialI cur = end; cur != start; cur = cameFrom[cur])
                path.Add(cur);
            path.Reverse();

            __result = path;
            return false; 
        }
    }
}
