using HarmonyLib;
using KSerialization;
using UnityEngine;

namespace Interstellar
{
    internal static class LimitedResourceNotifications
    {
        public static void PushDepletedNotification(GameObject target, string title, string tooltip)
        {
            if (target == null)
            {
                return;
            }

            string notificationTitle = string.IsNullOrEmpty(title) ? "Limited resource depleted" : title;
            string notificationTooltip = string.IsNullOrEmpty(tooltip) ? "This limited resource has been depleted." : tooltip;
            Notification notification = new Notification(
                notificationTitle,
                NotificationType.DuplicantThreatening,
                (System.Func<System.Collections.Generic.List<Notification>, object, string>)((notificationList, data) => notificationTooltip + notificationList.ReduceMessages(false)),
                clear_on_click: true,
                volume_attenuation: false);
            target.AddOrGet<Notifier>().Add(notification);
        }
    }

    [HarmonyPatch(typeof(OilWellConfig), nameof(OilWellConfig.CreatePrefab))]
    internal static class Patch_OilWellConfig_CreatePrefab_AddReservoirStorage
    {
        public static void Postfix(GameObject __result)
        {
            __result.AddOrGet<OilWellReservoirStorage>();
            __result.AddOrGet<OilWellReservoirDeconstructButton>();
        }
    }

    [HarmonyPatch(typeof(OilWellCapConfig), nameof(OilWellCapConfig.ConfigureBuildingTemplate))]
    internal static class Patch_OilWellCapConfig_ConfigureBuildingTemplate_AddStorageSideScreen
    {
        public static void Postfix(GameObject go)
        {
            go.AddOrGet<OilWellCapStorageSideScreen>();
        }
    }

    [HarmonyPatch(typeof(OilWellCap), "OnSpawn")]
    internal static class Patch_OilWellCap_OnSpawn_InitializeStorageSideScreen
    {
        public static void Postfix(OilWellCap __instance)
        {
            __instance.gameObject.AddOrGet<OilWellCapStorageSideScreen>().Initialize();
        }
    }

    internal static class OilWellStorage
    {
        // OilWellCap 原版转换比例：1 kg/s 水输入 -> 3.33333325 kg/s 原油输出。
        public const float OilOutputPerWater = 3.33333325f;

        // 储油石默认总储量，单位是 kg；实际生成时会乘以 RandomMin/RandomMax 随机浮动。
        public const float DefaultOilStorage = 4500000f; //base mass of 4500T
        public const float RandomMin = 0.5f;
        public const float RandomMax = 13.0f;

        public static readonly StatusItem OilWellDepletedStatusItem = CreateOilWellDepletedStatusItem();

        public static float RollStorageAmount()
        {
            return DefaultOilStorage * Random.Range(RandomMin, RandomMax);
        }

        public static GameObject GetAttachedOilWellCap(GameObject oilWell)
        {
            BuildingAttachPoint attachPoint = oilWell != null ? oilWell.GetComponent<BuildingAttachPoint>() : null;
            if (attachPoint == null)
            {
                return null;
            }

            for (int idx = 0; idx < attachPoint.points.Length; idx++)
            {
                AttachableBuilding attachedBuilding = attachPoint.points[idx].attachedBuilding;
                if (attachedBuilding != null && attachedBuilding.GetComponent<OilWellCap>() != null)
                {
                    return attachedBuilding.gameObject;
                }
            }

            return null;
        }

        public static void DeconstructAttachedOilWellCapWithRefund(GameObject oilWell)
        {
            GameObject oilWellCap = GetAttachedOilWellCap(oilWell);
            if (oilWellCap == null)
            {
                return;
            }

            if (DetailsScreen.Instance != null && DetailsScreen.Instance.CompareTargetWith(oilWellCap))
            {
                DetailsScreen.Instance.DeselectAndClose();
            }

            Deconstructable deconstructable = oilWellCap.GetComponent<Deconstructable>();
            if (deconstructable != null)
            {
                deconstructable.ForceDestroyAndGetMaterials();
            }
            else
            {
                oilWellCap.DeleteObject();
            }
        }

        private static StatusItem CreateOilWellDepletedStatusItem()
        {
            StatusItem statusItem = new StatusItem(
                "InterstellarOilWellStorageDepleted",
                "BUILDING",
                string.Empty,
                StatusItem.IconType.Exclamation,
                NotificationType.BadMinor,
                false,
                OverlayModes.None.ID).SetResolveStringCallback((str, data) => Interstellar.OilWellText3);
            statusItem.resolveTooltipCallback = (str, data) => Interstellar.OilWellText4;
            return statusItem;
        }
    }

    [SerializationConfig(MemberSerialization.OptIn)]
    public class OilWellReservoirStorage : KMonoBehaviour
    {
        [Serialize]
        public float totalOilStorage;

