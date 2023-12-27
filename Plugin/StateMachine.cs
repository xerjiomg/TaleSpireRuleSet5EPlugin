using BepInEx;
using Bounce.Singletons;
using Bounce.Unmanaged;
using BRClient;
using GameChat.UI;
using HarmonyLib;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using SRF.Components;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Windows.Forms;
using Tantawowa.Extensions;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Device;
using UnityEngine.EventSystems;
using static LordAshes.RuleSet5EPlugin;

namespace LordAshes
{
    public class MultiDCAttackData
    {
        public CreatureBoardAsset mVcitim { get; set; } = null;
        public bool mHalfDamage { get; set; } =false;
        public bool mReactionHalve { get; set; } = false;
    }

    public partial class RuleSet5EPlugin : BaseUnityPlugin
    {       
        //XJ (2022/11/09) variable to set diagnostic detail level.
        public static DiagnosticMode diagnostics;
        // Scale
        public const float scale = 5;        
        // Variables to track previous and current state in the state machine
        public static RollMode rollingSystem = RollMode.automaticDice;
        public static StateMachineState stateMachineState = StateMachineState.idle;
        public static StateMachineState stateMachineLastState = StateMachineState.idle;

        // Rolling related variables
        private Roll lastRollRequest = null;
        private RollTotal lastRollRequestTotal = RollTotal.normal;
        private Roll loadedRollRequest = null;
        private long lastRollId = -2;
        private Dictionary<string, object> lastResult = null;
        private float damageDieMultiplier = 1.0f;

        // Sequence actors
        private CreatureBoardAsset instigator = null;
        private CreatureBoardAsset victim = null;

        // Animation names // Valid Names are: "TLA_Twirl", "TLA_Action_Knockdown", "TLA_Wiggle", "TLA_MeleeAttack", "TLA_Surprise", "TLA_MagicMissileAttack"
        private string missAnimation = "TLA_Wiggle";
        private string deadAnimation = "TLA_Action_Knockdown";

        //XJ:2022/10/14 Change Base Colors var
        private bool changeBaseColors = true;
        private string[] npcColors  = { "6", "7", "8" };
        private string[] pcColors = { "2", "13", "1" };
        //private string
        //XJ: Switch auto-changeBaseColors true or false.Setting Colors.

        // Misc variables
        private Existence saveCamera = null;
        private string messageContent = "";
        private ChatManager chatManager = null;
        private bool totalAdv = false;
        private bool totalDis = false;
        //XJ: (2022/12/16) To use 
        private bool victim_totalAdv = false;
        private bool victim_totalDis = false;
        private bool useAttackBonusDie = false;
        private string amountAttackBonusDie = "";
        private bool useDamageBonusDie = false;
        private string amountDamageBonusDie = "";
        private bool useSkillBonusDie = false;
        private string amountSkillBonusDie = "";
        private bool victim_useSkillBonusDie = false;
        private string amountACBonusDie = "";
        private bool useACBonusDie = false;
        private string victim_amountSkillBonusDie = "";
        private string victim_amountACBonusDie = "";
        private bool reactionStop = false;
        public static float processSpeed = 1.0f;
        private Existence diceSideExistance = null;
        //XJ (2022/10/18) New bool secureSuccess   
        bool secureSuccess = false;
        //XJ (2022/10/18) New bool DC half damage on Successful Save.
        bool halfDamage = false;
        //XJ (2022/11/27) New bool criticalImmunity .
        bool criticalImmunity = false;
        //Multitarget DC attacks list
        bool firstWithDamageBonus = false;
        //XJ:(2023/03/08): Fix auxiliary camera bug after TS patch
        GameObject dolly = null;
        Camera camera = null;
        //RenderTexture auxCameraTexture = new RenderTexture(UnityEngine.Device.Screen.width/4, UnityEngine.Device.Screen.height/4,  32);
        RenderTexture auxCameraTexture = new RenderTexture(UnityEngine.Device.Screen.width , UnityEngine.Device.Screen.height , 32);


        private List <MultiDCAttackData> multiDCAttackDataList = new List <MultiDCAttackData>();

        //XJ (2022/11/09)
        public enum DiagnosticMode
        {
            none = 0,
            low = 1,
            high = 2,
            ultra = 3
        }

        public enum RollTotal
        {
            normal = 0,
            advantage = 1,
            disadvantage = 2
        }

        public enum RollMode
        {
            manual = 0,
            manual_side = 1,
            automaticDice = 2,
            automaticGenerator = 3
        }

        public enum StateMachineState
        {
            idle = 0,
            // Attack Sequence
            attackAttackRangeCheck,
            attackAttackIntention,
            attackRollSetup,
            attackAttackDieCreate,
            attackAttackDieWaitCreate,
            attackAttackDieRollExecute,
            attackAttackDieWaitRoll,
            attackAttackBonusDieCreate,
            attackAttackBonusDieWaitCreate,
            attackAttackBonusDieRollExecute,
            attackAttackBonusDieWaitRoll,
            attackAttackBonusDieReaction,
            attackAttackBonusDieReactionWait,
            attackAttackDieRollReport,
            attackAttackDefenceCheck,
            attackAttackMissReport,
            attackAttackHitReport,
            attackDamageDieCreate,
            attackDamageDieWaitCreate,
            attackDamageDieRollExecute,
            attackDamageDieWaitRoll,
            attackDamageDieRollReport,
            attackDamageDieDamageReport,
            attackDamageDieDamageTake,
            attackRollCleanup,
            // Skill Roll
            skillRollSetup,
            skillRollDieCreate,
            skillRollDieWaitCreate,
            skillRollDieRollExecute,
            skillRollDieWaitRoll,
            skillBonusRollDieCreate,
            skillBonusRollDieWaitCreate,
            skillBonusRollDieRollExecute,
            skillBonusRollDieWaitRoll,
            skillRollDieRollReport,
            skillRollCleanup,
            skillRollMore,
            // Healing Roll
            healingRollStart,
            healingRollDieCreate,
            healingRollDieWaitCreate,
            healingRollDieRollExecute,
            healingRollDieWaitRoll,
            healingRollDieRollReport,
            healingRollDieValueReport,
            healingRollDieValueTake,
            healingRollCleanup,  
        }

