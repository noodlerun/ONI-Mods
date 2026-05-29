using HarmonyLib;
using KSerialization;
using System.Collections.Generic;
using UnityEngine;

namespace Interstellar
{
    [HarmonyPatch(typeof(Geyser), "OnSpawn")]
    internal static class Patch_Geyser_OnSpawn_AddProgressBarSideScreen
    {
        public static void Postfix(Geyser __instance)
        {
            ExtendedGeysersSidescreen.AddProgressBarComponent(__instance);
        }
    }

    [HarmonyPatch(typeof(GeyserGenericConfig), nameof(GeyserGenericConfig.CreateGeyser), new System.Type[]
    {
        typeof(string),
        typeof(string),
        typeof(int),
        typeof(int),
        typeof(string),
        typeof(string),
        typeof(HashedString),
        typeof(float),
        typeof(string[]),
        typeof(string[])
    })]
    internal static class Patch_GeyserGenericConfig_CreateGeyser_AddGeyserStorageComponents
    {
        public static void Postfix(GameObject __result)
        {
            __result.AddOrGet<GeyserProgressBarSideScreen>();
            __result.AddOrGet<GeyserDepletedDeconstructButton>();
        }
    }

    [HarmonyPatch(typeof(ElementEmitter), nameof(ElementEmitter.SetEmitting))]
    internal static class Patch_ElementEmitter_SetEmitting_TrackGeyserEmission
    {
        public static void Prefix(ElementEmitter __instance, ref bool emitting)
        {
            if (__instance == null)
            {
                return;
            }

            GeyserProgressBarSideScreen component = __instance.GetComponent<GeyserProgressBarSideScreen>();
            component?.OnEmitterSetEmitting(__instance, ref emitting);
        }
    }

    internal static class ExtendedGeysersSidescreen
    {
        public static readonly StatusItem GeyserStorageDepletedStatusItem = CreateGeyserStorageDepletedStatusItem();