        [Serialize]
        public float extractedOilAmount;

        [Serialize]
        private bool depletionNotificationSent;

        protected override void OnSpawn()
        {
            base.OnSpawn();
            EnsureInitialized();
        }

        public void EnsureInitialized()
        {
            if (totalOilStorage > 0f)
            {
                extractedOilAmount = Mathf.Clamp(extractedOilAmount, 0f, totalOilStorage);
                return;
            }

            totalOilStorage = OilWellStorage.RollStorageAmount();
            extractedOilAmount = 0f;
        }

        public float RecordExtraction(float requestedOilAmount)
        {
            EnsureInitialized();

            if (requestedOilAmount <= 0f || IsDepleted())
            {
                return 0f;
            }

            float acceptedOilAmount = Mathf.Min(GetRemainingOilAmount(), requestedOilAmount);
            extractedOilAmount = Mathf.Min(totalOilStorage, extractedOilAmount + acceptedOilAmount);
            return acceptedOilAmount;
        }

        public bool IsDepleted()
        {
            return totalOilStorage > 0f && extractedOilAmount >= totalOilStorage;
        }

        public void PushDepletedNotificationOnce(GameObject notificationTarget)
        {
            if (depletionNotificationSent)
            {
                return;
            }

            depletionNotificationSent = true;
            LimitedResourceNotifications.PushDepletedNotification(notificationTarget, Interstellar.OilWellText3, Interstellar.OilWellText4);
        }

        public float GetRemainingOilAmount()
        {
            EnsureInitialized();
            return Mathf.Max(0f, totalOilStorage - extractedOilAmount);
        }

        public float GetRemainingOilPercentage()
        {
            EnsureInitialized();
            if (totalOilStorage <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(GetRemainingOilAmount() / totalOilStorage);
        }
    }

    public class OilWellReservoirDeconstructButton : KMonoBehaviour
    {
        private static readonly EventSystem.IntraObjectHandler<OilWellReservoirDeconstructButton> OnRefreshUserMenuDelegate =
            new EventSystem.IntraObjectHandler<OilWellReservoirDeconstructButton>((component, data) => component.OnRefreshUserMenu(data));

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

            this.Subscribe<OilWellReservoirDeconstructButton>(493375141, OnRefreshUserMenuDelegate);
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
            if (!deconstructable.IsMarkedForDeconstruction())
            {
                deconstructable.SetWorkTime(30f);
            }
        }
    }

    [HarmonyPatch(typeof(Deconstructable), "OnCompleteWork")]
    public static class Patch_Deconstructable_OnCompleteWork_DeconstructAttachedOilWellCap
    {
        public static bool Prefix(Deconstructable __instance)
        {
            if (__instance == null)
            {
                return true;
            }

            bool hasButton = __instance.GetComponent<OilWellReservoirDeconstructButton>() != null;
            bool hasOilWell = __instance.gameObject.PrefabID() == OilWellConfig.ID;
            if (hasButton && hasOilWell)
            {
                OilWellStorage.DeconstructAttachedOilWellCapWithRefund(__instance.gameObject);
            }

            return true;
        }
    }

    public class OilWellCapStorageSideScreen : KMonoBehaviour, IProgressBarSideScreen, ISim1000ms
    {
        private ElementConverter converter;
        private OilWellReservoirStorage reservoirStorage;
        private bool initialized;
        private bool converterDisabledForDepletion;
        private bool pendingDepletionAfterCurrentConversion;
        private System.Guid depletedStatusItemHandle = System.Guid.Empty;

        protected override void OnSpawn()
        {
            base.OnSpawn();
            Initialize();
        }

        protected override void OnCleanUp()
        {
            if (converter != null)
            {
                converter.onConvertMass -= OnConvertMass;
            }

            base.OnCleanUp();
        }

        public void Initialize()
        {
            if (initialized)
            {
                ResolveReservoirStorage();
                ApplyStorageState();
                return;
            }

            converter = GetComponent<ElementConverter>();
            if (converter != null)
            {
                converter.onConvertMass -= OnConvertMass;
                converter.onConvertMass += OnConvertMass;
            }

            initialized = true;
            ResolveReservoirStorage();
            ApplyStorageState();
        }

        public void Sim1000ms(float dt)
        {
            ResolveReservoirStorage();
            ApplyStorageState();
        }

