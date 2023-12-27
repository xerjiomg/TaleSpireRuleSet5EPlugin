using BepInEx;
using Bounce.Singletons;
using Bounce.TaleSpire.AssetManagement;
using DataModel;
using HarmonyLib;
using KinematicCharacterController;
using RadialUI;
using RadialUI.Extensions;
using System;
using System.Collections;
using System.Linq;
using System.Net.Sockets;
using System.Windows.Forms;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;
using static LordAshes.RuleSet5EPlugin;

namespace LordAshes
{

    public partial class RuleSet5EPlugin : BaseUnityPlugin
    {
        public static bool selectRuleMode = false;
        public static bool processCallback = true;

        [HarmonyPatch(typeof(CreaturePresenter), "OnCreatureDataChanged")]
        public static class PatchCreaturePresenterOnCreatureDataChanged
        {

            public static void Postfix(in CreatureDataV2 creatureData, bool teleport)
            {
                if(selectRuleMode) return;
                if (processCallback)
                {
                    RuleSet5EPlugin.Instance.StartCoroutine(SupressionSystem);
                    if (RuleSet5EPlugin.diagnostics >= DiagnosticMode.high) { Debug.Log("Ruleset5E Plugin: Patch: OnCreatureDataChanged"); }
                    processCallback = false;
                    RuleSet5EPlugin.Instance.StartCoroutine(waitimestandard(creatureData));                    
                }
            }
        }

        //XJ:(2022/10/25) Try to update mini's data when mini's stats are changed.
        public static IEnumerator waitimestandard(CreatureDataV2 creatureData)
        {
            CreatureBoardAsset asset2 = null;
            CreaturePresenter.TryGetAsset(creatureData.CreatureId, out asset2);
            AssetDataPlugin.ReadInfo(creatureData.ToString(), RuleSet5EPlugin.Guid + ".BonusData");
            yield return 0.1f;
            RuleSet5EPlugin.Instance.LoadDnd5eJson(asset2);            
            yield return 0.1f;
            if (asset2 != null)
            {
                if (RuleSet5EPlugin.diagnostics >= DiagnosticMode.ultra) { Debug.Log("Ruleset5E Plugin: Patch: Triggering LoadDnd5eJson"); }   
                RuleSet5EPlugin.Instance.CustomBColor(asset2, (int)asset2.Hp.Value, (int)asset2.Hp.Max);                                
            }
        }

        public static IEnumerator SupressionSystem
        {
            get
            {
                processCallback = false;
                WaitForSeconds varwait2 = new WaitForSeconds(0.5f);
                yield return varwait2;
                processCallback = true;
            }
        }
        [HarmonyPatch(typeof(CreatureBoardAsset), "Pickup")]
        public static class PatchCreatureBoardAssetPickup
        {
            public static bool Prefix()
            {               
                return !selectRuleMode;
            }
            public static void Postfix()
            {
                if (selectRuleMode) return;
                if (RuleSet5EPlugin.diagnostics >= DiagnosticMode.ultra) { Debug.Log("Ruleset5E Plugin: PatchCreatureBoardAssetPickup"); }
                RuleSet5EPlugin.Instance.LoadBonus(LocalClient.SelectedCreatureId);                               
            }
        }
    }
    [HarmonyPatch(typeof(LocalClient), "SetSelectedCreatureId")]
    public static class PatchLocalClientSetSelectedCreatureId
    {
        public static bool Prefix()
        {            
            if (RuleSet5EPlugin.Instance.multiTargetAssets.Count != 0) { return false; }
            return !selectRuleMode;
        }

        public static void Postfix()
        {
            if (RuleSet5EPlugin.diagnostics >= DiagnosticMode.ultra) { Debug.Log("Ruleset5E Plugin: PatchCreatureBoardAssetUpdate : "); }
            if (RuleSet5EPlugin.diagnostics >= DiagnosticMode.ultra) { Debug.Log(LocalClient.SelectedCreatureId.ToString()); }              
        }
    }
}



