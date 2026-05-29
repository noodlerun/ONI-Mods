using HarmonyLib;
using KSerialization;
using System.Reflection;
using UnityEngine;

namespace Interstellar
{
    [HarmonyPatch]
    internal static class Patch_GeothermalControllerConfig_CreatePrefab_AddLimitedResourceComponents
    {
        private static MethodBase TargetMethod()
        {
            foreach (MethodInfo method in typeof(GeothermalControllerConfig).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (method.Name == "CreatePrefab" || method.Name.EndsWith(".CreatePrefab"))
                {
                    return method;
                }
            }

            return null;
        }

        public static void Postfix(GameObject __result)
        {
            __result.AddOrGet<GeothermalControllerLimitedEmissionSideScreen>();
            __result.AddOrGet<GeothermalDeconstructButton>();
        }
    }

    [HarmonyPatch(typeof(GeothermalVentConfig), nameof(GeothermalVentConfig.CreatePrefab))]
    internal static class Patch_GeothermalVentConfig_CreatePrefab_AddDeconstructButton
    {
        public static void Postfix(GameObject __result)
        {
            __result.AddOrGet<GeothermalDeconstructButton>();
        }
    }

    [HarmonyPatch(typeof(GeothermalController), "OnSpawn")]
    internal static class Patch_GeothermalController_OnSpawn_InitializeLimitedResourceComponents
    {
        public static void Postfix(GeothermalController __instance)
        {
            GeothermalLimitedResource.EnsureControllerLimiter(__instance);
            __instance.gameObject.AddOrGet<GeothermalDeconstructButton>().EnsureSubscribed();
        }
    }

    [HarmonyPatch(typeof(GeothermalVent), "OnSpawn")]
    internal static class Patch_GeothermalVent_OnSpawn_InitializeDeconstructButton
    {
        public static void Postfix(GeothermalVent __instance)
        {
            __instance.gameObject.AddOrGet<GeothermalDeconstructButton>().EnsureSubscribed();
        }
    }

    [HarmonyPatch(typeof(GeothermalController), nameof(GeothermalController.IsObstructed))]
    internal static class Patch_GeothermalController_IsObstructed_WhenDepleted
    {
        public static void Postfix(GeothermalController __instance, ref bool __result)
        {
            GeothermalControllerLimitedEmissionSideScreen limiter = GeothermalLimitedResource.EnsureControllerLimiter(__instance);
            if (limiter != null && limiter.IsDepleted())
            {
                limiter.ApplyDepletedState();
                __result = true;
            }
        }
    }

    [HarmonyPatch(typeof(GeothermalController), nameof(GeothermalController.PushToVents), new System.Type[] { })]
    internal static class Patch_GeothermalController_PushToVents_BlockIfDepleted
    {
        public static bool Prefix(GeothermalController __instance)
        {
            GeothermalControllerLimitedEmissionSideScreen limiter = GeothermalLimitedResource.EnsureControllerLimiter(__instance);
            if (limiter != null && limiter.IsDepleted())
            {
                limiter.ApplyDepletedState();
                return false;
            }

            return true;
        }
    }

    [HarmonyPatch(typeof(GeothermalController), nameof(GeothermalController.PushToVents), new System.Type[] { typeof(GeothermalVent.ElementInfo) })]
    internal static class Patch_GeothermalController_PushToVents_LimitOutput
    {
        public static bool Prefix(GeothermalController __instance, ref GeothermalVent.ElementInfo info)
        {
            GeothermalControllerLimitedEmissionSideScreen limiter = GeothermalLimitedResource.EnsureControllerLimiter(__instance);
            if (limiter == null)
            {
                return true;
            }

            float remaining = limiter.GetRemainingEmissionAmount();
            if (remaining <= 0.001f)
            {
                limiter.ApplyDepletedState();
                return false;
            }

            if (info.mass > remaining)
            {
                float ratio = remaining / info.mass;
                info.mass = remaining;
                info.diseaseCount = Mathf.RoundToInt(info.diseaseCount * ratio);
            }

            limiter.RecordDispatchedMass(info.mass);
            return info.mass > 0.001f;
        }
    }

    internal static class GeothermalLimitedResource
    {
        // 地热热泵默认总喷发量限制，单位是 kg；实际生成时会乘以 RandomMin/RandomMax 随机浮动。
        public const float DefaultControllerEmissionLimit = 12000000f; //base mass of 12,000 T
        public const float RandomMin = 0.8f;
        public const float RandomMax = 7.2f;

        public static readonly StatusItem ControllerDepletedStatusItem = CreateControllerDepletedStatusItem();

        public static float RollControllerEmissionLimit()
        {
            return DefaultControllerEmissionLimit * Random.Range(RandomMin, RandomMax);
        }

        public static GeothermalControllerLimitedEmissionSideScreen EnsureControllerLimiter(GeothermalController controller)
        {
            if (controller == null)
            {
                return null;
            }

            GeothermalControllerLimitedEmissionSideScreen limiter = controller.gameObject.AddOrGet<GeothermalControllerLimitedEmissionSideScreen>();
            limiter.Initialize(controller);
            return limiter;
        }

        public static void DeleteNeutroniumUnder(GameObject target, int halfWidth)
        {
            if (target == null)
            {
                return;
            }

            int originCell = Grid.PosToCell(target.transform.GetPosition());
            int worldId = target.GetMyWorldId();
            for (int offsetX = -halfWidth; offsetX <= halfWidth; offsetX++)
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

        private static StatusItem CreateControllerDepletedStatusItem()
        {
            StatusItem item = new StatusItem(
                "InterstellarGeothermalControllerEmissionDepleted",
                "BUILDING",
                string.Empty,
                StatusItem.IconType.Exclamation,
                NotificationType.Bad,
                false,
                OverlayModes.None.ID).SetResolveStringCallback((str, data) => Interstellar.GeothermalText2);
            item.resolveTooltipCallback = (str, data) => Interstellar.GeothermalText3;
            item.showInHoverCardOnly = true;
            return item;
        }
    }

