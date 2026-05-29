using HarmonyLib;

namespace Interstellar
{
    [HarmonyPatch(typeof(HarvestablePOIStates.Instance), nameof(HarvestablePOIStates.Instance.RechargePOI))]
    internal static class Patch_HarvestablePOIStates_RechargePOI_DisableRecharge
    {
        public static bool Prefix()
        {
            return false;
        }
    }
}