        // GeyserStorageRoll BaseValue 单位是 kg；实际储量会乘以 RandomMin/RandomMax 随机浮动。
        public static readonly Dictionary<string, GeyserStorageRoll> GeyserStorageValues = new Dictionary<string, GeyserStorageRoll>
        {
            { GeyserGenericConfig.Steam, new GeyserStorageRoll(200000f, 0.5f, 3.2f) }, // 蒸汽喷孔 base mass 100T
            { GeyserGenericConfig.HotSteam, new GeyserStorageRoll(75000f, 0.3f, 5.2f) }, // 高温蒸汽喷孔 base mass 75T
            { GeyserGenericConfig.HotWater, new GeyserStorageRoll(10000000f, 0.8f, 9.0f) }, // 高温水泉 base mass 10000T
            { GeyserGenericConfig.SlushWater, new GeyserStorageRoll(500000f, 1.8f, 4.2f) }, // 低温泥浆泉 base mass 500T
            { GeyserGenericConfig.FilthyWater, new GeyserStorageRoll(300000f, 2.8f, 6.0f) }, // 污染水泉 base mass 300T
            { GeyserGenericConfig.SlushSaltWater, new GeyserStorageRoll(150000f, 0.2f, 3.0f) }, // 低温盐泥浆泉 base mass 150T
            { GeyserGenericConfig.SaltWater, new GeyserStorageRoll(3000000f, 0.8f, 1.2f) }, // 盐水泉 base mass 3000T
            { GeyserGenericConfig.SmallVolcano, new GeyserStorageRoll(90000f, 0.6f, 7.2f) }, // 小型火山 base mass 90T
            { GeyserGenericConfig.BigVolcano, new GeyserStorageRoll(190000f, 0.8f, 7.2f) }, // 火山 base mass 190T
            { GeyserGenericConfig.LiquidCO2, new GeyserStorageRoll(105000f, 0.8f, 1.2f) }, // 液态二氧化碳泉 base mass 105T
            { GeyserGenericConfig.HotCO2, new GeyserStorageRoll(105000f, 0.8f, 1.2f) }, // 高温二氧化碳喷孔 base mass 105T
            { GeyserGenericConfig.HotHydrogen, new GeyserStorageRoll(700000f, 0.8f, 5.2f) }, // 高温氢气喷孔 base mass 700T
            { GeyserGenericConfig.HotPO2, new GeyserStorageRoll(120000f, 0.8f, 10.2f) }, // 高温污染氧喷孔 base mass 120T
            { GeyserGenericConfig.SlimyPO2, new GeyserStorageRoll(120000f, 1.8f, 15.2f) }, // 含菌污染氧喷孔 base mass 120T
            { GeyserGenericConfig.ChlorineGas, new GeyserStorageRoll(105000f, 0.1f, 20.2f) }, // 氯气喷孔 base mass 105T
            { GeyserGenericConfig.ChlorineGasCool, new GeyserStorageRoll(105000f, 0.1f, 16.2f) }, // 低温氯气喷孔 base mass 105T
            { GeyserGenericConfig.Methane, new GeyserStorageRoll(1050000f, 1.0f, 25f) }, // 天然气间歇泉 base mass 1050T
            { GeyserGenericConfig.MoltenCopper, new GeyserStorageRoll(300000f, 1.9f, 3.2f) }, // 铜火山 base mass 300T 
            { GeyserGenericConfig.MoltenIron, new GeyserStorageRoll(300000f, 1.0f, 3.2f) }, // 铁火山 base mass 300T 
            { GeyserGenericConfig.MoltenGold, new GeyserStorageRoll(300000f, 1.0f, 3.2f) }, // 金火山 base mass 300T 
            { GeyserGenericConfig.MoltenAluminum, new GeyserStorageRoll(300000f, 1.0f, 3.2f) }, // 铝火山 base mass 300T 
            { GeyserGenericConfig.MoltenTungsten, new GeyserStorageRoll(300000f, 1.0f, 3.2f) }, // 钨火山 base mass 300T 
            { GeyserGenericConfig.MoltenNiobium, new GeyserStorageRoll(1300000f, 0.8f, 2.0f) }, // 铌火山 base mass 1300T
            { GeyserGenericConfig.MoltenCobalt, new GeyserStorageRoll(300000f, 1.0f, 3.2f) }, // 钴火山 base mass 300T
            { GeyserGenericConfig.OilDrip, new GeyserStorageRoll(3000000f, 1.0f, 6.2f) }, // 原油裂缝 base mass 3000T
            { GeyserGenericConfig.LiquidSulfur, new GeyserStorageRoll(700000f, 1.0f, 3.2f) } // 液态硫泉 base mass 700T
        };

        private static StatusItem CreateGeyserStorageDepletedStatusItem()
        {
            StatusItem item = new StatusItem(
                "InterstellarGeyserStorageDepleted",
                Interstellar.GeysersText4,
                Interstellar.GeysersText5,
                "",
                StatusItem.IconType.Exclamation,
                NotificationType.Bad,
                false,
                OverlayModes.None.ID);
            item.showInHoverCardOnly = true;
            return item;
        }

        public static GeyserProgressBarSideScreen AddProgressBarComponent(Geyser geyser)
        {
            if (geyser == null)
            {
                return null;
            }

            GeyserProgressBarSideScreen component = geyser.gameObject.AddOrGet<GeyserProgressBarSideScreen>();
            geyser.gameObject.AddOrGet<GeyserDepletedDeconstructButton>().EnsureSubscribed();
            component.Initialize(geyser);
            return component;
        }

        public static void DeleteNeutroniumUnderGeyser(GameObject geyser)
        {
            if (geyser == null)
            {
                return;
            }

            int originCell = Grid.PosToCell(geyser.transform.GetPosition());
            int worldId = geyser.GetMyWorldId();
            for (int offsetX = -3; offsetX <= 3; offsetX++)
            {
                int cell = Grid.OffsetCell(originCell, offsetX, -1);
                if (!Grid.IsValidCell(cell) || !Grid.IsValidCellInWorld(cell, worldId))
                {
                    continue;
                }

                Element element = Grid.Element[cell];
                if (element != null && element.IsSolid && element.id == SimHashes.Unobtanium)
                {
                    SimMessages.ReplaceElement(cell, SimHashes.Vacuum, CellEventLogger.Instance.DebugTool, 0f);
                }
            }
        }

