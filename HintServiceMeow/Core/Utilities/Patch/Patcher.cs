namespace HintServiceMeow.Core.Utilities.Patch
{
    using System;
    using System.Reflection;
    using HarmonyLib;

    /// <summary>
    /// Provides methods to apply and remove Harmony patches used by HintServiceMeow.
    /// </summary>
    public static class Patcher
    {
        private static readonly Harmony AspectRatioHarmony = new("HintServiceMeow.AspectRatio");
        private static bool aspectRatioPatched;

        /// <summary>
        /// Gets the active <see cref="HarmonyLib.Harmony"/> instance used to manage patches, or <see langword="null"/> if patching has not been applied.
        /// </summary>
        public static Harmony? Harmony { get; private set; }

        /// <summary>
        /// Applies all Harmony patches required by HintServiceMeow, including patches for hint display and hint sending methods.
        /// </summary>
        public static void Patch()
        {
            Harmony = new Harmony("HintServiceMeowHarmony" + Guid.NewGuid());

            // Unpatch all other patches
            MethodInfo hintDisplayMethod = typeof(Hints.HintDisplay).GetMethod(nameof(Hints.HintDisplay.Show))!;
            MethodInfo sendHintMethod1 = typeof(LabApi.Features.Wrappers.Player).GetMethod(nameof(LabApi.Features.Wrappers.Player.SendHint), [typeof(string), typeof(float)])!;
            MethodInfo sendHintMethod2 = typeof(LabApi.Features.Wrappers.Player).GetMethod(nameof(LabApi.Features.Wrappers.Player.SendHint), [typeof(string), typeof(Hints.HintEffect[]), typeof(float)])!;
            Harmony.Unpatch(hintDisplayMethod, HarmonyPatchType.All);
            Harmony.Unpatch(sendHintMethod1, HarmonyPatchType.All);
            Harmony.Unpatch(sendHintMethod2, HarmonyPatchType.All);

            Type patchType = typeof(Patches);

            // Patch the method
            Harmony.Patch(hintDisplayMethod, new HarmonyMethod(patchType.GetMethod(nameof(Patches.HintDisplayPatch))));
            Harmony.Patch(sendHintMethod1, new HarmonyMethod(patchType.GetMethod(nameof(Patches.SendHintPatch1))));
            Harmony.Patch(sendHintMethod2, new HarmonyMethod(patchType.GetMethod(nameof(Patches.SendHintPatch2))));

#if EXILED
            // Exiled methods
            MethodInfo showHintMethod1 = typeof(Exiled.API.Features.Player).GetMethod(nameof(Exiled.API.Features.Player.ShowHint), [typeof(string), typeof(float)])!;
            MethodInfo showHintMethod2 = typeof(Exiled.API.Features.Player).GetMethod(nameof(Exiled.API.Features.Player.ShowHint), [typeof(Exiled.API.Features.Hint)])!;

            Harmony.Unpatch(showHintMethod1, HarmonyPatchType.All);
            Harmony.Unpatch(showHintMethod2, HarmonyPatchType.All);

            MethodInfo exiledHintPatch1 = patchType.GetMethod(nameof(Patches.ExiledHintPatch1))!;
            MethodInfo exiledHintPatch2 = patchType.GetMethod(nameof(Patches.ExiledHintPatch2))!;
            Harmony.Patch(showHintMethod1, new HarmonyMethod(exiledHintPatch1));
            Harmony.Patch(showHintMethod2, new HarmonyMethod(exiledHintPatch2));
#endif
        }

        /// <summary>
        /// Removes all Harmony patches applied by this patcher.
        /// </summary>
        public static void Unpatch()
        {
            Harmony?.UnpatchAll();
        }

        internal static void PatchAspectRatio()
        {
            if (aspectRatioPatched)
                return;

            MethodInfo method = AccessTools.Method(typeof(AspectRatioSync), "UserCode_CmdSetAspectRatio__Single");
            AspectRatioHarmony.Patch(method, postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.AspectRatioChangedPostfix)));
            aspectRatioPatched = true;
        }

        internal static void UnpatchAspectRatio()
        {
            AspectRatioHarmony.UnpatchAll(AspectRatioHarmony.Id);
            aspectRatioPatched = false;
        }
    }
}
