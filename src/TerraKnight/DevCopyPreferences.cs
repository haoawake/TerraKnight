using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace TerraKnight
{
    /// <summary>Test copies read existing preferences but never write the player's shared Unity preferences.</summary>
    [HarmonyPatch]
    internal static class DevCopyPreferences
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            var type = AccessTools.TypeByName("PlayerPrefsSharedData");
            if(type == null) yield break;
            foreach(var method in AccessTools.GetDeclaredMethods(type))
                if(method.GetMethodBody()!=null && (method.Name.StartsWith("Set") || method.Name=="Save" || method.Name.StartsWith("Delete")))
                    yield return method;
        }
        [HarmonyPrefix]
        static bool KeepPlayerPreferences() => DevCopy.Name == null;
    }
}