        public static string GetGeyserTypeId(Geyser geyser)
        {
            return geyser?.configuration?.geyserType?.id;
        }

        public static bool TryGetStorageRoll(Geyser geyser, out GeyserStorageRoll value)
        {
            value = default;
            string geyserTypeId = GetGeyserTypeId(geyser);
            return !string.IsNullOrEmpty(geyserTypeId) && GeyserStorageValues.TryGetValue(geyserTypeId, out value);
        }

        public static float RollStorageAmount(GeyserStorageRoll roll)
        {
            return roll.BaseValue * Random.Range(roll.RandomMin, roll.RandomMax);
        }
    }

    internal struct GeyserStorageRoll
    {
        public readonly float BaseValue;
        public readonly float RandomMin;
        public readonly float RandomMax;

        public GeyserStorageRoll(float baseValue, float randomMin, float randomMax)
        {
            BaseValue = baseValue;
            RandomMin = randomMin;
            RandomMax = randomMax;
        }
    }

    [SerializationConfig(MemberSerialization.OptIn)]
    public class GeyserProgressBarSideScreen : KMonoBehaviour, IProgressBarSideScreen, ISim1000ms
    {
        [Serialize]
        public string geyserTypeId;

        [Serialize]
        public float geyserStorageAmount;

        [Serialize]
        public float totalEruptedAmount;

        [Serialize]
        private bool depletionNotificationSent;

        private ElementEmitter emitter;
        private GameObject damageFx;
        private System.Guid depletedStatusItemHandle = System.Guid.Empty;

        public void Initialize(Geyser geyser)
        {
            geyserTypeId = ExtendedGeysersSidescreen.GetGeyserTypeId(geyser);
            emitter = geyser?.GetComponent<ElementEmitter>();
            if (geyserStorageAmount > 0f || !ExtendedGeysersSidescreen.TryGetStorageRoll(geyser, out GeyserStorageRoll roll))
            {
                if (IsStorageDepleted())
                {
                    ApplyDepletedState();
                }
                return;
            }

            geyserStorageAmount = ExtendedGeysersSidescreen.RollStorageAmount(roll);
        }

        protected override void OnCleanUp()
        {
            base.OnCleanUp();
            CleanupDamageFx();
        }

        public void PrepareForComponentRemoval()
        {
            CleanupDamageFx();
        }

        private void CleanupDamageFx()
        {
            if (damageFx != null)
            {
                Util.KDestroyGameObject(damageFx);
                damageFx = null;
            }
        }

        public void Sim1000ms(float dt)
        {
            TrackActualEmission(dt);

            if (!IsStorageDepleted())
            {
                return;
            }

            totalEruptedAmount = Mathf.Max(totalEruptedAmount, geyserStorageAmount);
            ApplyDepletedState();
            if (emitter != null)
            {
                emitter.SetEmitting(false);
            }
        }

        public void OnEmitterSetEmitting(ElementEmitter emitter, ref bool emitting)
        {
            if (emitter == null || emitter.GetComponent<Geyser>() == null)
            {
                return;
            }

            this.emitter = emitter;
            if (emitting)
            {
                if (IsStorageDepleted())
                {
                    emitting = false;
                    ApplyDepletedState();
                    return;
                }
            }
        }

        private void TrackActualEmission(float dt)
        {
            if (emitter == null || !emitter.IsSimActive || emitter.isEmitterBlocked || IsStorageDepleted())
            {
                return;
            }

            float emittedAmount = Mathf.Max(0f, emitter.outputElement.massGenerationRate) * Mathf.Max(0f, dt);
            if (emittedAmount <= 0f)
            {
                return;
            }

            totalEruptedAmount = Mathf.Min(geyserStorageAmount, totalEruptedAmount + emittedAmount);
        }

        public float GetCurrentEruptedAmount()
        {
            return totalEruptedAmount;
        }

        public float GetRemainingStorageAmount()
        {
            return Mathf.Max(0f, geyserStorageAmount - GetCurrentEruptedAmount());
        }

