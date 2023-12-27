using BepInEx;
using HarmonyLib;
using TMPro;

using System;
using System.Collections.Generic;
using System.IO;
using Unity.Mathematics;
using UnityEngine;
using System.Windows.Forms.VisualStyles;
using System.Xml;
using static LordAshes.RuleSet5EPlugin;
using static Symbiotes.Api.v0_1;
using Bounce;
using Bounce.ManagedCollections;
using System.Reflection;
using System.Linq;
using static DiceManager;
using UnityEngine.Assertions.Must;
using Symbiotes;
using System.Collections.ObjectModel;

namespace LordAshes
{
    public partial class RuleSet5EPlugin : BaseUnityPlugin
    {
        public static Action<long> callbackRollReady = null;
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
           
            public static bool Prefix(ref DiceRollDescriptor rollDescriptor, ref bool showResult)
            {
                
                if (rollDescriptor.DiceGroupDescriptors != null) { if (!rollDescriptor.DiceGroupDescriptors[0].Name.Contains("XRuleset5e")) { Debug.Log("RuleSet 5E Plugin: SetDiceUrl with [XRuleset5e]"); return true; } }  //XJ(2022/12/02): To allow TS Core Rolls 
                //XJ (25/06/2023) Delete not relevant code since Symb update
                return true;
            }

