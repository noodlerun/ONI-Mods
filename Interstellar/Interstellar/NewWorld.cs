using Delaunay.Geo;
using HarmonyLib;
using KSerialization;
using Klei.AI;
using Klei.CustomSettings;
using ProcGen;
using STRINGS;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
//using UnityEngine.Windows;

namespace Interstellar
{
    [HarmonyPatch(typeof(FrontEndManager), "OnPrefabInit")]
    public static class FrontEndManager_OnPrefabInit_Patch
    {
        public static GameObject MyCanvas;
        public static void Postfix(FrontEndManager __instance)
        {
            MyCanvas = __instance.MakeKleiCanvas();
        }
    }
    public class Mod
    {
        public static bool Switch = false;
        public static bool IsOldReveal = false;
        public static int WaitFrames = 0;

        public static bool IsNewWorld = false;
        public static bool IsClearWorlds = false;
        public static bool IsReveal = false;
        public static bool IsOldWorld = false;
        public static bool IsUndump = false;
        public static bool IsReload = false;
        //public static float DefaultFontSize = 0.0f;
        //public static bool IsQuitOldWorld = false;
        public static bool IsLoadOldWorld = false;

        public static int counter = 0;
        private static int WorldGenRetryCount = 0;
        private const int MaxWorldGenRetryCount = 5;
        private static readonly System.Random WorldGenRetryRng = new System.Random();
        private static readonly object WorldGenRetryLock = new object();
        private static bool WorldGenRetryPending = false;
        private static string WorldGenLastErrorDesc = "";
        private static float DefaultOverlayFontSize = 0;
        private static bool FowPaintLog = false;
        private static bool IsClearingWorldsForNewWorld()
        {
            return Switch && IsOldReveal && !IsReload && !IsNewWorld && !IsOldWorld;
        }

        private static bool IsNewWorldTransitionActive()
        {
            return Switch && IsOldReveal && IsLoadOldWorld;
        }

        private static bool IsDebugHotKeyEnabled()
        {
            return InterstellarModConsole.Instance != null && InterstellarModConsole.Instance.OptionsDebugMode;
        }
        private static void ResetWorldGenRetryState()
        {
            lock (WorldGenRetryLock)
            {
                WorldGenRetryCount = 0;
                WorldGenRetryPending = false;
                WorldGenLastErrorDesc = "";
            }
        }

        public static string filename;
        private static readonly List<StarmapWorldSnapshot> CapturedStarmapWorlds = new List<StarmapWorldSnapshot>();
        private static readonly Dictionary<int, string> DumpedFilesByWorldId = new Dictionary<int, string>();
        private static readonly Dictionary<int, DumpFileData> RestoredDumpDataByWorldId = new Dictionary<int, DumpFileData>();
        private static readonly Dictionary<int, string> RestoreDebugLogPathByWorldId = new Dictionary<int, string>();
        private static int PreservedWorldIdDuringClear = -1;
        private const string DelayedRestoredGeyserMarker = "InterstellarDelayedRestoredGeyser";
        private static readonly List<TemplateClasses.Prefab> PendingDelayedSpawnPrefabs = new List<TemplateClasses.Prefab>();
        private static List<int> MainWorldRocketWorldId = new List<int>();
        private static List<string> DumpedFiles = new List<string>();
        private static readonly List<CachedTemporalTearEntry> CachedTemporalTears = new List<CachedTemporalTearEntry>();
        private const int AsteroidStarmapMinDistance = 2;
        private const int TemporalTearStarmapMinDistance = 7;

        private class CachedTemporalTearEntry
        {
            public TemporalTear Tear;
            public AxialI Location;
        }

        [SerializationConfig(MemberSerialization.OptIn)]
        public class CachedTemporalTearMarker : KMonoBehaviour
        {
            [Serialize]
            public AxialI Location;

            public void Init(TemporalTear tear, AxialI location)
            {
                Location = location;
                RegisterTemporalTearCache(tear, location);
            }

            protected override void OnSpawn()
            {
                base.OnSpawn();
                TemporalTear tear = GetComponent<TemporalTear>();
                if (tear != null)
                {
                    if (Location == AxialI.INVALID)
                        Location = tear.Location;
                    RegisterTemporalTearCache(tear, Location);
                }
            }

            protected override void OnCleanUp()
            {
                TemporalTear tear = GetComponent<TemporalTear>();
                if (tear != null)
                    UnregisterTemporalTearCache(tear);
                base.OnCleanUp();
            }
        }

        private static void RegisterTemporalTearCache(TemporalTear tear)
        {
            if (tear != null)
                RegisterTemporalTearCache(tear, tear.Location);
        }

        private static void RegisterTemporalTearCache(TemporalTear tear, AxialI location)
        {
            if (tear == null || location == AxialI.INVALID)
                return;

            CleanupTemporalTearCache();
            for (int i = 0; i < CachedTemporalTears.Count; i++)
            {
                CachedTemporalTearEntry entry = CachedTemporalTears[i];
                if (entry.Tear == tear)
                {
                    entry.Location = location;
                    return;
                }
            }

            CachedTemporalTears.Add(new CachedTemporalTearEntry
            {
                Tear = tear,
                Location = location
            });
        }

        private static void UnregisterTemporalTearCache(TemporalTear tear)
        {
            CachedTemporalTears.RemoveAll(entry => entry == null || entry.Tear == null || entry.Tear == tear);
        }

        private static void CleanupTemporalTearCache()
        {
            CachedTemporalTears.RemoveAll(entry => entry == null || entry.Tear == null || entry.Location == AxialI.INVALID);
        }

        private static void RebuildTemporalTearCache()
        {
            CleanupTemporalTearCache();
            if (ClusterGrid.Instance == null || ClusterGrid.Instance.cellContents == null)
                return;

            foreach (KeyValuePair<AxialI, List<ClusterGridEntity>> kv in ClusterGrid.Instance.cellContents)
            {
                for (int i = 0; i < kv.Value.Count; i++)
                {
                    TemporalTear tear = kv.Value[i] != null ? kv.Value[i].GetComponent<TemporalTear>() : null;
                    if (tear != null)
                        RegisterTemporalTearCache(tear, tear.Location);
                }
            }
        }

        private static int GetTemporalTearDistance(AxialI a, AxialI b)
        {
            Vector3I cubeA = a.ToCube();
            Vector3I cubeB = b.ToCube();
            return Mathf.Max(Mathf.Abs(cubeA.x - cubeB.x), Mathf.Abs(cubeA.y - cubeB.y), Mathf.Abs(cubeA.z - cubeB.z));
        }

        private static bool TryGetClosestTemporalTear(AxialI origin, bool includeOpenTears, out TemporalTear closest)
        {
            closest = null;
            if (origin == AxialI.INVALID)
                return false;

            RebuildTemporalTearCache();
            int closestDistance = int.MaxValue;
            for (int i = 0; i < CachedTemporalTears.Count; i++)
            {
                CachedTemporalTearEntry entry = CachedTemporalTears[i];
                TemporalTear tear = entry != null ? entry.Tear : null;
                if (tear == null || (!includeOpenTears && tear.IsOpen()))
                    continue;

                int distance = GetTemporalTearDistance(origin, entry.Location);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closest = tear;
                }
            }

            return closest != null;
        }

        private static bool OpenClosestTemporalTear(GameObject opener, int openerWorldId, out TemporalTear openedTear)
        {
            openedTear = null;
            if (opener == null)
                return false;

            AxialI openerLocation = opener.GetMyWorldLocation();
            if (!TryGetClosestTemporalTear(openerLocation, false, out TemporalTear tear))
                return TryGetClosestTemporalTear(openerLocation, true, out tear) && tear != null && tear.IsOpen();

            ClusterFogOfWarManager.Instance fow = SaveGame.Instance != null ? SaveGame.Instance.GetSMI<ClusterFogOfWarManager.Instance>() : null;
            if (fow != null)
                fow.RevealLocation(tear.Location, 1);

            tear.Open();
            openedTear = tear;
            RefreshTemporalTearVisual(tear);

            WorldContainer openerWorld = ClusterManager.Instance != null ? ClusterManager.Instance.GetWorld(openerWorldId) : null;
            if (openerWorld != null)
                openerWorld.GetSMI<GameplaySeasonManager.Instance>().StartNewSeason(Db.Get().GameplaySeasons.TemporalTearMeteorShowers);

            return true;
        }

        private static void RefreshTemporalTearVisual(TemporalTear tear)
        {
            if (tear == null)
                return;

            try
            {
                tear.UpdateStatus();
                if (ClusterMapScreen.Instance != null)
                    ClusterMapScreen.Instance.Trigger((int)GameHashes.UIRefresh, null);

                if (GameScheduler.Instance != null)
                {
                    GameScheduler.Instance.ScheduleNextFrame("Interstellar.RefreshTemporalTearVisual", data =>
                    {
                        TemporalTear delayedTear = data as TemporalTear;
                        if (delayedTear != null)
                            delayedTear.UpdateStatus();
                    }, tear);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MyWorldDumpMod] RefreshTemporalTearVisual failed. error={ex}");
            }
        }

        private static void ResetTemporalTearOpenerForNextTear(TemporalTearOpener.Instance opener)
        {
            if (opener == null || opener.gameObject == null)
                return;

            Action<object> resetAction = data =>
            {
                TemporalTearOpener.Instance delayedOpener = data as TemporalTearOpener.Instance;
                if (delayedOpener == null || delayedOpener.gameObject == null)
                    return;

                try
                {
                    TemporalTearOpener_particlesConsumed_Field(delayedOpener) = 0f;
                    delayedOpener.UpdateMeter();
                    delayedOpener.GoTo((StateMachine.BaseState)delayedOpener.sm.root);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MyWorldDumpMod] ResetTemporalTearOpenerForNextTear failed. error={ex}");
                }
            };