        public float GetRemainingStoragePercentage()
        {
            if (geyserStorageAmount <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(GetRemainingStorageAmount() / geyserStorageAmount);
        }

        private bool IsStorageDepleted()
        {
            return geyserStorageAmount > 0f && GetCurrentEruptedAmount() >= geyserStorageAmount;
        }

        private void ApplyDepletedState()
        {
            StopGeyserStateMachine();
            ClearConflictingSpoutStatusItems();

            KBatchedAnimController controller = GetComponent<KBatchedAnimController>();
            if (controller != null)
            {
                controller.ClearQueue();
                if (controller.HasAnimation((HashedString)"inactive"))
                {
                    controller.Play((HashedString)"inactive", KAnim.PlayMode.Loop);
                }
                else
                {
                    controller.Stop();
                }
                controller.SetBlendValue(1f);
            }

            if (damageFx == null)
            {
                KBatchedAnimController fxController = FXHelpers.CreateEffect("smoke_damage_kanim", transform.GetPosition() + new Vector3(0f, 1f, 0f), transform);
                damageFx = fxController.gameObject;
                fxController.Play((HashedString)"idle", KAnim.PlayMode.Loop);
            }

            if (Game.Instance != null && Game.Instance.userMenu != null)
            {
                Game.Instance.userMenu.Refresh(gameObject);
            }

            ShowDepletedHoverStatus();
            PushDepletedNotificationOnce();
        }

        private void StopGeyserStateMachine()
        {
            if (emitter != null && emitter.IsSimActive)
            {
                emitter.SetEmitting(false);
            }

            Geyser geyser = GetComponent<Geyser>();
            if (geyser != null && geyser.GetSMI() != null && geyser.GetSMI().IsRunning())
            {
                geyser.smi.StopSM("Geyser storage depleted");
            }
        }

        private void ClearConflictingSpoutStatusItems()
        {
            KSelectable selectable = GetComponent<KSelectable>();
            if (selectable == null)
            {
                return;
            }

            selectable.RemoveStatusItem(Db.Get().MiscStatusItems.SpoutOverPressure, true);
            selectable.RemoveStatusItem(Db.Get().MiscStatusItems.SpoutPressureBuilding, true);
            selectable.RemoveStatusItem(Db.Get().MiscStatusItems.SpoutIdle, true);
            selectable.RemoveStatusItem(Db.Get().MiscStatusItems.SpoutDormant, true);
        }

        private void ShowDepletedHoverStatus()
        {
            if (depletedStatusItemHandle != System.Guid.Empty)
            {
                return;
            }

            KSelectable selectable = GetComponent<KSelectable>();
            if (selectable != null)
            {
                depletedStatusItemHandle = selectable.AddStatusItem(ExtendedGeysersSidescreen.GeyserStorageDepletedStatusItem, this);
            }
        }

        private void PushDepletedNotificationOnce()
        {
            if (depletionNotificationSent)
            {
                return;
            }

            depletionNotificationSent = true;
            LimitedResourceNotifications.PushDepletedNotification(gameObject, Interstellar.GeysersText4, Interstellar.GeysersText5);
        }

        private bool IsStudied()
        {
            Studyable studyable = GetComponent<Studyable>();
            return studyable != null && studyable.Studied;
        }

        public bool CanDeconstructDepletedGeyser()
        {
            return IsStorageDepleted();
        }

        public float GetProgressBarMaxValue()
        {
            return Mathf.Max(geyserStorageAmount, 1f);
        }

        public float GetProgressBarFillPercentage()
        {
            if (!IsStudied())
            {
                return 0f;
            }

            return GetRemainingStoragePercentage();
        }

        public string GetProgressBarTitleLabel()
        {
            if (!IsStudied())
            {
                return Interstellar.GeysersText2;
            }

            return Interstellar.GeysersText1;
        }

        public string GetProgressBarLabel()
        {
            if (!IsStudied())
            {
                return Interstellar.GeysersText2;
            }

            return $"{GetRemainingStoragePercentage() * 100f:0.##}%";
        }

        public string GetProgressBarTooltip()
        {
            if (!IsStudied())
            {
                return Interstellar.GeysersText3;
            }

            return $"{GameUtil.GetFormattedMass(GetRemainingStorageAmount())} / {GameUtil.GetFormattedMass(geyserStorageAmount)}";
        }
    }

