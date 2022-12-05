using BepInEx;
using HarmonyLib;
using TMPro;

using System;
using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using UnityEngine;
using System.Windows.Forms.VisualStyles;

namespace LordAshes
{
    public partial class RuleSet5EPlugin : BaseUnityPlugin
    {       
        public static Action<int> callbackRollReady = null;
        public static Action<Dictionary<string, object>> callbackRollResult = null;

        public static Existence forceExistence = null;

        public static System.Random random = new System.Random();

        private static UnityEngine.Color diceColor = UnityEngine.Color.black;
        private static UnityEngine.Color32 diceHighlightColor = new Color32(255, 255, 0, 255);

        /// <summary>
        /// Patch to detect when dice are placed in the dice tray
        /// </summary>        
        [HarmonyPatch(typeof(UIDiceTray), "SetDiceUrl")]
        public static class Patches        {
           
            public static bool Prefix(ref DiceRollDescriptor rollDescriptor)
            {                
                if (rollDescriptor.Groups != null) { if (!rollDescriptor.Groups[0].Name.Contains("XRuleset5e")) { Debug.Log("RuleSet 5E Plugin: SetDiceUrl with [XRuleset5e]"); return true; } }  //XJ(2022/12/02): To allow TS Core Rolls 
                if (RuleSet5EPlugin.Instance.lastRollRequestTotal != RollTotal.normal)
                {
                    if (rollDescriptor.Groups.Length == 1)
                    {
                        if (rollDescriptor.Groups[0].Dice.Length == 1)
                        {
                            if (rollDescriptor.Groups[0].Dice[0].Resource == "numbered1D20")
                            {
                                if (LordAshes.RuleSet5EPlugin.diagnostics >= LordAshes.RuleSet5EPlugin.DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Patch: Copying Die For " + RuleSet5EPlugin.Instance.lastRollRequestTotal.ToString().ToUpper() + " Roll"); }
                                DiceDescriptor dd = new DiceDescriptor(rollDescriptor.Groups[0].Dice[0].Resource, rollDescriptor.Groups[0].Dice[0].Count + 1, rollDescriptor.Groups[0].Dice[0].Modifier, rollDescriptor.Groups[0].Dice[0].DiceOperator);
                                rollDescriptor = new DiceRollDescriptor(new DiceGroupDescriptor[]
                                {
                                new DiceGroupDescriptor(rollDescriptor.Groups[0].Name, new DiceDescriptor[] { dd })
                                });
                            }
                        }
                    }
                }
                return true;
            }

            public static void Postfix(DiceRollDescriptor rollDescriptor)
            {
                if (rollDescriptor.Groups != null) { if (!rollDescriptor.Groups[0].Name.Contains("XRuleset5e")) { return ; } }   //XJ(2022/12/02): To allow TS Core Rolls 
                if (LordAshes.RuleSet5EPlugin.diagnostics >= LordAshes.RuleSet5EPlugin.DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Patch: Spawning Dice Set"); }
                DiceManager dm = GameObject.FindObjectOfType<DiceManager>();
                foreach (DiceGroupDescriptor dgd in rollDescriptor.Groups)
                {
                    if (dgd.Name != null)
                    {
                        if (dgd.Name != "")
                        {
                            // Automatically spawn dice only if the dice set has a name.
                            // This prevents automatic spawning of manually added dice.
                            UIDiceTray dt = GameObject.FindObjectOfType<UIDiceTray>();
                            bool saveSetting = (bool)PatchAssistant.GetField(dt, "_buttonHeld");
                            PatchAssistant.SetField(dt, "_buttonHeld", true);
                            dt.SpawnDice();
                            PatchAssistant.SetField(dt, "_buttonHeld", saveSetting);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Patch to detect when dice have been spawned
        /// </summary>
        [HarmonyPatch(typeof(DiceManager), "CreateLocalRoll")]
        public static class PatchCreateLocalRoll
        {
            public static bool Prefix(DiceRollDescriptor rollDescriptor, bool isGmRoll)
            {
                return true;
            }

            public static void Postfix(DiceRollDescriptor rollDescriptor, bool isGmRoll, ref int __result)
            {
                if (rollDescriptor.Groups[0].Name != null) { if (!rollDescriptor.Groups[0].Name.Contains("XRuleset5e")) { Debug.Log("RuleSet 5E Plugin TieneXRuleset5e"); return; } }       //XJ(2022/12/02): To allow TS Core Rolls            
                if (LordAshes.RuleSet5EPlugin.diagnostics >= LordAshes.RuleSet5EPlugin.DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Patch: Dice Set Ready"); }
                if (callbackRollResult != null) { callbackRollReady(__result); }
            }
        }

        /// <summary>
        /// Path to capture dice results
        /// </summary>
        [HarmonyPatch(typeof(DiceManager), "RPC_DiceResult")]
        public static class PatchDiceResults
        {
            public static bool Prefix(bool isGmOnly, byte[] diceListData, PhotonMessageInfo msgInfo)
            {    
                DiceManager.DiceRollResultData drrd = BinaryIO.FromByteArray<DiceManager.DiceRollResultData>(diceListData, (BinaryReader br) => br.ReadDiceRollResultData());
                if (!drrd.GroupResults[0].Name.Contains("XRuleset5e")) { return true; }  //XJ(2022/12/02): To allow TS Core Rolls 

                return false; // !!!!change to false XJ (2022/11/30) Show dice result according to TS core even if state machine <> idle
            }

            public static void Postfix(bool isGmOnly, byte[] diceListData, PhotonMessageInfo msgInfo)
            {
                if (LordAshes.RuleSet5EPlugin.diagnostics >= LordAshes.RuleSet5EPlugin.DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Patch :  PatchDiceResults2 "); }
               // if (stateMachineState == StateMachineState.idle) { return; }               
                string formula = "";
                string expanded = "";
                short total = 0;
                bool isMax = true;
                bool isMin = true;
                Dictionary<string, object> Result = new Dictionary<string, object>();
                DiceManager.DiceRollResultData drrd = BinaryIO.FromByteArray<DiceManager.DiceRollResultData>(diceListData, (BinaryReader br) => br.ReadDiceRollResultData());
                if (!drrd.GroupResults[0].Name.Contains("XRuleset5e")) { return; }  //XJ(2022/12/02): To allow TS Core Rolls 
                Result.Add("Identifier", drrd.RollId);
                foreach (DiceManager.DiceGroupResultData dgrd in drrd.GroupResults)
                {
                    
                    if (!Result.ContainsKey("Name")) { Result.Add("Name", dgrd.Name.Replace("XRuleset5e","")); }
                    foreach (DiceManager.DiceResultData drd in dgrd.Dice)
                    {
                        formula = formula + drd.Results.Length + drd.Resource.ToString().Replace("numbered1", "");
                        
                        //XJ: (2022/10/08) Commented: 
                        // if (RuleSet5EPlugin.Instance.damageDieMultiplier == 1.0f)
                        // {
                        expanded = expanded + "[" + String.Join(",", drd.Results) + "]";
                        // }
                        // else
                        // {
                        //     expanded = expanded + RuleSet5EPlugin.Instance.damageDieMultiplier.ToString("0")+"x[" + String.Join(",", drd.Results) + "]";
                        // }
                        //XJ: Avoid 2x damage, because are calculated in roll dice now

                        int sides = int.Parse(drd.Resource.ToString().Replace("numbered1D", ""));
                        if ((RuleSet5EPlugin.Instance.lastRollRequestTotal == RollTotal.normal) || (!formula.StartsWith("2D20")))
                        {
                            foreach (short val in drd.Results)
                            {
                                //XJ(2022/10/08) Change: total = (short)(total + val * RuleSet5EPlugin.Instance.damageDieMultiplier);
                                total = (short)(total + val);
                                //XJ: Avoid 2x damage, because are calculated in roll dice now
                                if (val != 1) { isMin = false; }
                                if (val != sides) { isMax = false; }
                            }
                        }
                        else
                        {
                            int roll = (RuleSet5EPlugin.Instance.lastRollRequestTotal == RollTotal.advantage) ? Math.Max(drd.Results[0], drd.Results[1]) : Math.Min(drd.Results[0], drd.Results[1]);
                            //XJ(2022/10/08) Change: total = (short)(total + roll * RuleSet5EPlugin.Instance.damageDieMultiplier);
                            total = (short)(total + roll);
                            //XJ: Avoid 2x damage, because are calculated in roll dice now                           
                            if (roll != 1) { isMin = false; }
                            if (roll != sides) { isMax = false; }
                        }
                        formula = formula + ((drd.DiceOperator == DiceManager.DiceOperator.Add) ? "+" : "-");
                        expanded = expanded + ((drd.DiceOperator == DiceManager.DiceOperator.Add) ? "+" : "-");
                        if (drd.DiceOperator == DiceManager.DiceOperator.Add) { total = (short)(total + drd.Modifier); } else { total = (short)(total - drd.Modifier); }
                        formula = formula + drd.Modifier + "+";
                        expanded = expanded + drd.Modifier + "+";
                    }
                }
                formula = formula.Substring(0, formula.Length - 1);
                Debug.Log("formula :"+ formula);
                expanded = expanded.Substring(0, expanded.Length - 1);
                Debug.Log("expanded :" + formula);
                Result.Add("Roll", (RuleSet5EPlugin.Instance.lastRollRequestTotal == RollTotal.normal) ? formula : formula.Replace("2D20", "1D20"));
                Result.Add("Total", (int)total); ;
                Result.Add("Expanded", expanded);
                Result.Add("IsMin", (bool)isMin);
                Result.Add("IsMax", (bool)isMax);

                if (LordAshes.RuleSet5EPlugin.diagnostics >= LordAshes.RuleSet5EPlugin.DiagnosticMode.high) { Debug.Log("RuleSet 5E Patch: Rolled " + Result["Name"] + " (" + Result["Roll"] + ") = " + Result["Expanded"] + " = " + Result["Total"] + " (Min:" + isMin + "/Max:" + isMax + ")"); }

                if (callbackRollResult != null) { callbackRollResult(Result); }
            }
        }

        /// <summary>
        /// Patch to allow spawning at a forced location and rotation instead of the camera default
        /// </summary>
        [HarmonyPatch(typeof(Die), "Spawn")]
        public static class PatchSpawn
        {
            public static bool Prefix(string resource, float3 pos, quaternion rot, int rollId, byte groupId, bool gmOnlyDie)
            {
                if (stateMachineState == StateMachineState.idle) { return true; }
                return false;
            }

            public static void Postfix(string resource, float3 pos, quaternion rot, int rollId, byte groupId, bool gmOnlyDie, ref Die __result)
            {
                if (stateMachineState == StateMachineState.idle) { return; }
                object[] data = new object[]
                {
                    rollId,
                    groupId,
                    gmOnlyDie
                };
                if (LordAshes.RuleSet5EPlugin.diagnostics >= LordAshes.RuleSet5EPlugin.DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Patch: Spawning Dice At " + ((forceExistence == null) ? pos.ToString() : forceExistence.position.ToString())); }
                Die component = (forceExistence == null) ? PhotonNetwork.Instantiate(resource, pos, rot, 0, data).GetComponent<Die>() : PhotonNetwork.Instantiate(resource, forceExistence.position, Quaternion.Euler(forceExistence.rotation), 0, data).GetComponent<Die>();
                PatchAssistant.UseMethod(component, "Init", new object[] { rollId, groupId, gmOnlyDie }); // component.Init(rollId, groupId, gmOnlyDie);
                Vector3 orientation = new Vector3(random.Next(0, 180), random.Next(0, 180), random.Next(0, 180));
                if (LordAshes.RuleSet5EPlugin.diagnostics >= LordAshes.RuleSet5EPlugin.DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Patch: Randomizing Die Starting Orientation (" + orientation.ToString() + ")"); }
                component.transform.rotation = Quaternion.Euler(orientation);
                foreach (Transform transform in component.transform.Children())
                {
                    TextMeshPro tmp = transform.gameObject.GetComponent<TextMeshPro>();
                    if (tmp != null) { tmp.faceColor = diceHighlightColor; }
                }
                __result = component;
            }
        }

        /// <summary>
        /// Patch to detect when dice have been spawned
        /// </summary>
        [HarmonyPatch(typeof(Die), "SetMaterial")]
        public static class PatchSetMaterial
        {            
            private static bool Prefix(bool gmDie)
            {
               // if (stateMachineState == StateMachineState.idle) { return true; }
                return false;
            }

            private static void Postfix(ref Renderer ___dieRenderer, ref bool gmDie, Material ___normalMaterial, Material ___gmMaterial)
            {
               // if (stateMachineState == StateMachineState.idle) { return; }
                if (gmDie)
                {
                    if (___dieRenderer.sharedMaterial != ___gmMaterial)
                    {
                        ___dieRenderer.sharedMaterial = ___gmMaterial;
                        return;
                    }
                }
                else if (___dieRenderer.sharedMaterial != ___normalMaterial)
                {
                    ___dieRenderer.sharedMaterial = ___normalMaterial;
                }
                ___dieRenderer.material.SetColor("_Color", diceColor);
            }
        }
    }

    /// <summary>
    /// Extension methods
    /// </summary>
    public static class DiceExtensions
    {
        /// <summary>
        /// SpawnAt methods sets or clears the forced spawn position and orientation
        /// </summary>
        /// <param name="dt">Dice tray</param>
        /// <param name="pos">Vector3 Position</param>
        /// <param name="rot">Vector3 Euler Angles</param>
        public static void SpawnAt(this UIDiceTray dt, Vector3 pos, Vector3 rot)
        {
            if(pos!=Vector3.zero || rot!=Vector3.zero)
            {
                RuleSet5EPlugin.forceExistence = new RuleSet5EPlugin.Existence(pos, rot);
            }
            else
            {
                RuleSet5EPlugin.forceExistence = null;
            }
        }
    }
}
