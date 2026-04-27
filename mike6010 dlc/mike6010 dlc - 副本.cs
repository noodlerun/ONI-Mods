using HarmonyLib;
using Klei;
using KMod;
using KSerialization;
using STRINGS;
using System;
using System.Collections.Generic;
using System.Linq;
using TUNING;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static mike6010_dlc.NuclearBomb;
using static SimMessages;


namespace mike6010_dlc
{
    public class StaticSave : KMonoBehaviour, ISaveLoadable
    {
        public static StaticSave Instance { get; private set; }

        [Serialize]
        public List<DeletedWorldItem> DeletedWorld = new List<NuclearBomb.DeletedWorldItem>();
        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();
            Instance = this;
        }
    }

    [HarmonyPatch(typeof(SaveGame), "OnPrefabInit")]
    public static class SaveGame_OnPrefabInit_Patch
    {
        public static void Postfix(SaveGame __instance)
        {
            __instance.gameObject.AddOrGet<StaticSave>();
        }
    }
    public static class ELEMENTS
    {
        public static string ID = "Tritium";
        public static string ID_Liq = "LiquidTritium";
        public static string ID_Soi = "SolidTritium";

        public static string NAME = "Gas Tritium";
        public static string NAME_Liq = "Liquid Tritium";
        public static string NAME_Soi = "Solid Tritium";
        public static class TRITIUM
        {
            public static LocString NAME = (LocString)STRINGS.UI.FormatAsLink(ELEMENTS.NAME, ELEMENTS.ID);
            public static LocString DESC = (LocString)"Tritium is an isotope of hydrogen with similar physical properties to hydrogen. When the temperature of tritium is higher than 5000℃, it will fuse into liquid nuclear waste at 9700℃.";
        }
        public static class LIQUIDTRITIUM
        {
            public static LocString NAME = (LocString)STRINGS.UI.FormatAsLink(ELEMENTS.NAME_Liq, ELEMENTS.ID_Liq);
            public static LocString DESC = (LocString)"Liquid Tritium is an isotope of liquid hydrogen with similar physical properties to liquid hydrogen.";
        }
        public static class SOLIDTRITIUM
        {
            public static LocString NAME = (LocString)STRINGS.UI.FormatAsLink(ELEMENTS.NAME_Soi, ELEMENTS.ID_Soi);
            public static LocString DESC = (LocString)"Solid Tritium is an isotope of solid hydrogen with similar physical properties to solid hydrogen.";
        }
    }
    public class DLC_Entry : UserMod2
    {
        public static string modPath;
        public static Dictionary<string, string> dic = null;
        public override void OnLoad(Harmony harmony)
        {
            base.OnLoad(harmony);
            harmony.PatchAll();

            Localization.RegisterForTranslation(typeof(ELEMENTS));
            modPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string templatePath = System.IO.Path.Combine(modPath, "translations");
            string langCode = Localization.GetCurrentLanguageCode();
            string poPath = System.IO.Path.Combine(modPath, "translations", langCode + ".po");
            string _po_path = poPath.Replace("_klei", "");
            if (System.IO.File.Exists(_po_path))
                dic = Localization.LoadStringsFile(_po_path, false);

        }
    }

    [HarmonyPatch(typeof(ElementLoader), "Load")]
    public class ElementLoader_Initialise_Patch
    {
        public static bool isPatched = false;
        public static void Postfix()
        {
            if (isPatched) return;

            Element tritium = ElementLoader.FindElementByHash((SimHashes)Hash.SDBMLower(ELEMENTS.ID));
            if (tritium != null)
            {
                Element nuclearWaste = ElementLoader.FindElementByHash(SimHashes.NuclearWaste);
                if (nuclearWaste != null)
                {
                    tritium.highTempTransition = nuclearWaste;  // 强制设置高温相变引用
                    isPatched = true;
                }
            }
        }
    }

    [HarmonyPatch(typeof(ElementLoader), "CollectElementsFromYAML")]
    public class ElementLoader_CollectElementsFromYAML_Patch
    {
        public static bool isPatched = false;
        public static void Postfix(ref List<ElementLoader.ElementEntry> __result)
        {
            if (isPatched)
            {
                return;
            }

            string modPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            string customYamlPath = System.IO.Path.Combine(modPath, "elem");

            ListPool<FileHandle, ElementLoader>.PooledList result = ListPool<FileHandle, ElementLoader>.Allocate();
            FileSystem.GetFiles(customYamlPath, "*.yaml", (ICollection<FileHandle>)result);

            foreach (FileHandle fileHandle in (List<FileHandle>)result)
            {
                if (!System.IO.Path.GetFileName(fileHandle.full_path).StartsWith("."))
                {
                    ElementLoader.ElementEntryCollection elementEntryCollection = YamlIO.LoadFile<ElementLoader.ElementEntryCollection>(fileHandle.full_path);
                    if (elementEntryCollection != null)
                    {
                        __result.AddRange((IEnumerable<ElementLoader.ElementEntry>)elementEntryCollection.elements);
                    }
                }
            }
            isPatched = true;
        }
    }

    [HarmonyPatch(typeof(SubstanceTable), "GetSubstance")]
    public class ElementLoader_SubstanceTable_Patch
    {
        public static void Postfix(SimHashes substance, ref Substance __result)
        {
            if ((int)substance == Hash.SDBMLower(ELEMENTS.ID))
            {
                var hydrogen = ElementLoader.FindElementByHash(SimHashes.Hydrogen);
                var sub_gas = ModUtil.CreateSubstance(ELEMENTS.ID, Element.State.Gas, hydrogen.substance.anim, hydrogen.substance.material, hydrogen.substance.colour, hydrogen.substance.uiColour, hydrogen.substance.conduitColour);
                __result = sub_gas;
            }
            if ((int)substance == Hash.SDBMLower(ELEMENTS.ID_Liq))
            {
                var hydrogen = ElementLoader.FindElementByHash(SimHashes.LiquidHydrogen);
                var sub_liq = ModUtil.CreateSubstance(ELEMENTS.ID_Liq, Element.State.Liquid, hydrogen.substance.anim, hydrogen.substance.material, hydrogen.substance.colour, hydrogen.substance.uiColour, hydrogen.substance.conduitColour);
                __result = sub_liq;
            }
            if ((int)substance == Hash.SDBMLower(ELEMENTS.ID_Soi))
            {
                var hydrogen = ElementLoader.FindElementByHash(SimHashes.SolidHydrogen);
                var sub_soi = ModUtil.CreateSubstance(ELEMENTS.ID_Soi, Element.State.Solid, hydrogen.substance.anim, hydrogen.substance.material, hydrogen.substance.colour, hydrogen.substance.uiColour, hydrogen.substance.conduitColour);
                __result = sub_soi;
            }

        }
    }

    [HarmonyPatch(typeof(Localization), "Initialize")]
    public class Localization_Initialize_Patch
    {
        public static void Postfix()
        {
            if (DLC_Entry.dic != null)
            {
                string name, desc;
                DLC_Entry.dic.TryGetValue("ELEMENTS_TRITIUM.TRITIUM.NAME", out name);
                DLC_Entry.dic.TryGetValue("ELEMENTS_TRITIUM.TRITIUM.DESC", out desc);

                Strings.Add("STRINGS.ELEMENTS." + ELEMENTS.ID.ToUpper() + ".NAME", name);
                Strings.Add("STRINGS.ELEMENTS." + ELEMENTS.ID.ToUpper() + ".DESC", desc);

                DLC_Entry.dic.TryGetValue("ELEMENTS_TRITIUM.LIQUIDTRITIUM.NAME", out name);
                DLC_Entry.dic.TryGetValue("ELEMENTS_TRITIUM.LIQUIDTRITIUM.DESC", out desc);

                Strings.Add("STRINGS.ELEMENTS." + ELEMENTS.ID_Liq.ToUpper() + ".NAME", name);
                Strings.Add("STRINGS.ELEMENTS." + ELEMENTS.ID_Liq.ToUpper() + ".DESC", desc);

                DLC_Entry.dic.TryGetValue("ELEMENTS_TRITIUM.SOLIDTRITIUM.NAME", out name);
                DLC_Entry.dic.TryGetValue("ELEMENTS_TRITIUM.SOLIDTRITIUM.DESC", out desc);

                Strings.Add("STRINGS.ELEMENTS." + ELEMENTS.ID_Soi.ToUpper() + ".NAME", name);
                Strings.Add("STRINGS.ELEMENTS." + ELEMENTS.ID_Soi.ToUpper() + ".DESC", desc);
            }
            else
            {
                Strings.Add("STRINGS.ELEMENTS." + ELEMENTS.ID.ToUpper() + ".NAME", ELEMENTS.TRITIUM.NAME);
                Strings.Add("STRINGS.ELEMENTS." + ELEMENTS.ID.ToUpper() + ".DESC", ELEMENTS.TRITIUM.DESC);

                Strings.Add("STRINGS.ELEMENTS." + ELEMENTS.ID_Liq.ToUpper() + ".NAME", ELEMENTS.LIQUIDTRITIUM.NAME);
                Strings.Add("STRINGS.ELEMENTS." + ELEMENTS.ID_Liq.ToUpper() + ".DESC", ELEMENTS.LIQUIDTRITIUM.DESC);

                Strings.Add("STRINGS.ELEMENTS." + ELEMENTS.ID_Soi.ToUpper() + ".NAME", ELEMENTS.SOLIDTRITIUM.NAME);
                Strings.Add("STRINGS.ELEMENTS." + ELEMENTS.ID_Soi.ToUpper() + ".DESC", ELEMENTS.SOLIDTRITIUM.DESC);
            }
        }
    }

    [HarmonyPatch(typeof(Sim), "HandleMessage")]
    public class Patch_SimHandleMessage
    {
        private static int tritiumHash = Hash.SDBMLower(ELEMENTS.ID);
        private static int liquidNuclearWasteHash = Hash.SDBMLower("NUCLEARWASTE");
        public unsafe static void Postfix(ref IntPtr __result)
        {
            if (Patch_SimCell.Update == false) return;
            if (__result == IntPtr.Zero) return;

            Sim.GameDataUpdate* gameDataUpdatePtr = (Sim.GameDataUpdate*)(void*)__result;

            for (int index = 0; index < gameDataUpdatePtr->numSubstanceChangeInfo; ++index)
            {
                Sim.SubstanceChangeInfo substanceChangeInfo = gameDataUpdatePtr->substanceChangeInfo[index];
                Element element = ElementLoader.elements[(int)substanceChangeInfo.newElemIdx];
                if ((int)Grid.Element[substanceChangeInfo.cellIdx].id == tritiumHash && (int)element.id == liquidNuclearWasteHash)
                {
                    ModifyCell(substanceChangeInfo.cellIdx, element.idx, 9988, Grid.Mass[substanceChangeInfo.cellIdx] * 0.98f, byte.MaxValue, 0, ReplaceType.Replace);
                }
            }
        }
    }

    [HarmonyPatch(typeof(Game), "StepTheSim")]
    public class Patch_SimCell
    {
        public static bool Update = false;
        public unsafe static void Prefix()
        {
            Update = true;
        }
        public unsafe static void Postfix()
        {
            Update = false;
        }
    }

    //[HarmonyPatch(typeof(DetailsScreen), "OnPrefabInit")]
    //public static class DetailsScreen_OnPrefabInit_Patch
    //{
    //    public static void Postfix(List<DetailsScreen.SideScreenRef> ___sideScreens)
    //    {
    //        GameObject sideScreenPrefab = new GameObject("GasCentrifugeSideScreenPrefab");
    //        sideScreenPrefab.SetActive(false); // 模板必须是禁用的
    //        sideScreenPrefab.transform.SetParent(DetailsScreen.Instance.transform);
    //
    //        SideScreenUtil.AddCustomSideScreen<GasCentrifugeSideScreen>("GasCentrifugeSideScreen", sideScreenPrefab, ___sideScreens);
    //    }
    //}
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
            public static class LASERHEATER
            {  // 从 MY_BUILDING_ID 改为 LASERHEATER
                public static LocString NAME = (LocString)STRINGS.UI.FormatAsLink("Laser Heater", "LaserHeater");
                public static LocString DESC = (LocString)"Converting electricity into lasers to heat gases, liquids, and solids";
                public static LocString EFFECT = (LocString)"85% of the 2000 watts of electricity is converted into laser heat, heating the gas, liquid, and solid in a selected direction. The absorption of heat is related to the \"light absorption coefficient\" of the material.";
                public static LocString UI = (LocString)"Choose Laser Direction";
            }
            public static class GASCENTRIFUGE
            {
                public static LocString NAME = (LocString)STRINGS.UI.FormatAsLink("Gas Centrifuge", "GasCentrifuge");
                public static LocString DESC = (LocString)"Further refine the input gas.";
                public static LocString EFFECT = (LocString)"According to the selected recipe, the input gas is further refined.";
                public static LocString UI_RECIPE_TITLE = (LocString)"Choose a recipe";
            }
            public static class FUSIONREACTOR
            {
                public static LocString NAME = (LocString)STRINGS.UI.FormatAsLink("Fusion Reactor", "FusionReactor");
                public static LocString DESC = (LocString)"Make elements undergo nuclear fusion reaction.";
                public static LocString EFFECT = (LocString)"The refined <link=\"TRITIUM\">tritium gas</link> is fed into the reactor to undergo nuclear fusion reaction. The nuclear fusion reaction occurs every 6 seconds, consuming 1000 grams of <link=\"TRITIUM\">tritium</link> each time and producing <link=\"NUCLEARWASTE\">liquid nuclear waste</link> at 9715 degrees Celsius. The waste is discharged at the 5th second.";
            }
            public static class NUCLEARBOMB
            {
                public static LocString NAME = (LocString)STRINGS.UI.FormatAsLink("Nuclear Bomb", "NuclearBomb");
                public static LocString DESC = (LocString)"Nuclear bombs that can be used to destroy a planet, Need to load 5000KG of <link=\"TRITIUM\">tritium</link> and 800KG of <link=\"ENRICHEDURANIUM\">enriched uranium</link>，Back up game save data!, Back up game save data!, Back up game save data!";
                public static LocString EFFECT = (LocString)"A nuclear bomb made with <link=\"TRITIUM\">tritium gas</link> and <link=\"ENRICHEDURANIUM\">enriched uranium</link> is extremely powerful and can be used to destroy asteroids. Back up game save data!, Back up game save data!, Back up game save data!";
            }
        }
    }

    [HarmonyPatch(typeof(HighEnergyParticleDirectionSideScreen), "IsValidForTarget")]
    public static class DetailsScreen_IsValidForTarget_Patch
    {
        public static void Postfix(HighEnergyParticleDirectionSideScreen __instance, GameObject target, ref bool __result)
        {
            if (target.GetComponent<LaserHeater>() != null)
            {
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(HighEnergyParticleDirectionSideScreen), "GetTitle")]
    public static class DetailsScreen_GetTitle_Patch
    {
        public static void Postfix(HighEnergyParticleDirectionSideScreen __instance, ref string __result)
        {
            if (DLC_Entry.dic != null)
            {
                string title;
                DLC_Entry.dic.TryGetValue("LaserHeater.BUILDINGS.PREFABS.LASERHEATER.UI", out title);
                __result = title;
            }
            else
            {
                __result = BUILDINGS.PREFABS.LASERHEATER.UI;
            }
            __result = BUILDINGS.PREFABS.LASERHEATER.UI;
        }
    }
    [HarmonyPatch(typeof(Db))]
    [HarmonyPatch("Initialize")]
    public class Db_Initialize_Patch
    {
        static bool isPatched = false;
        public static void Prefix()
        {
            if (DLC_Entry.dic != null)
            {
                string name, desc, effect;
                DLC_Entry.dic.TryGetValue("LaserHeater.BUILDINGS.PREFABS.LASERHEATER.NAME", out name);
                DLC_Entry.dic.TryGetValue("LaserHeater.BUILDINGS.PREFABS.LASERHEATER.DESC", out desc);
                DLC_Entry.dic.TryGetValue("LaserHeater.BUILDINGS.PREFABS.LASERHEATER.EFFECT", out effect);

                StringUtils.AddBuildingStrings("LaserHeater", name, desc, effect);


                DLC_Entry.dic.TryGetValue("BUILDINGS.PREFABS.GASCENTRIFUGE.NAME", out name);
                DLC_Entry.dic.TryGetValue("BUILDINGS.PREFABS.GASCENTRIFUGE.DESC", out desc);
                DLC_Entry.dic.TryGetValue("BUILDINGS.PREFABS.GASCENTRIFUGE.EFFECT", out effect);

                StringUtils.AddBuildingStrings("GasCentrifuge", name, desc, effect);


                DLC_Entry.dic.TryGetValue("BUILDINGS.PREFABS.FUSIONREACTOR.NAME", out name);
                DLC_Entry.dic.TryGetValue("BUILDINGS.PREFABS.FUSIONREACTOR.DESC", out desc);
                DLC_Entry.dic.TryGetValue("BUILDINGS.PREFABS.FUSIONREACTOR.EFFECT", out effect);

                StringUtils.AddBuildingStrings("FusionReactor", name, desc, effect);

                DLC_Entry.dic.TryGetValue("BUILDINGS.PREFABS.NUCLEARBOMB.NAME", out name);
                DLC_Entry.dic.TryGetValue("BUILDINGS.PREFABS.NUCLEARBOMB.DESC", out desc);
                DLC_Entry.dic.TryGetValue("BUILDINGS.PREFABS.NUCLEARBOMB.EFFECT", out effect);

                StringUtils.AddBuildingStrings("NuclearBomb", name, desc, effect);
            }
            else
            {
                StringUtils.AddBuildingStrings("LaserHeater", (string)BUILDINGS.PREFABS.LASERHEATER.NAME, (string)BUILDINGS.PREFABS.LASERHEATER.DESC, (string)BUILDINGS.PREFABS.LASERHEATER.EFFECT);
                StringUtils.AddBuildingStrings("GasCentrifuge", (string)BUILDINGS.PREFABS.GASCENTRIFUGE.NAME, (string)BUILDINGS.PREFABS.GASCENTRIFUGE.DESC, (string)BUILDINGS.PREFABS.GASCENTRIFUGE.EFFECT);
                StringUtils.AddBuildingStrings("FusionReactor", (string)BUILDINGS.PREFABS.FUSIONREACTOR.NAME, (string)BUILDINGS.PREFABS.FUSIONREACTOR.DESC, (string)BUILDINGS.PREFABS.FUSIONREACTOR.EFFECT);
                StringUtils.AddBuildingStrings("NuclearBomb", (string)BUILDINGS.PREFABS.NUCLEARBOMB.NAME, (string)BUILDINGS.PREFABS.NUCLEARBOMB.DESC, (string)BUILDINGS.PREFABS.NUCLEARBOMB.EFFECT);
            }
            StringUtils.AddStatusItemStrings("FusionReactorTritiumTooCold", "BUILDING", "Tritium Too Cold", "Stored tritium is below {0}. Current temperature: {1}.");
        }

        public static void Postfix()
        {
            if (isPatched)
            {
                return;
            }

            BuildingUtils.AddBuildingToPlanScreen("LaserHeater", (HashedString)"UTILITIES");
            BuildingUtils.AddBuildingToTech("LaserHeater", "TemperatureModulation");

            BuildingUtils.AddBuildingToPlanScreen("GasCentrifuge", (HashedString)"REFINING");
            BuildingUtils.AddBuildingToTech("GasCentrifuge", "NuclearRefinement");

            BuildingUtils.AddBuildingToPlanScreen("FusionReactor", (HashedString)"HEP");
            BuildingUtils.AddBuildingToTech("FusionReactor", "NuclearRefinement");

            BuildingUtils.AddBuildingToTech("NuclearBomb", "AdvancedNuclearResearch");

            isPatched = true;
        }
    }
    public class LaserHeaterConfig : IBuildingConfig
    {
        public override string[] GetRequiredDlcIds() => DlcManager.EXPANSION1;

        public const string ID = "LaserHeater";
        public const float POWER_CONSUMPTION = 2000f; // W
        public const float POWER_CONVERSION = 0.85f;
        public override BuildingDef CreateBuildingDef()
        {
            int width = 1;
            int height = 2;
            string anim = "LaserHeater_kanim";
            string[] MaterialCategory = new string[1] { "DIAMOND" };

            int hitpoints = 30;
            float construction_time = 30f;
            float[] construction_mass = new float[1] { 250f };
            float melting_point = 3000f;
            BuildLocationRule build_location_rule = BuildLocationRule.Anywhere;
            EffectorValues none = NOISE_POLLUTION.NONE;
            BuildingDef buildingDef = BuildingTemplates.CreateBuildingDef("LaserHeater", width, height, anim, hitpoints, construction_time, construction_mass, MaterialCategory, melting_point, build_location_rule, TUNING.BUILDINGS.DECOR.PENALTY.TIER1, none);
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
            buildingDef.SelfHeatKilowattsWhenActive = POWER_CONSUMPTION * (1f - POWER_CONVERSION) / 1000f;
            buildingDef.EnergyConsumptionWhenActive = POWER_CONSUMPTION;
            buildingDef.OverheatTemperature = 548.15f; //K
            buildingDef.DragBuild = true;
            buildingDef.PermittedRotations = PermittedRotations.R360;

            return buildingDef;
        }

        public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
        {
            go.AddOrGet<Operational>();
        }
        public override void DoPostConfigureComplete(GameObject go)
        {
            go.AddOrGet<LaserHeater>();
        }
    }
    public class LaserHeater : StateMachineComponent<LaserHeater.Instance>, IHighEnergyParticleDirection, ISim1000ms
    {
        struct luxRecord
        {
            public int cell;
            public int lux;
        }
        [MyCmpReq]
        private Operational operational;
        private static readonly EventSystem.IntraObjectHandler<LaserHeater> OnOperationalChangedDelegate = new EventSystem.IntraObjectHandler<LaserHeater>((Action<LaserHeater, object>)((component, data) => component.OnOperationalChanged(data)));
        private static readonly EventSystem.IntraObjectHandler<LaserHeater> OnBuildingDamageDelegate = new EventSystem.IntraObjectHandler<LaserHeater>((component, data) => component.OnBuildingBroken(data));

        KBatchedAnimController component;
        private KBatchedAnimController arm_anim_ctrl;
        private GameObject arm_go;
        private KAnimLink link;

        private KBatchedAnimController glow_anim_ctrl;
        private GameObject glow;

        KBatchedAnimController component_3;
        private KBatchedAnimController las_anim_ctrl;
        private GameObject las;
        private Vector3 las_offset = new Vector3(0, 1.1f, 0);
        private Vector3 gun_pivot;
        [Serialize]
        private EightDirection _direction;
        [Serialize]
        private bool isFirstSpawn = true;
        [Serialize]
        private Vector3 worldPos;
        [Serialize]
        private Vector2I CellXY;
        [Serialize]
        private Vector2I head_cell_xy;
        [Serialize]
        private int cell;
        [Serialize]
        private int head_cell;
        [Serialize]
        public Orientation currentOrientation;
        private static int lux = 200000;
        private static float animation_speed = 0.1f; // 动画速度

        private static float ratio_lux_k = LaserHeaterConfig.POWER_CONSUMPTION / (float)lux;

        private List<luxRecord> lux_record = new List<luxRecord>();
        private int num_lux_record = 0;
        public void Sim1000ms(float dt)
        {
            if (!this.operational.IsOperational)
                return;
            if (SpeedControlScreen.Instance.IsPaused)
                return;

            float gameDt = dt * (12 * 60 * 60) / (600f * (SpeedControlScreen.Instance.GetSpeed() + 1)); //现实时间转换为游戏时间： 游戏秒=12小时*60分*60秒/600秒/天
            LaserHeaterUpdate(gameDt);
        }
        private Vector2I GetOffsetFromDirection(EightDirection dir)
        {
            switch (dir)
            {
                case EightDirection.Up: return new Vector2I(0, 1);
                case EightDirection.UpRight: return new Vector2I(1, 1);
                case EightDirection.Right: return new Vector2I(1, 0);
                case EightDirection.DownRight: return new Vector2I(1, -1);
                case EightDirection.Down: return new Vector2I(0, -1);
                case EightDirection.DownLeft: return new Vector2I(-1, -1);
                case EightDirection.Left: return new Vector2I(-1, 0);
                case EightDirection.UpLeft: return new Vector2I(-1, 1);
                default: return new Vector2I(0, 0);
            }
        }
        public EightDirection Direction
        {
            get => this._direction;
            set
            {
                this._direction = value;
                Vector2[] ori_angle = { new Vector2(0f, 1f), new Vector2(1f, 0f), new Vector2(0f, -1f), new Vector2(-1f, 0f) };

                Vector2 d1 = ((Vector2)GetOffsetFromDirection(_direction));
                Vector2 d2 = (ori_angle[(int)currentOrientation]);

                if (d1.x * d2.x + d1.y * d2.y >= 0)
                {
                    this.RotateArm(-GetAngleFromDirection(_direction));
                }

            }
        }
        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();
            if (GetComponent<Rotatable>() != null)
            {
                currentOrientation = GetComponent<Rotatable>().GetOrientation();
                switch (currentOrientation)
                {
                    case Orientation.Neutral:
                        _direction = EightDirection.Up;
                        break;
                    case Orientation.R90:
                        _direction = EightDirection.Right;
                        break;
                    case Orientation.R180:
                        _direction = EightDirection.Down;
                        break;
                    case Orientation.R270:
                        _direction = EightDirection.Left;
                        break;
                }
            }
        }
        protected override void OnSpawn()
        {
            base.OnSpawn();


            if (isFirstSpawn)
            {
                worldPos = transform.GetPosition();
                cell = Grid.PosToCell(worldPos);
                CellXY = Grid.CellToXY(cell);  // 转换为 (x, y) 整数坐标

                currentOrientation = GetComponent<Rotatable>().GetOrientation();

                switch (currentOrientation)
                {
                    case Orientation.Neutral:
                        _direction = EightDirection.Up;
                        break;
                    case Orientation.R90:
                        _direction = EightDirection.Right;
                        break;
                    case Orientation.R180:
                        _direction = EightDirection.Down;
                        break;
                    case Orientation.R270:
                        _direction = EightDirection.Left;
                        break;
                }

                head_cell_xy = CellXY + GetOffsetFromDirection(_direction);
                head_cell = Grid.XYToCell(head_cell_xy.x, head_cell_xy.y);

                isFirstSpawn = false;  // 设置为 false，避免重复初始化

            }

            component = this.GetComponent<KBatchedAnimController>();
            string name = component.name + ".gun";
            this.arm_go = new GameObject(name);
            this.arm_go.SetActive(false);
            this.arm_go.transform.parent = component.transform;
            this.arm_go.AddComponent<KPrefabID>().PrefabTag = new Tag(name);
            this.arm_anim_ctrl = this.arm_go.AddComponent<KBatchedAnimController>();
            this.arm_anim_ctrl.AnimFiles = new KAnimFile[1] { component.AnimFiles[0] };
            this.arm_anim_ctrl.initialAnim = "gun";
            this.arm_anim_ctrl.isMovable = true;
            this.arm_anim_ctrl.sceneLayer = Grid.SceneLayer.TransferArm;
            component.SetSymbolVisiblity((KAnimHashedString)"LaserHeater_Gun", false);
            this.arm_go.transform.SetPosition((Vector3)component.GetSymbolTransform(new HashedString("LaserHeater_Gun"), out bool _).GetColumn(3) with
            {
                z = Grid.GetLayerZ(Grid.SceneLayer.TransferArm)
            });
            this.arm_go.SetActive(true);
            this.link = new KAnimLink((KAnimControllerBase)component, (KAnimControllerBase)this.arm_anim_ctrl);

            KBatchedAnimController component_2 = GetComponent<KBatchedAnimController>();
            string name_2 = component_2.name + ".glow";
            glow = new GameObject(name_2);
            glow.SetActive(false);
            glow.transform.parent = component_2.transform;
            glow.AddComponent<KPrefabID>().PrefabTag = new Tag(name_2);
            glow_anim_ctrl = glow.AddComponent<KBatchedAnimController>();
            glow_anim_ctrl.AnimFiles = new KAnimFile[1] { component_2.AnimFiles[0] };
            glow_anim_ctrl.initialAnim = "glow";
            glow_anim_ctrl.isMovable = true;
            glow_anim_ctrl.sceneLayer = Grid.SceneLayer.TransferArm;
            glow.transform.SetPosition((Vector3)component_2.GetSymbolTransform(new HashedString("LaserHeater_Gun"), out bool _).GetColumn(3) with
            {
                z = Grid.GetLayerZ(Grid.SceneLayer.TransferArm)
            });
            this.link = new KAnimLink((KAnimControllerBase)component_2, (KAnimControllerBase)glow_anim_ctrl);


            component_3 = GetComponent<KBatchedAnimController>();
            gun_pivot = (Vector3)component_3.GetSymbolTransform(new HashedString("LaserHeater_Gun"), out bool _).GetColumn(3) with { z = Grid.GetLayerZ(Grid.SceneLayer.TransferArm) };

            string name_3 = component_3.name + ".las";
            las = new GameObject(name_3);
            las.SetActive(false);
            las.transform.parent = component_3.transform;
            las.transform.SetPosition(gun_pivot + las_offset);

            las_anim_ctrl = las.AddComponent<KBatchedAnimController>();
            las_anim_ctrl.AnimFiles = new KAnimFile[1] { component_3.AnimFiles[0] };
            las_anim_ctrl.initialAnim = "laser";
            las_anim_ctrl.isMovable = true;
            las_anim_ctrl.sceneLayer = Grid.SceneLayer.BuildingFront;// TransferArm;

            link = new KAnimLink((KAnimControllerBase)component_3, (KAnimControllerBase)las_anim_ctrl);

            Subscribe<LaserHeater>(-592767678, OnOperationalChangedDelegate);
            this.smi.StartSM();

            RotateArm(-GetAngleFromDirection(_direction));
            arm_anim_ctrl.Play(new HashedString("gun"), KAnim.PlayMode.Loop);

            Subscribe((int)GameHashes.BuildingBroken, OnBuildingBroken);
        }
        protected override void OnCleanUp()
        {
            for (int i = 0; i < num_lux_record; i++)
            {
                Grid.LightCount[lux_record[i].cell] -= lux_record[i].lux; // 减去之前的光照值
            }
            num_lux_record = 0; // 重置记录数量

            Unsubscribe((int)GameHashes.DoBuildingDamage, OnBuildingDamageDelegate);

            base.OnCleanUp();
        }
        private void OnBuildingBroken(object data)
        {
            component.SetSymbolVisiblity((KAnimHashedString)"LaserHeater_Gun", true);
            arm_go.SetActive(false);
        }
        private void RotateArm(float angle)
        {
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);  // 创建 Quaternion：绕 Y 轴旋转 90 度
            Vector3 rotated_offset = rotation * las_offset;  // 旋转偏移向量
            las.transform.SetPosition(gun_pivot + rotated_offset);

            arm_go.transform.rotation = rotation;
            glow.transform.rotation = rotation;
            las_anim_ctrl.transform.rotation = rotation;
        }
        private void OnOperationalChanged(object data)
        {

            bool isEnabled = operational.IsOperational;  // true 表示启用（operational），false 表示禁用
            if (isEnabled)
            {
                las.SetActive(true);
                las_anim_ctrl.Play(new HashedString("laser"), KAnim.PlayMode.Loop, animation_speed);

                glow.SetActive(true);
                glow_anim_ctrl.Play(new HashedString("glow"), KAnim.PlayMode.Loop, animation_speed);
            }
            else
            {
                las_anim_ctrl.Stop();
                las.SetActive(false);

                glow_anim_ctrl.Stop();
                glow.SetActive(false);

                for (int i = 0; i < num_lux_record; i++)
                {
                    Grid.LightCount[lux_record[i].cell] -= lux_record[i].lux; // 减去之前的光照值
                }
                num_lux_record = 0; // 重置记录数量
            }
        }
        public void LaserHeaterUpdate(float dt)
        {
            Vector2I offset = GetOffsetFromDirection(_direction);
            if (offset.x == 0 && offset.y == 0) return;

            int currentCell = head_cell;// cell;
            int currentLux = lux;
            int DeltaLux = 0;
            int i = 0;
            for (; ; )
            {
                currentCell = Grid.OffsetCell(currentCell, offset.x, offset.y);
                if (!Grid.IsValidCell(currentCell)) break;

                if (Grid.Element[currentCell].IsLiquid)
                {
                    double tmp_absorption = (double)Grid.Element[currentCell].lightAbsorptionFactor * ((double)Grid.Mass[currentCell] * 0.06);
                    double Absorption = Math.Min(1.0, tmp_absorption);

                    DeltaLux = Math.Max(0, currentLux - (int)((double)currentLux * (1 - Absorption)));
                    currentLux = (int)((double)currentLux * (1.0 - Absorption));
                }

                if (Grid.Element[currentCell].IsGas)
                {
                    double tmp_absorption = (double)Grid.Element[currentCell].lightAbsorptionFactor * ((double)Grid.Mass[currentCell] * 0.02);
                    double Absorption = Math.Min(1.0, tmp_absorption);

                    DeltaLux = Math.Max(0, currentLux - (int)((double)currentLux * (1 - Absorption)));
                    currentLux = (int)((double)currentLux * (1.0 - Absorption));
                }

                if (Grid.Element[currentCell].IsSolid)
                {
                    DeltaLux = Math.Max(0, currentLux - (int)(currentLux * (1f - Grid.Element[currentCell].lightAbsorptionFactor)));
                    currentLux = (int)((float)currentLux * (1f - Grid.Element[currentCell].lightAbsorptionFactor));
                }

                if (i >= lux_record.Count)// 分配内存
                    lux_record.Add(new luxRecord { });

                Grid.LightCount[currentCell] += currentLux; // 更新光照值
                if (i < num_lux_record)// 如果记录中已经有数据，还原之前的光照值
                {
                    Grid.LightCount[lux_record[i].cell] -= lux_record[i].lux; // 还原之前的光照值
                }

                float factor = 1.0f;
                if (Grid.Element[currentCell].IsTemperatureInsulated)
                {
                    factor = Grid.Element[currentCell].thermalConductivity;
                }
                var tmp = new luxRecord { cell = currentCell, lux = currentLux };//直接添加新的记录
                float energy = Math.Max(10000f, DeltaLux * ratio_lux_k * LaserHeaterConfig.POWER_CONVERSION * dt * factor);  // lux转换为热量J
                float maxTemp = 10000f;  // 最大温度限（K），防止无限加热
                EnergySourceID source = EnergySourceID.DebugHeat;
                ModifyEnergy(currentCell, energy / 1000f * 5, maxTemp, source);

                lux_record[i] = tmp;//记录
                i++;

                if (currentLux < 100f)
                {//如果全部被吸收了
                    int tmp_i = i;
                    for (; i < num_lux_record; i++)//清除多余旧的光照
                    {
                        Grid.LightCount[lux_record[i].cell] -= lux_record[i].lux; // 减去之前的光照值
                    }

                    num_lux_record = tmp_i;
                    break;
                }
            }

            Vector3 EndPos = Grid.CellToPosCCC(currentCell, Grid.SceneLayer.NoLayer);
            Vector3 StartPos = Grid.CellToPosCCC(head_cell, Grid.SceneLayer.NoLayer);
            this.las_anim_ctrl.GetBatchInstanceData().SetClipRadius(gun_pivot.x + las_offset.x, gun_pivot.y + las_offset.y, (EndPos - StartPos - las_offset).sqrMagnitude, true);
        }
        private float GetAngleFromDirection(EightDirection dir)
        {
            switch (dir)
            {
                case EightDirection.Up: return 0f;
                case EightDirection.UpRight: return 45f;
                case EightDirection.Right: return 90f;
                case EightDirection.DownRight: return 135f;
                case EightDirection.Down: return 180f;
                case EightDirection.DownLeft: return 225f;
                case EightDirection.Left: return 270f;
                case EightDirection.UpLeft: return 315f;
                default: return 0f;
            }
        }
        public class Instance(LaserHeater master) : GameStateMachine<LaserHeater.States, LaserHeater.Instance, LaserHeater, object>.GameInstance(master)
        {
        }
        public class States : GameStateMachine<LaserHeater.States, LaserHeater.Instance, LaserHeater>
        {
            public StateMachine<LaserHeater.States, LaserHeater.Instance, LaserHeater, object>.BoolParameter transferring;
            public GameStateMachine<LaserHeater.States, LaserHeater.Instance, LaserHeater, object>.State off;
            public GameStateMachine<LaserHeater.States, LaserHeater.Instance, LaserHeater, object>.State on;
            public override void InitializeStates(out StateMachine.BaseState default_state)
            {
                default_state = (StateMachine.BaseState)this.off;
                this.root.DoNothing();
                this.off
                    .PlayAnim("off")
                    .EventTransition(GameHashes.OperationalChanged, this.on, smi => smi.GetComponent<Operational>().IsOperational);

                this.on
                    .PlayAnim("on")
                    .Enter(smi => smi.master.operational.SetActive(true))  // 进入时设置 IsActive = true，触发自发热
                    .Exit(smi => smi.master.operational.SetActive(false))  // 退出时重置
                    .EventTransition(GameHashes.OperationalChanged, this.off, smi => !smi.GetComponent<Operational>().IsOperational);
            }
        }
    }
    public class GasCentrifugeConfig : IBuildingConfig
    {
        public override string[] GetRequiredDlcIds() => DlcManager.EXPANSION1;
        public const float POWER_CONSUMPTION = 180f; // W
        public const float CONSUMPTION_RATE = 0.1f; // kg/s
        public override BuildingDef CreateBuildingDef()
        {
            int width = 4;
            int height = 4;
            string anim = "GasCentrifuge_kanim";
            string[] MaterialCategory = new string[1] { "RefinedMetal" };
            int hitpoints = 30;
            float construction_time = 60f;
            float[] construction_mass = new float[1] { 400f };
            float melting_point = 1600f;
            BuildLocationRule build_location_rule = BuildLocationRule.OnFloor;
            EffectorValues none = NOISE_POLLUTION.NONE;
            BuildingDef buildingDef = BuildingTemplates.CreateBuildingDef("GasCentrifuge", width, height, anim, hitpoints, construction_time, construction_mass, MaterialCategory, melting_point, build_location_rule, TUNING.BUILDINGS.DECOR.PENALTY.TIER1, none);
            buildingDef.Floodable = true;
            buildingDef.AudioCategory = "HollowMetal";
            buildingDef.Overheatable = true;
            buildingDef.Repairable = true;
            buildingDef.Disinfectable = false;
            buildingDef.Invincible = false;
            buildingDef.LogicInputPorts = LogicOperationalController.CreateSingleInputPortList(new CellOffset(0, 0));
            buildingDef.PowerInputOffset = new CellOffset(0, 0);
            buildingDef.SceneLayer = Grid.SceneLayer.Building;
            buildingDef.ForegroundLayer = Grid.SceneLayer.BuildingFront;
            buildingDef.RequiresPowerInput = true;
            buildingDef.EnergyConsumptionWhenActive = POWER_CONSUMPTION;
            buildingDef.SelfHeatKilowattsWhenActive = 0.5f;
            buildingDef.OverheatTemperature = 348.15f; //K
            buildingDef.DragBuild = true;

            buildingDef.InputConduitType = ConduitType.Gas;
            buildingDef.UtilityInputOffset = new CellOffset(0, 0);
            buildingDef.OutputConduitType = ConduitType.Gas;
            buildingDef.UtilityOutputOffset = new CellOffset(1, 0);

            buildingDef.PermittedRotations = PermittedRotations.FlipH;
            return buildingDef;
        }
        public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
        {
            go.AddOrGet<Operational>();
            go.AddOrGet<DropAllWorkable>();

            go.GetComponent<KPrefabID>().AddTag(RoomConstraints.ConstraintTags.IndustrialMachinery);

            Storage defaultStorage = BuildingTemplates.CreateDefaultStorage(go);
            defaultStorage.capacityKg = 10f; 
            defaultStorage.showInUI = true; 
            defaultStorage.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);

            Storage outStorage = go.AddComponent<Storage>();

            ConduitConsumer conduitConsumer = go.AddOrGet<ConduitConsumer>();
            conduitConsumer.conduitType = ConduitType.Gas;
            conduitConsumer.consumptionRate = 1.0f/*GasCentrifugeConfig.CONSUMPTION_RATE*/;
            conduitConsumer.capacityTag = GameTags.Gas;
            conduitConsumer.wrongElementResult = ConduitConsumer.WrongElementResult.Dump;
            conduitConsumer.storage = defaultStorage;

            ConduitDispenser conduitDispenser = go.AddOrGet<ConduitDispenser>();
            conduitDispenser.conduitType = ConduitType.Gas;
            conduitDispenser.alwaysDispense = true;
            conduitDispenser.storage = defaultStorage;

            ElementConverter elementConverter = go.AddOrGet<ElementConverter>();
            elementConverter.consumedElements = new ElementConverter.ConsumedElement[0];
            elementConverter.outputElements = new ElementConverter.OutputElement[0];
            elementConverter.SetStorage(defaultStorage);

            elementConverter.consumedElements = new ElementConverter.ConsumedElement[]
            {
                new ElementConverter.ConsumedElement(SimHashes.Hydrogen.CreateTag(), 1.0f, true)
            };

            elementConverter.outputElements = new ElementConverter.OutputElement[]
            {
                new ElementConverter.OutputElement(GasCentrifugeConfig.CONSUMPTION_RATE, (SimHashes)Hash.SDBMLower(ELEMENTS.ID), 0f, true, true, 0f, 0.5f, 1f, byte.MaxValue, 0, true)
            };

            conduitConsumer.capacityTag = SimHashes.Hydrogen.CreateTag();

            conduitDispenser.alwaysDispense = true;
            conduitDispenser.elementFilter = new SimHashes[] { (SimHashes)Hash.SDBMLower(ELEMENTS.ID) };

            go.AddOrGet<GasCentrifuge>();
        }
        public override void DoPostConfigureComplete(GameObject go)
        {
        }
    }
    public class SideScreenUtil
    {
        public static void AddCustomSideScreen<T>(
          string name,
          GameObject prefab,
          List<DetailsScreen.SideScreenRef> existScreens)
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
    public class GasCentrifuge : StateMachineComponent<GasCentrifuge.StatesInstance>
    {
        [MyCmpReq]
        private ElementConverter converter;
        [MyCmpReq]
        private Operational operational;
        [MyCmpReq]
        private ConduitDispenser dispenser;
        [MyCmpReq]
        ConduitConsumer consumer;
        [Serialize]
        public int selected_recipe = -1;

        public readonly static List<SimHashes> input = new List<SimHashes>
        {
            SimHashes.Hydrogen,
        };
        public readonly static List<string> in_string = new List<string>
        {
            Strings.Get("STRINGS.ELEMENTS.HYDROGEN.NAME"),
        };
        public readonly static List<SimHashes> output = new List<SimHashes>
        {
            (SimHashes)Hash.SDBMLower(ELEMENTS.ID),
        };
        public readonly static List<string> out_string = new List<string>();
        protected override void OnSpawn()
        {
            base.OnSpawn();
            this.smi.StartSM();

        }
        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();
        }
        public void RecipeChange(int index)
        {
            selected_recipe = index;
            UpdateRecipe();
        }
        private void UpdateRecipe()
        {
            converter.SetAllConsumedActive(false);

            if (selected_recipe < 0 || selected_recipe >= input.Count)
            {
                if (converter != null)
                    converter.SetAllConsumedActive(false);
                dispenser.elementFilter = null;

                return;
            }

            if (converter.consumedElements.Length > 0 && converter.outputElements.Length > 0)
            {
                float consumptionRate = GasCentrifugeConfig.CONSUMPTION_RATE;
                // Assuming 1:1 ratio for simplicity; adjust proportion if needed
                float outputRate = consumptionRate;
                converter.consumedElements[0].Tag = input[selected_recipe].CreateTag();
                converter.consumedElements[0].MassConsumptionRate = GasCentrifugeConfig.CONSUMPTION_RATE;
                converter.consumedElements[0].IsActive = true;

                converter.outputElements[0].massGenerationRate = GasCentrifugeConfig.CONSUMPTION_RATE;
                converter.outputElements[0].elementHash = output[selected_recipe];
                converter.outputElements[0].minOutputTemperature = 0f;
                converter.outputElements[0].useEntityTemperature = true;
                converter.outputElements[0].storeOutput = true;
                converter.outputElements[0].outputElementOffset = new Vector2(0, 0);
                converter.outputElements[0].diseaseWeight = 0;
                converter.outputElements[0].addedDiseaseIdx = byte.MaxValue;
                converter.outputElements[0].addedDiseaseCount = 0;
                converter.outputElements[0].IsActive = true;

            }

            consumer.capacityTag = input[selected_recipe].CreateTag();//SimHashes.Hydrogen.CreateTag();

            dispenser.alwaysDispense = true;
            dispenser.elementFilter = new SimHashes[] { output[selected_recipe] };

            converter.SetAllConsumedActive(true);
        }
        GasCentrifuge()
        {
            if (DLC_Entry.dic != null) {
                string name;
                DLC_Entry.dic.TryGetValue("ELEMENTS_TRITIUM.TRITIUM.NAME", out name);
                out_string.Add(name);
            }
        }
        public class StatesInstance : GameStateMachine<GasCentrifuge.States, GasCentrifuge.StatesInstance, GasCentrifuge, object>.GameInstance
        {
            public StatesInstance(GasCentrifuge master) : base(master) { }
            public bool IsOp() => master.operational.IsOperational;

            // 仅用于测试：通电时维持 Active=true 以确保持续吃电与循环动画
            public void SetActive(bool on) => master.operational.SetActive(on, false);
        }
        public class States : GameStateMachine<GasCentrifuge.States, GasCentrifuge.StatesInstance, GasCentrifuge>
        {
            public State off;         // 断电/红灯
            public State turning_on;  // 通电过渡（播放 on 一次）
            public State working;     // 持续工作（working_loop 循环）

            public override void InitializeStates(out BaseState default_state)
            {
                default_state = off;

                off
                    .Enter(smi =>
                    {
                        smi.SetActive(false);
                        if (smi.IsOp()) smi.GoTo(turning_on);
                    })
                    .PlayAnim("off", KAnim.PlayMode.Loop)
                    .EventTransition(GameHashes.OperationalChanged, turning_on, smi => smi.IsOp());
                turning_on
                    .PlayAnim("on", KAnim.PlayMode.Once)
                    .EventTransition(GameHashes.OperationalChanged, off, smi => !smi.IsOp())
                    .OnAnimQueueComplete(working);

                // WORKING：循环 working_loop；掉电/红灯则回 off
                working
                    .Enter(smi =>
                    {
                        smi.SetActive(true); // 测试阶段：维持“工作中”
                    })
                    .PlayAnim("on", KAnim.PlayMode.Loop)
                    .EventTransition(GameHashes.OperationalChanged, off, smi => !smi.IsOp());
            }
        }
    }
    public class GasCentrifugeSideScreen : SideScreenContent
    {
        private GasCentrifuge m_target;
        private GameObject contentContainer;
        private GameObject rowPrefab;
        private static bool isInitialized = false;
        private static List<Button> buttons = new List<Button>();
        private UnityEngine.Events.UnityAction[] operations;
        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();
            if (isInitialized == true)
            {
                return;
            }

            var mainLayout = gameObject.GetComponent<VerticalLayoutGroup>();
            if (mainLayout == null) mainLayout = gameObject.AddComponent<VerticalLayoutGroup>();
            mainLayout.childAlignment = TextAnchor.UpperCenter;
            mainLayout.childForceExpandHeight = false;
            mainLayout.childForceExpandWidth = false;
            mainLayout.padding = new RectOffset(15, 15, 15, 15); // 上下左右边距
            mainLayout.spacing = 10f;

            contentContainer = new GameObject("ContentContainer");
            contentContainer.transform.SetParent(transform, false);

            var vlg = contentContainer.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.spacing = 10f;

            rowPrefab = new GameObject("RowPrefab");
            rowPrefab.transform.SetParent(transform, false); // 先放在根目录下，不影响布局
            rowPrefab.SetActive(false);

            var rowLayout = rowPrefab.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 10f;
            rowLayout.childControlWidth = false;
            rowLayout.childControlHeight = false;
            rowLayout.childAlignment = TextAnchor.MiddleLeft; // 垂直居中对齐

            var rowLayoutElement = rowPrefab.AddComponent<LayoutElement>();
            rowLayoutElement.minHeight = 40f;
            rowLayoutElement.preferredHeight = 60f;

            var rowFitter = rowPrefab.AddComponent<ContentSizeFitter>();
            rowFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var iconGO = new GameObject("Icon");
            iconGO.transform.SetParent(rowPrefab.transform, false);
            var iconImage = iconGO.AddComponent<Image>();
            var iconLayout = iconGO.AddComponent<LayoutElement>();
            iconLayout.preferredWidth = 15;
            iconLayout.preferredHeight = 15;

            var labelGO = new GameObject("Label");
            labelGO.transform.SetParent(rowPrefab.transform, false);
            var labelText = labelGO.AddComponent<LocText>();
            labelText.color = Color.black;
            labelText.fontSize = 18f;
            labelText.text = "";
            labelText.alignment = TMPro.TextAlignmentOptions.Left;
            labelText.margin = new Vector4(-80f, -15f, 0f, 0f);

            var rt = labelGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);  // 锚点左下
            rt.anchorMax = new Vector2(1f, 1f);  // 锚点右上（拉伸填充父级）
            rt.pivot = new Vector2(0.5f, 0.5f);  // 中心点居中
            rt.anchoredPosition = Vector2.zero;  // 位置重置
            rt.sizeDelta = Vector2.zero;  // 大小自适应


            var labelLayout = labelGO.AddComponent<LayoutElement>();
            labelLayout.flexibleWidth = 1f; // 占据剩余宽度

            rowPrefab.SetActive(false); // 确保模板是禁用的




            int count = 0;
            foreach (var recipe in GasCentrifuge.input)
            {
                Element element = ElementLoader.FindElementByHash(recipe);
                GameObject newRow = Instantiate(rowPrefab, contentContainer.transform);

                newRow.SetActive(true);
                // 步骤 5: 增加对 Find 和 GetComponent 的健壮性检查
                var bgImage = newRow.AddComponent<Image>();
                bgImage.color = Color.white; // Default neutral color; will be tinted by button states
                bgImage.raycastTarget = true; // Ensure it receives raycasts

                var button = newRow.AddComponent<Button>();
                button.targetGraphic = bgImage;

                var colors = button.colors;
                colors.normalColor = new Color(0.9f, 0.9f, 0.9f, 1f);
                colors.pressedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
                colors.highlightedColor = colors.pressedColor;
                colors.selectedColor = new Color(228f / 255f, 198f / 255f, 213f / 255f, 1f); ;
                colors.disabledColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
                button.colors = colors;
                buttons.Add(button);

                var outline = newRow.AddComponent<Outline>();
                outline.effectColor = new Color(135f / 255f, 69f / 255f, 102f / 255f, 1.0f);   // 边框颜色
                outline.effectDistance = new Vector2(4f, -4f); // 偏移像素，控制边框厚度

                EventTrigger trigger = newRow.AddComponent<EventTrigger>();
                var entryEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                entryEnter.callback.AddListener((eventData) => outline.enabled = true);
                trigger.triggers.Add(entryEnter);

                var entryExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                entryExit.callback.AddListener((eventData) => outline.enabled = false);
                trigger.triggers.Add(entryExit);


                Transform iconTransform = newRow.transform.Find("Icon");
                Transform labelTransform = newRow.transform.Find("Label");

                Image icon = iconTransform.GetComponent<Image>();
                LocText label = labelTransform.GetComponent<LocText>();


                Tuple<Sprite, Color> spriteInfo = Def.GetUISprite(element);
                icon.sprite = spriteInfo.first;
                icon.color = spriteInfo.second; // 使用从GetUISprite获取的颜色，更准确
                label.text = GasCentrifuge.in_string[count] + " -> " + GasCentrifuge.out_string[count]; // 显示配方名称，而不是元素名称

                var rt_icon = icon.GetComponent<RectTransform>();
                rt_icon.sizeDelta = new Vector2(43, 43);
                icon.preserveAspect = true;
                count++;
            }
            isInitialized = true;
        }
        public override bool IsValidForTarget(GameObject target)
        {
            if (target.GetComponent<GasCentrifuge>() != null)
            {
                return true;
            }
            return false;
        }
        public override string GetTitle()
        {
            if (DLC_Entry.dic != null)
            {
                string name;
                DLC_Entry.dic.TryGetValue("BUILDINGS.PREFABS.GASCENTRIFUGE.UI_RECIPE_TITLE", out name);
                return name;
            }
            else
            {
                return BUILDINGS.PREFABS.GASCENTRIFUGE.UI_RECIPE_TITLE;
            }
        }
        public override void SetTarget(GameObject target)
        {
            base.SetTarget(target);
            if (target == null)
            {
                return;
            }

            m_target = target.GetComponent<GasCentrifuge>();
            if (m_target == null)
            {
                return;
            }
            else
            {
                if (buttons == null || buttons.Count == 0)
                {
                    OnPrefabInit(); // 手动补一次
                }

                if (m_target.selected_recipe >= 0 && m_target.selected_recipe < buttons.Count)
                {
                    buttons[m_target.selected_recipe].Select();
                }
                buttons[0].onClick.AddListener(() => m_target.RecipeChange(0));
            }
        }
    }
    public class FusionReactorConfig : IBuildingConfig
    {
        public override string[] GetRequiredDlcIds() => DlcManager.EXPANSION1;
        public const string ID = "FusionReactor";
        public const float POWER_CONSUMPTION = 3000f; // W
        public const int RADIATION_EMITTER_RANGE = 10;
        public const float RADIATION_EMITTER_RADS = 800f;
        public override BuildingDef CreateBuildingDef()
        {
            int width = 7;
            int height = 7;
            string anim = "fusion_reactor_kanim";
            string[] MaterialCategory = new string[1] { "RefinedMetal" };

            int hitpoints = 500;
            float construction_time = 120f;
            float[] construction_mass = new float[1] { 1200f };
            float melting_point = 3000f;
            BuildLocationRule build_location_rule = BuildLocationRule.OnFloor;
            EffectorValues none = NOISE_POLLUTION.NONE;
            BuildingDef buildingDef = BuildingTemplates.CreateBuildingDef(ID, width, height, anim, hitpoints, construction_time, construction_mass, MaterialCategory, melting_point, build_location_rule, TUNING.BUILDINGS.DECOR.PENALTY.TIER1, none);
            buildingDef.Floodable = false;
            buildingDef.AudioCategory = "Metal";
            buildingDef.Overheatable = false;
            buildingDef.Repairable = true;
            buildingDef.Disinfectable = false;
            buildingDef.Invincible = false;
            buildingDef.LogicInputPorts = LogicOperationalController.CreateSingleInputPortList(new CellOffset(2, 0));
            buildingDef.PowerInputOffset = new CellOffset(2, 0);
            buildingDef.SceneLayer = Grid.SceneLayer.Building;
            buildingDef.ForegroundLayer = Grid.SceneLayer.BuildingFront;
            buildingDef.RequiresPowerInput = true;
            buildingDef.SelfHeatKilowattsWhenActive = 0;
            buildingDef.EnergyConsumptionWhenActive = POWER_CONSUMPTION;
            buildingDef.DragBuild = true;
            buildingDef.PermittedRotations = PermittedRotations.FlipH;

            buildingDef.InputConduitType = ConduitType.Gas;
            buildingDef.UtilityInputOffset = new CellOffset(2, 0);

            return buildingDef;
        }
        public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
        {
            go.AddOrGet<Operational>();
            go.AddOrGet<DropAllWorkable>();

            go.AddOrGet<Operational>();
            go.GetComponent<KPrefabID>().AddTag(RoomConstraints.ConstraintTags.IndustrialMachinery);

            Storage TritiumStorage = go.AddComponent<Storage>();
            TritiumStorage.capacityKg = 100f; // 设置一个小的内部缓冲容量
            TritiumStorage.showInUI = true; // 在UI中显示存储，方便调试
            TritiumStorage.storageFilters = new List<Tag>() { TagManager.Create(ELEMENTS.ID) };
            TritiumStorage.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);

            Storage nuclearWasteStorage = go.AddComponent<Storage>();
            nuclearWasteStorage.capacityKg = 100f;
            nuclearWasteStorage.showInUI = false;
            nuclearWasteStorage.storageFilters = new List<Tag>() { SimHashes.NuclearWaste.CreateTag() };
            nuclearWasteStorage.SetDefaultStoredItemModifiers(Storage.StandardSealedStorage);

            ConduitConsumer conduitConsumer = go.AddOrGet<ConduitConsumer>();
            conduitConsumer.conduitType = ConduitType.Gas;
            conduitConsumer.consumptionRate = 1;
            conduitConsumer.capacityTag = GameTags.Gas;
            conduitConsumer.wrongElementResult = ConduitConsumer.WrongElementResult.Dump;
            conduitConsumer.storage = TritiumStorage;

            conduitConsumer.forceAlwaysSatisfied = true;
            conduitConsumer.ignoreMinMassCheck = true;

            RadiationEmitter radiationEmitter = go.AddComponent<RadiationEmitter>();
            radiationEmitter.emitType = RadiationEmitter.RadiationEmitterType.Constant;
            radiationEmitter.emitRadiusX = (short)RADIATION_EMITTER_RANGE;
            radiationEmitter.emitRadiusY = (short)RADIATION_EMITTER_RANGE;
            radiationEmitter.radiusProportionalToRads = false;
            radiationEmitter.emissionOffset = new Vector3(0.0f, 2f, 0.0f);
            radiationEmitter.emitRads = 0f;
        }
        public override void DoPostConfigureComplete(GameObject go)
        {
            go.AddOrGet<KBatchedAnimHeatPostProcessingEffect>();
            go.AddOrGet<FusionReactor>();
        }
    }
    public class FusionReactor : StateMachineComponent<FusionReactor.StatesInstance>, ISim1000ms, IGameObjectEffectDescriptor
    {
        [MyCmpReq]
        protected Operational operational;
        [MyCmpReq]
        private KSelectable selectable;
        private Storage TritiumStorage;
        private Storage nuclearWasteStorage;
        private BuildingHP hp;
        public static readonly SimHashes TritiumID = (SimHashes)Hash.SDBMLower(ELEMENTS.ID);
        private static readonly Tag TritiumTag = TagManager.Create(ELEMENTS.ID);

        private Vector3 worldPos;
        private int cell;

        private bool HasFuel = false;
        private float wasteReleaseTimer = 0f;
        private float currentSelfHeatKW = 0f;
        private float currentTritiumKgPerSecond = 0f;
        private static ushort nuclearWasteIdx;
        private HandleVector<int>.Handle structureTemperature;
        private StatusItem heatStatusItem;
        private StatusItem lowTritiumTemperatureStatusItem;
        private Guid lowTritiumTemperatureStatusHandle = Guid.Empty;
        private bool hasLowTemperatureTritiumWarning;
        private float lowTemperatureTritiumSample = LOW_TRITIUM_WARNING_TEMPERATURE_KELVIN;
        [MyCmpGet]
        private KBatchedAnimController animController;
        [MyCmpGet]
        private RadiationEmitter radEmitter;
        [MyCmpGet]
        private KBatchedAnimHeatPostProcessingEffect heatEffect;

        public const float BASE_SELF_HEAT_DTU_PER_SECOND = 100000000000f;
        public const float MAX_TRITIUM_KG_PER_SECOND = 1f;
        public const float MIN_TRITIUM_KG_PER_SECOND = 0.1f;
        public const float LOW_TRITIUM_WARNING_TEMPERATURE_CELSIUS = 500f;
        public const float WASTE_RELEASE_INTERVAL = 4f;
        public const float NUCLEAR_WASTE_OUTPUT_RATIO = 0.9f;
        public const float NUCLEAR_WASTE_TEMPERATURE = 9900f;
        private const float DTU_PER_KW = 1000f;
        private const float KELVIN_OFFSET = 273.15f;
        private const float LOW_TRITIUM_WARNING_TEMPERATURE_KELVIN = LOW_TRITIUM_WARNING_TEMPERATURE_CELSIUS + KELVIN_OFFSET;
        private static readonly KAnimHashedString[] FuelStatusFillSymbols = new KAnimHashedString[10]
        {
            (KAnimHashedString)"bar_act_0",
            (KAnimHashedString)"bar_act_1",
            (KAnimHashedString)"bar_act_2",
            (KAnimHashedString)"bar_act_3",
            (KAnimHashedString)"bar_act_4",
            (KAnimHashedString)"bar_act_5",
            (KAnimHashedString)"bar_act_6",
            (KAnimHashedString)"bar_act_7",
            (KAnimHashedString)"bar_act_8",
            (KAnimHashedString)"bar_act_9"
        };
        protected override void OnSpawn()
        {
            base.OnSpawn();
            selectable = GetComponent<KSelectable>();
            animController = GetComponent<KBatchedAnimController>();
            radEmitter = GetComponent<RadiationEmitter>();
            worldPos = transform.GetPosition();
            cell = Grid.PosToCell(worldPos);

            hp = gameObject.GetComponent<BuildingHP>();
            CacheStorages();
            structureTemperature = GameComps.StructureTemperatures.GetHandle(gameObject);
            nuclearWasteIdx = ElementLoader.FindElementByHash(SimHashes.NuclearWaste).idx;
            SetFuelStatusMeter(0f);
            UpdateRadiationEmitter(false);
            SampleLowTritiumTemperature();
            UpdateLowTritiumTemperatureStatus();

            HasFuel = HasEnoughStoredTritium();
            this.smi.StartSM();
            smi.sm.isOp.Set(operational.IsOperational, smi);
            smi.sm.hasFuel.Set(HasFuel, smi);
            // 订阅通断电变化
            Subscribe((int)GameHashes.OperationalChanged, OnOperationalChanged);
        }
        private void OnOperationalChanged(object data)
        {
            if (smi != null)
            {
                smi.sm.isOp.Set(operational.IsOperational, smi);
                smi.sm.hasFuel.Set(HasFuel || HasEnoughStoredTritium(), smi);
            }
            if (!operational.IsOperational)
            {
                SetHasFuel(false);
                SetHeatOutput(0f, 0f);
            }
            UpdateLowTritiumTemperatureStatus();
        }
        protected override void OnPrefabInit()
        {
            base.OnPrefabInit();
            heatStatusItem = new StatusItem("OperatingEnergy", "BUILDING", "", StatusItem.IconType.Info, NotificationType.Neutral, false, OverlayModes.None.ID);
            heatStatusItem.resolveStringCallback = (str, data) =>
            {
                StatesInstance statesInstance = (StatesInstance)data;
                float dtu = statesInstance.master.currentSelfHeatKW * DTU_PER_KW;
                return string.Format(str, GameUtil.GetFormattedHeatEnergy(dtu));
            };
            heatStatusItem.resolveTooltipCallback = (str, data) =>
            {
                StatesInstance statesInstance = (StatesInstance)data;
                float dtu = statesInstance.master.currentSelfHeatKW * DTU_PER_KW;
                str = str.Replace("{0}", GameUtil.GetFormattedHeatEnergy(dtu, GameUtil.HeatEnergyFormatterUnit.DTU_S));
                string lineItem = string.Format((string)BUILDING.STATUSITEMS.OPERATINGENERGY.LINEITEM, (object)BUILDING.STATUSITEMS.OPERATINGENERGY.OPERATING, (object)GameUtil.GetFormattedHeatEnergy(dtu, GameUtil.HeatEnergyFormatterUnit.DTU_S));
                return str.Replace("{1}", lineItem);
            };
            lowTritiumTemperatureStatusItem = new StatusItem("FusionReactorTritiumTooCold", "BUILDING", "status_item_vent_disabled", StatusItem.IconType.Custom, NotificationType.BadMinor, false, OverlayModes.None.ID);
            lowTritiumTemperatureStatusItem.resolveTooltipCallback = (str, data) =>
            {
                FusionReactor fusionReactor = (FusionReactor)data;
                str = str.Replace("{0}", GameUtil.GetFormattedTemperature(LOW_TRITIUM_WARNING_TEMPERATURE_KELVIN));
                return str.Replace("{1}", GameUtil.GetFormattedTemperature(fusionReactor.lowTemperatureTritiumSample));
            };
        }

        private void CacheStorages()
        {
            ConduitConsumer conduitConsumer = GetComponent<ConduitConsumer>();
            if (conduitConsumer != null)
                TritiumStorage = conduitConsumer.storage;

            Storage[] storages = GetComponents<Storage>();
            if (TritiumStorage == null && storages.Length > 0)
                TritiumStorage = storages[0];

            nuclearWasteStorage = storages.FirstOrDefault(storage => storage != TritiumStorage && storage.storageFilters != null && storage.storageFilters.Contains(SimHashes.NuclearWaste.CreateTag()));
            if (nuclearWasteStorage == null)
                nuclearWasteStorage = storages.FirstOrDefault(storage => storage != TritiumStorage);
        }

        // private void InitializeFuelStatusMeter()
        // {
        //     KBatchedAnimController animController = GetComponent<KBatchedAnimController>();
        //     if (animController == null)
        //         return;
        //
        //     fuelStatusMeter = new MeterController((KAnimControllerBase)animController, FUEL_STATUS_TARGET_SYMBOL, FUEL_STATUS_ANIMATION, Meter.Offset.NoChange, Grid.SceneLayer.NoLayer, "bar_inact");
        //     fuelStatusMeter.interpolateFunction = MeterController.StandardLerp;
        //     fuelStatusMeter.meterController.Play((HashedString)FUEL_STATUS_ANIMATION, KAnim.PlayMode.Paused);
        // }

        public void Sim1000ms(float dt)
        {
            // LogAnimationState("Sim1000ms");
            SampleLowTritiumTemperature();
            if (!operational.IsOperational || TritiumStorage == null || nuclearWasteStorage == null)
            {
                SetHasFuel(false);
                SetHeatOutput(0f, 0f);
                UpdateLowTritiumTemperatureStatus();
                return;
            }

            float consumedKg = ConsumeTritium();
            if (consumedKg < MIN_TRITIUM_KG_PER_SECOND)
            {
                SetHasFuel(false);
                SetHeatOutput(0f, 0f);
            }
            else
            {
                float ratio = Mathf.Clamp01(consumedKg / MAX_TRITIUM_KG_PER_SECOND);
                float heatKW = BASE_SELF_HEAT_DTU_PER_SECOND * ratio / DTU_PER_KW;
                SetHeatOutput(heatKW, consumedKg);
                if (structureTemperature.IsValid())
                    GameComps.StructureTemperatures.ProduceEnergy(structureTemperature, heatKW * dt, (string)BUILDING.STATUSITEMS.OPERATINGENERGY.OPERATING, dt);
                StoreNuclearWaste(consumedKg * NUCLEAR_WASTE_OUTPUT_RATIO);
                SetHasFuel(true);
            }

            wasteReleaseTimer += dt;
            if (wasteReleaseTimer >= WASTE_RELEASE_INTERVAL)
            {
                ReleaseStoredNuclearWaste();
                wasteReleaseTimer = 0f;
            }
            UpdateLowTritiumTemperatureStatus();
        }

        private float ConsumeTritium()
        {
            DamageForWrongFuel();
            float availableKg = TritiumStorage.GetMassAvailable(TritiumID);
            if (availableKg < MIN_TRITIUM_KG_PER_SECOND)
                return 0f;

            float consumedKg = Mathf.Min(availableKg, MAX_TRITIUM_KG_PER_SECOND);
            TritiumStorage.ConsumeIgnoringDisease(TritiumTag, consumedKg);
            return consumedKg;
        }

        private bool HasEnoughStoredTritium()
        {
            return TritiumStorage != null && TritiumStorage.GetMassAvailable(TritiumID) >= MIN_TRITIUM_KG_PER_SECOND;
        }

        private bool TryGetColdestStoredTritiumTemperature(out float temperature)
        {
            temperature = float.MaxValue;
            if (TritiumStorage == null)
                return false;

            bool foundTritium = false;
            for (int i = 0; i < TritiumStorage.items.Count; i++)
            {
                GameObject item = TritiumStorage.items[i];
                if (item == null)
                    continue;

                PrimaryElement primaryElement = item.GetComponent<PrimaryElement>();
                if (primaryElement == null || primaryElement.ElementID != TritiumID || primaryElement.Mass <= 0f)
                    continue;

                foundTritium = true;
                if (primaryElement.Temperature < temperature)
                    temperature = primaryElement.Temperature;
            }

            if (!foundTritium)
                temperature = 0f;

            return foundTritium;
        }

        private void SampleLowTritiumTemperature()
        {
            float currentTemperature;
            hasLowTemperatureTritiumWarning = TryGetColdestStoredTritiumTemperature(out currentTemperature) && currentTemperature < LOW_TRITIUM_WARNING_TEMPERATURE_KELVIN;
            lowTemperatureTritiumSample = hasLowTemperatureTritiumWarning ? currentTemperature : LOW_TRITIUM_WARNING_TEMPERATURE_KELVIN;
        }

        private bool ShouldPlayOnAnimation(string source)
        {
            bool shouldPlayOn = operational != null && operational.IsOperational && (HasFuel || HasEnoughStoredTritium());
            // LogAnimationState(source, shouldPlayOn);
            return shouldPlayOn;
        }

        private void LogAnimationState(string source, bool? shouldPlayOn = null)
        {
            // EnergyConsumer energyConsumer = GetComponent<EnergyConsumer>();
            // float tritiumKg = TritiumStorage != null ? TritiumStorage.GetMassAvailable(TritiumID) : -1f;
            // bool hasPower = energyConsumer != null && energyConsumer.IsPowered;
            // bool logicGreen = operational != null && operational.GetFlag(LogicOperationalController.LogicOperationalFlag);
            // bool isOperational = operational != null && operational.IsOperational;
            // bool hasFuel = HasEnoughStoredTritium();
            // bool readyForOn = shouldPlayOn ?? (isOperational && hasFuel);
            // Debug.Log($"[mike6010 DLC][FusionReactor] {source}: IsOperational={isOperational}, HasPower={hasPower}, LogicGreen={logicGreen}, TritiumStorage={(TritiumStorage != null ? "yes" : "no")}, NuclearWasteStorage={(nuclearWasteStorage != null ? "yes" : "no")}, TritiumKg={tritiumKg}, HasFuel={hasFuel}, ReadyForOn={readyForOn}");
        }

        private void DamageForWrongFuel()
        {
            for (int i = TritiumStorage.items.Count - 1; i >= 0; i--)
            {
                GameObject item = TritiumStorage.items[i];
                if (item == null)
                    continue;

                PrimaryElement element = item.GetComponent<PrimaryElement>();
                if (element == null || element.ElementID == TritiumID)
                    continue;

                if (hp != null)
                    hp.DoDamage(1);
                TritiumStorage.Remove(item);
            }
        }

        private void StoreNuclearWaste(float mass)
        {
            if (mass <= 0f)
                return;
            nuclearWasteStorage.AddLiquid(SimHashes.NuclearWaste, mass, NUCLEAR_WASTE_TEMPERATURE, byte.MaxValue, 0, false, false);
        }

        private void ReleaseStoredNuclearWaste()
        {
            float wasteKg = nuclearWasteStorage.GetMassAvailable(SimHashes.NuclearWaste);
            if (wasteKg <= 0f)
                return;

            FallingWater.instance.AddParticle(cell, nuclearWasteIdx, wasteKg, NUCLEAR_WASTE_TEMPERATURE, byte.MaxValue, 0);
            nuclearWasteStorage.ConsumeIgnoringDisease(SimHashes.NuclearWaste.CreateTag(), wasteKg);
        }

        private void SetHasFuel(bool hasFuel)
        {
            if (HasFuel == hasFuel)
                return;

            HasFuel = hasFuel;
            if (smi != null)
                smi.sm.hasFuel.Set(HasFuel, smi);
        }

        private void SetHeatOutput(float heatKW, float tritiumKgPerSecond)
        {
            currentSelfHeatKW = heatKW;
            currentTritiumKgPerSecond = tritiumKgPerSecond;
            if (heatEffect != null)
                heatEffect.SetHeatBeingProducedValue(heatKW);
            SetFuelStatusMeter(tritiumKgPerSecond);
            UpdateRadiationEmitter(operational != null && operational.IsOperational && tritiumKgPerSecond >= MIN_TRITIUM_KG_PER_SECOND);
        }

        private void UpdateRadiationEmitter(bool emitting)
        {
            if (radEmitter == null)
                return;

            radEmitter.SetEmitting(emitting);
            radEmitter.emitRads = emitting ? FusionReactorConfig.RADIATION_EMITTER_RADS : 0f;
            radEmitter.Refresh();
        }

        private void UpdateLowTritiumTemperatureStatus()
        {
            if (selectable == null)
                return;

            bool shouldShow = operational != null
                && operational.IsOperational
                && hasLowTemperatureTritiumWarning;
            if (shouldShow)
            {
                if (lowTritiumTemperatureStatusHandle == Guid.Empty)
                {
                    lowTritiumTemperatureStatusHandle = selectable.AddStatusItem(lowTritiumTemperatureStatusItem, this);
                }
            }
            else if (lowTritiumTemperatureStatusHandle != Guid.Empty)
            {
                selectable.RemoveStatusItem(lowTritiumTemperatureStatusHandle);
                lowTritiumTemperatureStatusHandle = Guid.Empty;
            }
        }

        private void SetFuelStatusMeter(float tritiumKgPerSecond)
        {
            if (animController == null)
                return;

            int visibleSymbolIndex = -1;
            if (tritiumKgPerSecond >= MIN_TRITIUM_KG_PER_SECOND)
            {
                int displayedUnits = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp(tritiumKgPerSecond, MIN_TRITIUM_KG_PER_SECOND, MAX_TRITIUM_KG_PER_SECOND) * 10f), 1, 10);
                visibleSymbolIndex = 10 - displayedUnits;
            }

            for (int i = 0; i < FuelStatusFillSymbols.Length; i++)
                animController.SetSymbolVisiblity(FuelStatusFillSymbols[i], i == visibleSymbolIndex);
        }

        public List<Descriptor> GetDescriptors(GameObject go)
        {
            List<Descriptor> descriptors = new List<Descriptor>();
            string formattedHeatEnergy = GameUtil.GetFormattedHeatEnergy(BASE_SELF_HEAT_DTU_PER_SECOND);
            Descriptor descriptor = new Descriptor();
            descriptor.SetupDescriptor(string.Format((string)UI.BUILDINGEFFECTS.HEATGENERATED, (object)formattedHeatEnergy), string.Format((string)UI.BUILDINGEFFECTS.TOOLTIPS.HEATGENERATED, (object)formattedHeatEnergy));
            descriptors.Add(descriptor);
            return descriptors;
        }
        public class StatesInstance : GameStateMachine<FusionReactor.States, FusionReactor.StatesInstance, FusionReactor, object>.GameInstance
        {
            public StatesInstance(FusionReactor master) : base(master) { }
            public void SetActive(bool on) => master.operational.SetActive(on, false);
        }
        public class States : GameStateMachine<FusionReactor.States, FusionReactor.StatesInstance, FusionReactor>
        {
            public State off;
            public State on;

            public BoolParameter isOp;
            public BoolParameter hasFuel;
            public override void InitializeStates(out BaseState default_state)
            {
                default_state = off;

                off
                    .Enter(smi =>
                    {
                        smi.SetActive(false);
                        // smi.master.LogAnimationState("Enter off");
                    })
                    .PlayAnim("off", KAnim.PlayMode.Loop)
                    .Transition(on, smi => smi.master.ShouldPlayOnAnimation("Transition off -> on"), UpdateRate.SIM_1000ms);

                on
                    .Enter(smi =>
                    {
                        smi.SetActive(true);
                        // smi.master.LogAnimationState("Enter on");
                    })
                    .PlayAnim("on", KAnim.PlayMode.Loop)
                    .ToggleStatusItem(smi => smi.master.heatStatusItem, smi => (object)smi)
                    .Transition(off, smi => !smi.master.ShouldPlayOnAnimation("Transition on -> off"), UpdateRate.SIM_1000ms);
            }
        }
    }
    public class NuclearBombModuleConfig : IBuildingConfig
    {
        public override string[] GetRequiredDlcIds() => DlcManager.EXPANSION1;
        public override BuildingDef CreateBuildingDef()
        {
            float[] construction_mass = new float[1] { 200f };
            EffectorValues tieR2 = NOISE_POLLUTION.NOISY.TIER2;
            EffectorValues none = TUNING.BUILDINGS.DECOR.NONE;
            EffectorValues noise = tieR2;
            BuildingDef buildingDef = BuildingTemplates.CreateBuildingDef("NuclearBomb", 5, 5, "NuclearBomb_kanim", 1000, 120f, construction_mass, TUNING.MATERIALS.REFINED_METALS, 9999f, BuildLocationRule.BuildingAttachPoint, none, noise);
            BuildingTemplates.CreateRocketBuildingDef(buildingDef);
            buildingDef.SceneLayer = Grid.SceneLayer.Building;
            buildingDef.OverheatTemperature = 2273.15f;
            buildingDef.Floodable = false;
            buildingDef.AttachmentSlotTag = GameTags.Rocket;
            buildingDef.ObjectLayer = ObjectLayer.Building;
            buildingDef.RequiresPowerInput = false;
            buildingDef.attachablePosition = new CellOffset(0, 0);
            buildingDef.CanMove = true;
            buildingDef.Cancellable = false;
            return buildingDef;
        }
        public override void ConfigureBuildingTemplate(GameObject go, Tag prefab_tag)
        {
            BuildingConfigManager.Instance.IgnoreDefaultKComponent(typeof(RequiresFoundation), prefab_tag);
            go.AddOrGet<LoopingSounds>();
            go.GetComponent<KPrefabID>().AddTag(RoomConstraints.ConstraintTags.IndustrialMachinery);
            NuclearBomb.Def bomb_def = go.AddOrGetDef<NuclearBomb.Def>();
            go.AddOrGet<BuildingAttachPoint>().points = new BuildingAttachPoint.HardPoint[1]
            {
                new BuildingAttachPoint.HardPoint(new CellOffset(0, 5), GameTags.Rocket, (AttachableBuilding) null)
            };

            NuclearBombExplosiveCharge explosive_charge = go.AddOrGet<NuclearBombExplosiveCharge>();
            explosive_charge.Tritium = go.AddComponent<Storage>();
            explosive_charge.Tritium.capacityKg = bomb_def.ExplosiveChargeT;
            explosive_charge.Tritium.storageFilters = new List<Tag>() { TagManager.Create(ELEMENTS.ID) };
            explosive_charge.Tritium.showInUI = true;

            explosive_charge.Uranium = go.AddComponent<Storage>();
            explosive_charge.Uranium.capacityKg = bomb_def.ExplosiveChargeU;
            explosive_charge.Uranium.storageFilters = new List<Tag>() { SimHashes.EnrichedUranium.CreateTag() };
            explosive_charge.Uranium.showInUI = true;

            ManualDeliveryKG delivery_tritium = go.AddComponent<ManualDeliveryKG>();
            delivery_tritium.RequestedItemTag = TagManager.Create(ELEMENTS.ID);             
            delivery_tritium.capacity = bomb_def.ExplosiveChargeT;                   
            delivery_tritium.refillMass = bomb_def.ExplosiveChargeT;               
            delivery_tritium.choreTypeIDHash = Db.Get().ChoreTypes.BuildFetch.IdHash; 
            delivery_tritium.allowPause = true;

            ManualDeliveryKG delivery_uranium = go.AddComponent<ManualDeliveryKG>();
            delivery_uranium.RequestedItemTag = SimHashes.EnrichedUranium.CreateTag();           
            delivery_uranium.capacity = bomb_def.ExplosiveChargeU;                    
            delivery_uranium.refillMass = bomb_def.ExplosiveChargeU;               
            delivery_uranium.choreTypeIDHash = Db.Get().ChoreTypes.BuildFetch.IdHash; 
            delivery_uranium.allowPause = true;

            explosive_charge.delivery_tritium = delivery_tritium;
            explosive_charge.delivery_uranium = delivery_uranium;
        }
        public override void DoPostConfigureComplete(GameObject go) 
        {
            BuildingTemplates.ExtendBuildingToRocketModuleCluster(go, (string)null, TUNING.ROCKETRY.BURDEN.MINOR_PLUS);
            SelectModuleSideScreen.moduleButtonSortOrder.Add("NuclearBomb");
        }
    }
    public class NuclearBombExplosiveCharge : KMonoBehaviour
    {
        public Storage Tritium;
        public Storage Uranium;
        public ManualDeliveryKG delivery_tritium;
        public ManualDeliveryKG delivery_uranium;

        [MyCmpGet] private KBatchedAnimController anim_controller;
        private NuclearBomb.Def bomb_def;
        protected override void OnSpawn()
        {
            base.OnSpawn();
            if (Tritium == null || Uranium == null)
            {
                Storage[] storages = GetComponents<Storage>();
                Uranium = storages.FirstOrDefault(s => s.storageFilters.Contains(SimHashes.EnrichedUranium.CreateTag()));
                Tritium = storages.FirstOrDefault(s => s.storageFilters.Contains(TagManager.Create(ELEMENTS.ID)));
            }
            else
            {
                delivery_tritium.SetStorage(Tritium);                
                delivery_uranium.SetStorage(Uranium);                
            }

            if (Tritium != null)
            {
                Tritium.Subscribe((int)GameHashes.OnStore, OnAnyStorageChanged);
                Tritium.Subscribe((int)GameHashes.OnStorageChange, OnAnyStorageChanged);
            }
            if (Uranium != null)
            {
                Uranium.Subscribe((int)GameHashes.OnStore, OnAnyStorageChanged);
                Uranium.Subscribe((int)GameHashes.OnStorageChange, OnAnyStorageChanged);
            }

            if (anim_controller == null)
            {
                anim_controller = GetComponentInParent<KBatchedAnimController>();
            }
            anim_controller.SetSymbolVisiblity((KAnimHashedString)"light_green0", false);
            anim_controller.SetSymbolVisiblity((KAnimHashedString)"light_green1", false);

            bomb_def = this.GetDef<NuclearBomb.Def>();
        }
        private void OnAnyStorageChanged(object _)
        {
            if (Uranium.MassStored() >= bomb_def.ExplosiveChargeU)
            {
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"light_green0", true);
            }
            else
            {
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"light_green0", false);
            }

            if (Tritium.MassStored() >= bomb_def.ExplosiveChargeT)
            {
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"light_green1", true);
            }
            else
            {
                anim_controller.SetSymbolVisiblity((KAnimHashedString)"light_green1", false);
            }
        }
        protected override void OnCleanUp()
        {
            if (Tritium != null)
            {
                Tritium.Unsubscribe((int)GameHashes.OnStore, OnAnyStorageChanged);
            }
            if (Uranium != null)
            {
                Uranium.Unsubscribe((int)GameHashes.OnStore, OnAnyStorageChanged);
            }
            base.OnCleanUp();
        }
    }
    public class ClusterMapNuclearBombGridEntity : ClusterGridEntity
    {
        public float ClearProgress = 0f;
        public override string Name => "Cluster Map Nuclear Bomb";
        public override EntityLayer Layer => EntityLayer.FX;
        public override bool IsVisible => true;
        public override ClusterRevealLevel IsVisibleInFOW => ClusterRevealLevel.Visible;
        public override bool ShowName() => false;
        public override bool ShowProgressBar() => true;
        public override float GetProgress() => ClearProgress;

        private List<AnimConfig> _animConfigs;
        public override List<AnimConfig> AnimConfigs
        {
            get
            {
                if (_animConfigs != null) return _animConfigs;

                _animConfigs = new List<AnimConfig>(1);
                var file = Assets.GetAnim((HashedString)"NuclearBomb_kanim");
                if (file != null)
                {
                    _animConfigs.Add(new AnimConfig
                    {
                        animFile = file,
                        initialAnim = "explode",
                        playMode = KAnim.PlayMode.Loop,
                        // 以下均为可选项
                        symbolSwapTarget = null,
                        symbolSwapSymbol = null,
                        animOffset = Vector3.zero,
                        animPlaySpeedModifier = 1f
                    });
                }
                else
                {
                    Debug.LogWarning($"[ProbeGridEntity] Missing anim file: ");
                }

                return _animConfigs;
            }
        }
        public override void OnClusterMapIconShown(ClusterRevealLevel levelUsed)
        {
            base.OnClusterMapIconShown(levelUsed);

            if (levelUsed == ClusterRevealLevel.Visible)
            {
                ClusterMapVisualizer vis = ClusterMapScreen.Instance.GetEntityVisAnim(this);
                try
                {
                    vis.PlayAnim("explode", KAnim.PlayMode.Once);
                }
                catch
                {
                    // 忽略：当可视化尚未创建或被隐藏时，Play 可能为空
                }
            }
        }
    }
    public class ClusterMapNuclearBombConfig : IEntityConfig/*, IHasDlcRestrictions*/
    {
        public const string ID = "ClusterMapNuclearBomb";
        public const float MASS = 2000f;
        public const float STARMAP_SPEED = 10f;
        public string[] GetRequiredDlcIds() => DlcManager.EXPANSION1;
        //public string[] GetForbiddenDlcIds() => (string[])null;
        public string[] GetDlcIds() => DlcManager.EXPANSION1;
        public GameObject CreatePrefab()
        {
            GameObject basicEntity = EntityTemplates.CreateBasicEntity("ClusterMapNuclearBomb", (string)"CLUSTERMAPNUCLEARBOMB", (string)"Nuclear Bomb", 2000f, true, Assets.GetAnim((HashedString)"NuclearBomb_kanim"), "object", Grid.SceneLayer.Front, additionalTags: new List<Tag>()
            {
                GameTags.IgnoreMaterialCategory,
                GameTags.Experimental
            });
            basicEntity.AddOrGet<ClusterMapNuclearBombGridEntity>();

            return basicEntity;
        }
        public void OnPrefabInit(GameObject inst)
        {
        }
        public void OnSpawn(GameObject inst)
        {
        }
    }
    public class NuclearBomb : GameStateMachine<NuclearBomb, NuclearBomb.StatesInstance, IStateMachineTarget, NuclearBomb.Def>
    {
        [SerializationConfig(MemberSerialization.OptIn)]
        public struct DeletedWorldItem
        {
            [Serialize] public bool IsUserDeleted;
            [Serialize] public Vector2I WorldSize;
            [Serialize] public Vector2I WorldOffset;
            [Serialize] public int DeletedWorldID;
        };

        [Serialize]
        public class Def : StateMachine.BaseDef
        {
            [Serialize] public float ExplosiveChargeT = 800;
            [Serialize] public float ExplosiveChargeU = 10;
        }

        public NuclearBomb.Universal universal;
        public override void InitializeStates(out StateMachine.BaseState default_state)
        {
            default_state = (StateMachine.BaseState)this.universal;
        }
        public class Universal : GameStateMachine<NuclearBomb, NuclearBomb.StatesInstance, IStateMachineTarget, NuclearBomb.Def>.State
        {
            public GameStateMachine<NuclearBomb, NuclearBomb.StatesInstance, IStateMachineTarget, NuclearBomb.Def>.State loading;
            public GameStateMachine<NuclearBomb, NuclearBomb.StatesInstance, IStateMachineTarget, NuclearBomb.Def>.State loaded;
            public GameStateMachine<NuclearBomb, NuclearBomb.StatesInstance, IStateMachineTarget, NuclearBomb.Def>.State empty;
        }
        public class StatesInstance : GameStateMachine<NuclearBomb, NuclearBomb.StatesInstance, IStateMachineTarget, NuclearBomb.Def>.GameInstance, IEmptyableCargo
        {
            private bool autoDeploy;
            ClusterGridEntity asteroid;
            WorldContainer world;
            public StatesInstance(IStateMachineTarget master, NuclearBomb.Def def) : base(master, def)
            {

            }
            public bool CanTargetClusterGridEntities => false;
            public string GetButtonText => UI.UISIDESCREENS.MODULEFLIGHTUTILITYSIDESCREEN.DEPLOY_BUTTON;
            public string GetButtonToolip => UI.UISIDESCREENS.MODULEFLIGHTUTILITYSIDESCREEN.DEPLOY_BUTTON_TOOLTIP;
            public bool IsValidDropLocation()
            {
                ClusterGridEntity asteroid_entry = this.GetComponent<RocketModuleCluster>().CraftInterface.GetComponent<Clustercraft>().GetOrbitAsteroid();
                if (asteroid_entry != null)
                {
                    WorldContainer m_world = asteroid_entry.GetComponent<WorldContainer>();
                    if (m_world != null)
                    {
                        if (m_world.id != 0)
                        {
                            return true;
                        }
                    }
                }
                return false;
            }
            public bool IsEnoughExplosives()
            {
                return master.GetComponent<NuclearBombExplosiveCharge>().Tritium.MassStored() >= base.def.ExplosiveChargeT && master.GetComponent<NuclearBombExplosiveCharge>().Uranium.MassStored() >= base.def.ExplosiveChargeU;
            }
            public bool AutoDeploy
            {
                get => false;
                set => autoDeploy = false;
            }
            public bool CanAutoDeploy => false;
            public void WriteLog(DeletedWorldItem info, string WorldName)
            {
                try
                {
                    string logPath = System.IO.Path.Combine(DLC_Entry.modPath, "DeletedWorld.log");

                    System.Text.StringBuilder sb = new System.Text.StringBuilder();
                    {
                        string line =
                            $"{System.DateTime.Now:yyyy-MM-dd}、{System.DateTime.Now:HH:mm:ss}、" +
                            $"IsUserDeleted={info.IsUserDeleted},WorldSize={info.WorldSize.x}x{info.WorldSize.y}," +
                            $"WorldOffset={info.WorldOffset.x},{info.WorldOffset.y}" + 
                            $"WorldName={WorldName}";
                        sb.AppendLine(line);
                    }

                    // 以追加形式写入；文件不存在则会被创建
                    System.IO.File.AppendAllText(logPath, sb.ToString());
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[NuclearBomb] Append DeletedWorld log failed: {e}");
                }
            }
            public void UnRegAsteroid()
            {
                ClusterManager.Instance.UnregisterWorldContainer(world);
            }
            private void FixClusterTravelers(int world_id, AxialI world_loc)
            {
                foreach (ClusterTraveler clusterTraveler in Components.ClusterTravelers.Items)
                {
                    if (clusterTraveler == null || clusterTraveler.IsNullOrDestroyed())
                        continue;

                    ClusterDestinationSelector selector = clusterTraveler.GetComponentInParent<ClusterDestinationSelector>();
                    if (selector.GetDestinationWorld() == world.id)
                    {
                        selector.SetDestination(world_loc);
                        clusterTraveler.RevalidatePath(false);
                    }
                }
            }
            private void ClearWorld(WorldContainer world, Action<float> report)
            {
                ConduitFlow_DO_NOT_Dump.do_not_dump = true;
                HashSetPool<GameObject, SandboxDestroyerTool>.PooledHashSet pooledHashSet = HashSetPool<GameObject, SandboxDestroyerTool>.Allocate();

                foreach (Crop cmp in Components.Crops.Items)
                {
                    if (Grid.WorldIdx[Grid.PosToCell((KMonoBehaviour)cmp)] == world.id)
                        pooledHashSet.Add(cmp.gameObject);
                }
                foreach (Health cmp in Components.Health.Items)
                {
                    if (Grid.WorldIdx[Grid.PosToCell((KMonoBehaviour)cmp)] == world.id)
                        pooledHashSet.Add(cmp.gameObject);
                }
                foreach (Comet cmp in Components.Meteors.GetItems(world.id))
                {
                    if (!cmp.IsNullOrDestroyed())
                        pooledHashSet.Add(cmp.gameObject);
                }
                foreach (GameObject original in (HashSet<GameObject>)pooledHashSet)
                    Util.KDestroyGameObject(original);
                pooledHashSet.Recycle();

                for (int cell = 0; cell < Grid.CellCount; cell++)
                {
                    report.Invoke((cell + 1f) / Grid.CellCount);

                    if (Grid.WorldIdx[cell] != world.id) continue;

                    foreach (ObjectLayer layer in Enum.GetValues(typeof(ObjectLayer)))
                    {
                        GameObject go = null;
                        try { go = Grid.Objects[cell, (int)layer]; }
                        catch { /* 某些层可能越界/无效，忽略 */ }

                        if (go == null) continue;

                        if (layer == ObjectLayer.Pickupables)
                        {
                            ObjectLayerListItem objectLayerListItem = go.GetComponent<Pickupable>().objectLayerListItem;
                            if (objectLayerListItem != null)
                            {
                                while (objectLayerListItem != null)
                                {
                                    GameObject gameObject2 = objectLayerListItem.gameObject;
                                    objectLayerListItem = objectLayerListItem.nextItem;

                                    Util.KDestroyGameObject(gameObject2);
                                }
                            }
                        }

                        Storage storage_object = go.GetComponent<Storage>();
                        if (storage_object != null)
                        {
                            foreach (var tritium_array in storage_object.items.ToArray())
                            {
                                if (tritium_array == null)
                                {
                                    storage_object.items.Remove(tritium_array);
                                    continue;
                                }
                                storage_object.items.Remove(tritium_array);
                                Util.KDestroyGameObject(tritium_array);
                            }
                        }
                        Storage storage_object2 = go.GetComponentInChildren<Storage>();
                        if (storage_object2 != null)
                        {
                            foreach (var tritium_array in storage_object2.items.ToArray())
                            {
                                if (tritium_array == null)
                                {
                                    storage_object2.items.Remove(tritium_array);
                                    continue;
                                }
                                storage_object2.items.Remove(tritium_array);
                                Util.KDestroyGameObject(tritium_array);
                            }
                        }

                        try
                        {
                            Util.KDestroyGameObject(go);
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[Purge] Destroy failed: {go.name} @cell {cell} | {e}");
                        }
                    }
                }
                ConduitFlow_DO_NOT_Dump.do_not_dump = false;
            }
            public void DeployBomb() 
            {
                asteroid = GetComponent<RocketModuleCluster>().CraftInterface.GetComponent<Clustercraft>().GetOrbitAsteroid();
                world = asteroid.GetComponent<WorldContainer>();
                AxialI world_loc = asteroid.Location;

                var prefab = Assets.GetPrefab(new Tag("ClusterMapNuclearBomb"));
                if (prefab == null)
                {
                    Debug.LogWarning("[StarMapAnimDemo] Prefab not found. Did EntityConfig load?");
                    return;
                }
                GameObject go = Util.KInstantiate(prefab);
                go.SetActive(true);
                ClusterMapNuclearBombGridEntity clu_bomb_entity = go.GetComponent<ClusterMapNuclearBombGridEntity>();
                clu_bomb_entity.Location = asteroid.Location;

                Debug.Log("Nuclear Bomb Deployed!");

                int capture_speed = SpeedControlScreen.Instance.GetSpeed();
                SpeedControlScreen.Instance.Pause();
                SpeedControlScreen_Force_Pause.force_pause = true;

                ClusterManager.Instance.SetActiveWorld(0);
                DeletedWorldItem DeleteInfo = new DeletedWorldItem()
                {
                    IsUserDeleted = true,
                    WorldOffset = world.WorldOffset,
                    WorldSize = world.WorldSize,
                    DeletedWorldID = world.id
                };
                StaticSave.Instance.DeletedWorld.Add(DeleteInfo);
                WriteLog(DeleteInfo, world.name);
                UnRegAsteroid();

                ClearWorld(world, p => { clu_bomb_entity.ClearProgress = p; });
                FixClusterTravelers(world.id, ClusterManager.Instance.GetWorld(0).GetMyWorldLocation());
                Grid.FreeGridSpace(world.WorldSize,world.WorldOffset);
                Util.KDestroyGameObject(world.gameObject);

                SpeedControlScreen_Force_Pause.force_pause = false;
                SpeedControlScreen.Instance.SetSpeed(capture_speed);
                SpeedControlScreen.Instance.Unpause();

                NuclearBombExplosiveCharge stored = master.GetComponent<NuclearBombExplosiveCharge>();
                foreach (var tritium_array in stored.Tritium.items.ToArray())
                {
                    if (tritium_array == null) 
                    { 
                        stored.Tritium.items.Remove(tritium_array); 
                        continue; 
                    }
                    stored.Tritium.items.Remove(tritium_array);
                    Util.KDestroyGameObject(tritium_array);
                }
                foreach (var uranium_array in stored.Uranium.items.ToArray())
                {
                    if (uranium_array == null)
                    {
                        stored.Uranium.items.Remove(uranium_array);
                        continue;
                    }
                    stored.Uranium.items.Remove(uranium_array);
                    Util.KDestroyGameObject(uranium_array);
                }
                stored.Tritium.Trigger((int)GameHashes.OnStore, null);
                stored.Uranium.Trigger((int)GameHashes.OnStore, null);
            }
            public void EmptyCargo() => this.DeployBomb();
            public bool CanEmptyCargo() => this.IsValidDropLocation() && IsEnoughExplosives();
            public bool ChooseDuplicant => false;
            public MinionIdentity ChosenDuplicant
            {
                get => (MinionIdentity)null;
                set
                {
                }
            }
            public bool ModuleDeployed => false;
        }
    }

    [HarmonyPatch(typeof(SpeedControlScreen), "Unpause")]
    public class SpeedControlScreen_Force_Pause
    {
        public static bool force_pause = false;
        public static bool Prefix()
        {
            if (force_pause == true) 
                return false;
            return true;
        }
    }

    [HarmonyPatch(typeof(ConduitFlow), "DumpPipeContents")]
    public class ConduitFlow_DO_NOT_Dump
    {
        public static bool do_not_dump = false;
        public static bool Prefix()
        {
            if (do_not_dump == true)
                return false;

            return true;
        }
    }

    [HarmonyPatch(typeof(ClusterManager), "GetWorld")]
    public class ClusterManager_GetWorld
    {
        public static bool Prefix(ref int id, ClusterManager __instance, ref WorldContainer __result)
        {
            foreach(var deleted_world in StaticSave.Instance.DeletedWorld)
            {
                if(id == deleted_world.DeletedWorldID)
                {
                    __result = __instance.GetWorld(0);
                    return false;
                }
            }
            return true;
        }
    }
}