        private void OnConvertMass(float consumedWaterMass)
        {
            ResolveReservoirStorage();
            if (reservoirStorage == null || converter == null)
            {
                return;
            }

            if (reservoirStorage.IsDepleted())
            {
                converter.OutputMultiplier = 0f;
                ApplyStorageState();
                return;
            }

            float requestedOilAmount = Mathf.Max(0f, consumedWaterMass) * OilWellStorage.OilOutputPerWater;
            float acceptedOilAmount = reservoirStorage.RecordExtraction(requestedOilAmount);
            converter.OutputMultiplier = requestedOilAmount > 0f ? Mathf.Clamp01(acceptedOilAmount / requestedOilAmount) : 1f;

            if (reservoirStorage.IsDepleted())
            {
                pendingDepletionAfterCurrentConversion = true;
                if (!converterDisabledForDepletion)
                {
                    converter.SetAllConsumedActive(false);
                    converterDisabledForDepletion = true;
                    GetComponent<Operational>()?.SetActive(false);
                    GetComponent<Storage>()?.Trigger(-1697596308, gameObject);
                }

                ShowDepletedStatus();
            }
        }

        private void ResolveReservoirStorage()
        {
            if (reservoirStorage != null)
            {
                return;
            }

            AttachableBuilding attachable = GetComponent<AttachableBuilding>();
            BuildingAttachPoint attachPoint = attachable != null ? attachable.GetAttachedTo() : null;
            reservoirStorage = attachPoint != null ? attachPoint.GetComponent<OilWellReservoirStorage>() : null;

            if (reservoirStorage == null && attachable != null)
            {
                for (int idx = 0; idx < Components.BuildingAttachPoints.Count; idx++)
                {
                    BuildingAttachPoint candidate = Components.BuildingAttachPoints[idx];
                    for (int pointIdx = 0; pointIdx < candidate.points.Length; pointIdx++)
                    {
                        if (candidate.points[pointIdx].attachedBuilding == attachable)
                        {
                            reservoirStorage = candidate.GetComponent<OilWellReservoirStorage>();
                            if (reservoirStorage != null)
                            {
                                return;
                            }
                        }
                    }
                }
            }

            reservoirStorage?.EnsureInitialized();
        }

        private void ApplyStorageState()
        {
            if (reservoirStorage == null || converter == null)
            {
                return;
            }

            if (reservoirStorage.IsDepleted())
            {
                if (!pendingDepletionAfterCurrentConversion)
                {
                    converter.OutputMultiplier = 0f;
                    if (!converterDisabledForDepletion)
                    {
                        converter.SetAllConsumedActive(false);
                        converterDisabledForDepletion = true;
                        GetComponent<Operational>()?.SetActive(false);
                        GetComponent<Storage>()?.Trigger(-1697596308, gameObject);
                    }
                }

                pendingDepletionAfterCurrentConversion = false;
                ShowDepletedStatus();
                return;
            }

            pendingDepletionAfterCurrentConversion = false;
            converter.OutputMultiplier = 1f;
            if (converterDisabledForDepletion)
            {
                converter.SetAllConsumedActive(true);
                converterDisabledForDepletion = false;
            }

            HideDepletedStatus();
        }

        private void ShowDepletedStatus()
        {
            reservoirStorage?.PushDepletedNotificationOnce(gameObject);
            if (depletedStatusItemHandle != System.Guid.Empty)
            {
                return;
            }

            KSelectable selectable = GetComponent<KSelectable>();
            if (selectable != null)
            {
                depletedStatusItemHandle = selectable.AddStatusItem(OilWellStorage.OilWellDepletedStatusItem, this);
            }
        }

        private void HideDepletedStatus()
        {
            if (depletedStatusItemHandle == System.Guid.Empty)
            {
                return;
            }

            KSelectable selectable = GetComponent<KSelectable>();
            if (selectable != null)
            {
                selectable.RemoveStatusItem(depletedStatusItemHandle);
            }

            depletedStatusItemHandle = System.Guid.Empty;
        }

        public float GetProgressBarMaxValue()
        {
            ResolveReservoirStorage();
            return Mathf.Max(reservoirStorage?.totalOilStorage ?? 1f, 1f);
        }

        public float GetProgressBarFillPercentage()
        {
            ResolveReservoirStorage();
            return reservoirStorage?.GetRemainingOilPercentage() ?? 0f;
        }

        public string GetProgressBarTitleLabel()
        {
            return Interstellar.OilWellText1;
        }

        public string GetProgressBarLabel()
        {
            ResolveReservoirStorage();
            return reservoirStorage != null ? $"{reservoirStorage.GetRemainingOilPercentage() * 100f:0.##}%" : Interstellar.OilWellText2;
        }

        public string GetProgressBarTooltip()
        {
            ResolveReservoirStorage();
            if (reservoirStorage == null)
            {
                return Interstellar.OilWellText2;
            }

            return $"{GameUtil.GetFormattedMass(reservoirStorage.GetRemainingOilAmount())} / {GameUtil.GetFormattedMass(reservoirStorage.totalOilStorage)}";
        }
    }
}