    [HarmonyPatch(typeof(Deconstructable), "OnCompleteWork")]
    internal static class Patch_Deconstructable_OnCompleteWork_GeothermalObjects
    {
        public static bool Prefix(Deconstructable __instance)
        {
            if (__instance == null || __instance.GetComponent<GeothermalDeconstructButton>() == null)
            {
                return true;
            }

            GameObject target = __instance.gameObject;
            if (__instance.GetComponent<GeothermalController>() != null)
            {
                if (DetailsScreen.Instance != null && DetailsScreen.Instance.CompareTargetWith(target))
                {
                    DetailsScreen.Instance.DeselectAndClose();
                }

                GeothermalLimitedResource.DeleteNeutroniumUnder(target, 4);
                target.DeleteObject();
                return false;
            }

            if (__instance.GetComponent<GeothermalVent>() != null)
            {
                GeothermalLimitedResource.DeleteNeutroniumUnder(target, 1);
            }

            return true;
        }
    }

    [SerializationConfig(MemberSerialization.OptIn)]
    public class GeothermalControllerLimitedEmissionSideScreen : KMonoBehaviour, IProgressBarSideScreen, ISim1000ms
    {
        [Serialize]
        public float totalEmissionLimit;

        [Serialize]
        public float emittedMass;

        [Serialize]
        private bool depletionNotificationSent;

        private GeothermalController controller;
        private System.Guid depletedStatusItemHandle = System.Guid.Empty;

        protected override void OnSpawn()
        {
            base.OnSpawn();
            Initialize(GetComponent<GeothermalController>());
        }

        public void Initialize(GeothermalController geothermalController)
        {
            controller = geothermalController;
            EnsureInitialized();
            if (IsDepleted())
            {
                ApplyDepletedState();
            }
        }

        public void Sim1000ms(float dt)
        {
            if (IsDepleted())
            {
                ApplyDepletedState();
            }
        }

        public void EnsureInitialized()
        {
            if (totalEmissionLimit > 0f)
            {
                emittedMass = Mathf.Clamp(emittedMass, 0f, totalEmissionLimit);
                return;
            }

            totalEmissionLimit = GeothermalLimitedResource.RollControllerEmissionLimit();
            emittedMass = 0f;
        }

        public void RecordDispatchedMass(float mass)
        {
            EnsureInitialized();
            if (mass <= 0f || IsDepleted())
            {
                return;
            }

            emittedMass = Mathf.Min(totalEmissionLimit, emittedMass + mass);
            if (IsDepleted())
            {
                ApplyDepletedState();
            }
        }

        public float GetRemainingEmissionAmount()
        {
            EnsureInitialized();
            return Mathf.Max(0f, totalEmissionLimit - emittedMass);
        }

        public float GetRemainingEmissionPercentage()
        {
            EnsureInitialized();
            return totalEmissionLimit > 0f ? Mathf.Clamp01(GetRemainingEmissionAmount() / totalEmissionLimit) : 0f;
        }

        public bool IsDepleted()
        {
            return totalEmissionLimit > 0f && emittedMass >= totalEmissionLimit;
        }

        public void ApplyDepletedState()
        {
            PushDepletedNotificationOnce();
            if (ShowDepletedStatus() && Game.Instance != null && Game.Instance.userMenu != null)
            {
                Game.Instance.userMenu.Refresh(gameObject);
            }
        }

        private void PushDepletedNotificationOnce()
        {
            if (depletionNotificationSent)
            {
                return;
            }

            depletionNotificationSent = true;
            LimitedResourceNotifications.PushDepletedNotification(gameObject, Interstellar.GeothermalText2, Interstellar.GeothermalText3);
        }

        private bool ShowDepletedStatus()
        {
            if (depletedStatusItemHandle != System.Guid.Empty)
            {
                return false;
            }

            KSelectable selectable = GetComponent<KSelectable>();
            if (selectable != null)
            {
                depletedStatusItemHandle = selectable.AddStatusItem(GeothermalLimitedResource.ControllerDepletedStatusItem, this);
                return true;
            }

            return false;
        }

        public float GetProgressBarMaxValue()
        {
            EnsureInitialized();
            return Mathf.Max(totalEmissionLimit, 1f);
        }

        public float GetProgressBarFillPercentage()
        {
            return GetRemainingEmissionPercentage();
        }

        public string GetProgressBarTitleLabel()
        {
            return Interstellar.GeothermalText1;
        }

        public string GetProgressBarLabel()
        {
            return $"{GetRemainingEmissionPercentage() * 100f:0.##}%";
        }

        public string GetProgressBarTooltip()
        {
            return $"{GameUtil.GetFormattedMass(GetRemainingEmissionAmount())} / {GameUtil.GetFormattedMass(totalEmissionLimit)}";
        }
    }

    public class GeothermalDeconstructButton : KMonoBehaviour
    {
        private static readonly EventSystem.IntraObjectHandler<GeothermalDeconstructButton> OnRefreshUserMenuDelegate =
            new EventSystem.IntraObjectHandler<GeothermalDeconstructButton>((component, data) => component.OnRefreshUserMenu(data));

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

            this.Subscribe<GeothermalDeconstructButton>(493375141, OnRefreshUserMenuDelegate);
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
}