    public class GeyserDepletedDeconstructButton : KMonoBehaviour
    {
        private static readonly EventSystem.IntraObjectHandler<GeyserDepletedDeconstructButton> OnRefreshUserMenuDelegate =
            new EventSystem.IntraObjectHandler<GeyserDepletedDeconstructButton>((component, data) => component.OnRefreshUserMenu(data));

        private bool userMenuSubscribed;

        protected override void OnSpawn()
        {
            base.OnSpawn();
            EnsureSubscribed();
        }

        public void EnsureSubscribed()
        {
            if (userMenuSubscribed)
            {
                return;
            }

            this.Subscribe<GeyserDepletedDeconstructButton>(493375141, OnRefreshUserMenuDelegate);
            userMenuSubscribed = true;
        }

        private void OnRefreshUserMenu(object data)
        {
            Deconstructable deconstructable = GetComponent<Deconstructable>();
            if (deconstructable != null)
            {
                ConfigureDeconstructable(deconstructable);
                return;
            }

            Game.Instance.userMenu.AddButton(gameObject, new KIconButtonMenu.ButtonInfo(
                "action_deconstruct",
                (string)STRINGS.UI.USERMENUACTIONS.DECONSTRUCT.NAME,
                new System.Action(OnClickDeconstruct),
                tooltipText: (string)STRINGS.UI.USERMENUACTIONS.DECONSTRUCT.TOOLTIP), 0f);
        }

        private void OnClickDeconstruct()
        {
            Deconstructable deconstructable = GetComponent<Deconstructable>();
            if (deconstructable == null)
            {
                deconstructable = gameObject.AddComponent<Deconstructable>();
                ConfigureDeconstructable(deconstructable);
                deconstructable.Spawn();
            }

            ConfigureDeconstructable(deconstructable);
            deconstructable.QueueDeconstruction(true);

            if (Game.Instance != null && Game.Instance.userMenu != null)
            {
                Game.Instance.userMenu.Refresh(gameObject);
            }
        }

        internal static void ConfigureDeconstructable(Deconstructable deconstructable)
        {
            if (deconstructable == null)
            {
                return;
            }

            deconstructable.allowDeconstruction = true;
            deconstructable.customWorkTime = 30f;
            deconstructable.audioSize = "large";
            deconstructable.constructionElements = new Tag[0];
            if (!deconstructable.IsMarkedForDeconstruction())
            {
                deconstructable.SetWorkTime(30f);
            }
        }
    }

    [HarmonyPatch(typeof(Deconstructable), "OnCompleteWork")]
    public static class Patch_Deconstructable_OnCompleteWork_DestroyDepletedGeyserWithoutRefund
    {
        public static bool Prefix(Deconstructable __instance)
        {
            if (__instance == null)
            {
                return true;
            }

            bool hasButton = __instance.GetComponent<GeyserDepletedDeconstructButton>() != null;
            bool hasGeyser = __instance.GetComponent<Geyser>() != null;
            if (!hasButton || !hasGeyser)
            {
                return true;
            }

            GameObject target = __instance.gameObject;
            if (DetailsScreen.Instance != null && DetailsScreen.Instance.CompareTargetWith(target))
            {
                DetailsScreen.Instance.DeselectAndClose();
            }

            ExtendedGeysersSidescreen.DeleteNeutroniumUnderGeyser(target);

            GeyserProgressBarSideScreen progressBar = target.GetComponent<GeyserProgressBarSideScreen>();
            if (progressBar != null)
            {
                progressBar.PrepareForComponentRemoval();
                UnityEngine.Object.Destroy(progressBar);
            }

            target.DeleteObject();
            return false;
        }
    }

    [HarmonyPatch(typeof(ProgressBarSideScreen), nameof(ProgressBarSideScreen.Render1000ms))]
    public static class Patch_ProgressBarSideScreen_Render1000ms_SkipDestroyedTarget
    {
        public static bool Prefix(ProgressBarSideScreen __instance)
        {
            if (__instance == null || __instance.targetObject == null)
            {
                return false;
            }

            UnityEngine.Object target = __instance.targetObject as UnityEngine.Object;
            return target != null;
        }
    }
}
