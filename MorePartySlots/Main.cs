using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Reflection.Emit;
using Kingmaker.UI.Group;
using UnityEngine;
using static UnityModManagerNet.UnityModManager;
using Kingmaker.UI.MVVM._VM.GroupChanger;
using Kingmaker.Designers.EventConditionActionSystem.Actions;
using Kingmaker.UI.MVVM._PCView.GroupChanger;
using Kingmaker.UI.MVVM._ConsoleView.GroupChanger;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Blueprints;
using Kingmaker.UI.Common;
using UnityEngine.UI;

namespace MorePartySlots;

public static class Main {
    internal static Harmony HarmonyInstance;
    internal static ModEntry.ModLogger log;
    internal static string text;
    internal static Settings settings;
    internal static ModEntry modEntry;

    public static bool Load(ModEntry modEntry) {
        Main.modEntry = modEntry;
        log = modEntry.Logger;
        modEntry.OnGUI = OnGUI;
        modEntry.OnSaveGUI = OnSaveGUI;
        HarmonyInstance = new Harmony(modEntry.Info.Id);
        settings = Settings.Load<Settings>(modEntry);
        text = settings.Slots.ToString();
        HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
        return true;
    }
    static void OnSaveGUI(ModEntry modEntry) {
        settings.Save(modEntry);
    }

    public static void OnGUI(ModEntry modEntry) {
        GUILayout.Label($"Current Slots: {settings.Slots}", GUILayout.ExpandWidth(false));
        text = GUILayout.TextField(text, GUILayout.Width(100));
        if (GUILayout.Button("Apply Change", GUILayout.ExpandWidth(false))) {
            if (int.TryParse(text, out var tmp)) {
                settings.Slots = tmp;
                OnSaveGUI(modEntry);
                HarmonyInstance.UnpatchAll();
                HarmonyInstance.PatchAll();
            }
        }
    }
    [HarmonyPatch]
    public static class GroupChanger_Patch {
        [HarmonyPatch(typeof(GroupChangerPCView), nameof(GroupChangerPCView.AddToParty)), HarmonyPrefix]
        static void PC_AddToParty_Post(GroupChangerPCView __instance) {
            //__instance.gameObject: /GlobalMapPCView(Clone)/StaticCanvas/GroupChangerPCView
            //__instance.m_PartyContainer: /GlobalMapPCView(Clone)/StaticCanvas/GroupChangerPCView/Window/CurrentGroup/Content
            //__instance.m_RemoteContainer: /GlobalMapPCView(Clone)/StaticCanvas/GroupChangerPCView/Window/CompanionsGroup/Scroll
            var go = __instance.m_PartyContainer.gameObject;
            if (!go.TryGetComponent<ContentSizeFitterExtended>(out var _)) {
                var newGo = GameObject.Instantiate(__instance.m_RemoteContainer.gameObject, go.transform.parent);
                newGo.name = "Scroll";
                __instance.m_PartyContainer = newGo.GetComponent<ScrollRectExtended>().content;
            }
        }
        [HarmonyPatch(typeof(GroupChangerPCView), nameof(GroupChangerPCView.AddToReserve)), HarmonyPostfix]
        static void PC_AddToReserve_Post(GroupChangerPCView __instance) {
            if (__instance.ViewModel.PartyCharacter.Count <= 6) {
                __instance.m_PartyContainer.parent.parent.GetComponent<ScrollRectExtended>().ScrollToTop();
            }
        }
        // Doesn't seem to work properly
        [HarmonyPatch(typeof(GroupChangerConsoleView), nameof(GroupChangerConsoleView.AddToParty)), HarmonyPrefix]
        static void Console_AddToParty_Post(GroupChangerConsoleView __instance) {
            var go = __instance.m_PartyContainer.gameObject;
            if (!go.TryGetComponent<ContentSizeFitterExtended>(out var _)) {
                var newGo = GameObject.Instantiate(__instance.m_RemoteContainer.gameObject, go.transform.parent);
                newGo.name = "Scroll";
                __instance.m_PartyContainer = newGo.GetComponent<ScrollRectExtended>().content;
            }
        }
    }
    [HarmonyPatch]
    public static class Const_Patches {
        [HarmonyTargetMethods]
        static IEnumerable<MethodBase> GetMethods() {
            yield return AccessTools.Method(typeof(GroupController), nameof(GroupController.SetArrowsInteracteble));
            yield return AccessTools.Method(typeof(GroupController), nameof(GroupController.NavigateCharacters));
            yield return AccessTools.Method(typeof(GroupController), nameof(GroupController.UpdateSelectedUnits));
            yield return AccessTools.Method(typeof(GroupController), nameof(GroupController.FullScreenChanged));
            yield return AccessTools.Method(typeof(GroupController), nameof(GroupController.HandlePhotoModeChange));
            yield return AccessTools.Method(typeof(GroupController), nameof(GroupController.SetGroup));
            yield return AccessTools.Method(typeof(GroupController), nameof(GroupController.SetCharacter));
            yield return AccessTools.Method(typeof(GroupController), nameof(GroupController.DragCharacter));
            yield return AccessTools.Constructor(typeof(GroupChangerCommonVM), [typeof(Action), typeof(Action), typeof(List<UnitReference>), typeof(List<BlueprintUnit>), typeof(bool)]);
            yield return AccessTools.Method(typeof(GroupChangerVM), nameof(GroupChangerVM.MoveCharacter));
            yield return AccessTools.Method(typeof(GroupChangerVM), nameof(GroupChangerVM.MoveAllCharacter));
            yield return AccessTools.Method(typeof(PartyMembersDetach), nameof(PartyMembersDetach.RunAction));
            yield return AccessTools.Method(typeof(PartyMembersDetachEvaluated), nameof(PartyMembersDetachEvaluated.RunAction));
            yield return AccessTools.Method(typeof(GroupChangerConsoleView), nameof(GroupChangerConsoleView.SetMoveValue));
        }
        [HarmonyTranspiler]
        static IEnumerable<CodeInstruction> TranspilerPatch(IEnumerable<CodeInstruction> instructions) {
            return ConvertConstants(instructions, settings.Slots);
        }

        private static OpCode[] LdConstants = {
            OpCodes.Ldc_I4_0,
            OpCodes.Ldc_I4_1,
            OpCodes.Ldc_I4_2,
            OpCodes.Ldc_I4_3,
            OpCodes.Ldc_I4_4,
            OpCodes.Ldc_I4_5,
            OpCodes.Ldc_I4_6,
            OpCodes.Ldc_I4_7,
            OpCodes.Ldc_I4_8,
        };

        public static IEnumerable<CodeInstruction> ConvertConstants(IEnumerable<CodeInstruction> instructions, int to) {
            Func<CodeInstruction> makeReplacement;
            if (to <= 8)
                makeReplacement = () => new CodeInstruction(LdConstants[to]);
            else
                makeReplacement = () => new CodeInstruction(OpCodes.Ldc_I4_S, to);

            foreach (var ins in instructions) {
                if (ins.opcode == OpCodes.Ldc_I4_6)
                    yield return makeReplacement().WithLabels(ins.labels);
                else
                    yield return ins;
            }
        }
    }
}