            if (GameScheduler.Instance != null)
                GameScheduler.Instance.Schedule("Interstellar.ResetTemporalTearOpenerForNextTear", 1f, resetAction, opener);
            else
                resetAction(opener);
        }

        private static bool AreAllCachedTemporalTearsOpen()
        {
            RebuildTemporalTearCache();
            if (CachedTemporalTears.Count == 0)
                return false;

            for (int i = 0; i < CachedTemporalTears.Count; i++)
            {
                TemporalTear tear = CachedTemporalTears[i].Tear;
                if (tear != null && !tear.IsOpen())
                    return false;
            }
            return true;
        }

        private static bool IsAnyCachedTemporalTearRevealed()
        {
            RebuildTemporalTearCache();
            ClusterFogOfWarManager.Instance fow = SaveGame.Instance != null ? SaveGame.Instance.GetSMI<ClusterFogOfWarManager.Instance>() : null;
            if (fow == null)
                return false;

            for (int i = 0; i < CachedTemporalTears.Count; i++)
            {
                TemporalTear tear = CachedTemporalTears[i].Tear;
                if (tear != null && fow.IsLocationRevealed(tear.Location))
                    return true;
            }
            return false;
        }

        public class HotkeyListener : MonoBehaviour
        {
            public event System.Action OnCtrlPageDown;
            public event System.Action OnCtrlDelete;
            public void Update()
            {
                bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                if (ctrl && Input.GetKeyDown(KeyCode.PageDown))
                {
                    OnCtrlPageDown?.Invoke();
                }
                if (ctrl && Input.GetKeyDown(KeyCode.Delete))
                {
                    OnCtrlDelete?.Invoke();
                }
                if (ctrl && Input.GetKeyDown(KeyCode.Insert))
                {
                    FowPaintLog = !FowPaintLog;
                    Debug.Log($"[MyWorldDumpMod] SandboxFOWTool.OnPaintCell log: {(FowPaintLog ? "ON" : "OFF")}");
                }
            }
        }
        public class ClusterFxToggleController : MonoBehaviour
        {
            private bool disableClusterFx;

            public void ToggleByHotkey()
            {
                disableClusterFx = !disableClusterFx;
                ApplyState();
                Debug.Log($"[MyWorldDumpMod] Cluster FX bypass: {(disableClusterFx ? "ON" : "OFF")} (Ctrl+Delete)");
            }

            public void LateUpdate()
            {
                // Keep the desired state even if camera is recreated during scene transitions.
                ApplyState();
            }

            private void ApplyState()
            {
                CameraController camera = CameraController.Instance;
                if (camera == null)
                    return;

                if (camera.ignoreClusterFX != disableClusterFx)
                    camera.ToggleClusterFX();
            }
        }
        private static string EscapeCsv(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            bool needQuote = s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
            if (!needQuote) return s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }
        public static unsafe string DumpGrids(int worldId)
        {
            string outDir = GetGridDumpDirectory();
            Debug.Log($"[GridWorldDumper] Dumping world={worldId} to directory: {outDir}");

            Directory.CreateDirectory(outDir);
            WorldContainer dumpWorld = ClusterManager.Instance.GetWorld(worldId);
            if (dumpWorld == null)
            {
                Debug.LogWarning($"[GridWorldDumper] World {worldId} not found; dump skipped.");
                return null;
            }

            string filePath = System.IO.Path.Combine(outDir, $"grid_world_{worldId}.csv");
            DumpedFiles.Add(filePath);
            Stream stream = File.Create(filePath);

            var sw = new StreamWriter(stream, new UTF8Encoding(false), bufferSize: 1 << 20);
            var inv = CultureInfo.InvariantCulture;
            //var sb = new StringBuilder(512);

            // Header
            sw.WriteLine(
                "x,y,worldId,zoneType," +
                "elementIdx,elementId," +
                "temperature,radiation,mass," +
                "properties,strengthInfo,insulation," +
                "diseaseIdx,diseaseCount," +
                "exposedToSunlight,accumulatedFlow," +
                "Visible,Spawnable,Damage,Decor,GravitasFacility," +
                "Loudness,LightCount"
                );

            int cellCount = Grid.CellCount;

            for (int cell = 0; cell < cellCount; cell++)
            {
                // 只 dump 指定 world
                if (!Grid.IsValidCellInWorld(cell, worldId))
                    continue;

                int x = Grid.CellColumn(cell) - dumpWorld.WorldOffset.x;
                int y = Grid.CellRow(cell) - dumpWorld.WorldOffset.y;
                byte zoneType = byte.MaxValue;
                if (World.Instance != null && World.Instance.zoneRenderData != null)
                {
                    SubWorld.ZoneType cellZoneType = World.Instance.zoneRenderData.GetSubWorldZoneType(cell);
                    zoneType = cellZoneType == SubWorld.ZoneType.Space ? byte.MaxValue : (byte)cellZoneType;
                }

                ushort elementIdx = Grid.elementIdx != null ? Grid.elementIdx[cell] : (ushort)0;
                string elementId = "";
                if (Grid.Element != null)
                {
                    var elem = Grid.Element[cell];
                    if (elem != null)
                    {
                        elementId = elem.id.ToString();
                    }
                }

                float temperature = Grid.temperature != null ? Grid.temperature[cell] : 0f;
                float radiation = Grid.radiation != null ? Grid.radiation[cell] : 0f;
                float mass = Grid.mass != null ? Grid.mass[cell] : 0f;

                byte properties = Grid.properties != null ? Grid.properties[cell] : (byte)0;
                byte strengthInfo = Grid.strengthInfo != null ? Grid.strengthInfo[cell] : (byte)0;
                byte insulation = Grid.insulation != null ? Grid.insulation[cell] : (byte)0;

                byte diseaseIdx = Grid.diseaseIdx != null ? Grid.diseaseIdx[cell] : (byte)0;
                int diseaseCount = Grid.diseaseCount != null ? Grid.diseaseCount[cell] : 0;

                byte exposedToSunlight = Grid.exposedToSunlight != null ? Grid.exposedToSunlight[cell] : (byte)0;
                float accumulatedFlow = Grid.AccumulatedFlowValues != null ? Grid.AccumulatedFlowValues[cell] : 0f;

                byte visible = Grid.Visible != null ? Grid.Visible[cell] : (byte)0;
                byte spawnable = Grid.Spawnable != null ? Grid.Spawnable[cell] : (byte)0;

                float damage = Grid.Damage != null ? Grid.Damage[cell] : 0f;
                float decor = Grid.Decor != null ? Grid.Decor[cell] : 0f;

                bool gravitasFacility = Grid.GravitasFacility != null && Grid.GravitasFacility[cell];

                float loudness = Grid.Loudness != null ? Grid.Loudness[cell] : 0f;
                int lightCount = Grid.LightCount != null ? Grid.LightCount[cell] : 0;

                // CSV row
                sw.Write(x); sw.Write(',');
                sw.Write(y); sw.Write(',');
                sw.Write(worldId); sw.Write(',');
                sw.Write(zoneType); sw.Write(',');

                sw.Write(elementIdx); sw.Write(',');
                sw.Write(EscapeCsv(elementId)); sw.Write(',');

                sw.Write(temperature.ToString("R", inv)); sw.Write(',');
                sw.Write(radiation.ToString("R", inv)); sw.Write(',');
                sw.Write(mass.ToString("R", inv)); sw.Write(',');

                sw.Write(properties); sw.Write(',');
                sw.Write(strengthInfo); sw.Write(',');
                sw.Write(insulation); sw.Write(',');

                sw.Write(diseaseIdx); sw.Write(',');
                sw.Write(diseaseCount); sw.Write(',');

                sw.Write(exposedToSunlight); sw.Write(',');
                sw.Write(accumulatedFlow.ToString("R", inv)); sw.Write(',');

                sw.Write(visible); sw.Write(',');
                sw.Write(spawnable); sw.Write(',');

                sw.Write(damage.ToString("R", inv)); sw.Write(',');
                sw.Write(decor.ToString("R", inv)); sw.Write(',');
                sw.Write(gravitasFacility ? 1 : 0); sw.Write(',');

                sw.Write(loudness.ToString("R", inv)); sw.Write(',');
                sw.Write(lightCount); sw.Write(',');

                sw.WriteLine();
            }

            sw.Write("overworld_cells :");
            sw.WriteLine();
            if (SaveLoader.Instance != null && SaveLoader.Instance.clusterDetailSave != null && SaveLoader.Instance.clusterDetailSave.overworldCells != null)
            {
                int minX = dumpWorld.WorldOffset.x;
                int minY = dumpWorld.WorldOffset.y;
                int maxX = minX + dumpWorld.WorldSize.x;
                int maxY = minY + dumpWorld.WorldSize.y;
                foreach (Klei.WorldDetailSave.OverworldCell oc in SaveLoader.Instance.clusterDetailSave.overworldCells)
                {
                    if (oc == null || oc.poly == null || !PolyOverlapsRect(oc.poly, minX, minY, maxX, maxY))
                        continue;

                    List<Vector2> verts = TryGetPolygonVertices(oc.poly);
                    if (verts == null || verts.Count < 3)
                        continue;

                    byte zoneId = oc.zoneType == SubWorld.ZoneType.Space ? byte.MaxValue : (byte)oc.zoneType;
                    sw.Write(zoneId.ToString(inv)); sw.Write(',');
                    sw.Write(verts.Count.ToString(inv));
                    for (int i = 0; i < verts.Count; i++)
                    {
                        float lx = verts[i].x - dumpWorld.WorldOffset.x;
                        float ly = verts[i].y - dumpWorld.WorldOffset.y;
                        sw.Write(',');
                        sw.Write(lx.ToString("R", inv));
                        sw.Write(',');
                        sw.Write(ly.ToString("R", inv));
                    }
                    sw.WriteLine();
                }
            }
            //dump build crops health meteors....
            sw.Write("builds :");
            sw.WriteLine();
            foreach (BuildingComplete building in Components.BuildingCompletes.Items)
            {
                if (Grid.WorldIdx[Grid.PosToCell((KMonoBehaviour)building)] == worldId)
                {
                    if (building.Def != null && ShouldSkipSpecialBuildingForDump(building.Def.PrefabID))
                        continue;

                    // Unified building anchor: use the same anchor cell that BuildingDef.Build(...) consumes.
                    int cell = building.GetCell();
                    int localX = Grid.CellColumn(cell) - dumpWorld.WorldOffset.x;
                    int localY = Grid.CellRow(cell) - dumpWorld.WorldOffset.y;

                    sw.Write(building.Def.PrefabID); sw.Write(',');
                    sw.Write(localX.ToString(inv)); sw.Write(',');
                    sw.Write(localY.ToString(inv)); sw.Write(',');
                    sw.Write("0"); sw.Write(',');
                    sw.Write(building.Orientation.ToString()); sw.Write(',');
                    sw.Write(building.primaryElement.ElementID.ToString()); sw.Write(',');
                    sw.Write(EncodeBuildingStorage(building.gameObject, inv));
                    sw.WriteLine();
                }
            }
            sw.Write("crops :");
            sw.WriteLine();
            foreach (Crop cmp in Components.Crops.Items)
            {
                if (Grid.WorldIdx[Grid.PosToCell((KMonoBehaviour)cmp)] == worldId)
                {
                    int cell = Grid.PosToCell(cmp);
                    int localX = Grid.CellColumn(cell) - dumpWorld.WorldOffset.x;
                    int localY = Grid.CellRow(cell) - dumpWorld.WorldOffset.y;

                    var kpid = cmp.GetComponent<KPrefabID>();
                    Tag prefabTag = kpid.PrefabTag;     // 这是 prefab 的 ID（Tag）
                    string prefabId = prefabTag.ToString();  // 常见会得到 "Hatch" / "Drecko" / "Minion" 之类

                    sw.Write(prefabId/*cmp.cropId*/); sw.Write(',');
                    sw.Write(localX.ToString(inv)); sw.Write(',');
                    sw.Write(localY.ToString(inv)); sw.Write(',');
                    sw.Write("0"); sw.Write(',');
                    sw.WriteLine();
                }
            }
            sw.Write("health :");
            sw.WriteLine();
            foreach (Health cmp in Components.Health.Items)
            {
                if (Grid.WorldIdx[Grid.PosToCell((KMonoBehaviour)cmp)] == worldId)
                {
                    var kpid = cmp.GetComponent<KPrefabID>();
                    if (ShouldSkipPersonForDump(kpid))
                        continue;

                    Tag prefabTag = kpid.PrefabTag;     // 这是 prefab 的 ID（Tag）
                    string prefabId = prefabTag.ToString();  // 常见会得到 "Hatch" / "Drecko" / "Minion" 之类

                    int cell = Grid.PosToCell(cmp);
                    int localX = Grid.CellColumn(cell) - dumpWorld.WorldOffset.x;
                    int localY = Grid.CellRow(cell) - dumpWorld.WorldOffset.y;
                    sw.Write(prefabId); sw.Write(',');
                    sw.Write(localX.ToString(inv)); sw.Write(',');
                    sw.Write(localY.ToString(inv)); sw.Write(',');
                    sw.Write("0"); sw.Write(',');
                    sw.WriteLine();
                }
            }

            sw.Write("geysers :");
            sw.WriteLine();
            foreach (Geyser cmp in Components.Geysers.GetItems(worldId))
            {
                var kpid = cmp.GetComponent<KPrefabID>();
                Tag prefabTag = kpid.PrefabTag;     // 这是 prefab 的 ID（Tag）
                string prefabId = prefabTag.ToString();  // 常见会得到 "Hatch" / "Drecko" / "Minion" 之类

                int cell = Grid.PosToCell(cmp);
                int localX = Grid.CellColumn(cell) - dumpWorld.WorldOffset.x;
                int localY = Grid.CellRow(cell) - dumpWorld.WorldOffset.y;
                sw.Write(prefabId); sw.Write(',');
                sw.Write(prefabId); sw.Write(',');
                sw.Write(localX.ToString(inv)); sw.Write(',');
                sw.Write(localY.ToString(inv)); sw.Write(',');
                sw.Write("0"); sw.Write(',');
                sw.WriteLine();

            }

            sw.Write("other_entities :");
            sw.WriteLine();
            WriteOtherEntityRows(sw, inv, dumpWorld, worldId);

            sw.Flush();
            sw.Dispose();
            stream.Dispose();
            Debug.Log($"[GridWorldDumper] Dumped world={worldId} to: {filePath}");
            return filePath;
        }

        private static bool ShouldSkipPersonForDump(KPrefabID kpid)
        {
            if (kpid == null || kpid.gameObject == null)
                return true;

            if (kpid.HasTag(GameTags.BaseMinion))
                return true;
            if (kpid.HasTag(GameTags.Minions.Models.Bionic))
                return true;
            if (kpid.GetComponent<MinionBrain>() != null)
                return true;
            if (kpid.GetComponent<MinionIdentity>() != null)
                return true;

            return false;
        }

        private static bool ShouldSkipSpecialBuildingForDump(string prefabId)
        {
            return string.Equals(prefabId, "Headquarters", StringComparison.Ordinal) ||
                   string.Equals(prefabId, "WarpPortal", StringComparison.Ordinal) ||
                   string.Equals(prefabId, "WarpReceiver", StringComparison.Ordinal) ||
                   string.Equals(prefabId, "WarpConduitSender", StringComparison.Ordinal) ||
                   string.Equals(prefabId, "WarpConduitReceiver", StringComparison.Ordinal);
        }

        private static string GetGridDumpDirectory()
        {
            string modDir = Interstellar.modPath;
            if (string.IsNullOrWhiteSpace(modDir))
            {
                string assemblyLocation = Assembly.GetExecutingAssembly().Location;
                modDir = string.IsNullOrWhiteSpace(assemblyLocation) ? Application.persistentDataPath : System.IO.Path.GetDirectoryName(assemblyLocation);
            }

            return System.IO.Path.Combine(modDir, "grid_dumps");
        }

        private static string GetModDirectory()
        {
            string modDir = Interstellar.modPath;
            if (!string.IsNullOrWhiteSpace(modDir))
                return modDir;

            string assemblyLocation = Assembly.GetExecutingAssembly().Location;
            return string.IsNullOrWhiteSpace(assemblyLocation) ? Application.persistentDataPath : System.IO.Path.GetDirectoryName(assemblyLocation);
        }

        private static string GetRestoreDebugLogDirectory()
        {
            return System.IO.Path.Combine(GetModDirectory(), "restore_debug_logs");
        }

        private static string GetRestoreDebugLogPath()
        {
            return System.IO.Path.Combine(GetRestoreDebugLogDirectory(), "restore_debug.log");
        }

        private static void ResetRestoreDebugLog()
        {
            RestoreDebugLogPathByWorldId.Clear();
            if (!IsDebugHotKeyEnabled())
                return;

            Directory.CreateDirectory(GetRestoreDebugLogDirectory());
            File.WriteAllText(GetRestoreDebugLogPath(), "", new UTF8Encoding(false));
        }

        private static StreamWriter CreateRestoreDebugLog(int fromWorldId, int toWorldId, Vector2I targetOffset, Vector2I targetSize)
        {
            if (!IsDebugHotKeyEnabled())
                return null;

            Directory.CreateDirectory(GetRestoreDebugLogDirectory());
            string path = GetRestoreDebugLogPath();
            RestoreDebugLogPathByWorldId[toWorldId] = path;

            StreamWriter writer = new StreamWriter(File.Open(path, FileMode.Append, FileAccess.Write, FileShare.Read), new UTF8Encoding(false), bufferSize: 1 << 20);
            writer.WriteLine();
            writer.WriteLine($"Restore debug log. fromWorldId={fromWorldId}, toWorldId={toWorldId}, targetOffset=({targetOffset.x},{targetOffset.y}), targetSize=({targetSize.x},{targetSize.y})");
            writer.WriteLine("category\tid\toldRelGrid\tnewRelGrid\tnewAbsGrid\tnewCell\textra");
            return writer;
        }

        private static StreamWriter OpenRestoreDebugLogForAppend(int toWorldId)
        {
            if (!IsDebugHotKeyEnabled())
                return null;

            string path;
            if (!RestoreDebugLogPathByWorldId.TryGetValue(toWorldId, out path) || string.IsNullOrWhiteSpace(path))
                return null;

            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            return new StreamWriter(File.Open(path, FileMode.Append, FileAccess.Write, FileShare.Read), new UTF8Encoding(false), bufferSize: 1 << 20);
        }

        private static void WriteRestoreDebugLine(StreamWriter writer, string category, string id, float oldRelX, float oldRelY, Vector2I targetOffset, int targetCell, string extra = "")
        {
            if (writer == null)
                return;

            Vector2I abs = Grid.IsValidCell(targetCell)
                ? Grid.CellToXY(targetCell)
                : new Vector2I(targetOffset.x + Mathf.RoundToInt(oldRelX), targetOffset.y + Mathf.RoundToInt(oldRelY));
            int newRelX = abs.x - targetOffset.x;
            int newRelY = abs.y - targetOffset.y;

            writer.Write(category ?? "");
            writer.Write('\t');
            writer.Write(id ?? "");
            writer.Write('\t');
            writer.Write('(');
            writer.Write(oldRelX.ToString("R", CultureInfo.InvariantCulture));
            writer.Write(',');
            writer.Write(oldRelY.ToString("R", CultureInfo.InvariantCulture));
            writer.Write(')');
            writer.Write('\t');
            writer.Write($"({newRelX},{newRelY})");
            writer.Write('\t');
            writer.Write($"({abs.x},{abs.y})");
            writer.Write('\t');
            writer.Write(targetCell);
            writer.Write('\t');
            writer.WriteLine(extra ?? "");
        }

        private static void WriteOtherEntityRows(StreamWriter sw, CultureInfo inv, WorldContainer dumpWorld, int worldId)
        {
            HashSet<GameObject> dumped = new HashSet<GameObject>();

            foreach (Harvestable cmp in Components.Harvestables.Items)
                WriteOtherEntityRow(sw, inv, dumpWorld, worldId, cmp, dumped);
            foreach (Edible cmp in Components.Edibles.Items)
                WriteOtherEntityRow(sw, inv, dumpWorld, worldId, cmp, dumped);
            foreach (SpaceArtifact cmp in Components.SpaceArtifacts.Items)
                WriteOtherEntityRow(sw, inv, dumpWorld, worldId, cmp, dumped);

            foreach (UnityEngine.Object obj in UnityEngine.Object.FindObjectsByType(typeof(OccupyArea), FindObjectsSortMode.InstanceID))
                WriteOtherEntityRow(sw, inv, dumpWorld, worldId, obj as Component, dumped);
            foreach (UnityEngine.Object obj in UnityEngine.Object.FindObjectsByType(typeof(FogOfWarMask), FindObjectsSortMode.InstanceID))
                WriteOtherEntityRow(sw, inv, dumpWorld, worldId, obj as Component, dumped);
            foreach (UnityEngine.Object obj in UnityEngine.Object.FindObjectsByType(typeof(LoreBearer), FindObjectsSortMode.InstanceID))
                WriteOtherEntityRow(sw, inv, dumpWorld, worldId, obj as Component, dumped);
        }

        private static void WriteOtherEntityRow(StreamWriter sw, CultureInfo inv, WorldContainer dumpWorld, int worldId, Component cmp, HashSet<GameObject> dumped)
        {
            if (cmp == null || cmp.gameObject == null || dumped.Contains(cmp.gameObject))
                return;

            KPrefabID kpid = cmp.GetComponent<KPrefabID>();
            if (!ShouldDumpAsOtherEntity(kpid, worldId))
                return;

            int cell = Grid.PosToCell(kpid);
            int localX = Grid.CellColumn(cell) - dumpWorld.WorldOffset.x;
            int localY = Grid.CellRow(cell) - dumpWorld.WorldOffset.y;
            sw.Write(kpid.PrefabTag.ToString()); sw.Write(',');
            sw.Write(localX.ToString(inv)); sw.Write(',');
            sw.Write(localY.ToString(inv)); sw.Write(',');
            sw.Write("0"); sw.Write(',');
            sw.WriteLine();
            dumped.Add(cmp.gameObject);
        }

        private static bool ShouldDumpAsOtherEntity(KPrefabID kpid, int worldId)
        {
            if (kpid == null || kpid.gameObject == null || !kpid.gameObject.activeSelf)
                return false;

            int cell = Grid.PosToCell(kpid);
            if (!Grid.IsValidCell(cell) || Grid.WorldIdx[cell] != worldId)
                return false;

            GameObject prefab = Assets.GetPrefab(kpid.PrefabTag);
            if (prefab == null || prefab.HasTag(GameTags.ExcludeFromTemplate))
                return false;
            if (ShouldSkipSpecialBuildingForDump(kpid.PrefabTag.ToString()))
                return false;
            if (!PrefabHasValidAnimFiles(prefab))
                return false;

            GameObject go = kpid.gameObject;
            if (go.GetComponent<BuildingComplete>() != null)
                return false;
            if (go.GetComponent<Geyser>() != null)
                return false;
            if (go.GetComponent<Crop>() != null)
                return false;
            if (go.GetComponent<ElementChunk>() != null)
                return false;
            if (go.GetComponent<MinionBrain>() != null)
                return false;
            if (kpid.HasTag(GameTags.Creature) || kpid.HasTag(GameTags.BaseMinion))
                return false;
            if (kpid.HasTag(GameTags.Stored))
                return false;

            return true;
        }

        private static string EncodeBuildingStorage(GameObject building, CultureInfo inv)
        {
            Storage storage = building != null ? building.GetComponent<Storage>() : null;
            if (storage == null || storage.items == null || storage.items.Count == 0)
                return "";

            List<string> encodedItems = new List<string>();
            foreach (GameObject item in storage.items)
            {
                if (item == null)
                    continue;

                KPrefabID kpid = item.GetComponent<KPrefabID>();
                PrimaryElement primary = item.GetComponent<PrimaryElement>();
                if (kpid == null || primary == null)
                    continue;

                string disease = "";
                if (primary.DiseaseIdx != byte.MaxValue && Db.Get() != null && Db.Get().Diseases != null)
                    disease = Db.Get().Diseases[(int)primary.DiseaseIdx].Id;

                bool isOre = item.GetComponent<ElementChunk>() != null;
                float rot = 0f;
                Rottable.Instance rottable = item.GetSMI<Rottable.Instance>();
                if (rottable != null)
                    rot = rottable.RotValue;

                encodedItems.Add(string.Join("~", new[]
                {
                    EncodeStorageToken(kpid.PrefabTag.ToString()),
                    primary.Units.ToString("R", inv),
                    primary.Temperature.ToString("R", inv),
                    primary.ElementID.ToString(),
                    EncodeStorageToken(disease),
                    primary.DiseaseCount.ToString(inv),
                    isOre ? "1" : "0",
                    rot.ToString("R", inv)
                }));
            }

            return string.Join("|", encodedItems.ToArray());
        }

        private static string EncodeStorageToken(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        }

        private static string DecodeStorageToken(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }

        private static bool PrefabHasValidAnimFiles(GameObject prefab)
        {
            if (prefab == null)
                return false;

            KBatchedAnimController anim = prefab.GetComponent<KBatchedAnimController>();
            return anim == null || (anim.AnimFiles != null && anim.AnimFiles.Length > 0);
        }

        private struct GridDumpRow
        {
            public int X;
            public int Y;
            public int WorldId;
            public byte ZoneId;
            public ushort ElementIdx;
            public string ElementId;
            public float Temperature;
            public float Radiation;
            public float Mass;
            public byte Properties;
            public byte DiseaseIdx;
            public int DiseaseCount;
        }
        private struct BuildingDumpRow
        {
            public string PrefabId;
            public float X;
            public float Y;
            public float Z;
            public string Orientation;
            public string ElementId;
            public string StorageData;
        }
        private struct PrefabDumpRow
        {
            public string PrefabId;
            public float X;
            public float Y;
            public float Z;
        }
        private struct GeyserDumpRow
        {
            public string PrefabId;
            public float X;
            public float Y;
            public float Z;
        }
        private struct OverworldCellDumpRow
        {
            public byte ZoneId;
            public List<Vector2> LocalVertices;
        }
        private enum DumpSection
        {
            None,
            Grid,
            OverworldCells,
            Builds,
            Crops,
            Health,
            Geysers,
            OtherEntities
        }
        private class DumpFileData
        {
            public readonly List<GridDumpRow> GridRows = new List<GridDumpRow>();
            public readonly List<BuildingDumpRow> BuildingRows = new List<BuildingDumpRow>();
            public readonly List<PrefabDumpRow> CropRows = new List<PrefabDumpRow>();
            public readonly List<PrefabDumpRow> HealthRows = new List<PrefabDumpRow>();
            public readonly List<GeyserDumpRow> GeyserRows = new List<GeyserDumpRow>();
            public readonly List<PrefabDumpRow> OtherEntityRows = new List<PrefabDumpRow>();
            public readonly List<OverworldCellDumpRow> OverworldCellRows = new List<OverworldCellDumpRow>();
        }
        private static DumpFileData ReadDumpFile(string dumpPath)
        {
            DumpFileData data = new DumpFileData();
            if (string.IsNullOrWhiteSpace(dumpPath) || !File.Exists(dumpPath))
                return data;

            CultureInfo inv = CultureInfo.InvariantCulture;
            using (StreamReader sr = new StreamReader(dumpPath, Encoding.UTF8))
            {
                string line;
                bool skippedHeader = false;
                DumpSection section = DumpSection.Grid;
                while ((line = sr.ReadLine()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    if (!skippedHeader)
                    {
                        skippedHeader = true;
                        continue;
                    }

                    string trimmed = line.Trim();
                    if (trimmed.Equals("builds :", StringComparison.OrdinalIgnoreCase))
                    {
                        section = DumpSection.Builds;
                        continue;
                    }
                    if (trimmed.Equals("overworld_cells :", StringComparison.OrdinalIgnoreCase))
                    {
                        section = DumpSection.OverworldCells;
                        continue;
                    }
                    if (trimmed.Equals("crops :", StringComparison.OrdinalIgnoreCase))
                    {
                        section = DumpSection.Crops;
                        continue;
                    }
                    if (trimmed.Equals("health :", StringComparison.OrdinalIgnoreCase))
                    {
                        section = DumpSection.Health;
                        continue;
                    }
                    if (trimmed.Equals("geysers :", StringComparison.OrdinalIgnoreCase))
                    {
                        section = DumpSection.Geysers;
                        continue;
                    }
                    if (trimmed.Equals("other_entities :", StringComparison.OrdinalIgnoreCase))
                    {
                        section = DumpSection.OtherEntities;
                        continue;
                    }

                    try
                    {
                        if (section == DumpSection.Grid)
                        {
                            if (!char.IsDigit(trimmed[0]) && trimmed[0] != '-')
                                continue;
                            string[] p = line.Split(',');
                            if (p.Length < 13)
                                continue;
                            bool hasZoneColumn = p.Length >= 14;
                            int elementIdxColumn = hasZoneColumn ? 4 : 3;
                            int elementIdColumn = hasZoneColumn ? 5 : 4;
                            int temperatureColumn = hasZoneColumn ? 6 : 5;
                            int radiationColumn = hasZoneColumn ? 7 : 6;
                            int massColumn = hasZoneColumn ? 8 : 7;
                            int propertiesColumn = hasZoneColumn ? 9 : 8;
                            int diseaseIdxColumn = hasZoneColumn ? 12 : 11;
                            int diseaseCountColumn = hasZoneColumn ? 13 : 12;
                            GridDumpRow row = new GridDumpRow
                            {
                                X = int.Parse(p[0], inv),
                                Y = int.Parse(p[1], inv),
                                WorldId = int.Parse(p[2], inv),
                                ZoneId = hasZoneColumn ? byte.Parse(p[3], inv) : byte.MaxValue,
                                ElementIdx = ushort.Parse(p[elementIdxColumn], inv),
                                ElementId = p[elementIdColumn],
                                Temperature = float.Parse(p[temperatureColumn], inv),
                                Radiation = float.Parse(p[radiationColumn], inv),
                                Mass = float.Parse(p[massColumn], inv),
                                Properties = byte.Parse(p[propertiesColumn], inv),
                                DiseaseIdx = byte.Parse(p[diseaseIdxColumn], inv),
                                DiseaseCount = int.Parse(p[diseaseCountColumn], inv)
                            };
                            data.GridRows.Add(row);
                        }
                        else if (section == DumpSection.Builds)
                        {
                            string[] p = line.Split(',');
                            if (p.Length < 6 || string.IsNullOrWhiteSpace(p[0]))
                                continue;
                            BuildingDumpRow row = new BuildingDumpRow
                            {
                                PrefabId = p[0],
                                X = float.Parse(p[1], inv),
                                Y = float.Parse(p[2], inv),
                                Z = float.Parse(p[3], inv),
                                Orientation = p[4],
                                ElementId = p[5],
                                StorageData = p.Length >= 7 ? p[6] : ""
                            };
                            data.BuildingRows.Add(row);
                        }
                        else if (section == DumpSection.Crops || section == DumpSection.Health || section == DumpSection.OtherEntities)
                        {
                            string[] p = line.Split(',');
                            if (p.Length < 4 || string.IsNullOrWhiteSpace(p[0]))
                                continue;
                            PrefabDumpRow row = new PrefabDumpRow
                            {
                                PrefabId = p[0],
                                X = float.Parse(p[1], inv),
                                Y = float.Parse(p[2], inv),
                                Z = float.Parse(p[3], inv)
                            };
                            if (section == DumpSection.Crops)
                                data.CropRows.Add(row);
                            else if (section == DumpSection.Health)
                                data.HealthRows.Add(row);
                            else
                                data.OtherEntityRows.Add(row);
                        }
                        else if (section == DumpSection.Geysers)
                        {
                            string[] p = line.Split(',');
                            if (p.Length < 4 || string.IsNullOrWhiteSpace(p[0]))
                                continue;
                            int coordStart = p.Length >= 5 ? 2 : 1;
                            GeyserDumpRow row = new GeyserDumpRow
                            {
                                PrefabId = p[0],
                                X = float.Parse(p[coordStart], inv),
                                Y = float.Parse(p[coordStart + 1], inv),
                                Z = float.Parse(p[coordStart + 2], inv)
                            };
                            data.GeyserRows.Add(row);
                        }
                        else if (section == DumpSection.OverworldCells)
                        {
                            string[] p = line.Split(',');
                            if (p.Length < 2)
                                continue;
                            byte zoneId = byte.Parse(p[0], inv);
                            int vertCount = int.Parse(p[1], inv);
                            if (vertCount < 3 || p.Length < 2 + vertCount * 2)
                                continue;
                            List<Vector2> verts = new List<Vector2>(vertCount);
                            int idx = 2;
                            for (int i = 0; i < vertCount; i++)
                            {
                                float vx = float.Parse(p[idx++], inv);
                                float vy = float.Parse(p[idx++], inv);
                                verts.Add(new Vector2(vx, vy));
                            }
                            data.OverworldCellRows.Add(new OverworldCellDumpRow
                            {
                                ZoneId = zoneId,
                                LocalVertices = verts
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[MyWorldDumpMod] Bad dump row skipped: {ex.Message}");
                    }
                }
            }

            Debug.Log($"[MyWorldDumpMod] Loaded dump file. grid={data.GridRows.Count}, overworldCells={data.OverworldCellRows.Count}, builds={data.BuildingRows.Count}, crops={data.CropRows.Count}, health={data.HealthRows.Count}, geysers={data.GeyserRows.Count}, otherEntities={data.OtherEntityRows.Count}, path={dumpPath}");
            return data;
        }
        private static bool TryMapSourceCellToTargetCell(int sourceCell, Vector2I sourceOffset, Vector2I sourceSize, Vector2I targetOffset, Vector2I targetSize, out int targetCell)
        {
            targetCell = Grid.InvalidCell;
            if (!Grid.IsValidCell(sourceCell))
                return false;
            Vector2I sourceXY = Grid.CellToXY(sourceCell);
            int localX = sourceXY.x - sourceOffset.x;
            int localY = sourceXY.y - sourceOffset.y;
            if (localX < 0 || localY < 0 || localX >= sourceSize.x || localY >= sourceSize.y)
                return false;
            int targetX = targetOffset.x + localX;
            int targetY = targetOffset.y + localY;
            if (targetX < targetOffset.x || targetY < targetOffset.y ||
                targetX >= targetOffset.x + targetSize.x || targetY >= targetOffset.y + targetSize.y)
                return false;
            targetCell = Grid.XYToCell(targetX, targetY);
            return Grid.IsValidCell(targetCell);
        }
        private static bool TryMapSourcePositionToTargetCell(Vector3 sourcePos, Vector2I sourceOffset, Vector2I sourceSize, Vector2I targetOffset, Vector2I targetSize, int toWorldId, out int targetCell)
        {
            targetCell = Grid.InvalidCell;
            int sourceCell = Grid.PosToCell(sourcePos);
            if (!TryMapSourceCellToTargetCell(sourceCell, sourceOffset, sourceSize, targetOffset, targetSize, out targetCell))
                return false;
            return Grid.IsValidCellInWorld(targetCell, toWorldId);
        }
        private static bool TryResolveSimHash(string text, out SimHashes hash)
        {
            hash = SimHashes.Void;
            return !string.IsNullOrWhiteSpace(text) && Enum.TryParse(text, out hash);
        }
        private static bool TrySpawnPrefabAtCell(string prefabId, int cell, out GameObject spawned)
        {
            spawned = null;
            if (string.IsNullOrWhiteSpace(prefabId) || !Grid.IsValidCell(cell))
                return false;
            GameObject prefab = Assets.GetPrefab(TagManager.Create(prefabId));
            if (prefab == null)
                return false;
            Grid.SceneLayer sceneLayer = Grid.SceneLayer.Front;
            KBatchedAnimController anim = prefab.GetComponent<KBatchedAnimController>();
            if (anim != null)
                sceneLayer = anim.sceneLayer;
            spawned = GameUtil.KInstantiate(prefab, Grid.CellToPosCBC(cell, sceneLayer), sceneLayer);
            if (spawned == null)
                return false;
            spawned.SetActive(true);
            return true;
        }
        private static int QueueBuildingsFromDumpForDelayedSpawn(List<BuildingDumpRow> rows, Vector2I sourceOffset, Vector2I sourceSize, Vector2I targetOffset, Vector2I targetSize, int toWorldId, StreamWriter restoreDebugLog = null)
        {
            int queued = 0;
            foreach (BuildingDumpRow row in rows)
            {
                int anchorCell = Grid.XYToCell(targetOffset.x + Mathf.RoundToInt(row.X), targetOffset.y + Mathf.RoundToInt(row.Y));
                if (!Grid.IsValidCell(anchorCell) || !Grid.IsValidCellInWorld(anchorCell, toWorldId))
                    continue;
                try
                {
                    BuildingDef def = Assets.GetBuildingDef(row.PrefabId);
                    if (def == null)
                        continue;

                    Orientation orientation = Orientation.Neutral;
                    if (!string.IsNullOrWhiteSpace(row.Orientation))
                    {
                        Orientation parsed;
                        if (Enum.TryParse(row.Orientation, out parsed))
                            orientation = parsed;
                    }

                    Vector2I xy = Grid.CellToXY(anchorCell);
                    SimHashes buildElement = ResolveBuildingElement(row, def);
                    TemplateClasses.Prefab prefab = new TemplateClasses.Prefab(row.PrefabId, TemplateClasses.Prefab.Type.Building, xy.x, xy.y, buildElement, 293.15f, 0f, null, 0, orientation);
                    prefab.storage = DecodeBuildingStorage(row.StorageData);
                    PendingDelayedSpawnPrefabs.Add(prefab);
                    queued++;
                    WriteRestoreDebugLine(restoreDebugLog, "building", row.PrefabId, row.X, row.Y, targetOffset, anchorCell, $"orientation={orientation};element={buildElement}");
                    Debug.Log($"[MyWorldDumpMod] Queued delayed building id={row.PrefabId} at cell={anchorCell} (x={row.X}, y={row.Y}, z={row.Z}) with orientation={orientation} and element={buildElement}.");
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MyWorldDumpMod] Queue delayed building failed id={row.PrefabId}: {ex.Message}");
                }
            }
            return queued;
        }

        private static List<TemplateClasses.StorageItem> DecodeBuildingStorage(string storageData)
        {
            if (string.IsNullOrWhiteSpace(storageData))
                return null;

            CultureInfo inv = CultureInfo.InvariantCulture;
            List<TemplateClasses.StorageItem> storageItems = new List<TemplateClasses.StorageItem>();
            string[] itemSpecs = storageData.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < itemSpecs.Length; i++)
            {
                try
                {
                    string[] p = itemSpecs[i].Split('~');
                    if (p.Length < 7)
                        continue;

                    string id = DecodeStorageToken(p[0]);
                    float units = float.Parse(p[1], inv);
                    float temperature = float.Parse(p[2], inv);
                    SimHashes element;
                    if (!Enum.TryParse(p[3], out element))
                        element = SimHashes.Void;
                    string disease = DecodeStorageToken(p[4]);
                    int diseaseCount = int.Parse(p[5], inv);
                    bool isOre = p[6] == "1";
                    float rot = 0f;
                    if (p.Length >= 8)
                        float.TryParse(p[7], NumberStyles.Float, inv, out rot);

                    TemplateClasses.StorageItem storageItem = new TemplateClasses.StorageItem(id, units, temperature, element, disease, diseaseCount, isOre);
                    storageItem.rottable.rotAmount = rot;
                    storageItems.Add(storageItem);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[MyWorldDumpMod] Bad building storage item skipped: {ex.Message}");
                }
            }

            return storageItems.Count > 0 ? storageItems : null;
        }

        private static SimHashes ResolveBuildingElement(BuildingDumpRow row, BuildingDef def)
        {
            SimHashes buildElement;
            if (TryResolveSimHash(row.ElementId, out buildElement) && ElementLoader.FindElementByHash(buildElement) != null)
                return buildElement;

            List<Tag> selectedElements = def.DefaultElements();
            if (selectedElements != null && selectedElements.Count > 0)
            {
                SimHashes defaultElement = ElementLoader.GetElementID(selectedElements[0]);
                if (ElementLoader.FindElementByHash(defaultElement) != null)
                    return defaultElement;
            }
            return SimHashes.Cuprite;
        }

        private static int QueueCropsFromDumpForDelayedSpawn(List<PrefabDumpRow> rows, Vector2I sourceOffset, Vector2I sourceSize, Vector2I targetOffset, Vector2I targetSize, int toWorldId, StreamWriter restoreDebugLog = null)
        {
            int queued = 0;
            int skippedDependent = 0;
            foreach (PrefabDumpRow row in rows)
            {
                if (!CanRestoreAsStandaloneCrop(row.PrefabId))
                {
                    skippedDependent++;
                    continue;
                }

                int targetCell = Grid.XYToCell(targetOffset.x + Mathf.RoundToInt(row.X), targetOffset.y + Mathf.RoundToInt(row.Y));
                if (!Grid.IsValidCell(targetCell) || !Grid.IsValidCellInWorld(targetCell, toWorldId))
                    continue;

                PendingDelayedSpawnPrefabs.Add(CreateDelayedSpawnPrefab(row.PrefabId, targetCell, TemplateClasses.Prefab.Type.Other, false));
                queued++;
                WriteRestoreDebugLine(restoreDebugLog, "crop", row.PrefabId, row.X, row.Y, targetOffset, targetCell, $"z={row.Z.ToString("R", CultureInfo.InvariantCulture)}");
                Debug.Log($"[MyWorldDumpMod] Queued delayed crop id={row.PrefabId} at cell={targetCell} (x={row.X}, y={row.Y}, z={row.Z}).");
            }
            if (skippedDependent > 0)
                Debug.Log($"[MyWorldDumpMod] QueueCrops skipped dependent branch-like prefabs: {skippedDependent}");
            return queued;
        }
        private static bool CanRestoreAsStandaloneCrop(string prefabId)
        {
            if (string.IsNullOrWhiteSpace(prefabId))
                return false;

            // Known hard dependency: wood tree branches require a valid trunk reference.
            if (prefabId.Equals("ForestTreeBranch", StringComparison.OrdinalIgnoreCase))
                return false;

            GameObject prefab = Assets.GetPrefab(TagManager.Create(prefabId));
            if (prefab == null)
                return false;

            KPrefabID pid = prefab.GetComponent<KPrefabID>();
            if (pid != null && pid.HasTag(GameTags.PlantBranch))
                return false;

            if (prefab.GetComponent<PlantBranch>() != null)
                return false;

            return true;
        }
        private static int QueueCreaturesFromDumpForDelayedSpawn(List<PrefabDumpRow> rows, Vector2I sourceOffset, Vector2I sourceSize, Vector2I targetOffset, Vector2I targetSize, int toWorldId, StreamWriter restoreDebugLog = null)
        {
            int queued = 0;
            foreach (PrefabDumpRow row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.PrefabId))
                    continue;
                GameObject prefab = Assets.GetPrefab(TagManager.Create(row.PrefabId));
                if (prefab == null)
                    continue;
                KPrefabID pid = prefab.GetComponent<KPrefabID>();
                if (pid == null || !pid.HasTag(GameTags.Creature) || pid.HasTag(GameTags.BaseMinion))
                    continue;
                int targetCell = Grid.XYToCell(targetOffset.x + Mathf.RoundToInt(row.X), targetOffset.y + Mathf.RoundToInt(row.Y));
                if (!Grid.IsValidCell(targetCell) || !Grid.IsValidCellInWorld(targetCell, toWorldId))
                    continue;

                PendingDelayedSpawnPrefabs.Add(CreateDelayedSpawnPrefab(row.PrefabId, targetCell, TemplateClasses.Prefab.Type.Other, false));
                queued++;
                WriteRestoreDebugLine(restoreDebugLog, "creature", row.PrefabId, row.X, row.Y, targetOffset, targetCell, $"z={row.Z.ToString("R", CultureInfo.InvariantCulture)}");
            }
            return queued;
        }

        private static int QueueOtherEntitiesFromDumpForDelayedSpawn(List<PrefabDumpRow> rows, Vector2I sourceOffset, Vector2I sourceSize, Vector2I targetOffset, Vector2I targetSize, int toWorldId, StreamWriter restoreDebugLog = null)
        {
            int queued = 0;
            foreach (PrefabDumpRow row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.PrefabId))
                    continue;

                GameObject prefab = Assets.GetPrefab(TagManager.Create(row.PrefabId));
                if (prefab == null || prefab.HasTag(GameTags.ExcludeFromTemplate))
                    continue;
                if (!PrefabHasValidAnimFiles(prefab))
                    continue;
                if (prefab.GetComponent<BuildingComplete>() != null)
                    continue;
                if (prefab.GetComponent<Geyser>() != null)
                    continue;
                if (prefab.GetComponent<Crop>() != null)
                    continue;
                if (prefab.GetComponent<ElementChunk>() != null)
                    continue;

                KPrefabID pid = prefab.GetComponent<KPrefabID>();
                if (pid != null && (pid.HasTag(GameTags.Creature) || pid.HasTag(GameTags.BaseMinion)))
                    continue;

                int targetCell = Grid.XYToCell(targetOffset.x + Mathf.RoundToInt(row.X), targetOffset.y + Mathf.RoundToInt(row.Y));
                if (!Grid.IsValidCell(targetCell) || !Grid.IsValidCellInWorld(targetCell, toWorldId))
                    continue;

                PendingDelayedSpawnPrefabs.Add(CreateDelayedSpawnPrefab(row.PrefabId, targetCell, TemplateClasses.Prefab.Type.Other, false));
                queued++;
                WriteRestoreDebugLine(restoreDebugLog, "other_entity", row.PrefabId, row.X, row.Y, targetOffset, targetCell, $"z={row.Z.ToString("R", CultureInfo.InvariantCulture)}");
                Debug.Log($"[MyWorldDumpMod] Queued delayed other entity id={row.PrefabId} at cell={targetCell} (x={row.X}, y={row.Y}, z={row.Z}).");
            }
            return queued;
        }

        private static int QueueGeysersFromDumpForDelayedSpawn(List<GeyserDumpRow> rows, Vector2I sourceOffset, Vector2I sourceSize, Vector2I targetOffset, Vector2I targetSize, int toWorldId, StreamWriter restoreDebugLog = null)
        {
            int queued = 0;
            foreach (GeyserDumpRow row in rows)
            {
                int targetCell = Grid.XYToCell(targetOffset.x + Mathf.RoundToInt(row.X), targetOffset.y + Mathf.RoundToInt(row.Y));
                if (!Grid.IsValidCell(targetCell) || !Grid.IsValidCellInWorld(targetCell, toWorldId))
                    continue;
                GameObject prefab = Assets.GetPrefab(TagManager.Create(row.PrefabId));
                if (prefab == null || prefab.GetComponent<Geyser>() == null)
                    continue;

                PendingDelayedSpawnPrefabs.Add(CreateDelayedSpawnPrefab(row.PrefabId, targetCell, TemplateClasses.Prefab.Type.Other, true));
                queued++;
                WriteRestoreDebugLine(restoreDebugLog, "geyser", row.PrefabId, row.X, row.Y, targetOffset, targetCell, $"z={row.Z.ToString("R", CultureInfo.InvariantCulture)}");
                Debug.Log($"[MyWorldDumpMod] Queued delayed geyser id={row.PrefabId} at cell={targetCell} (x={row.X}, y={row.Y}, z={row.Z}).");
            }
            return queued;
        }

        private static TemplateClasses.Prefab CreateDelayedSpawnPrefab(string prefabId, int cell, TemplateClasses.Prefab.Type type, bool markAsDelayedGeyser)
        {
            Vector2I xy = Grid.CellToXY(cell);
            TemplateClasses.Prefab prefab = new TemplateClasses.Prefab(prefabId, type, xy.x, xy.y, SimHashes.Creature);
            if (markAsDelayedGeyser)
            {
                prefab.other_values = new[]
                {
                    new TemplateClasses.Prefab.template_amount_value(DelayedRestoredGeyserMarker, 1f)
                };
            }
            return prefab;
        }

        private static bool IsDelayedRestoredGeyserPrefab(TemplateClasses.Prefab prefab)
        {
            if (prefab == null || prefab.other_values == null)
                return false;

            for (int i = 0; i < prefab.other_values.Length; i++)
            {
                TemplateClasses.Prefab.template_amount_value value = prefab.other_values[i];
                if (value != null && value.id == DelayedRestoredGeyserMarker)
                    return true;
            }
            return false;
        }

        private static void ResetGeyserToBeginWaitingForNextEruption(Geyser geyser)
        {
            if (geyser == null || GameClock.Instance == null)
                return;

            if (!EnsureGeyserConfigurationReady(geyser))
                return;

            float targetLifeTime = geyser.configuration.GetOnDuration() + geyser.configuration.GetOffDuration() * 0.05f + 0.1f;
            geyser.AlterTime(targetLifeTime - GameClock.Instance.GetTime(), true);
        }

        private static bool EnsureGeyserConfigurationReady(Geyser geyser)
        {
            if (geyser.configuration != null && geyser.configuration.typeId.IsValid)
                return true;

            GeyserConfigurator configurator = geyser.GetComponent<GeyserConfigurator>();
            if (configurator == null || !configurator.presetType.IsValid)
            {
                Debug.LogWarning($"[MyWorldDumpMod] Delayed restored geyser configuration is not ready. geyser={geyser.name}");
                return false;
            }

            geyser.configuration = configurator.MakeConfiguration();
            geyser.ApplyConfigurationEmissionValues(geyser.configuration);
            return geyser.configuration != null && geyser.configuration.typeId.IsValid;
        }

        private static void ScheduleDelayedRestoredGeyserReset(Geyser geyser, string prefabId)
        {
            if (Game.Instance == null || Game.Instance.callbackManager == null)
            {
                ResetGeyserToBeginWaitingForNextEruption(geyser);
                return;
            }

            Game.Instance.callbackManager.Add(new Game.CallbackInfo(new System.Action(() =>
            {
                if (geyser == null || geyser.gameObject == null)
                    return;

                ResetGeyserToBeginWaitingForNextEruption(geyser);
                Debug.Log($"[MyWorldDumpMod] Reset delayed restored geyser to wait for next eruption. prefab={prefabId}, cell={Grid.PosToCell(geyser.gameObject)}");
            })));
        }

        [HarmonyPatch(typeof(TemplateLoader), nameof(TemplateLoader.PlaceOtherEntities))]
        private static class Patch_TemplateLoader_PlaceOtherEntities_DelayedRestoredGeyser
        {
            private static void Postfix(TemplateClasses.Prefab prefab, GameObject __result)
            {
                if (__result == null || !IsDelayedRestoredGeyserPrefab(prefab))
                    return;

                Geyser geyser = __result.GetComponent<Geyser>();
                if (geyser == null)
                    return;

                ScheduleDelayedRestoredGeyserReset(geyser, prefab.id);
            }
        }

        private static readonly FieldInfo PedestalArtifactSpawner_artifactSpawned_Field = AccessTools.Field(typeof(PedestalArtifactSpawner), "artifactSpawned");
        private static readonly FieldInfo SingleEntityReceptacle_occupyingObject_Field = AccessTools.Field(typeof(SingleEntityReceptacle), "occupyingObject");
        private static readonly MethodInfo SingleEntityReceptacle_UnsubscribeFromOccupant_Method = AccessTools.Method(typeof(SingleEntityReceptacle), "UnsubscribeFromOccupant");
        private static readonly FieldInfo StatusItemGroup_items_Field = AccessTools.Field(typeof(StatusItemGroup), "items");

        [HarmonyPatch(typeof(TemplateLoader), nameof(TemplateLoader.PlaceBuilding))]
        private static class Patch_TemplateLoader_PlaceBuilding_RestoredPedestalStorage
        {
            private static void Postfix(TemplateClasses.Prefab prefab, GameObject __result)
            {
                if (__result == null || prefab == null || prefab.storage == null || prefab.storage.Count == 0)
                    return;
                if (__result.GetComponent<ItemPedestal>() == null && __result.GetComponent<PedestalArtifactSpawner>() == null)
                    return;

                RestorePedestalStorageOccupant(__result, prefab.storage);
            }
        }

        private static void RestorePedestalStorageOccupant(GameObject pedestal, List<TemplateClasses.StorageItem> storageItems)
        {
            Storage storage = pedestal != null ? pedestal.GetComponent<Storage>() : null;
            SingleEntityReceptacle receptacle = pedestal != null ? pedestal.GetComponent<SingleEntityReceptacle>() : null;
            if (storage == null || receptacle == null || storage.items == null || storageItems == null || storageItems.Count == 0)
                return;

            string desiredId = storageItems[0].id;
            if (string.IsNullOrWhiteSpace(desiredId))
                return;

            GameObject desired = null;
            GameObject[] storedItems = storage.items.ToArray();
            for (int i = 0; i < storedItems.Length; i++)
            {
                GameObject item = storedItems[i];
                KPrefabID pid = item != null ? item.GetComponent<KPrefabID>() : null;
                if (pid != null && pid.PrefabTag.ToString() == desiredId)
                {
                    desired = item;
                    break;
                }
            }

            if (desired == null)
                return;

            storedItems = storage.items.ToArray();
            for (int i = 0; i < storedItems.Length; i++)
            {
                GameObject item = storedItems[i];
                if (item == null || item == desired || item.GetComponent<SpaceArtifact>() == null)
                    continue;

                storage.Drop(item, false);
                Util.KDestroyGameObject(item);
            }

            ClearPedestalOccupantReferenceWithoutDroppingStorage(receptacle);

            receptacle.ForceDeposit(desired);

            KBatchedAnimController anim = desired.GetComponent<KBatchedAnimController>();
            if (anim != null)
            {
                anim.enabled = true;
                anim.sceneLayer = Grid.SceneLayer.Move;
            }

            PedestalArtifactSpawner spawner = pedestal.GetComponent<PedestalArtifactSpawner>();
            if (spawner != null && PedestalArtifactSpawner_artifactSpawned_Field != null)
                PedestalArtifactSpawner_artifactSpawned_Field.SetValue(spawner, true);

            Debug.Log($"[MyWorldDumpMod] Restored pedestal storage occupant. pedestal={pedestal.name}, item={desiredId}");
        }

        private static void ClearPedestalOccupantReferenceWithoutDroppingStorage(SingleEntityReceptacle receptacle)
        {
            if (receptacle == null || SingleEntityReceptacle_occupyingObject_Field == null)
                return;

            GameObject oldOccupant = SingleEntityReceptacle_occupyingObject_Field.GetValue(receptacle) as GameObject;
            if (oldOccupant != null && SingleEntityReceptacle_UnsubscribeFromOccupant_Method != null)
                SingleEntityReceptacle_UnsubscribeFromOccupant_Method.Invoke(receptacle, null);

            SingleEntityReceptacle_occupyingObject_Field.SetValue(receptacle, null);
        }

        [HarmonyPatch(typeof(SingleEntityReceptacle), "OnOccupantDestroyed")]
        private static class Patch_SingleEntityReceptacle_OnOccupantDestroyed_NewWorldClearGuard
        {
            private static bool Prefix()
            {
                return !IsClearingWorldsForNewWorld();
            }
        }

        [HarmonyPatch(typeof(StatusItemGroup), "Destroy")]
        private static class Patch_StatusItemGroup_Destroy_NewWorldTransitionGuard
        {
            private static bool Prefix(StatusItemGroup __instance)
            {
                if (!IsNewWorldTransitionActive())
                    return true;

                object items = StatusItemGroup_items_Field != null ? StatusItemGroup_items_Field.GetValue(__instance) : null;
                (items as System.Collections.IList)?.Clear();
                return false;
            }
        }

        private static unsafe bool RestoreGridFromDumpViaSim(DumpFileData dumpData/*string dumpPath*/, int fromWorldId, int toWorldId)
        {
            List<GridDumpRow> rows = dumpData.GridRows;
            if (rows.Count == 0)
                return false;

            WorldContainer targetWorld = ClusterManager.Instance.GetWorld(toWorldId);
            if (targetWorld == null)
            {
                Debug.LogWarning($"[MyWorldDumpMod] Target world id={toWorldId} not found; restore skipped.");
                return false;
            }

            Vector2I targetOffset = targetWorld.WorldOffset;
            Vector2I targetSize = targetWorld.WorldSize;
            EnsureTargetWorldIdxAssigned(toWorldId, targetOffset, targetSize);

            int applied = 0;
            int skippedNotFromWorld = 0;
            int skippedInvalidCell = 0;
            int skippedWrongWorld = 0;
            int skippedInvalidElement = 0;
            List<int> zoneChangedCells = new List<int>();
            StreamWriter restoreDebugLog = CreateRestoreDebugLog(fromWorldId, toWorldId, targetOffset, targetSize);

            foreach (GridDumpRow row in rows)
            {
                if (row.WorldId != fromWorldId)
                {
                    skippedNotFromWorld++;
                    continue;
                }

                int cell = Grid.XYToCell(targetOffset.x + row.X, targetOffset.y + row.Y);

                if (!Grid.IsValidCell(cell))
                {
                    skippedInvalidCell++;
                    continue;
                }

                // Never write across world boundaries; 255 and other ids are expected in separator bands.
                if (!Grid.IsValidCellInWorld(cell, toWorldId))
                {
                    skippedWrongWorld++;
                    continue;
                }

                byte zoneId = row.ZoneId;
                SimMessages.ModifyCellWorldZone(cell, zoneId);
                zoneChangedCells.Add(cell);

                // Prefer stable element ID/name from dump to avoid element index drift between sessions/mod states.
                ushort elementIdx = ResolveElementIdx(row);
                if (elementIdx >= ElementLoader.elements.Count)
                {
                    skippedInvalidElement++;
                    continue;
                }

                // 使用 Sim 接口写入元素/质量/温度/病菌
                SimMessages.ModifyCell(
                    cell,
                    elementIdx,
                    row.Temperature,
                    row.Mass,
                    row.DiseaseIdx,
                    row.DiseaseCount,
                    SimMessages.ReplaceType.Replace);

                // 属性位也通过 Sim 接口刷新
                SimMessages.ClearCellProperties(cell, byte.MaxValue);
                if (row.Properties != 0)
                    SimMessages.SetCellProperties(cell, row.Properties);

                // 辐射仅有 delta 接口，按当前值补差
                if (Grid.radiation != null)
                {
                    float cur = Grid.radiation[cell];
                    float delta = row.Radiation - cur;
                    if (Mathf.Abs(delta) > 0.0001f)
                        SimMessages.ModifyRadiationOnCell(cell, delta);
                }

                if (Pathfinding.Instance != null)
                    Pathfinding.Instance.AddDirtyNavGridCell(cell);
                applied++;
                WriteRestoreDebugLine(restoreDebugLog, "natural_cell", row.ElementId, row.X, row.Y, targetOffset, cell, $"elementIdx={elementIdx};mass={row.Mass.ToString("R", CultureInfo.InvariantCulture)};temperature={row.Temperature.ToString("R", CultureInfo.InvariantCulture)};radiation={row.Radiation.ToString("R", CultureInfo.InvariantCulture)};zone={zoneId};diseaseIdx={row.DiseaseIdx};diseaseCount={row.DiseaseCount}");
            }

            SyncZoneRenderData(zoneChangedCells, rows, fromWorldId, toWorldId, targetOffset, targetSize);
            bool restoredOverworldCells = RestoreClusterDetailOverworldCellsFromDump(dumpData.OverworldCellRows, toWorldId, targetOffset, targetSize);
            if (!restoredOverworldCells)
                RebuildClusterDetailOverworldCellsForWorldFromDump(rows, fromWorldId, toWorldId, targetOffset, targetSize);

            int queuedBuildings = QueueBuildingsFromDumpForDelayedSpawn(dumpData.BuildingRows, Vector2I.zero, Vector2I.zero, targetOffset, targetSize, toWorldId, restoreDebugLog);
            int queuedGeysers = QueueGeysersFromDumpForDelayedSpawn(dumpData.GeyserRows, Vector2I.zero, Vector2I.zero, targetOffset, targetSize, toWorldId, restoreDebugLog);
            int queuedOtherEntities = QueueOtherEntitiesFromDumpForDelayedSpawn(dumpData.OtherEntityRows, Vector2I.zero, Vector2I.zero, targetOffset, targetSize, toWorldId, restoreDebugLog);
            restoreDebugLog?.Dispose();
            Debug.Log($"[MyWorldDumpMod] Queued delayed buildings/geysers/other entities for restored world. worldId={toWorldId}, buildings={queuedBuildings}, geysers={queuedGeysers}, otherEntities={queuedOtherEntities}");

            return applied > 0;
        }
        private static ushort ResolveElementIdx(GridDumpRow row)
        {
            if (!string.IsNullOrWhiteSpace(row.ElementId))
            {
                SimHashes hash;
                if (Enum.TryParse(row.ElementId, out hash))
                {
                    Element byHash = ElementLoader.FindElementByHash(hash);
                    if (byHash != null)
                        return byHash.idx;
                }
            }
            return row.ElementIdx;
        }
        private static void SyncZoneRenderData(List<int> changedCells, List<GridDumpRow> rows, int fromWorldId, int toWorldId, Vector2I targetOffset, Vector2I targetSize)
        {
            if (changedCells == null || changedCells.Count == 0 || World.Instance == null || World.Instance.zoneRenderData == null)
                return;

            SubworldZoneRenderData zoneRenderData = World.Instance.zoneRenderData;
            if (zoneRenderData.worldZoneTypes == null || zoneRenderData.worldZoneTypes.Length != Grid.CellCount)
                return;

            FieldInfo colourTexField = typeof(SubworldZoneRenderData).GetField("colourTex", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo indexTexField = typeof(SubworldZoneRenderData).GetField("indexTex", BindingFlags.Instance | BindingFlags.NonPublic);
            Texture2D colourTex = colourTexField != null ? colourTexField.GetValue(zoneRenderData) as Texture2D : null;
            Texture2D indexTex = indexTexField != null ? indexTexField.GetValue(zoneRenderData) as Texture2D : null;
            if (colourTex == null || indexTex == null)
                return;

            byte[] colourBytes = colourTex.GetRawTextureData();
            byte[] indexBytes = indexTex.GetRawTextureData();
            bool dirty = false;

            foreach (GridDumpRow row in rows)
            {
                if (row.WorldId != fromWorldId)
                    continue;

                int targetX = targetOffset.x + row.X;
                int targetY = targetOffset.y + row.Y;
                if (row.X < 0 || row.Y < 0 || row.X >= targetSize.x || row.Y >= targetSize.y)
                    continue;
                int targetCell = Grid.XYToCell(targetX, targetY);
                if (!Grid.IsValidCell(targetCell))
                    continue;
                if (!Grid.IsValidCellInWorld(targetCell, toWorldId))
                    continue;

                SubWorld.ZoneType zoneType = row.ZoneId == byte.MaxValue ? SubWorld.ZoneType.Space : (SubWorld.ZoneType)row.ZoneId;
                zoneRenderData.worldZoneTypes[targetCell] = zoneType;
                indexBytes[targetCell] = zoneType == SubWorld.ZoneType.Space ? byte.MaxValue : (byte)zoneRenderData.zoneTextureArrayIndices[(int)zoneType];
                Color32 zoneColour = zoneRenderData.zoneColours[(int)zoneType];
                int colourIndex = targetCell * 3;
                if (colourIndex + 2 >= colourBytes.Length)
                    continue;
                colourBytes[colourIndex] = zoneColour.r;
                colourBytes[colourIndex + 1] = zoneColour.g;
                colourBytes[colourIndex + 2] = zoneColour.b;
                dirty = true;
            }

            if (!dirty)
                return;

            colourTex.LoadRawTextureData(colourBytes);
            indexTex.LoadRawTextureData(indexBytes);
            colourTex.Apply();
            indexTex.Apply();
            Shader.SetGlobalTexture("_WorldZoneTex", colourTex);
            Shader.SetGlobalTexture("_WorldZoneIndexTex", indexTex);
        }
        private static bool PolyOverlapsRect(Polygon poly, int minX, int minY, int maxX, int maxY)
        {
            if (poly == null)
                return false;
            Rect b = poly.bounds;
            return b.xMin < maxX && b.xMax > minX && b.yMin < maxY && b.yMax > minY;
        }
        private static List<Vector2> TryGetPolygonVertices(Polygon poly)
        {
            if (poly == null)
                return null;

            BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            foreach (FieldInfo f in typeof(Polygon).GetFields(flags))
            {
                object value = f.GetValue(poly);
                if (value is List<Vector2> vecList && vecList.Count >= 3)
                    return new List<Vector2>(vecList);
                if (value is Vector2[] vecArray && vecArray.Length >= 3)
                    return new List<Vector2>(vecArray);
            }
            foreach (PropertyInfo p in typeof(Polygon).GetProperties(flags))
            {
                if (p.GetIndexParameters().Length != 0)
                    continue;
                object value;
                try { value = p.GetValue(poly, null); }
                catch { continue; }
                if (value is List<Vector2> vecList && vecList.Count >= 3)
                    return new List<Vector2>(vecList);
                if (value is Vector2[] vecArray && vecArray.Length >= 3)
                    return new List<Vector2>(vecArray);
            }

            return null;
        }
        private static SubWorld.ZoneType ZoneByteToZoneType(byte zone)
        {
            if (zone == byte.MaxValue)
                return SubWorld.ZoneType.Space;
            int zi = zone;
            if (!Enum.IsDefined(typeof(SubWorld.ZoneType), zi))
                return SubWorld.ZoneType.Sandstone;
            return (SubWorld.ZoneType)zi;
        }
        private static void RebuildClusterDetailOverworldCellsForWorldFromDump(List<GridDumpRow> rows, int fromWorldId, int toWorldId, Vector2I targetOffset, Vector2I targetSize)
        {
            if (SaveLoader.Instance == null || SaveLoader.Instance.clusterDetailSave == null || rows == null)
                return;

            List<Klei.WorldDetailSave.OverworldCell> overworldCells = SaveLoader.Instance.clusterDetailSave.overworldCells;
            if (overworldCells == null)
                return;

            int width = targetSize.x;
            int height = targetSize.y;
            if (width <= 0 || height <= 0)
                return;

            int minX = targetOffset.x;
            int minY = targetOffset.y;
            int maxX = minX + width;
            int maxY = minY + height;

            for (int i = overworldCells.Count - 1; i >= 0; i--)
            {
                Klei.WorldDetailSave.OverworldCell c = overworldCells[i];
                if (c == null || c.poly == null)
                    continue;
                if (PolyOverlapsRect(c.poly, minX, minY, maxX, maxY))
                    overworldCells.RemoveAt(i);
            }

            byte[] zoneGrid = new byte[width * height];
            for (int i = 0; i < zoneGrid.Length; i++)
                zoneGrid[i] = byte.MaxValue;

            foreach (GridDumpRow row in rows)
            {
                if (row.WorldId != fromWorldId)
                    continue;
                if (row.X < 0 || row.Y < 0 || row.X >= width || row.Y >= height)
                    continue;
                zoneGrid[row.Y * width + row.X] = row.ZoneId;
            }

            int added = 0;
            for (int y = 0; y < height; y++)
            {
                int x = 0;
                while (x < width)
                {
                    byte zone = zoneGrid[y * width + x];
                    int xStart = x;
                    while (x + 1 < width && zoneGrid[y * width + (x + 1)] == zone)
                        x++;
                    int xEnd = x;

                    float gx0 = minX + xStart;
                    float gx1 = minX + xEnd + 1;
                    float gy0 = minY + y + 0.25f;
                    float gy1 = minY + y + 1.25f;
                    List<Vector2> verts = new List<Vector2>(4)
                    {
                        new Vector2(gx0, gy0),
                        new Vector2(gx1, gy0),
                        new Vector2(gx1, gy1),
                        new Vector2(gx0, gy1)
                    };

                    Klei.WorldDetailSave.OverworldCell c = new Klei.WorldDetailSave.OverworldCell();
                    c.poly = new Polygon(verts);
                    c.zoneType = ZoneByteToZoneType(zone);
                    c.tags = new TagSet();
                    overworldCells.Add(c);
                    added++;

                    x++;
                }
            }

            if (World.Instance != null && World.Instance.zoneRenderData != null)
            {
                World.Instance.zoneRenderData.GenerateTexture();
                World.Instance.zoneRenderData.OnActiveWorldChanged();
            }

            Debug.Log($"[MyWorldDumpMod] Rebuilt clusterDetail overworldCells for world={toWorldId}, added={added}.");
        }
        private static bool RestoreClusterDetailOverworldCellsFromDump(List<OverworldCellDumpRow> rows, int toWorldId, Vector2I targetOffset, Vector2I targetSize)
        {
            if (rows == null || rows.Count == 0 || SaveLoader.Instance == null || SaveLoader.Instance.clusterDetailSave == null)
                return false;

            List<Klei.WorldDetailSave.OverworldCell> overworldCells = SaveLoader.Instance.clusterDetailSave.overworldCells;
            if (overworldCells == null)
                return false;

            int minX = targetOffset.x;
            int minY = targetOffset.y;
            int maxX = minX + targetSize.x;
            int maxY = minY + targetSize.y;
            for (int i = overworldCells.Count - 1; i >= 0; i--)
            {
                Klei.WorldDetailSave.OverworldCell c = overworldCells[i];
                if (c == null || c.poly == null)
                    continue;
                if (PolyOverlapsRect(c.poly, minX, minY, maxX, maxY))
                    overworldCells.RemoveAt(i);
            }

            int added = 0;
            foreach (OverworldCellDumpRow row in rows)
            {
                if (row.LocalVertices == null || row.LocalVertices.Count < 3)
                    continue;

                List<Vector2> globalVerts = new List<Vector2>(row.LocalVertices.Count);
                for (int i = 0; i < row.LocalVertices.Count; i++)
                    globalVerts.Add(new Vector2(row.LocalVertices[i].x + targetOffset.x, row.LocalVertices[i].y + targetOffset.y));

                Klei.WorldDetailSave.OverworldCell c = new Klei.WorldDetailSave.OverworldCell();
                c.poly = new Polygon(globalVerts);
                c.zoneType = ZoneByteToZoneType(row.ZoneId);
                c.tags = new TagSet();
                overworldCells.Add(c);
                added++;
            }

            if (World.Instance != null && World.Instance.zoneRenderData != null)
            {
                World.Instance.zoneRenderData.GenerateTexture();
                World.Instance.zoneRenderData.OnActiveWorldChanged();
            }

            Debug.Log($"[MyWorldDumpMod] Restored clusterDetail overworldCells from dump for world={toWorldId}, added={added}.");
            return added > 0;
        }
        private static void EnsureTargetWorldIdxAssigned(int worldId, Vector2I worldOffset, Vector2I worldSize)
        {
            int xEnd = worldOffset.x + worldSize.x;
            int yEnd = worldOffset.y + worldSize.y;
            for (int y = worldOffset.y; y < yEnd; y++)
            {
                for (int x = worldOffset.x; x < xEnd; x++)
                {
                    int cell = Grid.XYToCell(x, y);
                    if (!Grid.IsValidCell(cell))
                        continue;
                    Grid.WorldIdx[cell] = (byte)worldId;
                }
            }
        }
        private class StarmapWorldSnapshot
        {
            public int WorldId;
            public Vector2I WorldOffset;
            public Vector2I WorldSize;
            public int HiddenYOffset;
            public bool IsModuleInterior;
            public bool IsDiscovered;
            public bool IsStartWorld;
            public bool IsDupeVisited;
            public float DupeVisitedTimestamp;
            public float DiscoveryTimestamp;
            public string WorldName;
            public string[] NameTables;
            public Tag[] WorldTags;
            public string OverrideName;
            public string WorldType;
            public string WorldDescription;
            public List<string> SeasonIds;
            public List<string> SubworldNames;
            public List<string> WorldTraitIds;
            public List<string> StoryTraitIds;
            public List<string> GeneratedSubworlds;
            public AxialI ClusterLocation;
            public string AsteroidName;
            public string AsteroidAnim;
        }
        private class InjectedWorldMapping
        {
            public int SourceWorldId;
            public int TargetWorldId;
        }
        private static int AllocateMinAvailableWorldId(HashSet<int> usedIds)
        {
            for (int id = 0; id < byte.MaxValue; id++)
            {
                if (!usedIds.Contains(id))
                {
                    usedIds.Add(id);
                    return id;
                }
            }
            return -1;
        }
        private static StarmapWorldSnapshot BuildStarmapWorldSnapshot(WorldContainer source, AsteroidGridEntity sourceAsteroid)
        {
            FieldInfo asteroidAnimField = typeof(AsteroidGridEntity).GetField("m_asteroidAnim", BindingFlags.Instance | BindingFlags.NonPublic);
            return new StarmapWorldSnapshot
            {
                WorldId = source.id,
                WorldOffset = source.WorldOffset,
                WorldSize = source.WorldSize,
                HiddenYOffset = source.HiddenYOffset,
                IsModuleInterior = source.IsModuleInterior,
                IsDiscovered = source.IsDiscovered,
                IsStartWorld = source.IsStartWorld,
                IsDupeVisited = source.IsDupeVisited,
                DupeVisitedTimestamp = source.DupeVisitedTimestamp,
                DiscoveryTimestamp = source.DiscoveryTimestamp,
                WorldName = source.worldName,
                NameTables = source.nameTables,
                WorldTags = source.worldTags,
                OverrideName = source.overrideName,
                WorldType = source.worldType,
                WorldDescription = source.worldDescription,
                SeasonIds = source.GetSeasonIds() != null ? new List<string>(source.GetSeasonIds()) : new List<string>(),
                SubworldNames = new List<string>(source.Biomes ?? new List<string>()),
                WorldTraitIds = new List<string>(source.WorldTraitIds ?? new List<string>()),
                StoryTraitIds = new List<string>(source.StoryTraitIds ?? new List<string>()),
                GeneratedSubworlds = new List<string>(source.GeneratedBiomes ?? new List<string>()),
                ClusterLocation = sourceAsteroid.Location,
                AsteroidName = sourceAsteroid.Name,
                AsteroidAnim = asteroidAnimField != null ? asteroidAnimField.GetValue(sourceAsteroid) as string : null
            };
        }
        private static void ShuffleInPlace<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        private static bool TryCaptureStarmapWorldSnapshotAndDump(WorldContainer source, string logContext)
        {
            if (source == null)
                return false;

            AsteroidGridEntity sourceAsteroid = source.GetComponent<AsteroidGridEntity>();
            if (sourceAsteroid == null)
                return false;

            string dumpPath = DumpGrids(source.id);
            if (string.IsNullOrWhiteSpace(dumpPath))
            {
                Debug.LogWarning($"[MyWorldDumpMod] Failed to dump {logContext} world id={source.id}; snapshot skipped.");
                return false;
            }

            CapturedStarmapWorlds.Add(BuildStarmapWorldSnapshot(source, sourceAsteroid));
            DumpedFilesByWorldId[source.id] = dumpPath;
            return true;
        }

        private static WorldContainer FindHeadquartersStarmapWorld()
        {
            if (ClusterManager.Instance == null || ClusterManager.Instance.WorldContainers == null)
                return null;

            foreach (BuildingComplete building in Components.BuildingCompletes.Items)
            {
                if (building == null || building.Def == null)
                    continue;
                if (!string.Equals(building.Def.PrefabID, "Headquarters", StringComparison.Ordinal))
                    continue;

                int cell = Grid.PosToCell((KMonoBehaviour)building);
                if (!Grid.IsValidCell(cell))
                    continue;

                int worldId = Grid.WorldIdx[cell];
                WorldContainer world = ClusterManager.Instance.GetWorld(worldId);
                if (world == null || world.IsModuleInterior || world.GetComponent<AsteroidGridEntity>() == null)
                    continue;

                return world;
            }

            return null;
        }
        private static int CaptureHeadquartersAndRandomStarmapWorldSnapshotsAndDump()
        {
            CapturedStarmapWorlds.Clear();
            DumpedFilesByWorldId.Clear();
            RestoredDumpDataByWorldId.Clear();

            if (ClusterManager.Instance == null || ClusterManager.Instance.WorldContainers == null)
            {
                Debug.LogWarning("[MyWorldDumpMod] ClusterManager unavailable; Headquarters plus random starmap snapshot capture skipped.");
                return 0;
            }

            int captured = 0;
            HashSet<int> capturedWorldIds = new HashSet<int>();
            WorldContainer headquartersWorld = FindHeadquartersStarmapWorld();
            if (headquartersWorld != null && TryCaptureStarmapWorldSnapshotAndDump(headquartersWorld, "Headquarters"))
            {
                captured++;
                capturedWorldIds.Add(headquartersWorld.id);
            }
            else
            {
                Debug.LogWarning("[MyWorldDumpMod] No eligible Headquarters world captured; continuing with random starmap worlds.");
            }

            List<WorldContainer> eligibleWorlds = new List<WorldContainer>();
            foreach (WorldContainer wc in ClusterManager.Instance.WorldContainers)
            {
                if (wc == null || wc.IsModuleInterior)
                    continue;
                if (capturedWorldIds.Contains(wc.id))
                    continue;
                if (wc.GetComponent<AsteroidGridEntity>() == null)
                    continue;
                eligibleWorlds.Add(wc);
            }

            int randomPickCount = eligibleWorlds.Count > 0 ? UnityEngine.Random.Range(1, Mathf.Min(4, eligibleWorlds.Count) + 1) : 0;
            ShuffleInPlace(eligibleWorlds);
            int randomCaptured = 0;
            for (int i = 0; i < eligibleWorlds.Count && randomCaptured < randomPickCount; i++)
            {
                WorldContainer source = eligibleWorlds[i];
                if (TryCaptureStarmapWorldSnapshotAndDump(source, "random"))
                {
                    captured++;
                    randomCaptured++;
                    capturedWorldIds.Add(source.id);
                }
            }

            Debug.Log($"[MyWorldDumpMod] Captured Headquarters plus random starmap snapshots. headquartersWorldId={(headquartersWorld != null ? headquartersWorld.id.ToString() : "none")}, randomRequested={randomPickCount}, randomCaptured={randomCaptured}, captured={captured}, worldIds={string.Join(",", CapturedStarmapWorlds.Select(s => s.WorldId).ToArray())}");
            return captured;
        }
        private static int CaptureRandomStarmapWorldSnapshotsAndDump()
        {
            CapturedStarmapWorlds.Clear();
            DumpedFilesByWorldId.Clear();
            RestoredDumpDataByWorldId.Clear();

            if (ClusterManager.Instance == null || ClusterManager.Instance.WorldContainers == null)
            {
                Debug.LogWarning("[MyWorldDumpMod] ClusterManager unavailable; starmap snapshot capture skipped.");
                return 0;
            }

            List<WorldContainer> eligibleWorlds = new List<WorldContainer>();
            foreach (WorldContainer wc in ClusterManager.Instance.WorldContainers)
            {
                if (wc == null || wc.IsModuleInterior)
                    continue;
                if (wc.GetComponent<AsteroidGridEntity>() == null)
                    continue;
                eligibleWorlds.Add(wc);
            }

            if (eligibleWorlds.Count == 0)
            {
                Debug.LogWarning("[MyWorldDumpMod] No eligible worlds found for random dump capture.");
                return 0;
            }

            int pickCount = UnityEngine.Random.Range(1, Mathf.Min(4, eligibleWorlds.Count) + 1);
            ShuffleInPlace(eligibleWorlds);

            int captured = 0;
            for (int i = 0; i < eligibleWorlds.Count && captured < pickCount; i++)
            {
                WorldContainer source = eligibleWorlds[i];
                AsteroidGridEntity sourceAsteroid = source.GetComponent<AsteroidGridEntity>();
                if (sourceAsteroid == null)
                    continue;

                string dumpPath = DumpGrids(source.id);
                if (string.IsNullOrWhiteSpace(dumpPath))
                {
                    Debug.LogWarning($"[MyWorldDumpMod] Failed to dump world id={source.id}; snapshot skipped.");
                    continue;
                }

                CapturedStarmapWorlds.Add(BuildStarmapWorldSnapshot(source, sourceAsteroid));
                DumpedFilesByWorldId[source.id] = dumpPath;
                captured++;
            }

            Debug.Log($"[MyWorldDumpMod] Captured random starmap snapshots. requested={pickCount}, captured={captured}, worldIds={string.Join(",", CapturedStarmapWorlds.Select(s => s.WorldId).ToArray())}");
            return captured;
        }
        private static bool IsSuitableAsteroidSpawnLocation(AxialI location, HashSet<AxialI> reservedLocations)
        {
            if (ClusterGrid.Instance == null || !ClusterGrid.Instance.IsValidCell(location))
                return false;

            List<ClusterGridEntity> entities = ClusterGrid.Instance.GetEntitiesOnCell(location);
            if (entities != null)
            {
                for (int i = 0; i < entities.Count; i++)
                {
                    ClusterGridEntity e = entities[i];
                    if (e == null)
                        continue;
                    if (e.Layer == EntityLayer.Asteroid || e.Layer == EntityLayer.POI)
                        return false;
                }
            }

            List<AxialI> protectedLocations = GetProtectedStarmapLocationsForAsteroidPlacement();
            if (reservedLocations != null)
            {
                foreach (AxialI reserved in reservedLocations)
                    AddUniqueLocation(protectedLocations, reserved);
            }

            return IsFarEnoughFromLocations(location, protectedLocations, AsteroidStarmapMinDistance);
        }
        private static bool TryPickRandomAsteroidLocation(HashSet<AxialI> reservedLocations, out AxialI location)
        {
            location = AxialI.INVALID;
            if (ClusterGrid.Instance == null || ClusterGrid.Instance.cellContents == null)
                return false;

            List<AxialI> candidates = new List<AxialI>();
            foreach (KeyValuePair<AxialI, List<ClusterGridEntity>> kv in ClusterGrid.Instance.cellContents)
            {
                if (IsSuitableAsteroidSpawnLocation(kv.Key, reservedLocations))
                    candidates.Add(kv.Key);
            }

            while (candidates.Count > 0)
            {
                int pick = UnityEngine.Random.Range(0, candidates.Count);
                AxialI candidate = candidates[pick];
                if (IsSuitableAsteroidSpawnLocation(candidate, reservedLocations))
                {
                    location = candidate;
                    return true;
                }

                candidates.RemoveAt(pick);
            }

            return false;
        }
        private static List<InjectedWorldMapping> InjectCapturedStarmapWorldsIntoCurrentSave()
        {
            List<InjectedWorldMapping> injectedMappings = new List<InjectedWorldMapping>();
            if (CapturedStarmapWorlds.Count == 0)
            {
                Debug.LogWarning("[MyWorldDumpMod] No captured starmap snapshots to inject.");
                return injectedMappings;
            }

            HashSet<AxialI> reservedLocations = new HashSet<AxialI>();
            HashSet<int> usedWorldIds = new HashSet<int>();
            if (ClusterManager.Instance != null && ClusterManager.Instance.WorldContainers != null)
            {
                foreach (WorldContainer wc in ClusterManager.Instance.WorldContainers)
                {
                    if (wc != null)
                        usedWorldIds.Add(wc.id);
                }
            }

            for (int i = 0; i < CapturedStarmapWorlds.Count; i++)
            {
                StarmapWorldSnapshot snapshot = CapturedStarmapWorlds[i];
                int injectedWorldId = AllocateMinAvailableWorldId(usedWorldIds);
                if (injectedWorldId < 0)
                {
                    Debug.LogWarning($"[MyWorldDumpMod] No available world id for source world id={snapshot.WorldId}; injection skipped.");
                    continue;
                }

                AxialI randomLocation;
                if (!TryPickRandomAsteroidLocation(reservedLocations, out randomLocation))
                {
                    Debug.LogWarning($"[MyWorldDumpMod] No free starmap location for source world id={snapshot.WorldId}; injection skipped.");
                    usedWorldIds.Remove(injectedWorldId);
                    continue;
                }

                Vector2I allocatedOffset;
                bool allocated = Grid.GetFreeGridSpace(snapshot.WorldSize, out allocatedOffset);
                if (!allocated)
                {
                    usedWorldIds.Remove(injectedWorldId);
                    Debug.LogWarning($"[MyWorldDumpMod] No free grid space for source world id={snapshot.WorldId}, target world id={injectedWorldId}, size={snapshot.WorldSize}; injection and restore skipped.");
                    continue;
                }

                GameObject asteroidGO = Util.KInstantiate(Assets.GetPrefab((Tag)"Asteroid"));
                asteroidGO.SetActive(false);

                WorldContainer wc = asteroidGO.GetComponent<WorldContainer>();
                wc.SetID(injectedWorldId);
                wc.worldName = snapshot.WorldName;
                wc.nameTables = snapshot.NameTables;
                wc.worldTags = snapshot.WorldTags;
                wc.overrideName = snapshot.OverrideName;
                wc.worldType = snapshot.WorldType;
                wc.worldDescription = snapshot.WorldDescription;

                typeof(WorldContainer).GetField("worldOffset", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(wc, allocatedOffset);
                typeof(WorldContainer).GetField("worldSize", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(wc, snapshot.WorldSize);
                typeof(WorldContainer).GetField("hiddenYOffset", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(wc, snapshot.HiddenYOffset);
                typeof(WorldContainer).GetField("isModuleInterior", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(wc, snapshot.IsModuleInterior);
                typeof(WorldContainer).GetField("isDiscovered", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(wc, snapshot.IsDiscovered);
                typeof(WorldContainer).GetField("isStartWorld", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(wc, snapshot.IsStartWorld);
                typeof(WorldContainer).GetField("isDupeVisited", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(wc, snapshot.IsDupeVisited);
                typeof(WorldContainer).GetField("dupeVisitedTimestamp", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(wc, snapshot.DupeVisitedTimestamp);
                typeof(WorldContainer).GetField("discoveryTimestamp", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(wc, snapshot.DiscoveryTimestamp);
                typeof(WorldContainer).GetField("m_seasonIds", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(wc, snapshot.SeasonIds != null ? new List<string>(snapshot.SeasonIds) : new List<string>());
                typeof(WorldContainer).GetField("m_subworldNames", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(wc, snapshot.SubworldNames != null ? new List<string>(snapshot.SubworldNames) : new List<string>());
                typeof(WorldContainer).GetField("m_worldTraitIds", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(wc, snapshot.WorldTraitIds != null ? new List<string>(snapshot.WorldTraitIds) : new List<string>());
                typeof(WorldContainer).GetField("m_storyTraitIds", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(wc, snapshot.StoryTraitIds != null ? new List<string>(snapshot.StoryTraitIds) : new List<string>());
                typeof(WorldContainer).GetField("m_generatedSubworlds", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(wc, snapshot.GeneratedSubworlds != null ? new List<string>(snapshot.GeneratedSubworlds) : new List<string>());

                AsteroidGridEntity asteroid = asteroidGO.GetComponent<AsteroidGridEntity>();
                asteroid.Init(snapshot.AsteroidName, randomLocation, snapshot.AsteroidAnim);
                asteroidGO.SetActive(true);

                reservedLocations.Add(randomLocation);
                injectedMappings.Add(new InjectedWorldMapping
                {
                    SourceWorldId = snapshot.WorldId,
                    TargetWorldId = injectedWorldId
                });
            }

            SyncSimWorldOffsetsFromCluster("inject_random_worlds");
            if (ClusterMapScreen.Instance != null)
                ClusterMapScreen.Instance.Trigger((int)GameHashes.UIRefresh, null);

            Debug.Log($"[MyWorldDumpMod] Injected starmap data into current save. count={injectedMappings.Count}, mappings={string.Join(",", injectedMappings.Select(m => $"{m.SourceWorldId}->{m.TargetWorldId}").ToArray())}");
            return injectedMappings;
        }
        private static void SyncSimWorldOffsetsFromCluster(string reason)
        {
            if (ClusterManager.Instance == null)
                return;
            try
            {
                List<SimMessages.WorldOffsetData> worldOffsets = ClusterManager.Instance.WorldContainers
                    .Where(wc => wc != null && wc.WorldSize.x > 0 && wc.WorldSize.y > 0)
                    .Select(wc => new SimMessages.WorldOffsetData
                    {
                        worldOffsetX = wc.WorldOffset.x,
                        worldOffsetY = wc.WorldOffset.y,
                        worldSizeX = wc.WorldSize.x,
                        worldSizeY = wc.WorldSize.y
                    })
                    .ToList();
                SimMessages.DefineWorldOffsets(worldOffsets);
                Debug.Log($"[MyWorldDumpMod] Synced Sim world offsets. reason={reason}, count={worldOffsets.Count}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MyWorldDumpMod] SyncSimWorldOffsetsFromCluster failed. reason={reason}, error={ex.Message}");
            }
        }

        private static void RehideRestoredWorldsAndResetStarmapFog()
        {
            if (ClusterManager.Instance == null)
                return;

            int hiddenCellCount = 0;
            foreach (int worldId in RestoredDumpDataByWorldId.Keys)
            {
                WorldContainer world = ClusterManager.Instance.GetWorld(worldId);
                if (world == null)
                    continue;

                SetWorldDiscoveredState(world, false);
                for (int cell = 0; cell < Grid.CellCount; cell++)
                {
                    if (!Grid.IsValidCellInWorld(cell, worldId))
                        continue;
                    if (Grid.WorldIdx[cell] != worldId)
                        continue;

                    Grid.Visible[cell] = 0;
                    Grid.Spawnable[cell] = 0;
                    hiddenCellCount++;
                }
            }

            ResetClusterFogToPreservedWorldRadius(3);
            if (ClusterMapScreen.Instance != null)
                ClusterMapScreen.Instance.Trigger((int)GameHashes.UIRefresh, null);

            Debug.Log($"[MyWorldDumpMod] Rehid restored worlds and reset starmap fog. worlds={RestoredDumpDataByWorldId.Count}, hiddenCells={hiddenCellCount}, preservedWorldId={PreservedWorldIdDuringClear}");
        }

        private static void SetWorldDiscoveredState(WorldContainer world, bool discovered)
        {
            if (world == null)
                return;

            SetPrivateField(world, "isDiscovered", discovered);
            SetPrivateField(world, "discoveryTimestamp", discovered ? GameUtil.GetCurrentTimeInCycles() : -1f);
            if (!discovered)
            {
                SetPrivateField(world, "isDupeVisited", false);
                SetPrivateField(world, "dupeVisitedTimestamp", -1f);
                SetPrivateField(world, "isSurfaceRevealed", false);
            }
        }

        private static void SetPrivateField(object target, string fieldName, object value)
        {
            if (target == null)
                return;

            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field != null)
                field.SetValue(target, value);
        }

        private static void ResetClusterFogToPreservedWorldRadius(int radius)
        {
            ClusterFogOfWarManager.Instance fow = SaveGame.Instance != null ? SaveGame.Instance.GetSMI<ClusterFogOfWarManager.Instance>() : null;
            if (fow == null)
                return;

            FieldInfo revealPointsField = typeof(ClusterFogOfWarManager.Instance).GetField("m_revealPointsByCell", BindingFlags.Instance | BindingFlags.NonPublic);
            Dictionary<AxialI, float> revealPoints = revealPointsField != null ? revealPointsField.GetValue(fow) as Dictionary<AxialI, float> : null;
            if (revealPoints != null)
            {
                revealPoints.Clear();
            }
            else if (revealPointsField != null)
            {
                revealPointsField.SetValue(fow, new Dictionary<AxialI, float>());
            }

            WorldContainer preservedWorld = ClusterManager.Instance != null && PreservedWorldIdDuringClear >= 0 ? ClusterManager.Instance.GetWorld(PreservedWorldIdDuringClear) : null;
            ClusterGridEntity preservedEntity = preservedWorld != null ? preservedWorld.GetComponent<ClusterGridEntity>() : null;
            if (preservedEntity != null && preservedEntity.Location != AxialI.INVALID)
                fow.RevealLocation(preservedEntity.Location, radius, 0);
        }

        private static readonly AccessTools.FieldRef<WorldGenSpawner, TemplateClasses.Prefab[]> WorldGenSpawner_spawnInfos_Field = AccessTools.FieldRefAccess<WorldGenSpawner, TemplateClasses.Prefab[]>("spawnInfos");
        private static readonly AccessTools.FieldRef<WorldGenSpawner, bool> WorldGenSpawner_hasPlacedTemplates_Field = AccessTools.FieldRefAccess<WorldGenSpawner, bool>("hasPlacedTemplates");
        private static readonly AccessTools.FieldRef<WorldGenSpawner, List<WorldGenSpawner.Spawnable>> WorldGenSpawner_spawnables_Field = AccessTools.FieldRefAccess<WorldGenSpawner, List<WorldGenSpawner.Spawnable>>("spawnables");
        private static void InstallPendingDelayedSpawnPrefabs(string reason)
        {
            if (PendingDelayedSpawnPrefabs.Count == 0)
            {
                Debug.Log($"[MyWorldDumpMod] No delayed spawn prefabs to install. reason={reason}");
                return;
            }

            WorldGenSpawner spawner = SaveGame.Instance != null ? SaveGame.Instance.worldGenSpawner : null;
            if (spawner == null)
            {
                Debug.LogWarning($"[MyWorldDumpMod] Delayed spawn install skipped because WorldGenSpawner is missing. reason={reason}, pending={PendingDelayedSpawnPrefabs.Count}");
                return;
            }

            List<WorldGenSpawner.Spawnable> spawnables = WorldGenSpawner_spawnables_Field(spawner);
            if (spawnables == null)
            {
                spawnables = new List<WorldGenSpawner.Spawnable>();
                WorldGenSpawner_spawnables_Field(spawner) = spawnables;
            }

            int installed = 0;
            for (int i = 0; i < PendingDelayedSpawnPrefabs.Count; i++)
            {
                TemplateClasses.Prefab prefab = PendingDelayedSpawnPrefabs[i];
                if (prefab == null || string.IsNullOrWhiteSpace(prefab.id))
                    continue;

                spawnables.Add(new WorldGenSpawner.Spawnable(prefab));
                installed++;
            }

            List<TemplateClasses.Prefab> unspawned = new List<TemplateClasses.Prefab>();
            for (int i = 0; i < spawnables.Count; i++)
            {
                WorldGenSpawner.Spawnable spawnable = spawnables[i];
                if (spawnable != null && !spawnable.isSpawned)
                    unspawned.Add(spawnable.spawnInfo);
            }
            WorldGenSpawner_spawnInfos_Field(spawner) = unspawned.ToArray();

            PendingDelayedSpawnPrefabs.Clear();
            Debug.Log($"[MyWorldDumpMod] Installed delayed spawn prefabs. reason={reason}, installed={installed}, unspawned={unspawned.Count}");
        }
        private static void SanitizeWorldGenSpawnerAfterWorldLayoutChanges(string reason)
        {
            try
            {
                WorldGenSpawner[] spawners = UnityEngine.Object.FindObjectsByType<WorldGenSpawner>(FindObjectsSortMode.None);
                if (spawners == null || spawners.Length == 0)
                {
                    Debug.Log($"[MyWorldDumpMod] WorldGenSpawner sanitize skipped. reason={reason}, count=0");
                    return;
                }

                int sanitized = 0;
                foreach (WorldGenSpawner spawner in spawners)
                {
                    if (spawner == null)
                        continue;

                    // Keep OnSpawn from rebuilding from stale worldgen template state after offsets/size changes.
                    WorldGenSpawner_hasPlacedTemplates_Field(spawner) = true;
                    WorldGenSpawner_spawnInfos_Field(spawner) = Array.Empty<TemplateClasses.Prefab>();
                    WorldGenSpawner_spawnables_Field(spawner) = new List<WorldGenSpawner.Spawnable>();
                    sanitized++;
                }

                Debug.Log($"[MyWorldDumpMod] Sanitized WorldGenSpawner state. reason={reason}, spawners={sanitized}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MyWorldDumpMod] WorldGenSpawner sanitize failed. reason={reason}, error={ex}");
            }
        }
        private static unsafe void ReinitializeSimFromCurrentGridUsingGridTempAsBgTemp()
        {
            try
            {
                int width = Grid.WidthInCells;
                int height = Grid.HeightInCells;
                int cellCount = Grid.CellCount;

                Sim.Cell[] cells = new Sim.Cell[cellCount];
                Sim.DiseaseCell[] diseases = new Sim.DiseaseCell[cellCount];
                float[] bgTemp = new float[cellCount];

                for (int cell = 0; cell < cellCount; cell++)
                {
                    float mass = Grid.mass[cell];
                    float temp = Grid.temperature[cell];
                    if (mass > 0f && temp <= 0f)
                    {
                        Element elem = Grid.Element[cell];
                        temp = elem != null ? elem.defaultValues.temperature : 293.15f;
                    }

                    cells[cell].elementIdx = Grid.elementIdx[cell];
                    cells[cell].properties = Grid.properties[cell];
                    cells[cell].insulation = Grid.insulation[cell];
                    cells[cell].strengthInfo = Grid.strengthInfo[cell];
                    cells[cell].pad0 = 0;
                    cells[cell].pad1 = 0;
                    cells[cell].pad2 = 0;
                    cells[cell].temperature = temp;
                    cells[cell].mass = mass;

                    bgTemp[cell] = temp;

                    diseases[cell] = new Sim.DiseaseCell
                    {
                        diseaseIdx = Grid.diseaseIdx[cell],
                        elementCount = Grid.diseaseCount[cell]
                    };
                }

                SimMessages.SimDataInitializeFromCells(width, height, 0u, cells, bgTemp, diseases, false);
                Debug.Log("[MyWorldDumpMod] WaitFrames==30: called SimDataInitializeFromCells using current Grid as source and Grid temperature as bgTemp.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MyWorldDumpMod] WaitFrames==30: SimDataInitializeFromCells failed: {ex}");
            }
        }
        private static bool IsHarvestableSpacePoiPlacementId(string prefabId)
        {
            if (string.IsNullOrEmpty(prefabId))
                return false;

            if (prefabId.StartsWith("HarvestableSpacePOI_", StringComparison.Ordinal))
                return true;

            GameObject prefab = Assets.GetPrefab((Tag)prefabId);
            return prefab != null && prefab.GetComponent<HarvestablePOIClusterGridEntity>() != null;
        }
        private static readonly string[] RandomHarvestablePoiPrefabIds = new string[]
        {
            "HarvestableSpacePOI_CarbonAsteroidField",
            "HarvestableSpacePOI_MetallicAsteroidField",
            "HarvestableSpacePOI_SatelliteField",
            "HarvestableSpacePOI_RockyAsteroidField",
            "HarvestableSpacePOI_InterstellarIceField",
            "HarvestableSpacePOI_OrganicMassField",
            "HarvestableSpacePOI_IceAsteroidField",
            "HarvestableSpacePOI_GasGiantCloud",
            "HarvestableSpacePOI_ChlorineCloud",
            "HarvestableSpacePOI_GildedAsteroidField",
            "HarvestableSpacePOI_GlimmeringAsteroidField",
            "HarvestableSpacePOI_HeliumCloud",
            "HarvestableSpacePOI_OilyAsteroidField",
            "HarvestableSpacePOI_OxidizedAsteroidField",
            "HarvestableSpacePOI_SaltyAsteroidField",
            "HarvestableSpacePOI_FrozenOreField",
            "HarvestableSpacePOI_ForestyOreField",
            "HarvestableSpacePOI_SwampyOreField",
            "HarvestableSpacePOI_SandyOreField",
            "HarvestableSpacePOI_RadioactiveGasCloud",
            "HarvestableSpacePOI_RadioactiveAsteroidField",
            "HarvestableSpacePOI_OxygenRichAsteroidField",
            "HarvestableSpacePOI_InterstellarOcean",
            "HarvestableSpacePOI_DLC2CeresField",
            "HarvestableSpacePOI_DLC2CeresOreField",
            "HarvestableSpacePOI_DLC4PrehistoricMixingField",
            "HarvestableSpacePOI_DLC4PrehistoricOreField",
            "HarvestableSpacePOI_DLC4ImpactorDebrisField1",
            "HarvestableSpacePOI_DLC4ImpactorDebrisField2",
            "HarvestableSpacePOI_DLC4ImpactorDebrisField3"
        };
        private static bool IsSuitableHarvestablePoiSpawnLocation(AxialI location)
        {
            if (ClusterGrid.Instance == null || !ClusterGrid.Instance.IsValidCell(location))
                return false;

            List<ClusterGridEntity> entities = ClusterGrid.Instance.GetEntitiesOnCell(location);
            if (entities == null)
                return true;

            for (int i = 0; i < entities.Count; i++)
            {
                ClusterGridEntity e = entities[i];
                if (e == null)
                    continue;
                if (e.Layer == EntityLayer.Asteroid || e.Layer == EntityLayer.POI)
                    return false;
            }
            return true;
        }

        private static void AddAsteroidLocationsFromEntityList(List<AxialI> asteroidLocations, List<ClusterGridEntity> entities)
        {
            if (asteroidLocations == null || entities == null)
                return;

            for (int i = 0; i < entities.Count; i++)
            {
                ClusterGridEntity entity = entities[i];
                if (entity != null && entity.Layer == EntityLayer.Asteroid)
                    AddUniqueLocation(asteroidLocations, entity.Location);
            }
        }

        private static void AddUniqueLocation(List<AxialI> locations, AxialI location)
        {
            if (locations == null || location == AxialI.INVALID || locations.Contains(location))
                return;

            locations.Add(location);
        }

        private static void AddProtectedLocationsFromEntityList(List<AxialI> protectedLocations, List<ClusterGridEntity> entities)
        {
            if (protectedLocations == null || entities == null)
                return;

            for (int i = 0; i < entities.Count; i++)
            {
                ClusterGridEntity entity = entities[i];
                if (entity == null)
                    continue;
                if (entity.Layer == EntityLayer.Asteroid || entity.Layer == EntityLayer.POI)
                    AddUniqueLocation(protectedLocations, entity.Location);
            }
        }

        private static List<AxialI> GetAllAsteroidLocationsOnStarmap()
        {
            List<AxialI> asteroidLocations = new List<AxialI>();

            if (ClusterGrid.Instance != null && ClusterGrid.Instance.cellContents != null)
            {
                foreach (KeyValuePair<AxialI, List<ClusterGridEntity>> kv in ClusterGrid.Instance.cellContents)
                    AddAsteroidLocationsFromEntityList(asteroidLocations, kv.Value);
            }

            if (ClusterManager.Instance != null && ClusterManager.Instance.WorldContainers != null)
            {
                foreach (WorldContainer world in ClusterManager.Instance.WorldContainers)
                {
                    AsteroidGridEntity asteroid = world != null ? world.GetComponent<AsteroidGridEntity>() : null;
                    if (asteroid != null)
                        AddUniqueLocation(asteroidLocations, asteroid.Location);
                }
            }

            return asteroidLocations;
        }

        private static List<AxialI> GetAllTemporalTearLocationsOnStarmap()
        {
            List<AxialI> tearLocations = new List<AxialI>();

            RebuildTemporalTearCache();
            for (int i = 0; i < CachedTemporalTears.Count; i++)
            {
                CachedTemporalTearEntry entry = CachedTemporalTears[i];
                if (entry != null)
                    AddUniqueLocation(tearLocations, entry.Location);
            }

            if (ClusterGrid.Instance != null && ClusterGrid.Instance.cellContents != null)
            {
                foreach (KeyValuePair<AxialI, List<ClusterGridEntity>> kv in ClusterGrid.Instance.cellContents)
                {
                    List<ClusterGridEntity> entities = kv.Value;
                    if (entities == null)
                        continue;

                    for (int i = 0; i < entities.Count; i++)
                    {
                        ClusterGridEntity entity = entities[i];
                        if (entity != null && entity.GetComponent<TemporalTear>() != null)
                            AddUniqueLocation(tearLocations, entity.Location);
                    }
                }
            }

            return tearLocations;
        }

        private static List<AxialI> GetProtectedStarmapLocationsForAsteroidPlacement()
        {
            List<AxialI> protectedLocations = new List<AxialI>();

            if (ClusterGrid.Instance != null && ClusterGrid.Instance.cellContents != null)
            {
                foreach (KeyValuePair<AxialI, List<ClusterGridEntity>> kv in ClusterGrid.Instance.cellContents)
                    AddProtectedLocationsFromEntityList(protectedLocations, kv.Value);
            }

            if (SaveLoader.Instance != null && SaveLoader.Instance.Cluster != null && SaveLoader.Instance.Cluster.poiPlacements != null)
            {
                foreach (KeyValuePair<AxialI, string> kv in SaveLoader.Instance.Cluster.poiPlacements)
                    AddUniqueLocation(protectedLocations, kv.Key);
            }

            List<AxialI> asteroidLocations = GetAllAsteroidLocationsOnStarmap();
            for (int i = 0; i < asteroidLocations.Count; i++)
                AddUniqueLocation(protectedLocations, asteroidLocations[i]);

            List<AxialI> temporalTearLocations = GetAllTemporalTearLocationsOnStarmap();
            for (int i = 0; i < temporalTearLocations.Count; i++)
                AddUniqueLocation(protectedLocations, temporalTearLocations[i]);

            return protectedLocations;
        }

        private static bool IsFarEnoughFromLocations(AxialI location, List<AxialI> otherLocations, int minDistance)
        {
            if (location == AxialI.INVALID || otherLocations == null)
                return false;

            for (int i = 0; i < otherLocations.Count; i++)
            {
                AxialI otherLocation = otherLocations[i];
                if (otherLocation != AxialI.INVALID && GetTemporalTearDistance(location, otherLocation) < minDistance)
                    return false;
            }

            return true;
        }

        private static bool IsFarEnoughFromAsteroids(AxialI location, List<AxialI> asteroidLocations, int minDistance)
        {
            return IsFarEnoughFromLocations(location, asteroidLocations, minDistance);
        }

        private static bool IsSuitableTemporalTearSpawnLocation(AxialI location, List<AxialI> asteroidLocations, List<AxialI> temporalTearLocations)
        {
            return IsSuitableHarvestablePoiSpawnLocation(location) &&
                   IsFarEnoughFromAsteroids(location, asteroidLocations, TemporalTearStarmapMinDistance) &&
                   IsFarEnoughFromLocations(location, temporalTearLocations, TemporalTearStarmapMinDistance);
        }

        private static void SpawnOneRandomHarvestablePoi(string reason)
        {
            try
            {
                if (ClusterGrid.Instance == null || ClusterGrid.Instance.cellContents == null)
                {
                    Debug.Log($"[MyWorldDumpMod] SpawnOneRandomHarvestablePoi skipped. reason={reason}, ClusterGrid unavailable.");
                    return;
                }

                List<AxialI> candidates = new List<AxialI>();
                List<AxialI> protectedLocations = GetAllAsteroidLocationsOnStarmap();
                List<AxialI> temporalTearLocations = GetAllTemporalTearLocationsOnStarmap();
                for (int i = 0; i < temporalTearLocations.Count; i++)
                    AddUniqueLocation(protectedLocations, temporalTearLocations[i]);

                foreach (KeyValuePair<AxialI, List<ClusterGridEntity>> kv in ClusterGrid.Instance.cellContents)
                {
                    if (IsSuitableHarvestablePoiSpawnLocation(kv.Key) && IsFarEnoughFromLocations(kv.Key, protectedLocations, AsteroidStarmapMinDistance))
                        candidates.Add(kv.Key);
                }

                if (candidates.Count == 0)
                {
                    Debug.LogWarning($"[MyWorldDumpMod] SpawnOneRandomHarvestablePoi skipped. reason={reason}, no valid starmap cells.");
                    return;
                }

                List<string> validPrefabs = new List<string>();
                for (int i = 0; i < RandomHarvestablePoiPrefabIds.Length; i++)
                {
                    string id = RandomHarvestablePoiPrefabIds[i];
                    GameObject prefab = Assets.GetPrefab((Tag)id);
                    if (prefab != null && prefab.GetComponent<HarvestablePOIClusterGridEntity>() != null)
                        validPrefabs.Add(id);
                }

                if (validPrefabs.Count == 0)
                {
                    Debug.LogWarning($"[MyWorldDumpMod] SpawnOneRandomHarvestablePoi skipped. reason={reason}, no valid harvestable prefabs.");
                    return;
                }

                AxialI location = AxialI.INVALID;
                while (candidates.Count > 0)
                {
                    int pick = UnityEngine.Random.Range(0, candidates.Count);
                    AxialI candidate = candidates[pick];
                    if (IsSuitableHarvestablePoiSpawnLocation(candidate) && IsFarEnoughFromLocations(candidate, protectedLocations, AsteroidStarmapMinDistance))
                    {
                        location = candidate;
                        break;
                    }

                    // Cell became occupied between scans; remove and retry.
                    candidates.RemoveAt(pick);
                }

                if (location == AxialI.INVALID)
                {
                    Debug.LogWarning($"[MyWorldDumpMod] SpawnOneRandomHarvestablePoi skipped. reason={reason}, no free candidate after occupancy recheck.");
                    return;
                }

                string prefabId = validPrefabs[UnityEngine.Random.Range(0, validPrefabs.Count)];
                GameObject poiGo = Util.KInstantiate(Assets.GetPrefab((Tag)prefabId));
                HarvestablePOIClusterGridEntity poi = poiGo.GetComponent<HarvestablePOIClusterGridEntity>();
                if (poi == null)
                {
                    Util.KDestroyGameObject(poiGo);
                    Debug.LogWarning($"[MyWorldDumpMod] SpawnOneRandomHarvestablePoi failed. reason={reason}, prefab={prefabId} has no HarvestablePOIClusterGridEntity.");
                    return;
                }

                poi.Init(location);
                poiGo.SetActive(true);

                ProcGenGame.Cluster saveCluster = SaveLoader.Instance != null ? SaveLoader.Instance.Cluster : null;
                if (saveCluster != null && saveCluster.poiPlacements != null)
                    saveCluster.poiPlacements[location] = prefabId;

                Debug.Log($"[MyWorldDumpMod] Spawned random harvestable POI. reason={reason}, prefab={prefabId}, location={location}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MyWorldDumpMod] SpawnOneRandomHarvestablePoi failed. reason={reason}, error={ex}");
            }
        }
        private static bool IsNonHarvestablePoiPlacementId(string prefabId)
        {
            if (string.IsNullOrEmpty(prefabId))
                return false;

            GameObject prefab = Assets.GetPrefab((Tag)prefabId);
            if (prefab == null)
                return false;

            ClusterGridEntity clusterEntity = prefab.GetComponent<ClusterGridEntity>();
            if (clusterEntity == null || clusterEntity.Layer != EntityLayer.POI)
                return false;

            return prefab.GetComponent<HarvestablePOIClusterGridEntity>() == null;
        }
        private static void RemoveAllHarvestableSpacePoisFromStarmap(string reason)
        {
            try
            {
                int destroyedEntities = 0;
                int removedPlacements = 0;
                int removedGridRefs = 0;
                int clearedInventories = 0;
                int removedInventoryEntries = 0;
                int removedInventoryItems = 0;

                HashSet<GameObject> pendingDestroy = new HashSet<GameObject>();
                HashSet<AxialI> targetCells = new HashSet<AxialI>();

                HarvestablePOIClusterGridEntity[] scenePoiEntities = UnityEngine.Object.FindObjectsByType<HarvestablePOIClusterGridEntity>(FindObjectsSortMode.None);
                if (scenePoiEntities != null)
                {
                    for (int i = 0; i < scenePoiEntities.Length; i++)
                    {
                        HarvestablePOIClusterGridEntity poi = scenePoiEntities[i];
                        if (poi == null)
                            continue;

                        targetCells.Add(poi.Location);
                        if (poi.gameObject != null)
                            pendingDestroy.Add(poi.gameObject);
                    }
                }

                if (ClusterGrid.Instance != null && ClusterGrid.Instance.cellContents != null)
                {
                    foreach (KeyValuePair<AxialI, List<ClusterGridEntity>> kv in ClusterGrid.Instance.cellContents)
                    {
                        List<ClusterGridEntity> entities = kv.Value;
                        if (entities == null)
                            continue;

                        for (int i = entities.Count - 1; i >= 0; i--)
                        {
                            ClusterGridEntity entity = entities[i];
                            if (entity == null)
                                continue;
                            if (entity.GetComponent<HarvestablePOIClusterGridEntity>() != null)
                            {
                                entities.RemoveAt(i);
                                removedGridRefs++;
                                targetCells.Add(kv.Key);
                                if (entity.gameObject != null)
                                    pendingDestroy.Add(entity.gameObject);
                            }
                        }
                    }
                }

                ProcGenGame.Cluster saveCluster = SaveLoader.Instance != null ? SaveLoader.Instance.Cluster : null;
                if (saveCluster != null && saveCluster.poiPlacements != null && saveCluster.poiPlacements.Count > 0)
                {
                    foreach (KeyValuePair<AxialI, string> kv in saveCluster.poiPlacements)
                    {
                        if (IsHarvestableSpacePoiPlacementId(kv.Value))
                            targetCells.Add(kv.Key);
                    }
                }

                foreach (GameObject go in pendingDestroy)
                {
                    if (go == null)
                        continue;
                    Util.KDestroyGameObject(go);
                    destroyedEntities++;
                }

                if (StarmapHexCellInventory.AllInventories != null && StarmapHexCellInventory.AllInventories.Count > 0)
                {
                    List<AxialI> inventoryKeysToRemove = new List<AxialI>();
                    foreach (KeyValuePair<AxialI, StarmapHexCellInventory> kv in StarmapHexCellInventory.AllInventories)
                    {
                        if (!targetCells.Contains(kv.Key))
                            continue;

                        StarmapHexCellInventory inventory = kv.Value;
                        if (inventory != null)
                        {
                            if (inventory.Items != null)
                            {
                                removedInventoryItems += inventory.Items.Count;
                                inventory.Items.Clear();
                            }

                            if (inventory.gameObject != null)
                                Util.KDestroyGameObject(inventory.gameObject);

                            clearedInventories++;
                        }

                        inventoryKeysToRemove.Add(kv.Key);
                    }

                    for (int i = 0; i < inventoryKeysToRemove.Count; i++)
                    {
                        if (StarmapHexCellInventory.AllInventories.Remove(inventoryKeysToRemove[i]))
                            removedInventoryEntries++;
                    }
                }

                if (saveCluster != null && saveCluster.poiPlacements != null && saveCluster.poiPlacements.Count > 0)
                {
                    List<AxialI> keysToRemove = new List<AxialI>();
                    foreach (KeyValuePair<AxialI, string> kv in saveCluster.poiPlacements)
                    {
                        if (IsHarvestableSpacePoiPlacementId(kv.Value))
                            keysToRemove.Add(kv.Key);
                    }

                    for (int i = 0; i < keysToRemove.Count; i++)
                    {
                        if (saveCluster.poiPlacements.Remove(keysToRemove[i]))
                            removedPlacements++;
                    }
                }

                if (ClusterMapScreen.Instance != null)
                    ClusterMapScreen.Instance.Trigger((int)GameHashes.UIRefresh, null);

                Debug.Log($"[MyWorldDumpMod] Removed harvestable starmap POIs. reason={reason}, targetCells={targetCells.Count}, destroyedEntities={destroyedEntities}, removedGridRefs={removedGridRefs}, clearedInventories={clearedInventories}, removedInventoryEntries={removedInventoryEntries}, removedInventoryItems={removedInventoryItems}, removedPlacements={removedPlacements}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MyWorldDumpMod] RemoveAllHarvestableSpacePoisFromStarmap failed. reason={reason}, error={ex}");
            }
        }
        private static void RemoveAllNonHarvestablePoiObjectsFromStarmap(string reason)
        {
            try
            {
                int destroyedEntities = 0;
                int removedPlacements = 0;
                int removedGridRefs = 0;
                int clearedInventories = 0;
                int removedInventoryEntries = 0;
                int removedInventoryItems = 0;

                HashSet<GameObject> pendingDestroy = new HashSet<GameObject>();
                HashSet<AxialI> targetCells = new HashSet<AxialI>();

                if (ClusterGrid.Instance != null && ClusterGrid.Instance.cellContents != null)
                {
                    foreach (KeyValuePair<AxialI, List<ClusterGridEntity>> kv in ClusterGrid.Instance.cellContents)
                    {
                        List<ClusterGridEntity> entities = kv.Value;
                        if (entities == null)
                            continue;

                        for (int i = entities.Count - 1; i >= 0; i--)
                        {
                            ClusterGridEntity entity = entities[i];
                            if (entity == null)
                                continue;

                            bool isPoi = entity.Layer == EntityLayer.POI;
                            bool isHarvestable = entity.GetComponent<HarvestablePOIClusterGridEntity>() != null;
                            if (isPoi && !isHarvestable)
                            {
                                entities.RemoveAt(i);
                                removedGridRefs++;
                                targetCells.Add(kv.Key);
                                if (entity.gameObject != null)
                                    pendingDestroy.Add(entity.gameObject);
                            }
                        }
                    }
                }

                ClusterGridEntity[] sceneEntities = UnityEngine.Object.FindObjectsByType<ClusterGridEntity>(FindObjectsSortMode.None);
                if (sceneEntities != null)
                {
                    for (int i = 0; i < sceneEntities.Length; i++)
                    {
                        ClusterGridEntity entity = sceneEntities[i];
                        if (entity == null)
                            continue;

                        bool isPoi = entity.Layer == EntityLayer.POI;
                        bool isHarvestable = entity.GetComponent<HarvestablePOIClusterGridEntity>() != null;
                        if (isPoi && !isHarvestable)
                        {
                            targetCells.Add(entity.Location);
                            if (entity.gameObject != null)
                                pendingDestroy.Add(entity.gameObject);
                        }
                    }
                }

                ProcGenGame.Cluster saveCluster = SaveLoader.Instance != null ? SaveLoader.Instance.Cluster : null;
                if (saveCluster != null && saveCluster.poiPlacements != null && saveCluster.poiPlacements.Count > 0)
                {
                    List<AxialI> keysToRemove = new List<AxialI>();
                    foreach (KeyValuePair<AxialI, string> kv in saveCluster.poiPlacements)
                    {
                        if (IsNonHarvestablePoiPlacementId(kv.Value))
                        {
                            keysToRemove.Add(kv.Key);
                            targetCells.Add(kv.Key);
                        }
                    }

                    for (int i = 0; i < keysToRemove.Count; i++)
                    {
                        if (saveCluster.poiPlacements.Remove(keysToRemove[i]))
                            removedPlacements++;
                    }
                }

                foreach (GameObject go in pendingDestroy)
                {
                    if (go == null)
                        continue;
                    Util.KDestroyGameObject(go);
                    destroyedEntities++;
                }

                if (StarmapHexCellInventory.AllInventories != null && StarmapHexCellInventory.AllInventories.Count > 0)
                {
                    List<AxialI> inventoryKeysToRemove = new List<AxialI>();
                    foreach (KeyValuePair<AxialI, StarmapHexCellInventory> kv in StarmapHexCellInventory.AllInventories)
                    {
                        if (!targetCells.Contains(kv.Key))
                            continue;

                        StarmapHexCellInventory inventory = kv.Value;
                        if (inventory != null)
                        {
                            if (inventory.Items != null)
                            {
                                removedInventoryItems += inventory.Items.Count;
                                inventory.Items.Clear();
                            }

                            if (inventory.gameObject != null)
                                Util.KDestroyGameObject(inventory.gameObject);

                            clearedInventories++;
                        }

                        inventoryKeysToRemove.Add(kv.Key);
                    }

                    for (int i = 0; i < inventoryKeysToRemove.Count; i++)
                    {
                        if (StarmapHexCellInventory.AllInventories.Remove(inventoryKeysToRemove[i]))
                            removedInventoryEntries++;
                    }
                }

                // Remove manager-side references to destroyed special POIs (temporal tear / research destinations).
                ClusterPOIManager poiManager = ClusterManager.Instance != null ? ClusterManager.Instance.GetClusterPOIManager() : null;
                if (poiManager != null)
                {
                    poiManager.RegisterTemporalTear(null);
                    CachedTemporalTears.Clear();

                    FieldInfo researchField = AccessTools.Field(typeof(ClusterPOIManager), "m_researchDestinations");
                    if (researchField != null)
                    {
                        System.Collections.IList list = researchField.GetValue(poiManager) as System.Collections.IList;
                        if (list != null)
                            list.Clear();
                    }
                }

                if (ClusterMapScreen.Instance != null)
                    ClusterMapScreen.Instance.Trigger((int)GameHashes.UIRefresh, null);

                Debug.Log($"[MyWorldDumpMod] Removed non-harvestable POI starmap objects. reason={reason}, targetCells={targetCells.Count}, destroyedEntities={destroyedEntities}, removedGridRefs={removedGridRefs}, clearedInventories={clearedInventories}, removedInventoryEntries={removedInventoryEntries}, removedInventoryItems={removedInventoryItems}, removedPlacements={removedPlacements}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MyWorldDumpMod] RemoveAllNonHarvestablePoiObjectsFromStarmap failed. reason={reason}, error={ex}");
            }
        }
        private static void SpawnRandomTemporalTears(string reason)
        {
            try
            {
                if (ClusterGrid.Instance == null || ClusterGrid.Instance.cellContents == null)
                {
                    Debug.Log($"[MyWorldDumpMod] SpawnRandomTemporalTears skipped. reason={reason}, ClusterGrid unavailable.");
                    return;
                }

                GameObject temporalTearPrefab = Assets.GetPrefab((Tag)TemporalTearConfig.ID);
                if (temporalTearPrefab == null || temporalTearPrefab.GetComponent<TemporalTear>() == null)
                {
                    Debug.LogWarning($"[MyWorldDumpMod] SpawnRandomTemporalTears skipped. reason={reason}, TemporalTear prefab unavailable.");
                    return;
                }

                List<AxialI> candidates = new List<AxialI>();
                List<AxialI> asteroidLocations = GetAllAsteroidLocationsOnStarmap();
                List<AxialI> temporalTearLocations = GetAllTemporalTearLocationsOnStarmap();
                foreach (KeyValuePair<AxialI, List<ClusterGridEntity>> kv in ClusterGrid.Instance.cellContents)
                {
                    if (IsSuitableTemporalTearSpawnLocation(kv.Key, asteroidLocations, temporalTearLocations))
                        candidates.Add(kv.Key);
                }

                if (candidates.Count == 0)
                {
                    Debug.LogWarning($"[MyWorldDumpMod] SpawnRandomTemporalTears skipped. reason={reason}, no valid starmap cells far enough from asteroids and temporal tears.");
                    return;
                }

                int spawnCount = UnityEngine.Random.Range(1, 4);
                int spawned = 0;
                for (int i = 0; i < spawnCount; i++)
                {
                    AxialI location = AxialI.INVALID;
                    while (candidates.Count > 0)
                    {
                        int pick = UnityEngine.Random.Range(0, candidates.Count);
                        AxialI candidate = candidates[pick];
                        candidates.RemoveAt(pick);
                        if (IsSuitableTemporalTearSpawnLocation(candidate, asteroidLocations, temporalTearLocations))
                        {
                            location = candidate;
                            break;
                        }
                    }

                    if (location == AxialI.INVALID)
                        break;

                    GameObject go = Util.KInstantiate(temporalTearPrefab);
                    TemporalTear tear = go.GetComponent<TemporalTear>();
                    if (tear == null)
                    {
                        Util.KDestroyGameObject(go);
                        continue;
                    }

                    tear.Location = location;
                    go.AddOrGet<CachedTemporalTearMarker>().Init(tear, location);
                    go.SetActive(true);
                    RegisterTemporalTearCache(tear, location);
                    AddUniqueLocation(temporalTearLocations, location);
                    spawned++;

                    ProcGenGame.Cluster saveCluster = SaveLoader.Instance != null ? SaveLoader.Instance.Cluster : null;
                    if (saveCluster != null && saveCluster.poiPlacements != null)
                        saveCluster.poiPlacements[location] = TemporalTearConfig.ID;
                }

                if (ClusterMapScreen.Instance != null)
                    ClusterMapScreen.Instance.Trigger((int)GameHashes.UIRefresh, null);

                Debug.Log($"[MyWorldDumpMod] Spawned random temporal tears. reason={reason}, requested={spawnCount}, spawned={spawned}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MyWorldDumpMod] SpawnRandomTemporalTears failed. reason={reason}, error={ex}");
            }
        }
        public class NewGameMonitor : MonoBehaviour
        {
            public void Update()
            {
                if (Switch == true && IsOldReveal == true && IsNewWorld == true && IsClearWorlds == true && IsReveal == true && IsOldWorld == true && IsUndump == true && IsReload == true)
                {
                    SpeedControlScreen.Instance.Pause();

                    Switch = false;
                    IsOldReveal = false;
                    WaitFrames = 0;

                    //IsQuitOldWorld = false;
                    IsLoadOldWorld = false;

                    IsNewWorld = false;
                    IsClearWorlds = false;
                    IsReveal = false;
                    IsOldWorld = false;
                    IsUndump = false;
                    IsReload = false;
                    PreservedWorldIdDuringClear = -1;
                    MainWorldRocketWorldId.Clear();
                    CapturedStarmapWorlds.Clear();
                    DumpedFilesByWorldId.Clear();
                    RestoredDumpDataByWorldId.Clear();
                    //pipeline is done
                    EngineConsole.ClearStaticDestinationAndStopForStarmapRefresh("before_save_after_undump");
                }

                if (Switch == true && IsOldReveal == true && IsNewWorld == true && IsClearWorlds == true && IsReveal == true && IsOldWorld == true && IsUndump == false && IsReload == false)
                {
                    WaitFrames++;
                    if (WaitFrames == 0)
                    {
                        SpeedControlScreen.Instance.Pause();
                        LoadingOverlay.Load(() => { LoadingOverlayTextHelper.SetText(Interstellar.NewWorldTip4 + "....."); });
                    }
                    if (WaitFrames == 10)
                    {
                        Debug.Log($"[MyWorldDumpMod] Restore Dump Data By New World");
                        LoadingOverlay.Load(() => { });

                        RestoredDumpDataByWorldId.Clear();
                        PendingDelayedSpawnPrefabs.Clear();
                        List<InjectedWorldMapping> injectedWorldMappings = InjectCapturedStarmapWorldsIntoCurrentSave();
                        for (int i = 0; i < injectedWorldMappings.Count; i++)
                        {
                            int sourceWorldId = injectedWorldMappings[i].SourceWorldId;
                            int targetWorldId = injectedWorldMappings[i].TargetWorldId;
                            string dumpPath;
                            if (!DumpedFilesByWorldId.TryGetValue(sourceWorldId, out dumpPath))
                            {
                                Debug.LogWarning($"[MyWorldDumpMod] Missing dump path for source world id={sourceWorldId}; restore skipped.");
                                continue;
                            }

                            DumpFileData worldDumpData = ReadDumpFile(dumpPath);
                            RestoredDumpDataByWorldId[targetWorldId] = worldDumpData;
                            RestoreGridFromDumpViaSim(worldDumpData, sourceWorldId, targetWorldId);
                        }
                    }
                    if (WaitFrames == 20)
                    {
                        SpeedControlScreen.Instance.Pause();
                        LoadingOverlay.Load(() => { LoadingOverlayTextHelper.SetText(Interstellar.NewWorldTip4 + "....."); });

                        foreach (KeyValuePair<int, DumpFileData> kv in RestoredDumpDataByWorldId)
                        {
                            int worldId = kv.Key;
                            DumpFileData worldDumpData = kv.Value;
                            WorldContainer targetWorld = ClusterManager.Instance.GetWorld(worldId);
                            if (targetWorld == null)
                                continue;

                            StreamWriter restoreDebugLog = OpenRestoreDebugLogForAppend(worldId);
                            int queuedCrops = QueueCropsFromDumpForDelayedSpawn(worldDumpData.CropRows, Vector2I.zero, Vector2I.zero, targetWorld.WorldOffset, targetWorld.WorldSize, worldId, restoreDebugLog);
                            int queuedCreatures = QueueCreaturesFromDumpForDelayedSpawn(worldDumpData.HealthRows, Vector2I.zero, Vector2I.zero, targetWorld.WorldOffset, targetWorld.WorldSize, worldId, restoreDebugLog);
                            restoreDebugLog?.Dispose();
                            Debug.Log($"[MyWorldDumpMod] Queued delayed crops/creatures for restored world. worldId={worldId}, crops={queuedCrops}, creatures={queuedCreatures}");
                        }

                        RemoveAllHarvestableSpacePoisFromStarmap("restore_step_wait20");
                        int randomSpawnCount = UnityEngine.Random.Range(2, 6);
                        for (int i = 0; i < randomSpawnCount; i++)
                            SpawnOneRandomHarvestablePoi($"restore_step_wait20_{i + 1}/{randomSpawnCount}");
                        RemoveAllNonHarvestablePoiObjectsFromStarmap("");
                        SpawnRandomTemporalTears("restore_step_wait20");
                    }
                    if (WaitFrames == 25)
                    {
                        SpeedControlScreen.Instance.Pause();
                        LoadingOverlay.Load(() => { LoadingOverlayTextHelper.SetText(Interstellar.NewWorldTip4 + "....."); });
                        RehideRestoredWorldsAndResetStarmapFog();
                    }
                    if (WaitFrames == 30)
                    {
                        LoadingOverlay.Load((System.Action)(() =>
                        {
                            LoadingOverlayTextHelper.SetText(Interstellar.NewWorldTip4);

                            ReinitializeSimFromCurrentGridUsingGridTempAsBgTemp();
                            SanitizeWorldGenSpawnerAfterWorldLayoutChanges("before_save_after_undump");
                            InstallPendingDelayedSpawnPrefabs("before_save_after_undump");
                            SaveLoader.Instance.Save(filename);

                            ThreadedHttps<KleiMetrics>.Instance.EndGame();
                            LoadScreen.ForceStopGame();
                            SceneManager.sceneLoaded += OnSceneLoaded;
                            App.LoadScene("frontend");
                        }));

                        IsUndump = true;
                        WaitFrames = 0;
                    }
                }

                if (Switch == true && IsOldReveal == true && IsNewWorld == true && IsClearWorlds == true && IsReveal == true && IsLoadOldWorld == false && IsOldWorld == false && IsReload == false)
                {
                    WaitFrames++;
                    if (WaitFrames == 0)
                    {
                        SpeedControlScreen.Instance.Pause();
                        LoadingOverlay.Load(() => { LoadingOverlayTextHelper.SetText(Interstellar.NewWorldTip3 + "....."); });
                    }
                    if (WaitFrames == 100)
                    {
                        IsLoadOldWorld = true;
                        int capturedCount = CaptureHeadquartersAndRandomStarmapWorldSnapshotsAndDump();
                        Debug.Log($"[MyWorldDumpMod] Dump New World. capturedWorldCount={capturedCount}");
                        LoadingOverlay.Load((System.Action)(() =>
                        {
                            WaitFrames = 0;

                            Debug.Log($"[MyWorldDumpMod] Loading original save file: {filename}");
                            SaveLoader.SetActiveSaveFilePath(filename);
                            ThreadedHttps<KleiMetrics>.Instance.EndGame();
                            LoadScreen.ForceStopGame();
                            App.LoadScene("backend");
                        }));
                    }
                }
                if (Switch == true && IsOldReveal == true && IsNewWorld == true && IsClearWorlds == true && IsReveal == false && IsOldWorld == false && IsReload == false)
                {
                    WaitFrames++;
                    if (WaitFrames == 0)
                    {
                        SpeedControlScreen.Instance.Pause();
                        LoadingOverlay.Load(() => { LoadingOverlayTextHelper.SetText(Interstellar.NewWorldTip3 + "....."); });
                    }
                    if (WaitFrames == 5)
                    {
                        WorldContainer[] Worlds = ClusterManager.Instance.WorldContainers.ToArray();
                        Debug.Log($"[MyWorldDumpMod] Reveal New World");

                        for (int i = 0; i < Worlds.Length; i++)
                        {
                            ClusterManager.Instance.SetActiveWorld(Worlds[i].id);
                            Worlds[i].SetDiscovered();
                            for (int j = 0; j < Grid.CellCount; j++)
                            {
                                if (!Grid.IsValidCellInWorld(j, Worlds[i].id))
                                    continue;
                                if (Grid.WorldIdx[j] != Worlds[i].id)
                                    continue;

                                Grid.Reveal(j, forceReveal: true);
                            }
                        }
                        WaitFrames = 0;
                        IsReveal = true;
                    }
                }

                if (Switch == true && IsOldReveal == true && IsClearWorlds == true && IsNewWorld == false && IsOldWorld == false && IsReload == false)
                {
                    WaitFrames++;
                    if (WaitFrames == 20)
                    {
                        Debug.Log($"[MyWorldDumpMod] Clearing Old World Step 3.");

                        WorldContainer[] Worlds = ClusterManager.Instance.WorldContainers.ToArray();
                        for (int i = 0; i < Worlds.Length; i++)
                        {
                            if (Worlds[i] == null || ShouldPreserveWorldDuringClear(Worlds[i].id))
                                continue;
                            ClusterManager.Instance.UnregisterWorldContainer(Worlds[i]);
                            Util.KDestroyGameObject(Worlds[i].gameObject);
                        }
                        WorldContainer preservedWorld = ClusterManager.Instance.GetWorld(PreservedWorldIdDuringClear);
                        if (preservedWorld != null)
                            ClusterManager.Instance.SetActiveWorld(PreservedWorldIdDuringClear);
                        else if (ClusterManager.Instance.WorldContainers.Count > 0)
                            ClusterManager.Instance.SetActiveWorld(ClusterManager.Instance.WorldContainers[0].id);
                        SyncSimWorldOffsetsFromCluster("after_unregister_worlds");
                    }

                    if (WaitFrames == 40)
                    {
                        Debug.Log($"[MyWorldDumpMod] Save Cleared Old World");

                        filename = GetIncrementedNewWorldSavePath(filename);
                        SanitizeWorldGenSpawnerAfterWorldLayoutChanges("before_save_after_clear_worlds");
                        SaveLoader.Instance.Save(filename);
                    }

                    if (WaitFrames == 60)
                    {
                        LoadingOverlay.Load((System.Action)(() =>
                        {
                            LoadingOverlayTextHelper.SetFontSize(DefaultOverlayFontSize);
                            LoadingOverlayTextHelper.SetText(Interstellar.NewWorldTip2 + ".....");
                            Debug.Log($"[MyWorldDumpMod] goto frontend before world gen");

                            WaitFrames = 0;
                            //SaveLoader.Instance.Save(filename);

                            ThreadedHttps<KleiMetrics>.Instance.EndGame();
                            LoadScreen.ForceStopGame();
                            SceneManager.sceneLoaded += OnSceneLoaded;
                            App.LoadScene("frontend");
                        }));
                    }
                }

                if (Switch == true && IsOldReveal == true && IsClearWorlds == false && IsNewWorld == false && IsOldWorld == false && IsReload == false)
                {
                    WaitFrames++;
                    WorldContainer[] Worlds = ClusterManager.Instance.WorldContainers.ToArray();
                    if (WaitFrames == 20)
                    {
                        int headquartersWorldId = GetHeadquartersWorldId();
                        MainWorldRocketWorldId.Clear();
                        if (headquartersWorldId >= 0)
                        {
                            PreservedWorldIdDuringClear = headquartersWorldId;
                        }
                        else if (Worlds.Length > 0 && Worlds[0] != null)
                        {
                            PreservedWorldIdDuringClear = Worlds[0].id;
                            Debug.LogWarning($"[MyWorldDumpMod] Headquarters world not found. Fallback preserve world id={PreservedWorldIdDuringClear}.");
                        }
                        else
                        {
                            PreservedWorldIdDuringClear = -1;
                        }
                        Debug.Log($"[MyWorldDumpMod] Clearing Old World Step 1");
                        Debug.Log($"[MyWorldDumpMod] Preserving world id={PreservedWorldIdDuringClear}.");
                        DeleteRocketsNotLandedOnPreservedWorld();
                        for (int i = 0; i < Worlds.Length; i++)
                        {
                            WorldContainer currentWorld = Worlds[i];
                            if (currentWorld == null)
                                continue;

                            bool isRocketInterior = IsRocketInteriorWorld(currentWorld);
                            int rocketHostWorldId = GetRocketHostWorldIdForInterior(currentWorld);
                            Debug.Log($"[MyWorldDumpMod] Step1 world id={currentWorld.id}, isRocketInterior={isRocketInterior}, rocketHostWorldId={rocketHostWorldId}, parentWorldId={currentWorld.ParentWorldId}");

                            if (isRocketInterior == true && rocketHostWorldId == PreservedWorldIdDuringClear)
                            {
                                if (!MainWorldRocketWorldId.Contains(currentWorld.id))
                                    MainWorldRocketWorldId.Add(currentWorld.id);
                                Debug.Log($"[MyWorldDumpMod] Preserving rocket interior world id={currentWorld.id} since its host world id={rocketHostWorldId} is preserved.");
                                continue;
                            }
                            if (ShouldPreserveWorldDuringClear(currentWorld.id))
                                continue;

                            int cleanedCount = CleanupStaleClusterCraftInteriorDoors(currentWorld.id);
                            if (cleanedCount > 0)
                                Debug.Log($"[MyWorldDumpMod] Cleaned stale ClusterCraftInteriorDoors entries: {cleanedCount}");

                            ClusterManager.Instance.SetActiveWorld(currentWorld.id);
                            ClearWorld(currentWorld, p => { });
                        }
                    }
                    if (WaitFrames == 40)
                    {
                        Debug.Log($"[MyWorldDumpMod] Clearing Old World Step 2");
                        for (int i = 0; i < Worlds.Length; i++)
                        {
                            if (Worlds[i] == null)
                                continue;

                            if (ShouldPreserveWorldDuringClear(Worlds[i].id))
                            {
                                Debug.Log($"[MyWorldDumpMod] Preserving world id={Worlds[i].id} during grid free step.");
                                continue;
                            }

                            Grid.FreeGridSpace(Worlds[i].WorldSize, Worlds[i].WorldOffset);
                        }
                        IsClearWorlds = true;
                        WaitFrames = 0;
                    }
                }
            }
        }

        [HarmonyPatch(typeof(MinionSelectScreen), "OnSpawn")]
        public static class MinionSelectScreen_OnSpawn_Patch
        {
            public static void Postfix(MinionSelectScreen __instance)
            {
                if (Mod.IsNewWorld == true && Mod.Switch == true)
                {
                    Debug.Log($"[MyWorldDumpMod] MinionSelectScreen skip original OnSpawn. switch:{Mod.Switch}, IsNewWorld:{Mod.IsNewWorld} ");
                    __instance.Show(false);
                    LoadingOverlay.Load(() => { });
                }
            }
        }
        public class Patches
        {
            [HarmonyPatch(typeof(Game))]
            [HarmonyPatch("OnSpawn")]
            public class Db_Initialize_Patch
            {
                public static void Postfix(Game __instance)
                {

                    HotkeyListener hl = __instance.gameObject.AddOrGet<HotkeyListener>();
                    ClusterFxToggleController clusterFxToggle = __instance.gameObject.AddOrGet<ClusterFxToggleController>();
                    __instance.gameObject.AddOrGet<NewGameMonitor>();

                    hl.OnCtrlPageDown -= MyCallback;
                    hl.OnCtrlDelete -= clusterFxToggle.ToggleByHotkey;
                    if (IsDebugHotKeyEnabled())
                    {
                        hl.OnCtrlPageDown += MyCallback;
                        hl.OnCtrlDelete += clusterFxToggle.ToggleByHotkey;
                    }
                }
            }

            static void MyCallback()
            {
                Debug.Log("Ctrl+PageDown callback!");
                Switch = true;
                CreateNewWorlds();
            }

            [HarmonyPatch(typeof(TemporalTearOpener.Instance), nameof(TemporalTearOpener.Instance.HasSufficientColonies))]
            public static class TemporalTearOpener_HasSufficientColonies_AlwaysTrue_Patch
            {
                public static bool Prefix(ref bool __result)
                {
                    __result = true;
                    return false;
                }
            }

            [HarmonyPatch(typeof(TemporalTear), "OnSpawn")]
            public static class TemporalTear_OnSpawn_Cache_Patch
            {
                public static void Postfix(TemporalTear __instance)
                {
                    if (__instance == null)
                        return;

                    __instance.gameObject.AddOrGet<CachedTemporalTearMarker>().Init(__instance, __instance.Location);
                }
            }

            [HarmonyPatch(typeof(TemporalTear), "OnCleanUp")]
            public static class TemporalTear_OnCleanUp_Cache_Patch
            {
                public static void Prefix(TemporalTear __instance)
                {
                    if (__instance != null)
                        UnregisterTemporalTearCache(__instance);
                }
            }

            [HarmonyPatch(typeof(TemporalTearOpener.Instance), nameof(TemporalTearOpener.Instance.OpenTemporalTear))]
            public static class TemporalTearOpener_OpenTemporalTear_ClosestTear_Patch
            {
                public static bool Prefix(TemporalTearOpener.Instance __instance)
                {
                    if (__instance == null || __instance.gameObject == null)
                        return true;

                    bool handled = OpenClosestTemporalTear(__instance.gameObject, __instance.gameObject.GetMyWorldId(), out TemporalTear openedTear);
                    if (handled && openedTear != null && !AreAllCachedTemporalTearsOpen())
                        ResetTemporalTearOpenerForNextTear(__instance);

                    return !handled;
                }
            }

            [HarmonyPatch(typeof(ClusterMapVisualizer), nameof(ClusterMapVisualizer.Show))]
            public static class ClusterMapVisualizer_Show_TemporalTearStatus_Patch
            {
                public static void Postfix(ClusterMapVisualizer __instance, ClusterRevealLevel level)
                {
                    if (__instance == null || level != ClusterRevealLevel.Visible)
                        return;

                    ClusterGridEntity entity = ClusterMapVisualizer_entity_Field(__instance);
                    TemporalTear tear = entity != null ? entity.GetComponent<TemporalTear>() : null;
                    if (tear != null)
                        tear.UpdateStatus();
                }
            }

            [HarmonyPatch(typeof(ClusterPOIManager), nameof(ClusterPOIManager.IsTemporalTearOpen))]
            public static class ClusterPOIManager_IsTemporalTearOpen_AllCachedTears_Patch
            {
                public static bool Prefix(ref bool __result)
                {
                    RebuildTemporalTearCache();
                    if (CachedTemporalTears.Count == 0)
                        return true;

                    __result = AreAllCachedTemporalTearsOpen();
                    return false;
                }
            }

            [HarmonyPatch(typeof(ClusterPOIManager), nameof(ClusterPOIManager.IsTemporalTearRevealed))]
            public static class ClusterPOIManager_IsTemporalTearRevealed_CachedTears_Patch
            {
                public static bool Prefix(ref bool __result)
                {
                    RebuildTemporalTearCache();
                    if (CachedTemporalTears.Count == 0)
                        return true;

                    __result = IsAnyCachedTemporalTearRevealed();
                    return false;
                }
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

        private static readonly AccessTools.FieldRef<TemporalTearOpener.Instance, float> TemporalTearOpener_particlesConsumed_Field = AccessTools.FieldRefAccess<TemporalTearOpener.Instance, float>("m_particlesConsumed");
        private static readonly AccessTools.FieldRef<ClusterMapVisualizer, ClusterGridEntity> ClusterMapVisualizer_entity_Field = AccessTools.FieldRefAccess<ClusterMapVisualizer, ClusterGridEntity>("entity");
        private static readonly AccessTools.FieldRef<EntombedItemManager, List<int>> EntombedItemManager_cells_Field = AccessTools.FieldRefAccess<EntombedItemManager, List<int>>("cells");
        private static readonly AccessTools.FieldRef<EntombedItemManager, List<int>> EntombedItemManager_elementIds_Field = AccessTools.FieldRefAccess<EntombedItemManager, List<int>>("elementIds");
        private static readonly AccessTools.FieldRef<EntombedItemManager, List<float>> EntombedItemManager_masses_Field = AccessTools.FieldRefAccess<EntombedItemManager, List<float>>("masses");
        private static readonly AccessTools.FieldRef<EntombedItemManager, List<float>> EntombedItemManager_temperatures_Field = AccessTools.FieldRefAccess<EntombedItemManager, List<float>>("temperatures");
        private static readonly AccessTools.FieldRef<EntombedItemManager, List<byte>> EntombedItemManager_diseaseIndices_Field = AccessTools.FieldRefAccess<EntombedItemManager, List<byte>>("diseaseIndices");
        private static readonly AccessTools.FieldRef<EntombedItemManager, List<int>> EntombedItemManager_diseaseCounts_Field = AccessTools.FieldRefAccess<EntombedItemManager, List<int>>("diseaseCounts");
        private static readonly AccessTools.FieldRef<EntombedItemManager, List<Pickupable>> EntombedItemManager_pickupables_Field = AccessTools.FieldRefAccess<EntombedItemManager, List<Pickupable>>("pickupables");
        private static readonly AccessTools.FieldRef<WarpPortal, Coroutine> WarpPortal_delayWarpRoutine_Field = AccessTools.FieldRefAccess<WarpPortal, Coroutine>("delayWarpRoutine");
        private static readonly AccessTools.FieldRef<WarpPortal, bool> WarpPortal_discovered_Field = AccessTools.FieldRefAccess<WarpPortal, bool>("discovered");
        private static int GetHeadquartersWorldId()
        {
            foreach (BuildingComplete building in Components.BuildingCompletes.Items)
            {
                if (building == null || building.Def == null)
                    continue;

                if (!string.Equals(building.Def.PrefabID, "Headquarters", StringComparison.Ordinal))
                    continue;

                int cell = Grid.PosToCell((KMonoBehaviour)building);
                if (!Grid.IsValidCell(cell))
                    continue;

                return Grid.WorldIdx[cell];
            }

            return -1;
        }
        private static bool IsRocketInteriorWorld(WorldContainer world)
        {
            return world != null && world.IsModuleInterior;
        }
        private static bool ShouldPreserveWorldDuringClear(int worldId)
        {
            if (worldId < 0)
                return false;
            if (worldId == PreservedWorldIdDuringClear)
                return true;
            return MainWorldRocketWorldId.Contains(worldId);
        }
        private static bool IsGameObjectInWorld(GameObject go, int worldId)
        {
            if (go == null || worldId < 0)
                return false;

            try
            {
                int cell = Grid.PosToCell(go);
                if (Grid.IsValidCell(cell) && Grid.WorldIdx[cell] == worldId)
                    return true;
            }
            catch
            {
            }

            try
            {
                return go.GetMyWorldId() == worldId;
            }
            catch
            {
                return false;
            }
        }
        private static bool IsValidWarpReceiverForPortal(WarpPortal portal, WarpReceiver receiver)
        {
            if (portal == null || receiver == null || receiver.gameObject == null)
                return false;

            int portalWorldId;
            int receiverWorldId;
            try
            {
                portalWorldId = portal.GetMyWorldId();
                receiverWorldId = receiver.GetMyWorldId();
            }
            catch
            {
                return false;
            }

            if (receiverWorldId < 0 || receiverWorldId == portalWorldId)
                return false;

            WorldContainer receiverWorld = ClusterManager.Instance != null ? ClusterManager.Instance.GetWorld(receiverWorldId) : null;
            if (receiverWorld == null)
                return false;

            int receiverCell = Grid.PosToCell((KMonoBehaviour)receiver);
            return Grid.IsValidCell(receiverCell) && Grid.WorldIdx[receiverCell] == receiverWorldId;
        }
        private static void TrySpawnWarpReceiver()
        {
            try
            {
                WorldGenSpawner spawner = SaveGame.Instance != null ? SaveGame.Instance.GetComponent<WorldGenSpawner>() : null;
                if (spawner != null)
                    spawner.SpawnTag(WarpReceiverConfig.ID);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Interstellar] Failed to spawn/check warp receiver: {ex.Message}");
            }
        }
        private static WarpReceiver FindValidWarpReceiver(WarpPortal portal, bool spawnIfMissing)
        {
            if (portal == null)
                return null;

            if (spawnIfMissing)
                TrySpawnWarpReceiver();

            foreach (WarpReceiver receiver in UnityEngine.Object.FindObjectsByType<WarpReceiver>(FindObjectsSortMode.None))
            {
                if (IsValidWarpReceiverForPortal(portal, receiver))
                    return receiver;
            }

            return null;
        }
        private static int FindValidWarpReceiverWorldId(WarpPortal portal, bool spawnIfMissing)
        {
            WarpReceiver receiver = FindValidWarpReceiver(portal, spawnIfMissing);
            return receiver != null ? receiver.GetMyWorldId() : -1;
        }
        private static IEnumerator EmptyEnumerator()
        {
            yield break;
        }
        private static void CleanupWarpTeleportersForClearedWorld(WorldContainer world, ICollection<GameObject> targets)
        {
            if (world == null || targets == null)
                return;

            int worldId = world.id;
            if (SelectTool.Instance != null && SelectTool.Instance.selected != null && IsGameObjectInWorld(SelectTool.Instance.selected.gameObject, worldId))
                SelectTool.Instance.Select(null, true);

            foreach (WarpPortal portal in UnityEngine.Object.FindObjectsByType<WarpPortal>(FindObjectsSortMode.None))
            {
                if (portal == null || !IsGameObjectInWorld(portal.gameObject, worldId))
                    continue;

                try
                {
                    portal.CancelAssignment();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Interstellar] Failed to cancel warp portal before clearing world {worldId}: {ex.Message}");
                }

                targets.Add(portal.gameObject);
            }

            foreach (WarpReceiver receiver in UnityEngine.Object.FindObjectsByType<WarpReceiver>(FindObjectsSortMode.None))
            {
                if (receiver != null && IsGameObjectInWorld(receiver.gameObject, worldId))
                    targets.Add(receiver.gameObject);
            }

            List<WarpReceiver> staleReceivers = null;
            foreach (WarpReceiver receiver in Components.WarpReceivers.Items)
            {
                bool stale = receiver == null || receiver.gameObject == null;
                if (!stale)
                {
                    int receiverWorldId = -1;
                    int receiverCell = -1;
                    try
                    {
                        receiverWorldId = receiver.GetMyWorldId();
                        receiverCell = Grid.PosToCell((KMonoBehaviour)receiver);
                    }
                    catch
                    {
                        stale = true;
                    }

                    if (!stale)
                        stale = receiverWorldId < 0 || ClusterManager.Instance.GetWorld(receiverWorldId) == null || !Grid.IsValidCell(receiverCell);
                }

                if (!stale)
                    continue;

                if (staleReceivers == null)
                    staleReceivers = new List<WarpReceiver>();
                staleReceivers.Add(receiver);
            }

            if (staleReceivers != null)
            {
                foreach (WarpReceiver receiver in staleReceivers)
                    Components.WarpReceivers.Remove(receiver);
            }
        }
        [HarmonyPatch(typeof(WarpPortal), "GetTargetWorldID")]
        public static class Patch_WarpPortal_GetTargetWorldID
        {
            public static bool Prefix(WarpPortal __instance, ref int __result)
            {
                __result = FindValidWarpReceiverWorldId(__instance, true);
                if (__result < 0)
                    Debug.LogWarning("[Interstellar] No valid warp receiver world found for warp portal.");
                return false;
            }
        }
        [HarmonyPatch(typeof(WarpPortal), "Discover")]
        public static class Patch_WarpPortal_Discover
        {
            public static bool Prefix(WarpPortal __instance)
            {
                if (__instance == null)
                    return false;

                if (WarpPortal_discovered_Field(__instance))
                    return false;

                WarpReceiver receiver = FindValidWarpReceiver(__instance, true);
                if (receiver == null)
                {
                    Debug.LogWarning("[Interstellar] Warp portal discover skipped because no valid receiver exists.");
                    return false;
                }

                int targetWorldId = receiver.GetMyWorldId();
                WorldContainer targetWorld = ClusterManager.Instance.GetWorld(targetWorldId);
                if (targetWorld == null)
                    return false;

                targetWorld.SetDiscovered(true);
                if (Components.LiveMinionIdentities.Count <= 0)
                {
                    WarpPortal_discovered_Field(__instance) = true;
                    return false;
                }

                SimpleEvent.StatesInstance smi = GameplayEventManager.Instance.StartNewEvent(Db.Get().GameplayEvents.WarpWorldReveal).smi as SimpleEvent.StatesInstance;
                if (smi == null)
                {
                    WarpPortal_discovered_Field(__instance) = true;
                    return false;
                }

                smi.minions = new GameObject[1] { Components.LiveMinionIdentities[0].gameObject };
                smi.callback = () =>
                {
                    WorldContainer focusWorld = ClusterManager.Instance.GetWorld(targetWorldId);
                    if (focusWorld == null)
                        return;

                    if (ManagementMenu.Instance != null)
                        ManagementMenu.Instance.OpenClusterMap();
                    if (ClusterMapScreen.Instance != null)
                        ClusterMapScreen.Instance.SetTargetFocusPosition(focusWorld.GetMyWorldLocation());
                };
                smi.ShowEventPopup();
                WarpPortal_discovered_Field(__instance) = true;
                return false;
            }
        }
        [HarmonyPatch(typeof(WarpPortal), "Warp")]
        public static class Patch_WarpPortal_Warp
        {
            public static bool Prefix(WarpPortal __instance)
            {
                if (__instance == null || __instance.worker == null || __instance.worker.HasTag(GameTags.Dying) || __instance.worker.HasTag(GameTags.Dead))
                    return false;

                WarpReceiver receiver = FindValidWarpReceiver(__instance, true);
                if (receiver != null)
                {
                    WarpPortal_delayWarpRoutine_Field(__instance) = __instance.StartCoroutine(__instance.DelayedWarp(receiver));
                }
                else
                {
                    Debug.LogWarning("[Interstellar] Warp cancelled because no valid receiver exists.");
                    try
                    {
                        __instance.CancelAssignment();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[Interstellar] Failed to cancel warp portal assignment: {ex.Message}");
                    }
                }

                if (SelectTool.Instance != null && SelectTool.Instance.selected == __instance.GetComponent<KSelectable>())
                    SelectTool.Instance.Select(null, true);

                return false;
            }
        }
        [HarmonyPatch(typeof(WarpPortal), nameof(WarpPortal.DelayedWarp))]
        public static class Patch_WarpPortal_DelayedWarp
        {
            public static bool Prefix(WarpPortal __instance, WarpReceiver receiver, ref IEnumerator __result)
            {
                if (IsValidWarpReceiverForPortal(__instance, receiver))
                    return true;

                Debug.LogWarning("[Interstellar] Delayed warp skipped because receiver is stale or belongs to a deleted world.");
                __result = EmptyEnumerator();
                return false;
            }
        }
        private static int GetRocketLandedWorldId(Clustercraft craft)
        {
            if (craft == null)
                return -1;

            CraftModuleInterface craftInterface = craft.ModuleInterface;
            if (craftInterface == null)
                return -1;

            if (craft.Status == Clustercraft.CraftStatus.Grounded)
            {
                LaunchPad currentPad = craftInterface.CurrentPad;
                if (currentPad != null)
                    return currentPad.GetMyWorldId();
            }

            return -1;
        }
        private static void DeleteRocketsNotLandedOnPreservedWorld()
        {
            if (PreservedWorldIdDuringClear < 0)
                return;

            HashSet<int> processedCraftIds = new HashSet<int>();
            List<Clustercraft> craftsToDelete = new List<Clustercraft>();

            foreach (Clustercraft craft in Components.Clustercrafts.Items)
            {
                if (craft == null || craft.gameObject == null)
                    continue;

                int craftId = craft.GetInstanceID();
                if (!processedCraftIds.Add(craftId))
                    continue;

                int landedWorldId = GetRocketLandedWorldId(craft);
                if (landedWorldId == PreservedWorldIdDuringClear)
                    continue;

                craftsToDelete.Add(craft);
                Debug.Log($"[MyWorldDumpMod] Mark rocket for delete. rocket={craft.Name}, landedWorldId={landedWorldId}, preservedWorldId={PreservedWorldIdDuringClear}");
            }

            // Fallback: scan launch pads for landed rockets to avoid missing any craft references.
            foreach (LaunchPad pad in Components.LaunchPads.Items)
            {
                if (pad == null)
                    continue;

                RocketModuleCluster landedRocket = pad.LandedRocket;
                if (landedRocket == null || landedRocket.CraftInterface == null)
                    continue;

                Clustercraft craft = landedRocket.CraftInterface.GetComponent<Clustercraft>();
                if (craft == null || craft.gameObject == null)
                    continue;

                int craftId = craft.GetInstanceID();
                if (!processedCraftIds.Add(craftId))
                    continue;

                int landedWorldId = pad.GetMyWorldId();
                if (landedWorldId == PreservedWorldIdDuringClear)
                    continue;

                craftsToDelete.Add(craft);
                Debug.Log($"[MyWorldDumpMod] Mark rocket for delete from LaunchPad scan. rocket={craft.Name}, padWorldId={landedWorldId}, preservedWorldId={PreservedWorldIdDuringClear}");
            }

            for (int i = 0; i < craftsToDelete.Count; i++)
            {
                Clustercraft craft = craftsToDelete[i];
                if (craft == null || craft.gameObject == null)
                    continue;
                craft.DestroyCraftAndModules();
            }

            int orphanModulesDeleted = 0;
            RocketModuleCluster[] modules = UnityEngine.Object.FindObjectsByType<RocketModuleCluster>(FindObjectsSortMode.None);
            for (int i = 0; i < modules.Length; i++)
            {
                RocketModuleCluster module = modules[i];
                if (module == null || module.gameObject == null)
                    continue;

                int moduleWorldId = module.GetMyWorldId();
                if (moduleWorldId == PreservedWorldIdDuringClear)
                    continue;

                CraftModuleInterface craftInterface = module.CraftInterface;
                Clustercraft moduleCraft = craftInterface != null ? craftInterface.GetComponent<Clustercraft>() : null;
                if (craftInterface == null || moduleCraft == null || moduleCraft.gameObject == null)
                {
                    orphanModulesDeleted++;
                    Debug.Log($"[MyWorldDumpMod] Delete orphan rocket module. module={module.name}, worldId={moduleWorldId}, preservedWorldId={PreservedWorldIdDuringClear}");
                    Util.KDestroyGameObject(module.gameObject);
                }
            }

            if (craftsToDelete.Count > 0)
                Debug.Log($"[MyWorldDumpMod] Deleted rockets not landed on preserved world. count={craftsToDelete.Count}, preservedWorldId={PreservedWorldIdDuringClear}");
            if (orphanModulesDeleted > 0)
                Debug.Log($"[MyWorldDumpMod] Deleted orphan rocket modules not on preserved world. count={orphanModulesDeleted}, preservedWorldId={PreservedWorldIdDuringClear}");
        }
        private static int CleanupStaleClusterCraftInteriorDoors(int currentWorldId)
        {
            if (currentWorldId < 0)
                return 0;

            List<ClustercraftInteriorDoor> staleInteriorDoors = null;
            foreach (ClustercraftInteriorDoor interiorDoor in Components.ClusterCraftInteriorDoors.Items)
            {
                if (interiorDoor == null || interiorDoor.gameObject == null)
                    continue;

                if (interiorDoor.GetMyWorldId() != currentWorldId)
                    continue;

                int doorCell = Grid.PosToCell((KMonoBehaviour)interiorDoor);
                if (!Grid.IsValidCell(doorCell))
                {
                    if (staleInteriorDoors == null)
                        staleInteriorDoors = new List<ClustercraftInteriorDoor>();
                    staleInteriorDoors.Add(interiorDoor);
                }
            }

            if (staleInteriorDoors == null || staleInteriorDoors.Count == 0)
                return 0;

            for (int i = 0; i < staleInteriorDoors.Count; i++)
                Components.ClusterCraftInteriorDoors.Remove(staleInteriorDoors[i]);

            return staleInteriorDoors.Count;
        }
        private static int GetRocketHostWorldIdForInterior(WorldContainer interiorWorld)
        {
            if (interiorWorld == null || !interiorWorld.IsModuleInterior)
                return -1;

            int parentWorldId = interiorWorld.ParentWorldId;
            if (parentWorldId >= 0 && parentWorldId != interiorWorld.id)
                return parentWorldId;

            ClustercraftInteriorDoor matchedInteriorDoor = null;
            foreach (ClustercraftInteriorDoor interiorDoor in Components.ClusterCraftInteriorDoors.Items)
            {
                if (interiorDoor == null || interiorDoor.gameObject == null)
                    continue;

                if (interiorDoor.GetMyWorldId() == interiorWorld.id)
                {
                    matchedInteriorDoor = interiorDoor;
                    break;
                }
            }

            ClustercraftExteriorDoor[] exteriorDoors = UnityEngine.Object.FindObjectsByType<ClustercraftExteriorDoor>(FindObjectsSortMode.None);
            for (int i = 0; i < exteriorDoors.Length; i++)
            {
                ClustercraftExteriorDoor exteriorDoor = exteriorDoors[i];
                if (exteriorDoor == null || !exteriorDoor.HasTargetWorld())
                    continue;

                bool isMatched = false;
                if (matchedInteriorDoor != null && exteriorDoor.GetInteriorDoor() == matchedInteriorDoor)
                    isMatched = true;
                else
                {
                    WorldContainer targetWorld = exteriorDoor.GetTargetWorld();
                    if (targetWorld != null && targetWorld.id == interiorWorld.id)
                        isMatched = true;
                }

                if (!isMatched)
                    continue;

                return exteriorDoor.GetMyWorldId();
            }

            return parentWorldId;
        }
        private static string GetIncrementedNewWorldSavePath(string currentPath)
        {
            if (string.IsNullOrWhiteSpace(currentPath))
                return currentPath;

            const string suffix = "_NewWorld";
            string dir = System.IO.Path.GetDirectoryName(currentPath);
            string ext = System.IO.Path.GetExtension(currentPath);
            string name = System.IO.Path.GetFileNameWithoutExtension(currentPath);

            string baseName = name;
            int nextIndex = 0;

            int suffixPos = name.LastIndexOf(suffix, StringComparison.Ordinal);
            if (suffixPos >= 0)
            {
                int digitsStart = suffixPos + suffix.Length;
                string trailing = digitsStart < name.Length ? name.Substring(digitsStart) : string.Empty;
                bool trailingIsDigits = trailing.Length > 0 && trailing.All(char.IsDigit);

                if (digitsStart == name.Length)
                {
                    baseName = name.Substring(0, suffixPos);
                    nextIndex = 0;
                }
                else if (trailingIsDigits)
                {
                    baseName = name.Substring(0, suffixPos);
                    int parsed;
                    nextIndex = int.TryParse(trailing, out parsed) ? parsed + 1 : 0;
                }
            }

            string newName = $"{baseName}{suffix}{nextIndex}";
            return System.IO.Path.Combine(dir, newName + ext);
        }
        private static void ClearWorld(WorldContainer world, Action<float> report)
        {
            ConduitFlow_DO_NOT_Dump.do_not_dump = true;
            HashSetPool<GameObject, SandboxDestroyerTool>.PooledHashSet pooledHashSet = HashSetPool<GameObject, SandboxDestroyerTool>.Allocate();
            CleanupWarpTeleportersForClearedWorld(world, (HashSet<GameObject>)pooledHashSet);

            var count = EntombedItemManager_pickupables_Field(SaveGame.Instance.entombedItemManager);
            for (int i = count.Count - 1; i >= 0; i--)
            {
                Pickupable pickup = EntombedItemManager_pickupables_Field(SaveGame.Instance.entombedItemManager)[i];
                int pickupCell = Grid.PosToCell((KMonoBehaviour)pickup);
                if (!Grid.IsValidCell(pickupCell))
                    continue;
                if (Grid.WorldIdx[pickupCell] == world.id)
                {
                    EntombedItemManager_pickupables_Field(SaveGame.Instance.entombedItemManager).RemoveAt(i);
                }
            }
            var cells = EntombedItemManager_cells_Field(SaveGame.Instance.entombedItemManager);
            for (int i = cells.Count - 1; i >= 0; i--)
            {
                int entombedCell = EntombedItemManager_cells_Field(SaveGame.Instance.entombedItemManager)[i];
                if (!Grid.IsValidCell(entombedCell))
                    continue;
                if (Grid.WorldIdx[entombedCell] == world.id)
                {
                    EntombedItemManager_cells_Field(SaveGame.Instance.entombedItemManager).RemoveAt(i);
                    EntombedItemManager_elementIds_Field(SaveGame.Instance.entombedItemManager).RemoveAt(i);
                    EntombedItemManager_masses_Field(SaveGame.Instance.entombedItemManager).RemoveAt(i);
                    EntombedItemManager_temperatures_Field(SaveGame.Instance.entombedItemManager).RemoveAt(i);
                    EntombedItemManager_diseaseIndices_Field(SaveGame.Instance.entombedItemManager).RemoveAt(i);
                    EntombedItemManager_diseaseCounts_Field(SaveGame.Instance.entombedItemManager).RemoveAt(i);

                    Game.Instance.GetComponent<EntombedItemVisualizer>().ForceClear(i);
                }
            }
            foreach (BuildingComplete building in Components.BuildingCompletes.Items)
            {
                int buildingCell = Grid.PosToCell((KMonoBehaviour)building);
                if (!Grid.IsValidCell(buildingCell))
                    continue;
                if (Grid.WorldIdx[buildingCell] == world.id)
                    pooledHashSet.Add(building.gameObject);
            }

            foreach (Pickupable cmp in Components.Pickupables.Items)
            {
                int pickupableCell = Grid.PosToCell((KMonoBehaviour)cmp);
                if (!Grid.IsValidCell(pickupableCell))
                    continue;
                if (Grid.WorldIdx[pickupableCell] == world.id)
                    pooledHashSet.Add(cmp.gameObject);
            }

            foreach (Geyser cmp in Components.Geysers.GetItems(world.id))
            {
                pooledHashSet.Add(cmp.gameObject);
            }

            foreach (Crop cmp in Components.Crops.Items)
            {
                int cropCell = Grid.PosToCell((KMonoBehaviour)cmp);
                if (!Grid.IsValidCell(cropCell))
                    continue;
                if (Grid.WorldIdx[cropCell] == world.id)
                    pooledHashSet.Add(cmp.gameObject);
            }
            foreach (Health cmp in Components.Health.Items)
            {
                int healthCell = Grid.PosToCell((KMonoBehaviour)cmp);
                if (!Grid.IsValidCell(healthCell))
                    continue;
                if (Grid.WorldIdx[healthCell] == world.id)
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
        private static void ClearOldTempFiles()
        {
            string[] DumpedFilesPath = DumpedFiles.ToArray();
            for (int i = 0; i < DumpedFilesPath.Length; i++)
            {
                string path = DumpedFilesPath[i];
                if (File.Exists(path))
                {
                    try
                    {
                        File.Delete(path);
                        Debug.Log($"[MyWorldDumpMod] Deleted dump file: {path}");
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[MyWorldDumpMod] Failed to delete dump file: {path}, error: {ex.Message}");
                    }
                }
                DumpedFiles.Remove(path);
            }

            try
            {
                string dumpDir = GetGridDumpDirectory();
                Directory.CreateDirectory(dumpDir);
                if (Directory.Exists(dumpDir))

                {
                    string[] staleDumpFiles = Directory.GetFiles(dumpDir, "grid_world_*.csv", SearchOption.TopDirectoryOnly);
                    for (int i = 0; i < staleDumpFiles.Length; i++)
                    {
                        string stalePath = staleDumpFiles[i];
                        try
                        {
                            if (File.Exists(stalePath))
                            {
                                File.Delete(stalePath);
                                Debug.Log($"[MyWorldDumpMod] Deleted stale dump file: {stalePath}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning($"[MyWorldDumpMod] Failed to delete stale dump file: {stalePath}, error: {ex.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MyWorldDumpMod] ClearOldTempFiles directory scan failed: {ex.Message}");
            }
        }
        private static bool IsClusterLayoutLevelAllowedByActiveDlc(string levelId)
        {
            if (string.IsNullOrWhiteSpace(levelId))
                return false;

            int sep = levelId.IndexOf("::", StringComparison.Ordinal);
            if (sep <= 0)
                return true; // Vanilla entry without DLC prefix.

            string dlcPrefix = levelId.Substring(0, sep).ToLowerInvariant();
            switch (dlcPrefix)
            {
                case "expansion1":
                    return DlcManager.IsContentSubscribed("EXPANSION1_ID");
                case "dlc2":
                    return DlcManager.IsContentSubscribed("DLC2_ID");
                case "dlc3":
                    return DlcManager.IsContentSubscribed("DLC3_ID");
                case "dlc4":
                    return DlcManager.IsContentSubscribed("DLC4_ID");
                default:
                    return false;
            }
        }
        private static string GetBackupSavePath()
        {
            string assemblyLocation = Assembly.GetExecutingAssembly().Location;
            string modDir = System.IO.Path.GetDirectoryName(assemblyLocation);
            if (string.IsNullOrWhiteSpace(modDir))
            {
                Debug.LogWarning("[MyWorldDumpMod] Backup skipped: mod directory not found.");
                return null;
            }

            string backupDir = System.IO.Path.Combine(modDir, "backup");
            if (!Directory.Exists(backupDir))
                Directory.CreateDirectory(backupDir);

            string timestamp = System.DateTime.Now.ToString("yyyy_MM_dd_HH_mm_ss", CultureInfo.InvariantCulture);
            string backupPath = System.IO.Path.Combine(backupDir, $"backup{timestamp}.sav");
            if (File.Exists(backupPath))
            {
                int suffix = 1;
                while (File.Exists(backupPath))
                {
                    backupPath = System.IO.Path.Combine(backupDir, $"backup{timestamp}_{suffix}.sav");
                    suffix++;
                }
            }
            return backupPath;
        }
        private static void BackupCurrentSaveToModFolder(string backupPath)
        {
            string originalActivePath = SaveLoader.GetActiveSaveFilePath();
            try
            {
                if (SaveLoader.Instance != null)
                {
                    SaveLoader.Instance.Save(backupPath);
                    Debug.Log($"[MyWorldDumpMod] Backup save created: {backupPath}");
                    return;
                }

                string activePath = SaveLoader.GetActiveSaveFilePath();
                if (!string.IsNullOrWhiteSpace(activePath) && File.Exists(activePath))
                {
                    File.Copy(activePath, backupPath, overwrite: false);
                    Debug.Log($"[MyWorldDumpMod] Backup save copied: {backupPath}");
                    return;
                }

                Debug.LogWarning("[MyWorldDumpMod] Backup skipped: no active save path found.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MyWorldDumpMod] Backup save failed: {ex}");
            }
            finally
            {
                if (!string.IsNullOrWhiteSpace(originalActivePath))
                {
                    SaveLoader.SetActiveSaveFilePath(originalActivePath);
                    Debug.Log($"[MyWorldDumpMod] Restored active save path after backup: {originalActivePath}");
                }
            }
        }
        public static void CreateNewWorlds()
        {
            string BackupSavePath = GetBackupSavePath();
            ResetRestoreDebugLog();
            EngineConsole.ClearStaticDestinationAndStopForStarmapRefresh("before_create_new_worlds");
            LoadingOverlay.Load((System.Action)(() =>
            {
                DefaultOverlayFontSize = LoadingOverlayTextHelper.GetFontSize();
                LoadingOverlayTextHelper.SetFontSize(DefaultOverlayFontSize * 0.5f);
                LoadingOverlayTextHelper.SetText(Interstellar.NewWorldTip1 + BackupSavePath);
            }));

            BackupCurrentSaveToModFolder(BackupSavePath);
            ClearOldTempFiles();
            ResetWorldGenRetryState();
            List<SettingLevel> settings = new List<SettingLevel>(CustomGameSettingConfigs.ClusterLayout.levels);

            SpeedControlScreen.Instance.Pause();

            filename = SaveLoader.GetActiveSaveFilePath();
            KRandom random = new KRandom();

            CustomGameSettings.Instance.SetQualitySetting(CustomGameSettingConfigs.WorldgenSeed, random.Next().ToString());
            List<SettingLevel> clusterCandidates = settings
                .Skip(1) // Ignore the first levels ID as requested.
                .Where(s => s != null && IsClusterLayoutLevelAllowedByActiveDlc(s.id))
                .ToList();
            SettingLevel selectedCluster = clusterCandidates.Count > 0
                ? clusterCandidates[UnityEngine.Random.Range(0, clusterCandidates.Count)]
                : settings.Skip(1).FirstOrDefault() ?? settings.FirstOrDefault();
            if (selectedCluster != null)
            {
                CustomGameSettings.Instance.SetQualitySetting(CustomGameSettingConfigs.ClusterLayout, selectedCluster.id);
                Debug.Log($"[MyWorldDumpMod] Selected random ClusterLayout ID: {selectedCluster.id}, candidateCount={clusterCandidates.Count}");
            }
            else
            {
                Debug.LogWarning("[MyWorldDumpMod] No ClusterLayout setting available to select.");
            }

            LoadingOverlay.Load((System.Action)(() =>
            {
                LoadingOverlayTextHelper.SetFontSize(DefaultOverlayFontSize * 0.5f);
                LoadingOverlayTextHelper.SetText(Interstellar.NewWorldTip1 + BackupSavePath);
                Debug.Log($"[MyWorldDumpMod] Reveal Old World");

                WorldContainer[] Worlds = ClusterManager.Instance.WorldContainers.ToArray();
                for (int i = 1; i < ClusterManager.Instance.WorldContainers.Count; i++)
                {
                    Debug.Log($"[MyWorldDumpMod] Revealing Old World id = {Worlds[i].id}");

                    ClusterManager.Instance.SetActiveWorld(Worlds[i].id);
                    Worlds[i].SetDiscovered();
                    for (int k = 0; k < Grid.CellCount; k++)
                    {
                        if (!Grid.IsValidCellInWorld(k, Worlds[i].id))
                            continue;
                        if (Grid.WorldIdx[k] != Worlds[i].id)
                            continue;

                        Grid.Reveal(k, forceReveal: true);
                    }
                }
                IsOldReveal = true;
            }));
        }
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == "frontend" && Switch == true && IsNewWorld == false && IsOldWorld == false)
            {
                SceneManager.sceneLoaded -= OnSceneLoaded;
                Util.KInstantiateUI<WorldGenScreen>(ScreenPrefabs.Instance.WorldGenScreen.gameObject, FrontEndManager_OnPrefabInit_Patch.MyCanvas, true);
            }

            if (scene.name == "frontend" && Switch == true && IsNewWorld == true && IsClearWorlds == true && IsReveal == true && IsOldWorld == true && IsUndump == true)
            {
                LoadingOverlay.Load((System.Action)(() =>
                {
                    SceneManager.sceneLoaded -= OnSceneLoaded;

                    SaveLoader.SetActiveSaveFilePath(filename);
                    App.LoadScene("backend");
                }));
            }
        }

        [HarmonyPatch(typeof(SaveLoader))]
        [HarmonyPatch(nameof(SaveLoader.Load), new System.Type[] { typeof(string) })]
        public static class SaveLoader_Load_Patch
        {
            public static void Postfix(string filename)
            {
                Debug.Log($"[MyWorldDumpMod] SaveLoader.Load called with filename: {filename}");
                if (Mod.Switch == true && Mod.IsNewWorld == true && Mod.IsClearWorlds == true && Mod.IsReveal == true && Mod.IsOldWorld == true && Mod.IsUndump == true)
                {
                    IsReload = true;
                }
                if (Mod.Switch == true && Mod.IsNewWorld == true && Mod.IsClearWorlds == true && Mod.IsReveal == true && Mod.IsOldWorld == false && Mod.IsUndump == false)
                {
                    Mod.IsOldWorld = true;
                }
            }
        }

        [HarmonyPatch(typeof(SaveLoader), "LoadFromWorldGen")]
        public static class SaveLoader_LoadFromWorldGen_Patch
        {
            public static void Postfix()
            {
                Debug.Log($"[MyWorldDumpMod] SaveLoader.LoadFromWorldGen called");
                if (Mod.Switch == true)
                {
                    IsNewWorld = true;
                    ResetWorldGenRetryState();
                }
            }
        }
        [HarmonyPatch(typeof(RetireColonyUtility), "SaveColonySummaryData")]
        public static class RetireColonyUtility_SaveColonySummaryData_Patch
        {
            public static bool Prefix()
            {
                if (Switch == true && IsOldReveal == true && IsNewWorld == true)
                {
                    return false;
                }
                return true;
            }
        }

        [HarmonyPatch(typeof(WorldGenScreen), "OnSpawn")]
        public static class WorldGenScreen_OnSpawn_Keep3Lines_Patch
        {
            //base.OnSpawn() —— base 类型是 NewGameFlowScreen（从你 decompile 的代码可见）
            private static readonly Action<NewGameFlowScreen> Base_OnSpawn = AccessTools.MethodDelegate<Action<NewGameFlowScreen>>(AccessTools.Method(typeof(NewGameFlowScreen), "OnSpawn"), null, false, Type.EmptyTypes);

            // this.TriggerLoadingMusic() —— private 方法
            private static readonly Action<WorldGenScreen> TriggerLoadingMusic = AccessTools.MethodDelegate<Action<WorldGenScreen>>(AccessTools.Method(typeof(WorldGenScreen), "TriggerLoadingMusic"), null, false, Type.EmptyTypes);

            // this.offlineWorldGen —— private 字段（[MyCmpReq] OfflineWorldGen offlineWorldGen）
            private static readonly AccessTools.FieldRef<WorldGenScreen, OfflineWorldGen> OfflineWorldGenRef = AccessTools.FieldRefAccess<WorldGenScreen, OfflineWorldGen>("offlineWorldGen");

            // Prefix 返回 false => 跳过原 OnSpawn
            public static bool Prefix(WorldGenScreen __instance)
            {
                Debug.Log($"world gen start");
                if (Switch == false)
                    return true; // 执行原始 OnSpawn
                Base_OnSpawn(__instance);

                TriggerLoadingMusic(__instance);

                SaveLoader.SetActiveSaveFilePath((string)null);
                try
                {
                    Debug.Log($"[MyWorldDumpMod] Attempting to delete worldgen save file: {ProcGenGame.WorldGen.WORLDGEN_SAVE_FILENAME}");
                    if (System.IO.File.Exists(ProcGenGame.WorldGen.WORLDGEN_SAVE_FILENAME))
                        System.IO.File.Delete(ProcGenGame.WorldGen.WORLDGEN_SAVE_FILENAME);
                }
                catch (Exception ex)
                {
                    DebugUtil.LogWarningArgs((object)ex.ToString());
                }

                ref OfflineWorldGen owg = ref OfflineWorldGenRef(__instance);
                if (owg != null)
                    owg.Generate();
                else
                    Debug.LogWarning("[MyMod] offlineWorldGen is null; skipped Generate().");

                return false;
            }
        }

        [HarmonyPatch(typeof(OfflineWorldGen), "OnError")]
        public static class OfflineWorldGen_OnError_RetryPatch
        {
            public static bool Prefix(OfflineWorldGen.ErrorInfo error)
            {
                if (!Switch)
                    return true;

                lock (WorldGenRetryLock)
                {
                    if (WorldGenRetryCount >= MaxWorldGenRetryCount)
                    {
                        Debug.LogWarning($"[MyWorldDumpMod] WorldGen retry limit reached ({WorldGenRetryCount}/{MaxWorldGenRetryCount}). Fallback to default error flow.");
                        return true;
                    }

                    WorldGenRetryPending = true;
                    WorldGenLastErrorDesc = error.errorDesc ?? "";
                }

                Debug.LogWarning($"[MyWorldDumpMod] Intercepted worldgen failure. Queueing retry. error={error.errorDesc}");
                return false;
            }
        }

        [HarmonyPatch(typeof(OfflineWorldGen), "DoExitFlow")]
        public static class OfflineWorldGen_DoExitFlow_RetryPatch
        {
            public static bool Prefix()
            {
                if (!Switch)
                    return true;

                lock (WorldGenRetryLock)
                {
                    if (WorldGenRetryPending && WorldGenRetryCount < MaxWorldGenRetryCount)
                    {
                        Debug.Log("[MyWorldDumpMod] Blocked OfflineWorldGen.DoExitFlow because retry is pending.");
                        return false;
                    }
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(OfflineWorldGen), "Update")]
        public static class OfflineWorldGen_Update_RetryPatch
        {
            private static readonly AccessTools.FieldRef<OfflineWorldGen, bool> LoadTriggeredRef =
                AccessTools.FieldRefAccess<OfflineWorldGen, bool>("loadTriggered");
            private static readonly AccessTools.FieldRef<OfflineWorldGen, bool> StartedExitFlowRef =
                AccessTools.FieldRefAccess<OfflineWorldGen, bool>("startedExitFlow");
            private static readonly AccessTools.FieldRef<OfflineWorldGen, bool> ShouldStopRef =
                AccessTools.FieldRefAccess<OfflineWorldGen, bool>("shouldStop");
            private static readonly AccessTools.FieldRef<OfflineWorldGen, float> CurrentPercentRef =
                AccessTools.FieldRefAccess<OfflineWorldGen, float>("currentPercent");
            private static readonly AccessTools.FieldRef<OfflineWorldGen, StringKey> CurrentConvertedCurrentStageRef =
                AccessTools.FieldRefAccess<OfflineWorldGen, StringKey>("currentConvertedCurrentStage");
            private static readonly AccessTools.FieldRef<OfflineWorldGen, ProcGenGame.Cluster> ClusterRef =
                AccessTools.FieldRefAccess<OfflineWorldGen, ProcGenGame.Cluster>("cluster");
            private static readonly MethodInfo DoWorldGenInitializeMethod =
                AccessTools.Method(typeof(OfflineWorldGen), "DoWorldGenInitialize");

            public static void Postfix(OfflineWorldGen __instance)
            {
                if (!Switch)
                    return;

                bool shouldRetry;
                lock (WorldGenRetryLock)
                {
                    shouldRetry = WorldGenRetryPending && WorldGenRetryCount < MaxWorldGenRetryCount;
                }
                if (!shouldRetry)
                    return;

                ProcGenGame.Cluster cluster = ClusterRef(__instance);
                if (cluster != null && cluster.IsGenerating)
                    return;

                int nextSeed;
                lock (WorldGenRetryLock)
                {
                    WorldGenRetryCount++;
                    nextSeed = WorldGenRetryRng.Next(1, int.MaxValue);
                    WorldGenRetryPending = false;
                }

                Debug.LogWarning($"[MyWorldDumpMod] Retrying worldgen with new seed={nextSeed} attempt={WorldGenRetryCount}/{MaxWorldGenRetryCount} lastError={WorldGenLastErrorDesc}");

                if (CustomGameSettings.Instance == null)
                {
                    Debug.LogWarning("[MyWorldDumpMod] CustomGameSettings.Instance is null. Skip worldgen retry.");
                    return;
                }

                CustomGameSettings.Instance.SetQualitySetting(CustomGameSettingConfigs.WorldgenSeed, nextSeed.ToString());
                ShouldStopRef(__instance) = false;
                LoadTriggeredRef(__instance) = false;
                StartedExitFlowRef(__instance) = false;
                CurrentPercentRef(__instance) = 0f;
                CurrentConvertedCurrentStageRef(__instance) = UI.WORLDGEN.GENERATESOLARSYSTEM.key;

                if (DoWorldGenInitializeMethod != null)
                    DoWorldGenInitializeMethod.Invoke(__instance, null);
                else
                    Debug.LogWarning("[MyWorldDumpMod] DoWorldGenInitialize method not found. Cannot retry worldgen.");
            }
        }
        public static class LoadingOverlayTextHelper
        {
            private static readonly FieldInfo OverlayInstanceField = typeof(LoadingOverlay).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static);
            private static LocText GetOverlayLocText()
            {
                var overlay = OverlayInstanceField?.GetValue(null) as LoadingOverlay;
                if (overlay == null)
                    return null;
                return overlay.GetComponentInChildren<LocText>(true);
            }
            public static bool SetText(string text)
            {
                var locText = GetOverlayLocText();
                if (locText == null)
                    return false;

                locText.SetText(text ?? string.Empty);
                return true;
            }
            public static float GetFontSize()
            {
                var locText = GetOverlayLocText();
                if (locText == null)
                    return -1;
                return locText.fontSize;
            }
            public static bool SetFontSize(float size)
            {
                var locText = GetOverlayLocText();
                if (locText == null)
                    return false;
                locText.fontSize = size;
                return true;
            }
        }

        [HarmonyPatch(typeof(SandboxFOWTool), "OnPaintCell")]
        public static class test_Patch
        {
            public static void Prefix(int cell, int distFromOrigin)
            {
                if (InterstellarModConsole.Instance.OptionsDebugMode == true && FowPaintLog)
                    Debug.Log($"[MyWorldDumpMod] SandboxFOWTool.OnPaintCell cell={Grid.CellToXY(cell)} distFromOrigin={distFromOrigin} world={Grid.WorldIdx[cell]} revealed={Grid.IsVisible(cell)}");
            }
        }
    }
}