            public static void Postfix(DiceRollDescriptor rollDescriptor)
            {
                if (rollDescriptor.DiceGroupDescriptors != null) { if (!rollDescriptor.DiceGroupDescriptors[0].Name.Contains("XRuleset5e")) { return ; } }   //XJ(2022/12/02): To allow TS Core Rolls 
                if (LordAshes.RuleSet5EPlugin.diagnostics >= LordAshes.RuleSet5EPlugin.DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Patch: Spawning Dice Set"); }
                DiceManager dm = GameObject.FindObjectOfType<DiceManager>();
                foreach (DiceGroupDescriptor dgd in rollDescriptor.DiceGroupDescriptors)
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
            public static bool Prefix(DiceRollDescriptor rollDescriptor, bool isGmRoll, bool showResult, RollId rollId)
            {
                return true;
            }

            public static void Postfix(DiceRollDescriptor rollDescriptor, bool isGmRoll, bool showResult, RollId rollId)//, ref int __result)
            {               
                if (rollDescriptor.DiceGroupDescriptors[0].Name != null) { if (!rollDescriptor.DiceGroupDescriptors[0].Name.Contains("XRuleset5e")) { Debug.Log("RuleSet 5E Plugin have XRuleset5e"); return; } }       //XJ(2022/12/02): To allow TS Core Rolls            
                if (LordAshes.RuleSet5EPlugin.diagnostics >= LordAshes.RuleSet5EPlugin.DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Patch: Dice Set Ready"); }

                if (callbackRollResult != null) { callbackRollReady(rollId.AsLong); }


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

                //DiceManager.DiceRollResultData drrd = BinaryIO.FromByteArray<DiceManager.DiceRollResultData>(diceListData, (BinaryReader br) => br.ReadDiceRollResultData());
                DiceManager.RollResults drrd;
                BrSerializeHelpers.DeserializeFromByteArray<DiceManager.RollResults>(new BrSerialize.Reader(), diceListData, new BrSerializeHelpers.BrDeserializer<DiceManager.RollResults>(DiceManager.RollResults.Deserialize), out drrd);
                if (drrd.ResultsGroups[0].Name == null || !drrd.ResultsGroups[0].Name.Contains("XRuleset5e")) { return true; }  //XJ(2022/12/02): To allow TS Core Rolls                 
                return false; // !!!!change to false XJ (2022/11/30) Show dice result according to TS core even if state machine <> idle
            }

            public static void Postfix(bool isGmOnly, byte[] diceListData, PhotonMessageInfo msgInfo)
            {
                if (LordAshes.RuleSet5EPlugin.diagnostics >= LordAshes.RuleSet5EPlugin.DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Patch :  PatchDiceResults2 "); }
                string formula = "";
                string expanded = "";
                short total = 0;
                bool isMax = true;
                bool isMin = true;
                Dictionary<string, object> Result = new Dictionary<string, object>();
                DiceManager.RollResults drrd;
                BrSerializeHelpers.DeserializeFromByteArray<DiceManager.RollResults>(new BrSerialize.Reader(), diceListData, new BrSerializeHelpers.BrDeserializer<DiceManager.RollResults>(DiceManager.RollResults.Deserialize), out drrd);
                if (drrd.ResultsGroups[0].Name == null || !drrd.ResultsGroups[0].Name.Contains("XRuleset5e")) { return; }
                Result.Add("Identifier", drrd.RollId.AsLong);
                if (LordAshes.RuleSet5EPlugin.diagnostics >= LordAshes.RuleSet5EPlugin.DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Patch :  resultgroups " + drrd.ResultsGroups.Length.ToString()); }
                foreach (DiceManager.RollResultsGroup dgrd in drrd.ResultsGroups)
                {
                    if (!Result.ContainsKey("Name")) { Result.Add("Name", dgrd.Name.Replace("XRuleset5e", "")); }
                    
                    Collection<RollOperand> operandList = new Collection<RollOperand>();
                    Collection<RollOperand> subOperandList = new Collection<RollOperand>();
                    operandList.Add(dgrd.Result);
                    int count = 0;
                    bool isEmptyOperand = false;
                    int subcount = 0;
                    Collection<String> OperatorList = new Collection<string>();
                    while (operandList.Count > 0)
                    {
                        count++;
                        subcount = 0;                        
                        foreach (RollOperand operand in operandList)
                        {
                            subcount++;
                            if (LordAshes.RuleSet5EPlugin.diagnostics >= LordAshes.RuleSet5EPlugin.DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Patch : Operando número " + subcount.ToString()); }
                            operand.Get(out DiceManager.RollResultsOperation operation, out DiceManager.RollResult result, out DiceManager.RollValue value);
                            if (operation.Operands != null)
                            {
                                if (LordAshes.RuleSet5EPlugin.diagnostics >= LordAshes.RuleSet5EPlugin.DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Patch : Operando CONTIENE " + operation.Operands.Length.ToString() +" subOperandos"); }
                                foreach (RollOperand subOperand in operation.Operands)
                                {
                                    subOperandList.Add(subOperand);
                                }
                                //XJ To solve incorrect mathematical TS calculations 
                                OperatorList.Add(operation.Operator == DiceManager.DiceOperator.Add ? "+" : "-");
                                if (LordAshes.RuleSet5EPlugin.diagnostics >= LordAshes.RuleSet5EPlugin.DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Patch : Operador de grupo" + OperatorList[count - 1]); }
                            }
                            else
                            {                                
                                //string op = (isEmptyOperand == true ? (subcount == 1 ? OperatorList[count - 3] : (OperatorList[count - 2] == "+" ? OperatorList[count - 3] : (OperatorList[count - 3] == "+" ? "-" : "+"))) : (subcount == 1 ? "+" : OperatorList[count - 2]));
                                string op = subcount == 1 ? (isEmptyOperand == true ? OperatorList[count - 3] : "+") : OperatorList[count - 2];                   
                                if (result.Kind.RegisteredName == "<unknown>")
                                {                   
                                    if (value.Value != 0)
                                    {
                                        formula = formula + op + value.Value;
                                        expanded = expanded + op + value.Value;
                                        total = (short)(op == "+" ? total + value.Value : total - value.Value);
                                        if (isEmptyOperand == true & subcount == 2) { isEmptyOperand = false; }
                                    }
                                    //XJ To solve incorrect mathematical TS calculations
                                    else
                                    {
                                        isEmptyOperand = true;
                                    }
                                }
                                else 
                                {                                   
                                    formula = formula + op + result.Results.Length.ToString() + "D" + result.Kind.RegisteredName.Substring(1);
                                    expanded = expanded +  op + "[" + String.Join(",", result.Results) + "]";
                                    if ((RuleSet5EPlugin.Instance.lastRollRequestTotal == RollTotal.normal) || (!formula.StartsWith("+2D20")))
                                    {
                                        foreach (short v in result.Results)
                                        {                                            
                                            total = (short)(op == "+" ? total + v : total - v);
                                            if (v != 1) { isMin = false; }
                                            if (v != int.Parse(result.Kind.RegisteredName.Substring(1))) { isMax = false; }
                                        }
                                    }
                                    else
                                    {
                                        int roll = (RuleSet5EPlugin.Instance.lastRollRequestTotal == RollTotal.advantage) ? Math.Max(result.Results[0], result.Results[1]) : Math.Min(result.Results[0], result.Results[1]);
                                        total = (short)(total + roll);
                                        if (roll != 1) { isMin = false; }
                                        if (roll != int.Parse(result.Kind.RegisteredName.Substring(1))) { isMax = false; }                                        
                                    }
                                    if (isEmptyOperand == true & subcount==2) { isEmptyOperand = false; }
                                }
                            }
                        }
                        operandList.Clear();
                        for (int i = 0; i< subOperandList.Count; i++)
                        {
                            operandList.Add(subOperandList[i]);
                        }
                        subOperandList.Clear();
                    }
                    if (formula.Substring(0, 1) == "+") { formula = formula.Substring(1, formula.Length - 1); }
                    //expanded = expanded.Substring(1, expanded.Length - 1);
                    if (expanded.Substring(0, 1) == "+") { expanded = expanded.Substring(1, expanded.Length - 1); }
                    Result.Add("Roll", ((RuleSet5EPlugin.Instance.lastRollRequestTotal == RollTotal.normal) ? formula : formula.Replace("2D20", "1D20")).Replace("D", "d"));
                    Result.Add("Total", (int)total); ;
                    Result.Add("Expanded", expanded);
                    Result.Add("IsMin", (bool)isMin);
                    Result.Add("IsMax", (bool)isMax);
                    if (LordAshes.RuleSet5EPlugin.diagnostics >= LordAshes.RuleSet5EPlugin.DiagnosticMode.high) { Debug.Log("RuleSet 5E Patch: Rolled " + Result["Name"] + " (" + Result["Roll"] + ") = " + Result["Expanded"] + " = " + Result["Total"] + " (Min:" + isMin + "/Max:" + isMax + ")"); }
                    if (callbackRollResult != null) { callbackRollResult(Result); }
                }
                //DiceManager.RollResults drrd;
                //BrSerializeHelpers.DeserializeFromByteArray<DiceManager.RollResults>(new BrSerialize.Reader(), diceListData, new BrSerializeHelpers.BrDeserializer<DiceManager.RollResults>(DiceManager.RollResults.Deserialize), out drrd);
                ////DiceManager.DiceRollResultData drrd = BinaryIO.FromByteArray<DiceManager.DiceRollResultData>(diceListData, (BinaryReader br) => br.ReadDiceRollResultData());
                //if (drrd.ResultsGroups[0].Name == null || !drrd.ResultsGroups[0].Name.Contains("XRuleset5e")) { return; }  //XJ(2022/12/02): To allow TS Core Rolls 
                //Result.Add("Identifier", drrd.RollId.AsLong);
                //foreach (DiceManager.RollResultsGroup dgrd in drrd.ResultsGroups)
                //{
                //  if (!Result.ContainsKey("Name")) { Result.Add("Name", dgrd.Name.Replace("XRuleset5e", "")); }
                //  dgrd.Result.Get(out DiceManager.RollResultsOperation operation, out DiceManager.RollResult result, out DiceManager.RollValue value);
                //    if (operation.Operands == null)
                //    {
                //        if (LordAshes.RuleSet5EPlugin.diagnostics >= LordAshes.RuleSet5EPlugin.DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Patch :  operation.operator :" + operation.Operator.ToString()); }
                //        if (result.Kind.RegisteredName == "<unknown>")
                //        {
                //            if (LordAshes.RuleSet5EPlugin.diagnostics >= LordAshes.RuleSet5EPlugin.DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Patch :  value.value :" + value.Value.ToString()); }
                //            if (operation.Operator == DiceManager.DiceOperator.Add)
                //            {
                //                mod = +value.Value;
                //            }
                //            else
                //            {
                //                mod = -value.Value;
                //            }
                //        }
                //        else
                //        {
                //            if (LordAshes.RuleSet5EPlugin.diagnostics >= LordAshes.RuleSet5EPlugin.DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Patch :  result.lengt :" + result.Results.Length.ToString()); }                           
                //            if (LordAshes.RuleSet5EPlugin.diagnostics >= LordAshes.RuleSet5EPlugin.DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Patch :  resulto.kind.reg:" + result.Kind.RegisteredName.ToString()); }
                //            if (operation.Operator == DiceManager.DiceOperator.Add)
                //            {
                //                formula = formula + "+" + result.Results.Length.ToString() + "D" + result.Kind.RegisteredName.Substring(1);//dgrd.Name.ToString().Replace("numbered1", "");
                //            }
                //            else
                //            {
                //                formula = formula + "-" + result.Results.Length.ToString() + "D" + result.Kind.RegisteredName.Substring(1);//dgrd.Name.ToString().Replace("numbered1", "");+ dgrd.Name.ToString().Replace("numbered1", "");
                //            }
                //            //XJ: (2022/10/08) Commented: 
                //            // if (RuleSet5EPlugin.Instance.damageDieMultiplier == 1.0f)
                //            // {                            
                //            if (operation.Operator == DiceManager.DiceOperator.Add)
                //            {
                //                expanded = expanded + "+" + "[" + String.Join(",", result.Results) + "]";
                //            }
                //            else
                //            {
                //                expanded = expanded + "-" + "[" + String.Join(",", result.Results) + "]";
                //            }
                //            // }
                //            // else
                //            // {
                //            //     expanded = expanded + RuleSet5EPlugin.Instance.damageDieMultiplier.ToString("0")+"x[" + String.Join(",", drd.Results) + "]";
                //            // }
                //            //XJ: Avoid 2x damage, because are calculated in roll dice now
                //            //int sides = int.Parse(dgrd.Name.ToString().Replace("numbered1D", ""));                            
                //            int sides = int.Parse(result.Kind.RegisteredName.Substring(1));
                //            if ((RuleSet5EPlugin.Instance.lastRollRequestTotal == RollTotal.normal) || (!formula.StartsWith("+2D20")))
                //            {
                //                foreach (short val in result.Results)
                //                {
                //                    if (LordAshes.RuleSet5EPlugin.diagnostics >= LordAshes.RuleSet5EPlugin.DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Patch :  result.result.value :" + val.ToString()); }
                //                    //XJ(2022/10/08) Change: total = (short)(total + val * RuleSet5EPlugin.Instance.damageDieMultiplier);
                //                    if (operation.Operator == DiceManager.DiceOperator.Add)
                //                    {
                //                        total = (short)(total + val);
                //                    }
                //                    else
                //                    {
                //                        total = (short)(total - val);
                //                    }
                //                    //total = (short)(total + val);
                //                    //XJ: Avoid 2x damage, because are calculated in roll dice now
                //                    if (val != 1) { isMin = false; }
                //                    if (val != sides) { isMax = false; }
                //                }
                //            }
                //            else
                //            {
                //                int roll = (RuleSet5EPlugin.Instance.lastRollRequestTotal == RollTotal.advantage) ? Math.Max(result.Results[0], result.Results[1]) : Math.Min(result.Results[0], result.Results[1]);
                //                //XJ(2022/10/08) Change: total = (short)(total + roll * RuleSet5EPlugin.Instance.damageDieMultiplier);
                //                total = (short)(total + roll);
                //                //XJ: Avoid 2x damage, because are calculated in roll dice now                           
                //                if (roll != 1) { isMin = false; }
                //                if (roll != sides) { isMax = false; }
                //            }
                //        }
                //        if (mod != 0)
                //        {
                //            formula = formula + ("+-".Contains(mod.ToString().Substring(0, 1)) ? "" : "+") + mod;
                //            expanded = expanded + ("+-".Contains(mod.ToString().Substring(0, 1)) ? "" : "+") + mod;
                //            total = (short)(total + mod);
                //        }
                //    }
                //    else
                //    {
                //        //foreach (DiceManager.RollOperand drd in DiceManager.)
                //        foreach (DiceManager.RollOperand operand in operation.Operands)
                //        {
                //            if (LordAshes.RuleSet5EPlugin.diagnostics >= LordAshes.RuleSet5EPlugin.DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Patch :  operation.operand.leng :" + operation.Operands.Length.ToString()); }
                //            operand.Get(out DiceManager.RollResultsOperation rolloperation, out DiceManager.RollResult rollresult, out DiceManager.RollValue rollvalue);
                //            if (LordAshes.RuleSet5EPlugin.diagnostics >= LordAshes.RuleSet5EPlugin.DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Patch :  operation.operator :" + rolloperation.Operator.ToString()); }
                //            if (rollresult.Kind.RegisteredName == "<unknown>")
                //            {
                //                if (LordAshes.RuleSet5EPlugin.diagnostics >= LordAshes.RuleSet5EPlugin.DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Patch :  value.value <unknow> :" + rollvalue.Value.ToString()); }
                //                if (rolloperation.Operator == DiceManager.DiceOperator.Add)
                //                {
                //                    mod = +rollvalue.Value;
                //                }
                //                else
                //                {
                //                    mod = -rollvalue.Value;
                //                }
                //            }
                //            else
                //            {
                //                if (LordAshes.RuleSet5EPlugin.diagnostics >= LordAshes.RuleSet5EPlugin.DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Patch :  result.lengt :" + rollresult.Results.Length.ToString()); }
                //                if (LordAshes.RuleSet5EPlugin.diagnostics >= LordAshes.RuleSet5EPlugin.DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Patch :  resulto.kind.reg:" + rollresult.Kind.RegisteredName.ToString()); }
                //                if (rolloperation.Operator == DiceManager.DiceOperator.Add)
                //                {
                //                    formula = formula + "+" + rollresult.Results.Length.ToString() + "D" + rollresult.Kind.RegisteredName.Substring(1);//dgrd.Name.ToString().Replace("numbered1", "");
                //                }
                //                else
                //                {
                //                    formula = formula + "-" + rollresult.Results.Length.ToString() + "D" + rollresult.Kind.RegisteredName.Substring(1);//dgrd.Name.ToString().Replace("numbered1", "");+ dgrd.Name.ToString().Replace("numbered1", "");
                //                }
                //                //XJ: (2022/10/08) Commented: 
                //                // if (RuleSet5EPlugin.Instance.damageDieMultiplier == 1.0f)
                //                // {                                
                //                if (rolloperation.Operator == DiceManager.DiceOperator.Add)
                //                {
                //                    expanded = expanded + "+" + "[" + String.Join(",", rollresult.Results) + "]";
                //                }
                //                else
                //                {
                //                    expanded = expanded + "-" + "[" + String.Join(",", rollresult.Results) + "]";
                //                }
                //                // }
                //                // else
                //                // {
                //                //     expanded = expanded + RuleSet5EPlugin.Instance.damageDieMultiplier.ToString("0")+"x[" + String.Join(",", drd.Results) + "]";
                //                // }
                //                //XJ: Avoid 2x damage, because are calculated in roll dice now
                //                //int sides = int.Parse(dgrd.Name.ToString().Replace("numbered1D", ""));                                
                //                int sides = int.Parse(rollresult.Kind.RegisteredName.Substring(1));
                //                if ((RuleSet5EPlugin.Instance.lastRollRequestTotal == RollTotal.normal) || (!formula.StartsWith("+2D20")))
                //                {
                //                    foreach (short val in rollresult.Results)
                //                    {
                //                        if (LordAshes.RuleSet5EPlugin.diagnostics >= LordAshes.RuleSet5EPlugin.DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Patch :  result.result.value :" + val.ToString()); }
                //                        //XJ(2022/10/08) Change: total = (short)(total + val * RuleSet5EPlugin.Instance.damageDieMultiplier);
                //                        if (rolloperation.Operator == DiceManager.DiceOperator.Add)
                //                        {
                //                            total = (short)(total + val);
                //                        }
                //                        else
                //                        {
                //                            total = (short)(total - val);
                //                        }
                //                        //total = (short)(total + val);
                //                        //XJ: Avoid 2x damage, because are calculated in roll dice now
                //                        if (val != 1) { isMin = false; }
                //                        if (val != sides) { isMax = false; }
                //                    }
                //                }
                //                else
                //                {
                //                    int roll = (RuleSet5EPlugin.Instance.lastRollRequestTotal == RollTotal.advantage) ? Math.Max(rollresult.Results[0], rollresult.Results[1]) : Math.Min(rollresult.Results[0], rollresult.Results[1]);
                //                    //XJ(2022/10/08) Change: total = (short)(total + roll * RuleSet5EPlugin.Instance.damageDieMultiplier);
                //                    total = (short)(total + roll);
                //                    //XJ: Avoid 2x damage, because are calculated in roll dice now                           
                //                    if (roll != 1) { isMin = false; }
                //                    if (roll != sides) { isMax = false; }
                //                }
                //            }
                //            if (mod != 0)
                //            {
                //                formula = formula + ("+-".Contains(mod.ToString().Substring(0, 1)) ? "" : "+") + mod;
                //                expanded = expanded + ("+-".Contains(mod.ToString().Substring(0, 1)) ? "" : "+") + mod;
                //                total = (short)(total + mod);
                //            }
                //            //formula = formula + ((drd.DiceOperator == DiceManager.DiceOperator.Add) ? "+" : "-");
                //            //expanded = expanded + ((drd.DiceOperator == DiceManager.DiceOperator.Add) ? "+" : "-");
                //            //if (drd.DiceOperator == DiceManager.DiceOperator.Add) { total = (short)(total + drd.Modifier); } else { total = (short)(total - drd.Modifier); }
                //            //formula = formula + drd.Modifier + "+";
                //            //expanded = expanded + drd.Modifier + "+";
                //        }
                //    }
                //    if (formula.Substring(0, 1) == "+") { formula = formula.Substring(1, formula.Length - 1); }
                //    Debug.Log("formula :"+ formula);
                //    expanded = expanded.Substring(1, expanded.Length-1 );
                //    Debug.Log("expanded :" + expanded);
                //    Result.Add("Roll", ((RuleSet5EPlugin.Instance.lastRollRequestTotal == RollTotal.normal) ? formula : formula.Replace("2D20", "1D20")).Replace("D","d"));
                //    Result.Add("Total", (int)total); ;
                //    Result.Add("Expanded", expanded);
                //    Result.Add("IsMin", (bool)isMin);
                //    Result.Add("IsMax", (bool)isMax);
                //    if (LordAshes.RuleSet5EPlugin.diagnostics >= LordAshes.RuleSet5EPlugin.DiagnosticMode.high) { Debug.Log("RuleSet 5E Patch: Rolled " + Result["Name"] + " (" + Result["Roll"] + ") = " + Result["Expanded"] + " = " + Result["Total"] + " (Min:" + isMin + "/Max:" + isMax + ")"); }
                //    if (callbackRollResult != null) { callbackRollResult(Result); }
                //}
            }
        }

        /// <summary>
        /// Patch to allow spawning at a forced location and rotation instead of the camera default
        /// </summary>
        [HarmonyPatch(typeof(Die), "Spawn")]
        public static class PatchSpawn
        {
            public static bool Prefix(DieKind kind, float3 pos, quaternion rot, RollId rollId, byte groupId, bool gmOnlyDie,bool showResult)
            {
                if (stateMachineState == StateMachineState.idle) { return true; }
                return false;
            }

            //public static void Postfix(string resource, float3 pos, quaternion rot, int rollId, byte groupId, bool gmOnlyDie, ref Die __result)
            public static void Postfix(DieKind kind, float3 pos, quaternion rot, RollId rollId, byte groupId, bool gmOnlyDie, bool showResult, ref Die __result)
            {
                if (stateMachineState == StateMachineState.idle) { return; }
                RegisteredDieResources registeredDieResources;
                BDebug.AssertHard(DiceManager.TryGetDieResources(kind, out registeredDieResources));              
                object[] data = new object[]
                {
                    rollId.AsLong,
                    groupId,
                    gmOnlyDie,
                    showResult
                };
                if (LordAshes.RuleSet5EPlugin.diagnostics >= LordAshes.RuleSet5EPlugin.DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Patch: Spawning Dice At " + ((forceExistence == null) ? pos.ToString() : forceExistence.position.ToString())); }
                Die component = (forceExistence == null) ? PhotonNetwork.Instantiate(registeredDieResources.ResourcePath, pos, rot, 0, data).GetComponent<Die>() : PhotonNetwork.Instantiate(registeredDieResources.ResourcePath, forceExistence.position, Quaternion.Euler(forceExistence.rotation), 0, data).GetComponent<Die>();                
                PatchAssistant.UseMethod(component, "Init", new object[] {kind, LocalClient.Id, rollId, groupId, gmOnlyDie,showResult }); // component.Init(rollId, groupId, gmOnlyDie);                
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