        private IEnumerator Executor()
        {            
            DiceManager dm = GameObject.FindObjectOfType<DiceManager>();            
            UIDiceTray dt = GameObject.FindObjectOfType<UIDiceTray>();           
            List<Damage> damages = new List<Damage>();        
            Roll tmp = null;
            Dictionary<string, object> hold = null;          
            string players = "";
            string owner = "";
            string gm = "";

            while (true)
            {                
                if (stateMachineState != stateMachineLastState) { if (diagnostics >= DiagnosticMode.high) { Debug.Log("RuleSet 5E Plugin: State = " + stateMachineState); stateMachineLastState = stateMachineState; } }
                float stepDelay = 0.100f;
                players = "";
                owner = "";
                gm = "";
                int total = 0;
                string info = "";
                int hp = 0;
                int hpMax = 0;
                //XJ (2022/10/18) Allow secureSuccess                 

                switch (stateMachineState)
                {
                    // *******************
                    // * Attack Sequence *
                    // *******************
                    case StateMachineState.attackAttackRangeCheck:
                        stateMachineState = StateMachineState.attackAttackIntention;
                        if (healSequence) { stateMachineState = StateMachineState.healingRollStart; }
                        //XJ(2022/10/18) 
                        secureSuccess = false;
                        halfDamage = false;             
                        //XJ Restore defaul secureSuccess state and half damage (DC Attacks).
                        float dist;
                        float reachAdjust = 0.5f; // (extra adjust)
                        //XJ: (2022/10/19)
                        if (CreatureManager.SnapToGrid)
                        {
                            Vector3 vecresult = instigator.transform.position - victim.transform.position;
                            dist = scale * Math.Max(Math.Abs(vecresult[0]), Math.Max(Math.Abs(vecresult[1]), Math.Abs(vecresult[2]))); //XJ: Get the difference from each axis and use the max value. 
                            //XJ  (2022/10/18)                       
                            dist = dist - (((instigator.Scale >= 1) ? instigator.Scale : 1) - 1) * (scale / 2) - (((victim.Scale >= 1) ? victim.Scale : 1) - 1) * (scale / 2);
                            //XJ Adjust distance according to creature's size. Size 1/2 works like 1
                            dist = float.Parse(Math.Round(dist).ToString());
                        }
                        else
                        {
                            dist = (scale * Vector3.Distance(instigator.transform.position, victim.transform.position));
                            //XJ  (2022/10/18) 
                            if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Attack:" + dist + "|" + instigator.ScaledBaseRadius.ToString() + "|" + victim.ScaledBaseRadius.ToString() + "|" + scale.ToString()); }
                            dist = dist - ((instigator.ScaledBaseRadius + victim.ScaledBaseRadius) * scale - scale);
                            if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Attack: dist : " + dist); }
                            //XJ Adjust distance according to creature's size.
                        }
                        //XJ: On Sntap to grid. Distance measure on grid squares.(The grid is snap to the axis)                      

                        if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Attack: Ran-+ge=" + dist); }

                        int attackRange = (lastRollRequest.type.ToUpper() == "MELEE" & lastRollRequest.range == "0/0") ? characters[Utility.GetCharacterName(instigator)].reach : int.Parse(lastRollRequest.range.Split('/')[1]);
                        if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("distancia:" + dist.ToString() + "attackrange + adjust:" + (attackRange + reachAdjust).ToString() + "atacck range:" + attackRange.ToString() + "adjust:" + reachAdjust.ToString()); ; }
                        if (dist > (attackRange + reachAdjust))
                        {
                            StartCoroutine(DisplayMessage(Utility.GetCharacterName(instigator) + " cannot reach " + Utility.GetCharacterName(victim) + " at " + dist + "' with " + lastRollRequest.name + " (Range: " + attackRange + "')", 1.0f));
                            if (victim != null) { victim.SetGlow(false, UnityEngine.Color.red); }

                            stateMachineState = StateMachineState.idle;
                            
                            if (multiTargetAssets.Count != MultitargetAssetsIndex) { if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Cannot reach Multi"); }; StartSequencePre(multiAttackType, multiRoll, instigator.CreatureId, null, null); }
                        }
                        else if ((lastRollRequest.type.ToUpper() == "RANGE") || (lastRollRequest.type.ToUpper() == "RANGED") || (lastRollRequest.type.ToUpper() == "MAGIC"))
                        {
                            attackRange = int.Parse(lastRollRequest.range.Split('/')[0]);
                            if (dist <= (attackRange + reachAdjust))  //XJ (2022/10/19): Change 2.0f > reachAdjust
                            {
                                
                                foreach (CreatureBoardAsset asset in CreaturePresenter.GetTempReadOnlyViewOfAllCreatureAssets())
                                {
                                    int reach = 5;
                                    bool npc = true;
                                    if (characters.ContainsKey(Utility.GetCharacterName(asset)))
                                    {
                                        npc = characters[Utility.GetCharacterName(asset)].NPC;
                                        reach = characters[Utility.GetCharacterName(asset)].reach;
                                    }
                                    //dist = scale * Vector3.Distance(instigator.transform.position, asset.transform.position); //XJ (2022/10/19) previously calculated.
                                    if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: " + (npc ? "Foe" : "Ally") + " " + Utility.GetCharacterName(asset) + " at " + dist + "' with reach " + reach); }
                                    if (npc && (dist < (reach + reachAdjust)) && (instigator.CreatureId != asset.CreatureId)) //XJ (2022/10/19): Change 2.0f > reachAdjust
                                    {
                                        StartCoroutine(DisplayMessage(Utility.GetCharacterName(instigator) + " is with " + reach + "' reach of " + Utility.GetCharacterName(asset) + ". Disadvantage on ranged attacks.", 1.0f));
                                        //lastRollRequestTotal = RollTotal.disadvantage;
                                    }
                                }
                            }
                            else
                            {
                                StartCoroutine(DisplayMessage(Utility.GetCharacterName(instigator) + " requires a long range shot (" + attackRange + "'+) to reach of " + Utility.GetCharacterName(victim) + " at " + dist + "'. Disadvantage on ranged attacks.", 1.0f));
                                //lastRollRequestTotal = RollTotal.disadvantage;
                            }
                        }
                        
                        break;
                    case StateMachineState.attackAttackIntention:
                        stateMachineState = StateMachineState.attackRollSetup;
                        instigator.SpeakEx("Attack!");
                        
                        players = "[" + RuleSet5EPlugin.Utility.GetCharacterName(instigator) + "] <size=28>Attacks " + RuleSet5EPlugin.Utility.GetCharacterName(victim);
                        owner = players;
                        gm = players;
                        chatManager.SendChatMessageEx(players, owner, gm, instigator.CreatureId, LocalClient.Id.Value);
                        for (int r = 0; r < 10; r++)
                        {
                            instigator.RotateTowards(victim.transform.position);
                            victim.RotateTowards(instigator.transform.position);
                            yield return new WaitForSeconds(0.010f * processSpeed);
                        }
                        break;
                    case StateMachineState.attackRollSetup:
                        stateMachineState = StateMachineState.attackAttackDieCreate;
                        RollSetup(dm, ref stepDelay);
                        if (rollingSystem == RollMode.automaticDice) { dolly.transform.position = new Vector3(-100f, 2f, -1.5f); }
                        damageDieMultiplier = 1.0f;
                        break;
                    case StateMachineState.attackAttackDieCreate:
                        stateMachineState = StateMachineState.attackAttackDieWaitCreate;
                        //XJ(2022/10/12)
                        if (dcAttack)
                        {
                            //XJ(2022/10/17) 
                            if (lastRollRequest.roll.Contains("/"))
                            {
                                bool havedata = false;
                                foreach (Roll roll in characters[Utility.GetCharacterName(victim)].saves)
                                {
                                    if (roll.name.ToUpper() == lastRollRequest.roll.Split('/')[1].ToUpper())
                                    {
                                        RollCreate(dt, $"talespire://dice/" + SafeForProtocolName(lastRollRequest.name) + ":" + roll.roll, ref stepDelay);
                                        havedata = true;
                                        break;
                                    }
                                }
                                if (havedata == false)
                                {
                                    foreach (Roll roll in characters[Utility.GetCharacterName(victim)].skills)
                                    {
                                        if (roll.name.ToUpper().Contains(lastRollRequest.roll.Split('/')[1].ToUpper()))
                                        {
                                            RollCreate(dt, $"talespire://dice/" + SafeForProtocolName(lastRollRequest.name) + ":" + roll.roll, ref stepDelay);
                                            havedata = true;
                                            break;
                                        }
                                    }
                                }
                                if (havedata == false) { RollCreate(dt, $"talespire://dice/" + SafeForProtocolName(lastRollRequest.name) + ":" + "1d20", ref stepDelay); SystemMessage.DisplayInfoText("Victim dont have:" + lastRollRequest.roll.Split('/')[1].ToString(), 4.0f); if (diagnostics >= DiagnosticMode.high) { Debug.Log("RuleSet 5E Plugin: " + "Victim dont have: " + lastRollRequest.roll.Split('/')[1].ToString()); }; }
                            }
                            else
                            {
                                secureSuccess = true;
                                stateMachineState = StateMachineState.attackAttackBonusDieReaction;
                                if (diagnostics >= DiagnosticMode.high) { Debug.Log("RuleSet 5E Plugin: Secure Success (DC Attack)"); }
                            }
                        }
                        //XJ: get enemy Save roll on DC Attacks.
                        else
                        {
                            if (lastRollRequest.roll.ToUpper().Contains("D"))
                            {
                                RollCreate(dt, $"talespire://dice/" + SafeForProtocolName(lastRollRequest.name) + ":" + lastRollRequest.roll, ref stepDelay);
                            }
                            else
                            {
                                secureSuccess = true;
                                stateMachineState = StateMachineState.attackAttackBonusDieReaction;
                                if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Secure Success (Attack)"); }
                            }
                        }
                        //XJ: Allow Sucess 
                        if (rollingSystem.ToString().ToUpper().Contains("MANUAL")) { StartCoroutine(DisplayMessage("Please Roll Provided Die Or Dice To Continue...", 3f)); }
                        break;
                    case StateMachineState.attackAttackDieWaitCreate:
                        // Callback propagates to next phase
                        break;
                    case StateMachineState.attackAttackDieRollExecute:
                        stateMachineState = StateMachineState.attackAttackDieWaitRoll;
                        RollExecute(dm, ref stepDelay);
                        break;
                    case StateMachineState.attackAttackDieWaitRoll:
                        // Callback propagates to next phase
                        break;
                    case StateMachineState.attackAttackBonusDieCreate:
                        if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("Critical Check Stage 1 = " + lastResult["IsMax"]); }
                        //XJ: (2022/10/04) changed  codeline:
                        //stateMachineState = StateMachineState.attackAttackDieRollReport;
                        //for:
                        stateMachineState = StateMachineState.attackAttackBonusDieReaction;
                        //XJ: To avoid skip statemachine where reaction called. 
                        //XJ (2022/10/13) 
                        if (dcAttack)
                        {
                            //XJ (2022/11/28) New bonus system
                            useAttackBonusDie = victim_useSkillBonusDie;  // useAttackBonusDie = characters[Utility.GetCharacterName(victim)]._usingSkillBonus;
                            amountAttackBonusDie = victim_amountSkillBonusDie;  // amountAttackBonusDie = characters[Utility.GetCharacterName(victim)]._usingSkillBonusAmonunt;
                        }
                        //XJ: get save defence bonus in DC attacks.  
                        if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: UseAttackBonusDie: " + useAttackBonusDie.ToString() + " mountAttackBonusDie: " + amountAttackBonusDie.ToString()); }
                        if (useAttackBonusDie & amountAttackBonusDie != "")
                        {
                            hold = lastResult;
                            if (amountAttackBonusDie.ToUpper().Contains("D"))
                            {
                                // AttackBonus is a Die Roll
                                //XJ: (2022/10/07) Added new codeline: 
                                RollId.TryParse(lastRollId.ToString(), out RollId idLastroll2);                               
                                dm.ClearDiceRoll(idLastroll2);
                                
                                //XJ: To clear previus rolls.
                                stateMachineState = StateMachineState.attackAttackBonusDieWaitCreate;
                                RollCreate(dt, $"talespire://dice/" + SafeForProtocolName("Bonus Die") + ":" + amountAttackBonusDie, ref stepDelay);
                                if (rollingSystem.ToString().ToUpper().Contains("MANUAL")) { StartCoroutine(DisplayMessage("Please Roll Provided Die Or Dice To Continue...", 3f)); }
                            }
                            else
                            {
                                // AttackBonus is Constant
                                lastResult = ResolveRoll(amountAttackBonusDie);
                            }
                        }
                        break;
                    case StateMachineState.attackAttackBonusDieWaitCreate:
                        // Callback propagates to next phase
                        break;
                    case StateMachineState.attackAttackBonusDieRollExecute:
                        stateMachineState = StateMachineState.attackAttackBonusDieWaitRoll;
                        RollExecute(dm, ref stepDelay);
                        break;
                    case StateMachineState.attackAttackBonusDieWaitRoll:
                        // Callback propagates to next phase
                        break;
                    case StateMachineState.attackAttackBonusDieReaction:
                        stateMachineState = StateMachineState.attackAttackDieRollReport;
                        //XJ: (2022 / 10 / 17) 
                        if (secureSuccess) { stateMachineState = StateMachineState.attackAttackHitReport; }
                        //XJ: Allow secure Success
                        RollId.TryParse(lastRollId.ToString(), out RollId idLastroll);
                        dm.ClearDiceRoll(idLastroll);
                        //XJ:(2022/10/17) new condition Secure attack.                        
                        if (useAttackBonusDie & amountAttackBonusDie != "" & secureSuccess == false)
                        {
                            if (diagnostics >= DiagnosticMode.high) { Debug.Log("Adding Bonus Die"); }

                            hold["Total"] = ((int)hold["Total"] + (int)lastResult["Total"]);

                            //if ( "-".Contains(lastResult["Roll"].ToString().Substring(0, 1)) & loadedRollRequest == null)
                            if ( "-".Contains(lastResult["Expanded"].ToString().Substring(0, 1)) & loadedRollRequest == null)
                            {
                                //hold["Expanded"] = hold["Expanded"] +"-"+ lastResult["Expanded"].ToString();
                                hold["Expanded"] = hold["Expanded"] + lastResult["Expanded"].ToString();
                            }
                            else 
                            {
                                hold["Expanded"] = hold["Expanded"] + "+" + lastResult["Expanded"].ToString();
                            }
                            //hold["Expanded"] = hold["Expanded"] + (("+-".Contains(lastResult["Expanded"].ToString().Substring(0, 1))) ? lastResult["Expanded"].ToString().Replace("+-", "-") : "+" + lastResult["Expanded"].ToString().Replace("+-", "-"));

                            //}
                            //XJ: To allow negative roll bonus dice attack and solve chat visual problems with expanded string in this case (2022/10/05): when rolltype not is automaticGenerator.

                            hold["Roll"] = hold["Roll"] + (("+-".Contains(lastResult["Roll"].ToString().Substring(0, 1))) ? lastResult["Roll"].ToString() : "+" + lastResult["Roll"].ToString());
                            lastResult = hold;
                            if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("Bonus Die Added"); }
                        }
                        criticalImmunity = false;
                        if (secureSuccess == false && (bool)lastResult["IsMax"] == true & characters[RuleSet5EPlugin.Utility.GetCharacterName(victim)].immunity.Contains("critical")) //XJ (2022/11/27) Crítical immunity
                        {
                            if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Critical immunity "); }
                            criticalImmunity = true;
                            lastResult["IsMax"] = false;
                        }
                        if (reactionStop)
                        {
                            stateMachineState = StateMachineState.attackAttackBonusDieReactionWait;
                             
                            reactionStopContinue = true;
                            //XJ:(2022/10/13)
                            if (secureSuccess)
                            {
                                reactionRollTotal = "Automatic Success";
                            }
                            //XJ: Allow SecureSuccess
                            else if (dcAttack)
                            {
                                reactionRollTotal = lastResult["Expanded"].ToString() + " = " + lastResult["Total"].ToString() + " VS DC:" + lastRollRequest.roll.Split('/')[0];
                            }
                            else
                            {

                                reactionRollTotal = lastResult["Expanded"].ToString() + " = " + lastResult["Total"].ToString() + " VS AC"; // characters[RuleSet5EPlugin.Utility.GetCharacterName(victim)].ac 

                            }
                            //XJ: (2022/10/08) To show total attack and not only dice roll.
                        }
                        break;
                    case StateMachineState.attackAttackBonusDieReactionWait:
                        break;
                    case StateMachineState.attackAttackDieRollReport:
                        stateMachineState = StateMachineState.attackAttackDefenceCheck;
                        if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("Critical Check State 2 = " + lastResult["IsMax"]); }
                        //XJ (2022/10/13) add new if, not critical on DC attacks. 
                        if (dcAttack == false)
                        {
                            //XJ: (2022/10/05) Added:
                            int dieresult = int.Parse(lastResult["Expanded"].ToString().Substring(lastResult["Expanded"].ToString().IndexOf("[") + 1, lastResult["Expanded"].ToString().IndexOf("]") - 1).Split(',')[0]);
                            if (totalAdv || totalDis)
                            {
                                if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("Prueba 1 - Expanded: "+ lastResult["Expanded"]); }
                                if (totalAdv & int.Parse(lastResult["Expanded"].ToString().Substring(lastResult["Expanded"].ToString().IndexOf("[") + 1, lastResult["Expanded"].ToString().IndexOf("]") - 1).Split(',')[1]) > dieresult)
                                {
                                    dieresult = int.Parse(lastResult["Expanded"].ToString().Substring(lastResult["Expanded"].ToString().IndexOf("[") + 1, lastResult["Expanded"].ToString().IndexOf("]") - 1).Split(',')[1]);
                                    if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("Prueba 1Bis - Expanded: " + dieresult); }
                                }
                                if (totalDis & int.Parse(lastResult["Expanded"].ToString().Substring(lastResult["Expanded"].ToString().IndexOf("[") + 1, lastResult["Expanded"].ToString().IndexOf("]") - 1).Split(',')[1]) < dieresult)
                                {
                                    dieresult = int.Parse(lastResult["Expanded"].ToString().Substring(lastResult["Expanded"].ToString().IndexOf("[") + 1, lastResult["Expanded"].ToString().IndexOf("]") - 1).Split(',')[1]);
                                }
                            }
                            if (dieresult >= int.Parse(lastRollRequest.critrangemin))
                            {
                                if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("Prueba 2"); }
                                lastResult["IsMax"] = true;

