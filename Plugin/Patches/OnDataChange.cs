using BepInEx;
using Bounce.TaleSpire.AssetManagement;
using HarmonyLib;
using KinematicCharacterController;
using RadialUI;
using RadialUI.Extensions;
using System.Collections;
using System.Linq;
using System.Windows.Forms;
using UnityEngine;
using UnityEngine.Assertions;

namespace LordAshes
{

    public partial class RuleSet5EPlugin : BaseUnityPlugin
    {
        public static bool processCallback = true;

        [HarmonyPatch(typeof(CreaturePresenter), "OnCreatureDataChanged")]
        public static class PatchCreaturePresenterOnCreatureDataChanged
        {

            public static void Postfix(in CreatureDataV2 creatureData, bool teleport)
            {
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
                //RuleSet5EPlugin.Instance.LoadBonus(asset2.CreatureId);
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
        [HarmonyPatch(typeof(CreatureBoardAsset), "OnLocalPickup")]
        public static class PatchCreatureBoardAssetOnLocalPickup
        {

            public static void Postfix()
            {
                if (RuleSet5EPlugin.diagnostics >= DiagnosticMode.ultra) { Debug.Log("Ruleset5E Plugin:OnLocalPickup"); }
                RuleSet5EPlugin.Instance.LoadBonus(LocalClient.SelectedCreatureId);
            }
        }      
    }
}

