
using System;
using System.Collections.Generic;
using System.Reflection;
using EFT.InputSystem;
using SPT.Reflection.Patching;

namespace PocketRoulette
{
    internal sealed class MenuInputPatch : ModulePatch
    {
        internal static MethodBase Target => typeof(InputNode).GetMethod(nameof(InputNode.TranslateInput));
        protected override MethodBase GetTargetMethod() => Target;

        [PatchPrefix]
        private static void Prefix(InputNode __instance, List<ECommand> commands, ref float[] axes, ref ECursorResult shouldLockCursor)
        {
            if (!MenuInteraction.BlocksPlayerInput(__instance)) return;
            Neutralize(commands, axes);
            shouldLockCursor = ECursorResult.ShowCursor;
        }

        // Keep the original translators running with release commands/zero axes.
        // No scene search or active InputManager component is required (including Fika).
        internal static void Neutralize(List<ECommand> commands, float[] axes)
        {
            commands.Clear();
            commands.Add(ECommand.EndShooting);
            commands.Add(ECommand.EndAlternativeShooting);
            commands.Add(ECommand.EndSprinting);
            commands.Add(ECommand.EndBreathing);
            if (axes != null) Array.Clear(axes, 0, axes.Length);
        }
    }
}