                                if (characters[RuleSet5EPlugin.Utility.GetCharacterName(victim)].immunity.Contains("critical")) //XJ: (2022/11/29) critical immunity
                                {
                                    criticalImmunity = true;
                                    lastResult["IsMax"] = false;
                                }
                            }
                            //XJ: Allow critical hit according to the instigator's critical range stat.
                        }
                        else
                        {
                            if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("Prueba 3"); }
                            lastResult["IsMax"] = false;
                            lastResult["IsMin"] = false;
                        }
                        //XJ: end if: no critical on Dc attacks.
                        if ((bool)lastResult["IsMax"] == true)
                        {
                            if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("Prueba 4"); }
                            if (criticalImmunity == true)
                            {
                                instigator.SpeakEx(lastRollRequest.name + " " + lastResult["Total"] + " (Critical Immunity)");
                            }
                            else
                            {
                                instigator.SpeakEx(lastRollRequest.name + " " + lastResult["Total"] + " (Critical Hit)");
                            }
                        }
                        else if ((bool)lastResult["IsMin"] == true)
                        {
                            if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("Prueba 5"); }
                            instigator.SpeakEx(lastRollRequest.name + " " + lastResult["Total"] + " (Critical Miss)");
                        }
                        else
                        {
                            //XJ: (2022/10/13)
                            if (dcAttack)
                            {
                                if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("Prueba 6"); }
                                victim.SpeakEx("Save: " + lastResult["Total"] + " VS " + lastRollRequest.roll.Split('/')[1].ToString() + " (" + lastRollRequest.name + ")");
                            }
                            else
                            {
                                instigator.SpeakEx(lastRollRequest.name + " " + lastResult["Total"]);
                            }
                            //XJ: DC attacks Save check, not Attack Roll.
                        }
                        //XJ(2022/10/13) new if, dc attacks:
                        if (dcAttack)
                        {
                            if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("Prueba 7"); }
                            players = "[" + RuleSet5EPlugin.Utility.GetCharacterName(victim) + "]";
                            players = players + "<size=28>" + "Save: " + lastResult["Total"] + " VS " + lastRollRequest.roll.Split('/')[1].ToString() + " (" + lastRollRequest.name + ")" + "\r\n";
                        }
                        else
                        {
                            players = "[" + RuleSet5EPlugin.Utility.GetCharacterName(instigator) + "]";
                            players = players + "<size=28>" + "Attack: " + lastResult["Total"] + " VS AC (" + lastRollRequest.name + ")\r\n";
                        }
                        //XJ end if.

                        owner = players;
                        owner = owner + "<size=16>" + lastResult["Roll"] + " = ";
                        owner = owner + "<size=16>" + lastResult["Expanded"];
                        if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("Prueba 8"); }
                        if ((bool)lastResult["IsMax"] == true)
                        {
                            owner = owner + " (Critical Hit)";
                        }
                        else if (criticalImmunity == true) //XJ (2022/11/27) Crítical immunity
                        {
                            owner = owner + " (Critical Hit) [Critical Immunity]";
                        }
                        else if ((bool)lastResult["IsMin"] == true)
                        {
                            owner = owner + " (Critical Miss)";
                        }
                        gm = owner;
                        
                        if (dcAttack) { chatManager.SendChatMessageEx(players, owner, gm, victim.CreatureId, LocalClient.Id.Value); }                                                
                        else { chatManager.SendChatMessageEx(players, owner, gm, instigator.CreatureId, LocalClient.Id.Value); }                        
                        
                        stepDelay = 1.0f;
                        break;
                    case StateMachineState.attackAttackDefenceCheck:
                        if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("Getting Total from '" + lastResult["Total"] + "'"); }
                        int attack = (int)lastResult["Total"];
                        //XJ: Get CA from Dnd5e isnead victim stat
                        //int ac = (int)(victim.Stat0.Value);
                        int ac = int.Parse(characters[RuleSet5EPlugin.Utility.GetCharacterName(victim)].ac) + (victim_amountACBonusDie != "" ? int.Parse(victim_amountACBonusDie):0);
                        //XJ: Charge CA from dnd5e instead TS stat.
                        //XJ (2022/10/13) if, dc attacks.                         
                        if (dcAttack) { ac = int.Parse(lastRollRequest.roll.Split('/')[0]); }
                        //XJ end if.                        
                        if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("Getting Min from '" + lastResult["IsMin"] + "'"); }
                        //XJ: (2022/10/04) if ((attack < ac) || ((bool)lastResult["IsMin"] == true))  Change by:                        
                        if ((attack < ac & (bool)lastResult["IsMax"] == false & dcAttack == false) || ((bool)lastResult["IsMin"] == true) || (dcAttack == true & attack >= ac))
                        //XJ: to secure a hit when critical
                        {
                            stateMachineState = StateMachineState.attackAttackMissReport;

                            //XJ: 2022/10/18
                            if (dcAttack & lastRollRequest.roll.Contains("/"))
                            {
                                if (lastRollRequest.roll.Split('/')[2].ToUpper() == "HALF") { halfDamage = true; stateMachineState = StateMachineState.attackAttackHitReport; }
                            }
                            //XJ: Allow Half Damage on miss Dc attacks.
                        }
                        else
                        {
                            stateMachineState = StateMachineState.attackAttackHitReport;
                        }
                        stepDelay = 0f;
                        break;
                    case StateMachineState.attackAttackMissReport:
                        stateMachineState = StateMachineState.attackRollCleanup;
                        victim.StartTargetEmote(instigator, missAnimation); //XJ (2022/12/04)  Animation after reaction.
                        //instigator.Attack(CreatureBoardAsset.AttackEmotes.,victim.CreatureId, victim.transform.position,0.2f);
                        if (dcAttack) { victim.Speak("Save!"); } else { victim.SpeakEx("Miss!"); }
                       
                       
                        //XJ:(2022/10/13)  (2022/10/18)  add (secureSuccess miss)
                        if (secureSuccess == true)
                        {
                            players = "[" + RuleSet5EPlugin.Utility.GetCharacterName(victim)+ "]<size=28>Evades attack<size=4>\r\n";
                            gm = players;
                            owner = players;
                            chatManager.SendChatMessageEx(players, owner, gm, victim.CreatureId, LocalClient.Id.Value);
                        }
                        else if (dcAttack)
                        {
                            players = "[" + RuleSet5EPlugin.Utility.GetCharacterName(victim) + "]<size=28>Successfull saving throw<size=4>\r\n";
                            gm = players + "<size=16>" + lastResult["Total"] + " vs DC " + lastRollRequest.roll.Split('/')[0];
                            owner = gm;
                            chatManager.SendChatMessageEx(players, owner, gm, instigator.CreatureId, LocalClient.Id.Value);
                        }
                        else
                        {
                            players = "[" + RuleSet5EPlugin.Utility.GetCharacterName(victim) + "]<size=28>Evades attack<size=4>\r\n";
                            gm = players + "<size=16>" + lastResult["Total"] + " vs AC " + (int.Parse(characters[RuleSet5EPlugin.Utility.GetCharacterName(victim)].ac) + (victim_amountACBonusDie != "" ? int.Parse(victim_amountACBonusDie) : 0));
                            owner = gm;                            
                            chatManager.SendChatMessageEx(players, owner, gm, victim.CreatureId, LocalClient.Id.Value);
                        }
                        

                        if (secureSuccess == false & dcAttack == true & multiTargetAssets.Count != 0)
                        {                     
                            if (multiTargetAssets.Count != MultitargetAssetsIndex) { stateMachineState = StateMachineState.idle; victim.SetGlow(false,UnityEngine.Color.red); RollCleanup(dm, ref stepDelay); StartSequencePre(multiAttackType, multiRoll, instigator.CreatureId, null, null); }
                        }
                        break;
                    case StateMachineState.attackAttackHitReport:
                        stateMachineState = StateMachineState.attackDamageDieCreate;
                        //XJ (2022/12/04) add:
                        //CreatureBoardAsset.AttackEmotes attackEmote = new CreatureBoardAsset.AttackEmotes();                        
                        if (lastRollRequest.info != "")
                        {
                            instigator.StartTargetEmote(victim, lastRollRequest.info);
                        }
                        else
                        {
                            switch (lastRollRequest.type.ToUpper())
                            {
                                //XJ (2022/10/19): Change: // XJ (2023/02/22) Uncommented. All characters can see effect
                                case "MAGIC":
                                    instigator.StartTargetEmote(victim, "TLA_MagicMissileAttack");
                                    break;
                               case "RANGE":
                                case "RANGED":
                                   instigator.StartTargetEmote(victim, "TLA_LaserRed");
                                    break;
                                default:
                                    instigator.StartTargetEmote(victim, "TLA_MeleeAttack");
                                    break;

                                //case "MAGIC":
                                //   // attackEmote = CreatureBoardAsset.AttackEmotes.TLA_MagicMissileAttack;
                                //    break;
                                //case "RANGE":
                                //case "RANGED":
                                //    //attackEmote = CreatureBoardAsset.AttackEmotes.TLA_LaserRed;
                                //    break;
                                //default:
                                //   // attackEmote = CreatureBoardAsset.AttackEmotes.Hit;
                                //    break;
                                    //   XJ: Change effects system according to new "Attack" function(TALESPIRE UPDATE).
                            }
                        }
                        //XJ:(2022/10/17) add two arguments 
                        // instigator.Attack(attackEmote, victim.CreatureId, victim.transform.position, 0.2f);
                        //CreatureManager.AttackCreature(attackEmote, instigator.CreatureId, instigator.transform.position, victim.CreatureId, instigator.transform.position); //XJ:(2022/02/22) all characters can see effect
                        //XJ (2022/12/04)  Animation after reaction  

                        if (dcAttack) { if (halfDamage) { victim.Speak("Save!"); } else { victim.Speak("Fail!"); } } else { victim.SpeakEx("Hit!"); }
                                               
                        
                        //XJ:(2022/10/18)
                        if (secureSuccess)
                        {
                            players = "[" + RuleSet5EPlugin.Utility.GetCharacterName(instigator) + "]<size=28>Hits "+ RuleSet5EPlugin.Utility.GetCharacterName(victim)+ "<size=4>\r\n";
                            gm = players + "<size=16>" + "Automatic Success";
                            owner = gm;
                            chatManager.SendChatMessageEx(players, owner, gm, instigator.CreatureId, LocalClient.Id.Value);
                        }
                        else if (dcAttack)
                        {                            
                            players = "[" + RuleSet5EPlugin.Utility.GetCharacterName(victim) + "]<size=28>Failed saving throw<size=4>\r\n";
                            if (halfDamage) {players = "[" + RuleSet5EPlugin.Utility.GetCharacterName(victim) + "]<size=28>Successfull saving throw<size=4>\r\n"; }
                            gm = players + "<size=16>" + lastResult["Total"] + " vs DC " + lastRollRequest.roll.Split('/')[0];
                            //XJ: 2022/10/18
                            if (halfDamage) { gm = gm + " (On save: half damage)"; }
                            //XJ: Change info message when miss Dc attack with half damage.
                            owner = gm;
                            chatManager.SendChatMessageEx(players, owner, gm, instigator.CreatureId, LocalClient.Id.Value);
                        }
                        else
                        {
                            players = "[" + RuleSet5EPlugin.Utility.GetCharacterName(instigator) + "]<size=28>Hits " + RuleSet5EPlugin.Utility.GetCharacterName(victim)+ "<size=4>\r\n";
                            gm = players + "<size=16>" + lastResult["Total"] + " vs AC " + (int.Parse(characters[RuleSet5EPlugin.Utility.GetCharacterName(victim)].ac) + (victim_amountACBonusDie != "" ? int.Parse(victim_amountACBonusDie) : 0));
                            owner = gm;
                            chatManager.SendChatMessageEx(players, owner, gm, victim.CreatureId, LocalClient.Id.Value);
                        }
                        firstWithDamageBonus = true;
                        tmp = lastRollRequest.link;
                        damages.Clear();
                        //XJ:(2022/10/08) add (2022/10/18) (if (secureSuccess == false) to avoid crash if secureSuccess is true and Lastroll not exist.
                        if (secureSuccess == false) { if ((bool)lastResult["IsMax"] == true) { damageDieMultiplier = float.Parse(lastRollRequest.critmultip); } else { damageDieMultiplier = 1.0f; } }
                        //XJ: Change 2.0f multiplier by critical multiplier attack stat.
                        //XJ: (2022/12/14) Multitarget DC, add to list:
                        if (secureSuccess == false & dcAttack == true & multiTargetAssets.Count != 0)
                        {                            
                            MultiDCAttackData multiDCAttackData = new MultiDCAttackData();
                            multiDCAttackData.mVcitim = victim;                           
                            multiDCAttackData.mHalfDamage = halfDamage;
                            multiDCAttackData.mReactionHalve = reactionHalve;
                            multiDCAttackDataList.Add(multiDCAttackData);                            
                            if (multiTargetAssets.Count != MultitargetAssetsIndex) { stateMachineState = StateMachineState.idle; victim.SetGlow(false, UnityEngine.Color.red); RollCleanup(dm, ref stepDelay); StartSequencePre(multiAttackType, multiRoll, instigator.CreatureId, null, null); }
                        }
                      
                        stepDelay = 1f;                        
                        break;
                    case StateMachineState.attackDamageDieCreate:
                        try
                        {
                            if (tmp != null)
                            {
                                lastRollRequest = tmp;
                                if (rollingSystem == RollMode.automaticDice)
                                {
                                    if (tmp.roll.ToUpper().Contains("D"))
                                    {
                                        if (int.Parse(tmp.roll.Substring(0, tmp.roll.ToUpper().IndexOf("D"))) > 3)
                                        {
                                            //GameObject dolly = GameObject.Find("dolly");
                                            if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Adjusting Dolly And Camera For Large Dice Count"); }
                                            dolly.transform.position = new Vector3(-100f, 4f, -3f);
                                        }
                                        else
                                        {
                                            //GameObject dolly = GameObject.Find("dolly");
                                            if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Adjusting Dolly And Camera For Small Dice Count"); }
                                            dolly.transform.position = new Vector3(-100f, 2f, -1.5f);
                                        }
                                    }
                                }
                                stateMachineState = StateMachineState.attackDamageDieWaitCreate;
                                //XJ: (2022/10/08) Added Code line
                                if (tmp.roll.ToUpper().Contains("D"))
                                {
                                    int Pos = tmp.roll.ToUpper().IndexOf("D");
                                    int sPos = Pos;
                                    while ("0123456789".Contains(tmp.roll.Substring(sPos - 1, 1))) { sPos--; if (sPos == 0) { break; } }
                                    if (sPos > 0)
                                    {
                                        tmp.roll = tmp.roll.Substring(0, sPos) + (int.Parse(tmp.roll.Substring(sPos, Pos - sPos)) * damageDieMultiplier).ToString() + tmp.roll.Substring(Pos, tmp.roll.Length - Pos);
                                    }
                                    else
                                    {
                                        tmp.roll = (int.Parse(tmp.roll.Substring(sPos, Pos - sPos)) * damageDieMultiplier).ToString() + tmp.roll.Substring(Pos, tmp.roll.Length - Pos);
                                    }
                                }
                                //XJ: double damage dies on critical hit.                                
                                if (useDamageBonusDie & firstWithDamageBonus & tmp.roll != "0"){  RollCreate(dt, $"talespire://dice/" + SafeForProtocolName(tmp.name) + ":" + tmp.roll + ("+-".Contains(amountDamageBonusDie.Substring(0,1)) ? amountDamageBonusDie : "+" + amountDamageBonusDie), ref stepDelay); firstWithDamageBonus= false; }
                                else { RollCreate(dt, $"talespire://dice/" + SafeForProtocolName(tmp.name) + ":" + tmp.roll, ref stepDelay); }
                                
                            }
                            else
                            {
                                stateMachineState = StateMachineState.attackDamageDieDamageReport;
                            }
                        }
                        catch (Exception e)
                        {
                            stateMachineState = StateMachineState.attackRollCleanup;
                            Debug.LogWarning("RuleSet 5E Plugin:!Critical error:[ " + e.Message + " ]!");
                        }
                        break;
                    case StateMachineState.attackDamageDieWaitCreate:
                        // Callback propagates to next phase
                        break;
                    case StateMachineState.attackDamageDieRollExecute:
                        stateMachineState = StateMachineState.attackDamageDieWaitRoll;
                        dt.SpawnAt(Vector3.zero, Vector3.zero);
                        RollExecute(dm, ref stepDelay);
                        if (rollingSystem.ToString().ToUpper().Contains("MANUAL")) { StartCoroutine(DisplayMessage("Please Roll Provided Die Or Dice To Continue...", 3f)); }
                        break;
                    case StateMachineState.attackDamageDieWaitRoll:
                        // Callback propagates to next phase
                        break;
                    case StateMachineState.attackDamageDieRollReport:
                        RollId.TryParse(lastRollId.ToString(), out  idLastroll);
                        dm.ClearDiceRoll(idLastroll);
                        stateMachineState = StateMachineState.attackDamageDieCreate;
                        //XJ (2022/10/08) add
                        //if (loadedRollRequest == null)
                        //{
                        //    int Pos = tmp.roll.ToUpper().IndexOf("D");
                        //    int sPos = Pos;                           
                        //    if (sPos > 1)
                        //    {
                        //        while ("0123456789".Contains(tmp.roll.Substring(sPos - 1, 1))) { sPos--; if (sPos == 0) { break; } }
                        //        if ("+".Contains(tmp.roll.Substring(sPos - 1, 1)))
                        //        {
                        //            lastResult["Total"] = int.Parse(lastResult["Total"].ToString()) + int.Parse(tmp.roll.Substring(0, sPos - 1));
                        //        }
                        //        else
                        //        {
                        //            lastResult["Total"] = int.Parse(lastResult["Total"].ToString()) - int.Parse(tmp.roll.Substring(0, sPos - 1));
                        //        }
                        //        lastResult["Expanded"] = tmp.roll.Substring(0, sPos) + lastResult["Expanded"];
                        //    }
                        //}
                        ////XJ:add modifier previous of damage rolls.
                        if ((int)lastResult["Total"] < 0) { lastResult["Total"] = 0; }
                        if (int.Parse(lastResult["Total"].ToString()) == 0)
                        {
                            instigator.SpeakEx(lastRollRequest.name + ":\r\n" + "No damage");
                            damages.Add(new Damage(lastRollRequest.name, lastRollRequest.type, lastResult["Roll"].ToString(), lastResult["Expanded"].ToString(), (int)lastResult["Total"]));
                        }
                        else
                        {                            
                            if (lastRollRequest.roll != "")
                            {
                                instigator.SpeakEx(lastRollRequest.name + ":\r\n" + lastResult["Total"] + " " + lastRollRequest.type);
                                if (useDamageBonusDie & damages.Count==0)
                                {
                                    //damages.Add(new Damage(lastRollRequest.name, lastRollRequest.type, lastRollRequest.roll + ("+-".Contains(amountDamageBonusDie.Substring(0,1))? amountDamageBonusDie:"+"+ amountDamageBonusDie), lastResult["Expanded"].ToString(), (int)lastResult["Total"]));
                                    damages.Add(new Damage(lastRollRequest.name, lastRollRequest.type, lastResult["Roll"].ToString(), lastResult["Expanded"].ToString(), (int)lastResult["Total"]));
                                } 
                                else
                                {
                                    damages.Add(new Damage(lastRollRequest.name, lastRollRequest.type, lastResult["Roll"].ToString(), lastResult["Expanded"].ToString(), (int)lastResult["Total"]));
                                }
                            }
                            else
                            {
                                instigator.SpeakEx(lastRollRequest.name + ":\r\n" + lastRollRequest.type);
                                damages.Add(new Damage(lastRollRequest.name, lastRollRequest.type, lastResult["Roll"].ToString(), lastResult["Expanded"].ToString(), (int)lastResult["Total"]));
                            }
                        }
                        stepDelay = 1.0f;
                        tmp = tmp.link;
                        break;
                    case StateMachineState.attackDamageDieDamageReport:
                        stateMachineState = StateMachineState.attackDamageDieDamageTake;
                        total = 0;
                        info = "";
                        foreach (Damage dmg in damages)
                        {
    
                            total = total + dmg.total;
                            //XJ(2022/10/17)
                            // info = info + dmg.total + " " + dmg.type + " (" + dmg.name + ") " + dmg.roll + " = " + dmg.expansion.Replace("+-", "-") + "\r\n";
                            if (dmg.roll != "0") {info = info + dmg.total + " " + dmg.type + " (" + dmg.name + ") " + dmg.roll + " = " + dmg.expansion + "\r\n";}
                            //XJ: Add .Replace("+-", "-")  to show "-" on chat instead "+-".

                        }
                        //XJ:(2022/10/27) Show "No damage" if attack dont have damage.
                        if (total == 0)
                        {
                            //info = lastRollRequest.roll.states.conditions  //XJ:to add states in the future
                            //if (lastRollRequest.roll == "0") { info = ""; }
                            players = "[" + Utility.GetCharacterName(instigator) + "]<size=28>Attack without damage <size=16>";
                            owner = players + "\r\n" + info;
                            gm = owner;
                            chatManager.SendChatMessageEx(players, owner, gm, instigator.CreatureId, LocalClient.Id.Value);
                            stateMachineState = StateMachineState.attackRollCleanup;
                        }
                        else
                        {
                            if (damages.Count > 1)
                            {
                                yield return new WaitForSeconds(0.5f * processSpeed);
                                instigator.SpeakEx("Total Damage: " + total);
                            }
                            players = "[" + Utility.GetCharacterName(instigator) + "]<size=28>Attack damage: " + total + "<size=16>";//XJ(2022/10/26) It only takes into account the last die.  + (((bool)lastResult["IsMax"] == true) ? " (Critical Hit)" : "");
                            owner = players + "\r\n" + info;
                            gm = owner;
                            chatManager.SendChatMessageEx(players, owner, gm, instigator.CreatureId, LocalClient.Id.Value);
                        }
                        break;
                    case StateMachineState.attackDamageDieDamageTake:
                        stateMachineState = StateMachineState.attackRollCleanup;
                        //XJ: (2022/12/14) Multitarget DC Code.
                        if (secureSuccess == true || dcAttack == false || multiTargetAssets.Count == 0)
                        {                   
                            MultiDCAttackData multiDCAttackData = new MultiDCAttackData();
                            multiDCAttackData.mVcitim = victim;
                            multiDCAttackData.mHalfDamage = halfDamage;
                            multiDCAttackData.mReactionHalve = reactionHalve;
                            multiDCAttackDataList.Add(multiDCAttackData);                             
                        }                       
                        
                        foreach (MultiDCAttackData tempmultiDcattackData in multiDCAttackDataList)
                        {                            
                            victim = tempmultiDcattackData.mVcitim;
                            halfDamage = tempmultiDcattackData.mHalfDamage;
                            reactionHalve = tempmultiDcattackData.mReactionHalve;
                            
                            bool fullDamage = true;
                            int adjustedDamage = 0;
                            string damageList = "";
                            string damageListVictim = "";

                            foreach (Damage dmg in damages)
                            {
                                int tempTotal = dmg.total;
                                string tempExpansion = dmg.expansion;

                                if (halfDamage) { tempTotal = tempTotal / 2; tempExpansion = tempExpansion + " (Miss: Half Damage)"; }
                                //XJ:(2022/10/08) add:
                                if (reactionHalve == true) { tempTotal = (int)(tempTotal / 2); fullDamage = false; }
                                //XJ:To implement Halve damage
                                if (characters.ContainsKey(RuleSet5EPlugin.Utility.GetCharacterName(victim)))
                                {
                                    foreach (string immunity in characters[RuleSet5EPlugin.Utility.GetCharacterName(victim)].immunity)
                                    {
                                        if (dmg.type == immunity) { tempTotal = 0; dmg.type = dmg.type + ":Immunity"; fullDamage = false; }
                                    }
                                    foreach (string resisitance in characters[RuleSet5EPlugin.Utility.GetCharacterName(victim)].resistance)
                                    {
                                        if (dmg.type == resisitance) { tempTotal = (int)(tempTotal / 2); dmg.type = dmg.type + ":Resistance"; fullDamage = false; }
                                    }
                                    //XJ:(2022/10/09) add                                
                                    foreach (string vulnerability in characters[RuleSet5EPlugin.Utility.GetCharacterName(victim)].vulnerability)
                                    {
                                        if (dmg.type == vulnerability) { tempTotal = (int)(tempTotal * 2); dmg.type = dmg.type + ":Vulnerability"; fullDamage = true; }
                                    }
                                    //XJ: (2022/10/09) allow vulnerability
                                }
                                adjustedDamage = adjustedDamage + tempTotal;
                                if (reactionHalve) { dmg.type = dmg.type + " [Halve]"; }
                                damageList = damageList + tempTotal + " " + dmg.type + " (" + dmg.name + ") " + dmg.roll + " = " + tempExpansion + "\r\n";
                                damageListVictim = damageListVictim + tempTotal + " " + dmg.type + " (" + dmg.name + ") " + "\r\n";
                            }
                            //XJ change each{} / if {} order. To allow take damage on victim if dnd5e file is not load.
                            //XJ:(2022/10/08)
                            reactionHalve = false;
                            hp = Math.Max((int)(victim.Hp.Value - adjustedDamage), 0);
                            hpMax = (int)victim.Hp.Max;
                            CreatureManager.SetCreatureStatByIndex(victim.CreatureId, new CreatureStat(hp, hpMax), -1);
                            damageList = "<size=24>Damage: " + adjustedDamage + "<size=16>\r\n" + damageList;
                            if (adjustedDamage == 0 & fullDamage == true)
                            {
                                victim.SpeakEx("Your attempts are futile!");
                                damageList = "<size=16>\r\n" + damageList;
                                players = "[" + RuleSet5EPlugin.Utility.GetCharacterName(victim) + "]<size=28>Takes no damage<size=4>\r\n";
                                owner = players + "<size=16>";
                                gm = players + "<size=16>";
                            }
                            else if (!fullDamage)
                            {
                                if (hp > 0)
                                {
                                    victim.SpeakEx("I resist your efforts!");
                                }
                                else
                                {
                                    victim.SpeakEx("I resist your efforts\r\nbut I am slain!");
                                    if (deadAnimation.ToUpper() != "REMOVE")
                                    {
                                        if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Playing Death Animation '" + deadAnimation + "'"); }
                                        victim.StartTargetEmote(instigator, deadAnimation);
                                    }
                                    else
                                    {
                                        yield return new WaitForSeconds(1f);
                                        if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Requesting Mini Remove"); }
                                        victim.RequestDelete();
                                    }
                                }
                                if (adjustedDamage == 0)
                                {
                                    players = "[" + RuleSet5EPlugin.Utility.GetCharacterName(victim) + "]<size=28>Takes no damage<size=4>\r\n";
                                }
                                else
                                {
                                    players = "[" + RuleSet5EPlugin.Utility.GetCharacterName(victim) + "]<size=28>Takes some damage<size=4>\r\n";
                                }
                                owner = players + "<size=16>" + damageListVictim;
                                gm = players + "<size=16>" + damageList;
                            }
                            else
                            {
                                if (hp > 0)
                                {
                                    victim.SpeakEx("Ouch!");
                                }
                                else
                                {
                                    victim.SpeakEx("I am slain!");
                                    if (deadAnimation.ToUpper() != "REMOVE")
                                    {
                                        if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Playing Death Animation '" + deadAnimation + "'"); }
                                        victim.StartTargetEmote(instigator, deadAnimation);
                                    }
                                    else
                                    {
                                        yield return new WaitForSeconds(1f);
                                        if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Requesting Mini Remove"); }
                                        victim.RequestDelete();
                                    }
                                }                                
                                players = "[" + RuleSet5EPlugin.Utility.GetCharacterName(victim) + "]<size=28>Takes the damage<size=4>\r\n";
                                owner = players + "<size=16>" + damageListVictim;
                                gm = players + "<size=16>" + damageList;
                            }
                            gm = gm + "\r\nRemaining HP: " + hp + " of " + hpMax;
                            owner = owner + "\r\nRemaining HP: " + hp + " of " + hpMax;

                            CreatureManager.SetCreatureStatByIndex(victim.CreatureId, new CreatureStat(hp, hpMax), -1);
                            chatManager.SendChatMessageEx(players, owner, gm, victim.CreatureId, LocalClient.Id.Value);
                            if (halfDamage) { halfDamage= false; }  
                        }

                        break;
                    case StateMachineState.attackRollCleanup:
                        stateMachineState = StateMachineState.idle;
                        RollCleanup(dm, ref stepDelay);
                        multiDCAttackDataList.Clear();
                        if (victim != null) { victim.SetGlow(false, UnityEngine.Color.red); }
                        if (multiTargetAssets.Count != 0) { StartSequencePre(multiAttackType, multiRoll, instigator.CreatureId, null, null); };
                        break;
                    // ******************
                    // * Skill Sequence *
                    // *****************
                    case StateMachineState.skillRollSetup:
                        stateMachineState = StateMachineState.skillRollDieCreate;
                        RollSetup(dm, ref stepDelay);
                        if (rollingSystem == RollMode.automaticDice) { dolly.transform.position = new Vector3(-100f, 2f, -1.5f); }
                        damageDieMultiplier = 1.0f;
                        break;
                    case StateMachineState.skillRollDieCreate:
                        stateMachineState = StateMachineState.skillRollDieWaitCreate;
                        RollCreate(dt, $"talespire://dice/" + SafeForProtocolName(lastRollRequest.name) + ":" + lastRollRequest.roll, ref stepDelay);
                        if (rollingSystem.ToString().ToUpper().Contains("MANUAL")) { StartCoroutine(DisplayMessage("Please Roll Provided Die Or Dice To Continue...", 3f)); }
                        break;
                    case StateMachineState.skillRollDieWaitCreate:
                        // Callback propagates to next phase
                        break;
                    case StateMachineState.skillRollDieRollExecute:
                        stateMachineState = StateMachineState.skillRollDieWaitRoll;
                        RollExecute(dm, ref stepDelay);
                        break;
                    case StateMachineState.skillRollDieWaitRoll:
                        // Callback propagates to next phase
                        break;
                    case StateMachineState.skillBonusRollDieCreate:
                        stateMachineState = StateMachineState.skillRollDieRollReport;

                        if (useSkillBonusDie & amountSkillBonusDie != "")
                        {
                            stateMachineState = StateMachineState.skillBonusRollDieWaitCreate;
                            hold = lastResult;
                            //XJ: (2022/10/07) Added new codeline: 
                            RollId.TryParse(lastRollId.ToString(), out  idLastroll);
                            dm.ClearDiceRoll(idLastroll);
                            //XJ: To clear previus rolls.
                            RollCreate(dt, $"talespire://dice/" + SafeForProtocolName("Bonus Die") + ":" + amountSkillBonusDie, ref stepDelay);
                            if (rollingSystem.ToString().ToUpper().Contains("MANUAL")) { StartCoroutine(DisplayMessage("Please Roll Provided Die Or Dice To Continue...", 3f)); }
                        }
                        break;
                    case StateMachineState.skillBonusRollDieWaitCreate:
                        // Callback propagates to next phase
                        break;
                    case StateMachineState.skillBonusRollDieRollExecute:
                        stateMachineState = StateMachineState.skillBonusRollDieWaitRoll;
                        RollExecute(dm, ref stepDelay);
                        break;
                    case StateMachineState.skillBonusRollDieWaitRoll:
                        // Callback propagates to next phase
                        break;
                    case StateMachineState.skillRollDieRollReport:
                        stateMachineState = StateMachineState.skillRollCleanup;
                        RollId.TryParse(lastRollId.ToString(), out RollId idLastroll3);
                        dm.ClearDiceRoll(idLastroll3);
                        if (useSkillBonusDie & amountSkillBonusDie != "")
                        {

                            hold["Total"] = ((int)hold["Total"] + (int)lastResult["Total"]);                           
                            if ( "-".Contains(lastResult["Expanded"].ToString().Substring(0, 1)))// & loadedRollRequest == null)
                            {
                                //hold["Expanded"] = hold["Expanded"] + "-" + lastResult["Expanded"].ToString();
                                hold["Expanded"] = hold["Expanded"] + lastResult["Expanded"].ToString();
                            }
                            else
                            {
                                hold["Expanded"] = hold["Expanded"] + "+" + lastResult["Expanded"].ToString();
                            }
                            // hold["Expanded"] = hold["Expanded"] + (("+-".Contains(lastResult["Expanded"].ToString().Substring(0, 1))) ? lastResult["Expanded"].ToString().Replace("+-", "-") : "+" + lastResult["Expanded"].ToString().Replace("+-", "-"));
                            //}

                            //XJ: To allow negative roll bonus dice skill and solve chat visual problems with expanded string in this case (2022/10/07): when rolltype not is automaticGenerator.

                            hold["Roll"] = hold["Roll"] + (("+-".Contains(lastResult["Roll"].ToString().Substring(0, 1))) ? lastResult["Roll"].ToString() : "+" + lastResult["Roll"].ToString());                            
                            lastResult = hold;
                        }
                        if (lastRollRequest.roll != "")
                        {
                            players = "[" + RuleSet5EPlugin.Utility.GetCharacterName(instigator) + "]<size=28>" + lastRollRequest.name + ": " + lastResult["Total"] + "\r\n";
                        }
                        else
                        {
                            players = "[" + RuleSet5EPlugin.Utility.GetCharacterName(instigator) + "]<size=28>" + lastRollRequest.name + "\r\n";
                        }
                        owner = players;
                        owner = owner + "<size=16>" + lastResult["Roll"] + " = ";
                        owner = owner + "<size=16>" + lastResult["Expanded"];
                        if (lastRollRequest.roll != "")
                        {
                            if ((bool)lastResult["IsMax"] == true)
                            {
                                owner = owner + " (Max)";
                            }
                            else if ((bool)lastResult["IsMin"] == true)
                            {
                                owner = owner + " (Min)";
                            }
                        }
                        gm = owner;
                        if (lastRollRequest.type.ToUpper().Contains("SECRET"))
                        {
                            players = null;
                        }
                        else if (lastRollRequest.type.ToUpper().Contains("PRIVATE"))
                        {
                            instigator.SpeakEx(lastRollRequest.name);
                            players = "[" + RuleSet5EPlugin.Utility.GetCharacterName(instigator) + "]<size=28>" + lastRollRequest.name + "\r\n";
                        }
                        else // if (lastRollRequest.type.ToUpper().Contains("PUBLIC"))
                        {
                            instigator.SpeakEx(lastRollRequest.name + " " + lastResult["Total"]);
                        }
                        if (lastRollRequest.type.ToUpper().Contains("GM"))
                        {
                            players = null;
                            owner = null;
                        }                        
                        chatManager.SendChatMessageEx(players, owner, gm, instigator.CreatureId, LocalClient.Id.Value);
                        stepDelay = 1.0f;
                        break;
                    case StateMachineState.skillRollCleanup:
                        stateMachineState = StateMachineState.skillRollMore;
                        RollCleanup(dm, ref stepDelay);
                        break;
                    case StateMachineState.skillRollMore:
                        stateMachineState = StateMachineState.idle;
                        victim.SetGlow(false,UnityEngine.Color.green);
                        if (lastRollRequest.link != null)
                        {
                            lastRollRequest = lastRollRequest.link;
                            stateMachineState = StateMachineState.skillRollSetup;
                        }
                        break;
                    // ********************
                    // * Healing Sequence *
                    // ********************
                    case StateMachineState.healingRollStart:
                        RollSetup(dm, ref stepDelay);
                        if (rollingSystem == RollMode.automaticDice) { dolly.transform.position = new Vector3(-100f, 2f, -1.5f); }
                        damageDieMultiplier = 1.0f;
                        tmp = lastRollRequest;
                        firstWithDamageBonus = true;
                        damages.Clear();
                        stepDelay = 1f;
                        stateMachineState = StateMachineState.healingRollDieCreate;
                        break;
                    case StateMachineState.healingRollDieCreate:
                        if (tmp != null)
                        {
                            lastRollRequest = tmp;
                            if (rollingSystem == RollMode.automaticDice)
                            {
                                if (tmp.roll.ToUpper().Contains("D"))
                                {
                                    if (int.Parse(tmp.roll.Substring(0, tmp.roll.ToUpper().IndexOf("D"))) > 3)
                                    {
                                        //GameObject dolly = GameObject.Find("dolly");
                                        if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Adjusting Dolly And Camera For Large Dice Count"); }
                                        dolly.transform.position = new Vector3(-100f, 4f, -3f);
                                    }
                                    else
                                    {
                                        //GameObject dolly = GameObject.Find("dolly");
                                        if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Adjusting Dolly And Camera For Small Dice Count"); }
                                        dolly.transform.position = new Vector3(-100f, 2f, -1.5f);
                                    }
                                }
                            }
                            stateMachineState = StateMachineState.healingRollDieWaitCreate;
                            if (useDamageBonusDie & firstWithDamageBonus) { if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: First Heal Link"); }; RollCreate(dt, $"talespire://dice/" + SafeForProtocolName(tmp.name) + ":" + tmp.roll + ("+-".Contains(amountDamageBonusDie.Substring(0, 1)) ? amountDamageBonusDie : "+" + amountDamageBonusDie), ref stepDelay); firstWithDamageBonus = false; }
                            else { RollCreate(dt, $"talespire://dice/" + SafeForProtocolName(tmp.name) + ":" + tmp.roll, ref stepDelay); }
                        }
                        else
                        {
                            stateMachineState = StateMachineState.healingRollDieValueReport;
                        }
                        break;
                    case StateMachineState.healingRollDieWaitCreate:
                        // Callback propagates to next phase
                        break;
                    case StateMachineState.healingRollDieRollExecute:
                        stateMachineState = StateMachineState.healingRollDieWaitRoll;
                        dt.SpawnAt(Vector3.zero, Vector3.zero);
                        RollExecute(dm, ref stepDelay);
                        if (rollingSystem.ToString().ToUpper().Contains("MANUAL")) { StartCoroutine(DisplayMessage("Please Roll Provided Die Or Dice To Continue...", 3f)); }
                        break;
                    case StateMachineState.healingRollDieWaitRoll:
                        // Callback propagates to next phase
                        break;
                    case StateMachineState.healingRollDieRollReport:
                        RollId.TryParse(lastRollId.ToString(), out  idLastroll);
                        dm.ClearDiceRoll(idLastroll);




                        stateMachineState = StateMachineState.healingRollDieCreate;
                        if (lastRollRequest.roll != "")
                        {
                            instigator.SpeakEx(lastRollRequest.name + ":\r\n" + lastResult["Total"]);
                            if (useDamageBonusDie & damages.Count == 0)
                            {
                                damages.Add(new Damage(lastRollRequest.name, lastRollRequest.type, lastRollRequest.roll + ("+-".Contains(amountDamageBonusDie.Substring(0, 1)) ? amountDamageBonusDie : "+" + amountDamageBonusDie), lastResult["Expanded"].ToString(), (int)lastResult["Total"]));
                            }
                            else
                            {
                                damages.Add(new Damage(lastRollRequest.name, lastRollRequest.type, lastResult["Roll"].ToString(), lastResult["Expanded"].ToString(), (int)lastResult["Total"]));
                            }                           
                        }
                        else
                        {
                            instigator.SpeakEx(lastRollRequest.name + ":\r\n" + lastRollRequest.type);
                            damages.Add(new Damage(lastRollRequest.name, lastRollRequest.type, lastResult["Roll"].ToString(), lastResult["Expanded"].ToString(), (int)lastResult["Total"]));
                        }
                        stepDelay = 1.0f;
                        tmp = tmp.link;
                        break;
                    case StateMachineState.healingRollDieValueReport:
                        stateMachineState = StateMachineState.healingRollDieValueTake;
                        total = 0;
                        info = "";
                        foreach (Damage dmg in damages)
                        {
                            total = total + dmg.total;
                            info = info + dmg.total + " " + dmg.type + " (" + dmg.name + ") " + dmg.roll + " = " + dmg.expansion + "\r\n";
                        }
                        //XJ:(2022/10/17) change:
                        players = "[" + Utility.GetCharacterName(instigator) + "]<size=28>Heal " + Utility.GetCharacterName(victim) + " " + total + " hp<size=16>";
                        //XJ: show instigator  and victim names.
                        owner = players + "\r\n" + info;
                        gm = owner;
                        if (damages.Count > 1) { instigator.SpeakEx("Total Healing " + total); }
                        chatManager.SendChatMessageEx(players, owner, gm, instigator.CreatureId, LocalClient.Id.Value);
                        break;
                    case StateMachineState.healingRollDieValueTake:
                        stateMachineState = StateMachineState.attackRollCleanup;
                        int adjustedHealing = 0;
                        string healingList = "";
                        if (characters.ContainsKey(RuleSet5EPlugin.Utility.GetCharacterName(victim)))
                        {
                            foreach (Damage dmg in damages)
                            {
                                adjustedHealing = adjustedHealing + dmg.total;
                                healingList = healingList + dmg.total + " " + dmg.type + " (" + dmg.name + ") " + dmg.roll + " = " + dmg.expansion + "\r\n";
                            }
                        }
                        hp = Math.Min((int)(victim.Hp.Value + adjustedHealing), (int)victim.Hp.Max);
                        hpMax = (int)victim.Hp.Max;
                        CreatureManager.SetCreatureStatByIndex(victim.CreatureId, new CreatureStat(hp, hpMax), -1);
                        healingList = "<size=28>Healing: " + adjustedHealing + "<size=16>\r\n" + healingList;
                        //XJ:2022/10/17
                        players = "[" + Utility.GetCharacterName(victim) + "]<size=28>Regain " + adjustedHealing + " hp<size=16>";
                        owner = players + "\r\nCurrent HP: " + hp + " of " + hpMax; ;
                        gm = players;
                        //XJ: To avoid show the player name in chat for players and owner.  
                        gm = gm + "\r\nCurrent HP: " + hp + " of " + hpMax;

                      
                        chatManager.SendChatMessageEx(null, owner, gm, victim.CreatureId, LocalClient.Id.Value);
          
                        break;
                    case StateMachineState.healingRollCleanup:
                        stateMachineState = StateMachineState.idle;
                        victim.SetGlow(false, UnityEngine.Color.green);
                        RollCleanup(dm, ref stepDelay);
                        if (multiTargetAssets.Count != 0) { StartSequencePre(multiAttackType, multiRoll, instigator.CreatureId, null, null); };
                        break;
                }
                yield return new WaitForSeconds(stepDelay * processSpeed);
            }
        }

        //XJ:

      
        public void CustomBColor(CreatureBoardAsset sujeto,int hp , int hpMax)
        {          
            try
            {
                if (changeBaseColors)
                {
                    if (changeBaseColors & characters.ContainsKey(RuleSet5EPlugin.Utility.GetCharacterName(sujeto)))
                    {
                        //Debug.Log("RuleSet 5E Plugin:CustomBColor: " + sujeto.Name.ToString() + "NPC: "+npcColors[0] +"|"+ npcColors[1] + "|" + npcColors[2] + " PC:" + pcColors[0] + "|" + pcColors[1] + "|" + pcColors[2]);
                        if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: CustomBColor: " + sujeto.Name.ToString()); }
                        if (npcColors.Length == 3 & pcColors.Length == 3)
                        {
                            if (characters.ContainsKey(Utility.GetCharacterName(sujeto.Name)))
                            {
                                if (characters[Utility.GetCharacterName(sujeto)].NPC == true)
                                {
                                    if (hp <= hpMax / 2) { CreatureManager.SetBaseColorIndex(sujeto.CreatureId, new CreatureColorIndex(ushort.Parse(npcColors[2]))); }
                                    else if (hp < hpMax) { CreatureManager.SetBaseColorIndex(sujeto.CreatureId, new CreatureColorIndex(ushort.Parse(npcColors[1]))); }
                                    else { CreatureManager.SetBaseColorIndex(sujeto.CreatureId, new CreatureColorIndex(ushort.Parse(npcColors[0]))); }
                                }
                                else
                                {
                                    if (hp <= hpMax / 2) { CreatureManager.SetBaseColorIndex(sujeto.CreatureId, new CreatureColorIndex(ushort.Parse(pcColors[2]))); }
                                    else if (hp < hpMax) { CreatureManager.SetBaseColorIndex(sujeto.CreatureId, new CreatureColorIndex(ushort.Parse(pcColors[1]))); }
                                    else { CreatureManager.SetBaseColorIndex(sujeto.CreatureId, new CreatureColorIndex(ushort.Parse(pcColors[0]))); }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("RuleSet 5E Plugin:!Error CustomBColor: " + e.ToString());
            }
        }
        //XJ: Change color function.

        public void RollSetup(DiceManager dm, ref float stepDelay)
        {
            switch (rollingSystem)
            {
                case RollMode.manual:
                    break;
                case RollMode.manual_side:
                    Utility.DisableProcessing(true);
                    saveCamera = new Existence(Camera.main.transform.position, Camera.main.transform.rotation.eulerAngles);
                    // new Existance(new Vector3(-100, 12, -12), new Vector3(45, 0, 0)).Apply(Camera.main.transform);
                    //diceSideExistance.Apply(Camera.main.transform);
                    break;
                case RollMode.automaticDice:
                    if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Creating Dolly And Camera"); }
                    dolly = new GameObject();
                    dolly.name = "dolly";
                    camera = dolly.AddComponent<Camera>();
                   // camera.rect = new Rect(0.005f, 0.20f, 0.20f, 0.25f);
                    dolly.transform.position = diceSideExistance.position; // new Vector3(-100f, 2f, -1.5f);
                    camera.transform.rotation = Quaternion.Euler(diceSideExistance.rotation); // Quaternion.Euler(new Vector3(55, 0, 0));
                    camera.targetTexture = auxCameraTexture;
                    stepDelay = 0.1f;
                    break;
                case RollMode.automaticGenerator:
                    stepDelay = 0.0f;
                    break;
            }
        }

        public void RollCreate(UIDiceTray dt, string old_formula, ref float stepDelay)
        {
            if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("formula before " + old_formula); }
            //formula = formula.Replace("talespire://dice/","!"); //XJ(2022/11/30 Adapted to the new dice system without using the relay
            old_formula = old_formula.Replace("talespire://dice/", "talespire://dice/XRuleset5e");
            string formula=old_formula.Substring(0,old_formula.LastIndexOf(":")+1);
            int totalmodifier= 0;
            old_formula = old_formula.Replace("+", "|+").Replace("-", "|-");
            string formulaneg = String.Empty;
            
            foreach (string s_value in old_formula.Substring(old_formula.LastIndexOf(":")+1).Replace(" ", "").Split('|'))
            {
                if (s_value.ToUpper().Contains("D"))
                {
                    //XJ (26/06/2023) talespire update.
                    if (s_value.Substring(0, 1) == "-") 
                    {
                        formulaneg = formulaneg +  s_value;                    
                    }
                    else
                    {
                        if (rollingSystem != RollMode.automaticGenerator & s_value.ToUpper().Contains("1D20") & ((dcAttack & (victim_totalAdv | victim_totalDis)) | (!dcAttack & (totalAdv | totalDis)))) { formula = formula + s_value.ToUpper().Replace("1D20", "2D20"); }
                        else { formula = formula + s_value; }
                    }
                }
                else if (s_value !="")
                {                    
                    totalmodifier = "-".Contains(s_value.Substring(0, 1)) ? totalmodifier - int.Parse(s_value.Substring(1)) : totalmodifier + int.Parse(s_value.Replace("+",""));                
                }                               
            }
            //XJ 2023/06/27 Solver temporal TS bug roll resolve:  
            //  formula = formula + (totalmodifier < 0 ? totalmodifier.ToString() : "+" + totalmodifier.ToString()) + formulaneg;
            
            if (formula == old_formula.Substring(0, old_formula.LastIndexOf(":") + 1))
            {
                formula = formula + formulaneg + (totalmodifier < 0 ? totalmodifier.ToString() : "+" + totalmodifier.ToString());
            }
            else 
            { 
                formula = formula + (totalmodifier < 0 ? totalmodifier.ToString() : "+" + totalmodifier.ToString()) + formulaneg; 
            
            }
            if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("formula after " + formula); }
            RollMode mode = rollingSystem;
            if (!formula.ToUpper().Substring(formula.LastIndexOf(":") + 1).Contains("D"))
            {
                if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("Roll Create Diversion Due To Lack Of Dice In Formula: " + formula.ToUpper()); }
                mode = RollMode.automaticGenerator;
            }
            switch (mode)
            {
                case RollMode.manual:
                    dt.SpawnAt(new Vector3(instigator.transform.position.x + 1.0f, instigator.transform.position.y + 2, instigator.transform.position.z + 1.0f), Vector3.zero);
                    LocalConnectionManager.ProcessTaleSpireUrl(formula); //XJ: (To not use the Relay)
                    //System.Diagnostics.Process.Start(formula).WaitForExit();
                    break;
                case RollMode.manual_side:
                case RollMode.automaticDice:
                    Vector3 spawnposition = new Vector3(diceSideExistance.position.x, diceSideExistance.position.y + ((rollingSystem == RollMode.automaticDice) ? 5 : 1), diceSideExistance.position.z);
                    dt.SpawnAt(spawnposition, Vector3.zero);
                    if (rollingSystem == RollMode.manual_side)
                    {
                        CameraController.MoveToPosition(spawnposition, false);
                        CameraController.LookAtTarget(spawnposition);
                    }
                    LocalConnectionManager.ProcessTaleSpireUrl(formula); //XJ: (To not use the Relay)
                    // System.Diagnostics.Process.Start(formula).WaitForExit(); 
                    break;
                case RollMode.automaticGenerator:
                    formula = formula.Substring("talespire://dice/XRuleset5e".Length);
                    loadedRollRequest = new Roll()
                    {
                        name = formula.Substring(0, formula.LastIndexOf(":")),
                        roll = formula.Substring(formula.LastIndexOf(":") + 1)
                    };
                    NewDiceSet(-2);
                    break;
            }
        }

        public void RollExecute(DiceManager dm, ref float stepDelay)
        {
            stepDelay = 0.0f;
            RollMode mode = rollingSystem;
            if (loadedRollRequest != null)
            {
                if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("Roll Execute Diversion Due To Load Roll"); }
                mode = RollMode.automaticGenerator;
            }
            switch (mode)
            {
                case RollMode.manual:
                case RollMode.manual_side:
                    // Do Nothing - Let player roll manually
                    break;
                case RollMode.automaticDice:
                    RollId.TryParse(lastRollId.ToString(), out RollId idLastroll);
                    Vector3 spawnposition = new Vector3(diceSideExistance.position.x, diceSideExistance.position.y + ((rollingSystem == RollMode.automaticDice) ? 5 : 1), diceSideExistance.position.z);
                    //dm.GatherDice(spawnposition, idLastroll);                             
                    dm.ThrowDice(idLastroll, new Unity.Mathematics.float3(0f, 1f, 0f));                   
                          
                    break;
                case RollMode.automaticGenerator:
                    ResultDiceSet(ResolveRoll(loadedRollRequest.roll));                    
                    break;
            }
        }

        public void RollCleanup(DiceManager dm, ref float stepDelay)
        {
            switch (rollingSystem)
            {
                case RollMode.manual:
                    break;
                case RollMode.manual_side:
                    Utility.DisableProcessing(false);
                    //saveCamera.Apply(Camera.main.transform);
                    CameraController.MoveToPosition(saveCamera.position,false);
                    CameraController.LookAtTarget(instigator.TargetPosition);
                    
                    saveCamera = null;
                    break;
                case RollMode.automaticDice:
                    //GameObject dolly = GameObject.Find("dolly");
                    GameObject.Destroy(dolly);
                    break;
                case RollMode.automaticGenerator:
                    break;
            }
            loadedRollRequest = null;
            SyncDisNormAdv();
        }

        public void NewDiceSet(long rollId)
        {
            switch (stateMachineState)
            {
                case StateMachineState.attackAttackDieWaitCreate:
                case StateMachineState.attackAttackBonusDieWaitCreate:
                case StateMachineState.attackDamageDieWaitCreate:
                case StateMachineState.skillRollDieWaitCreate:
                case StateMachineState.skillBonusRollDieWaitCreate:
                case StateMachineState.healingRollDieWaitCreate:
                    if (diagnostics >= DiagnosticMode.high) { Debug.Log("RuleSet 5E Plugin: Dice Set Ready"); }
                    lastRollId = rollId;
                    stateMachineState++;
                    if (diagnostics >= DiagnosticMode.high) { Debug.Log("RuleSet 5E Plugin: Transitioned To " + stateMachineState); }
                    break;
                default:
                    break;
            }
        }

        public void ResultDiceSet(Dictionary<string, object> result)
        {            
            if ((lastRollId == (long)result["Identifier"]) || ((long)result["Identifier"] == -2))
            {
                switch (stateMachineState)
                {
                    case StateMachineState.attackAttackDieWaitRoll:
                    case StateMachineState.attackAttackBonusDieWaitRoll:
                    case StateMachineState.attackDamageDieWaitRoll:
                    case StateMachineState.skillRollDieWaitRoll:
                    case StateMachineState.skillBonusRollDieWaitRoll:
                    case StateMachineState.healingRollDieWaitRoll:
                        if (diagnostics >= DiagnosticMode.high) { Debug.Log("RuleSet 5E Plugin: Dice Set Roll Result Ready"); }
                        lastResult = result;
                        stateMachineState++;
                        if (diagnostics >= DiagnosticMode.high) { Debug.Log("RuleSet 5E Plugin: Transitioned To " + stateMachineState); }
                        break;
                    default:
                        break;
                }
            }
            else
            {
                if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Request '" + lastRollId.ToString() + "' Result '" + result["Identifier"] + "'. Ignoring."); }
            }
        }

        public IEnumerator DisplayMessage(string text, float duration)
        {
            string origMessage = text;
            messageContent = text;
            if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Displaying Message For " + Math.Max(1.0f, duration * processSpeed) + " Seconds"); }
            yield return new WaitForSeconds(Math.Max(1.0f, duration * processSpeed));
            if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Displaying Message Duration Expired"); }
            if (messageContent == origMessage) { messageContent = ""; }
        }

        private static void SyncDisNormAdv()
        {
            if (RuleSet5EPlugin.Instance.totalAdv == true)
            {
                RuleSet5EPlugin.Instance.lastRollRequestTotal = RollTotal.advantage;
            }
            else if (RuleSet5EPlugin.Instance.totalDis == true)
            {
                RuleSet5EPlugin.Instance.lastRollRequestTotal = RollTotal.disadvantage;
            }
            else // if (RuleSet5EPlugin.Instance.totalNorm == true)
            {
                RuleSet5EPlugin.Instance.lastRollRequestTotal = RollTotal.normal;
            }
        }

        private static string SafeForProtocolName(string tmp)
        {
            tmp = tmp.Replace(" ", " "); // Space => Alt255
            tmp = tmp.Replace("&", " And ");
            return tmp;
        }

        private Dictionary<string, object> ResolveRoll(string roll)
        {
            try
            {
                if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("Roll: " + roll + " (" + ((lastRollRequest.type == "x2") ? "x2" : "x1") + ")"); }
                bool min = true;
                bool max = true;
                System.Random ran = new System.Random();
                roll = roll.Substring(roll.IndexOf(" ") + 1).Trim();
                roll = "0+" + roll + "+0";
                roll = roll.ToUpper();
                string originalRoll = roll;
                string expanded = originalRoll;
                while (roll.Contains("D"))
                {
                    int total = 0;
                    int pos = roll.IndexOf("D");
                    int sPos = pos - 1;
                    int ePos = pos + 1;
                    while ("0123456789".Contains(roll.Substring(sPos, 1))) { sPos--; if (sPos == 0) { break; } }
                    while ("0123456789".Contains(roll.Substring(ePos, 1))) { ePos++; if (ePos > roll.Length) { break; } }
                    int dice = int.Parse(roll.Substring(sPos + 1, pos - (sPos + 1)));
                    int sides = int.Parse(roll.Substring(pos + 1, ePos - (pos + 1)));
                    string rolls = "[";
                    for (int d = 0; d < dice; d++)
                    {
                        //XJ://(2022/10/06) changed int pick = ran.Next(1, sides + 1); by:
                        int pick1 = ran.Next(1, sides + 1);
                        int pick2 = ran.Next(1, sides + 1);
                        int pick = pick1;
                        //XJ (2022/12/16) On DC Attack, get victim Adv or Dis.
                        if (dcAttack)
                        {
                            if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin:  (victim_totalAdv) (victim_totalDis) | " + victim_totalAdv.ToString() + " | " + victim_totalDis.ToString()); }
                            if (victim_totalAdv & sides == 20) { pick = Math.Max(pick1, pick2); }
                            if (victim_totalDis & sides == 20) { pick = Math.Min(pick1, pick2); }
                        }
                        else
                        {
                            if (totalAdv & sides == 20) { pick = Math.Max(pick1, pick2); }
                            if (totalDis & sides == 20) { pick = Math.Min(pick1, pick2); }
                        }
                        //XJ: to create two rolls when adventage or disventage is selected. Only for d20 rolls.
                        //XJ: (2022/10/08) commented
                        //if (damageDieMultiplier == 1.0f)
                        //{
                        rolls = rolls + pick + ",";
                        total = total + pick;
                        //}
                        //else
                        //{
                        //    rolls = rolls + pick + "x" + damageDieMultiplier.ToString("0") + ",";
                        //    total = total + (int)(damageDieMultiplier * pick);
                        //}
                        //XJ: Avoid 2x damage, because are calculated in roll dice now 
                        if (pick != 1) { min = false; }
                        if (pick != sides) { max = false; }
                        //XJ://(2022/10/06) Add:
                        //XJ (2022/12/16) On DC Attack, get victim Adv or Dis.
                        if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin:  (victim_totalAdv) (victim_totalDis) | " + victim_totalAdv.ToString() + " | " + victim_totalDis.ToString()); }
                        if (dcAttack) { if ((victim_totalAdv || victim_totalDis) & sides == 20) { rolls = rolls + (rolls.Contains(pick2.ToString()) ? pick1 : pick2) + ","; } }
                        else { if ((totalAdv || totalDis) & sides == 20) { rolls = rolls + (rolls.Contains(pick2.ToString()) ? pick1 : pick2) + ","; } }
                        //XJ: To keep the two roll results obtained when adventage or disventage is selected. Only d20 rolls.
                    }
                    roll = roll.Substring(0, sPos + 1) + total + roll.Substring(ePos);
                    rolls = rolls.Substring(0, rolls.Length - 1) + "]";
                    int expPos = expanded.IndexOf(dice + "D" + sides);
                    expanded = expanded.Substring(0, expPos) + rolls + expanded.Substring(expPos + (dice.ToString() + "D" + sides.ToString()).Length);
                    if (originalRoll != "0+0+0") {expanded = expanded.Replace("+0+0", "+0"); }
                }
                DataTable dt = new DataTable();
                Dictionary<string, object> results = new Dictionary<string, object>();
                results.Add("Identifier", (long)-2);
                results.Add("Roll", originalRoll.Substring(2).Substring(0, originalRoll.Substring(2).Length - 2).Replace("D","d").Replace("+0",""));
                results.Add("Total", (int)dt.Compute(roll, null));
                expanded = expanded.Substring(2).Substring(0, expanded.Substring(2).Length - 2);
                expanded = "+-".Contains(expanded.Substring(0,1))?expanded.Substring(1): expanded;
                results.Add("Expanded",expanded) ;
                results.Add("IsMax", (bool)max);
                results.Add("IsMin", (bool)min);
                return results;
            }
            catch (Exception e)
            {
                Dictionary<string, object> results = new Dictionary<string, object>();
                results.Add("Identifier", (long)-2);
                results.Add("Roll", roll.Substring(2).Substring(0, roll.Substring(2).Length - 2));
                results.Add("Total", 0);
                results.Add("Expanded", e.Message);
                results.Add("IsMax", false);
                results.Add("IsMin", false);
                return results;
            }
        }
    }
}
