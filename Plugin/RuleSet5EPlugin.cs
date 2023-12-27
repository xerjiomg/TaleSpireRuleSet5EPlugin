using BepInEx;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using static UI_RearrangeableList.ListChangeData;
using System.Runtime.CompilerServices;
using UnityEngine.Assertions;
using static LordAshes.RuleSet5EPlugin;
using UnityEngine.Assertions.Must;
using RadialUI;
using RadialUI.Extensions;
using UnityEngine.Profiling;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using DataModel;
using Unity.Mathematics;
using Bounce.Textures;
using Bounce.Tinting;
using UnityEngineInternal;
using System.Drawing.Imaging;
using UnityEngine.Rendering.PostProcessing;
using System.Security.Cryptography;
using BepInEx.Configuration;
using UnityEngine.UIElements;
using System.Reflection;


namespace LordAshes
{
    [BepInPlugin(Guid, Name, Version)]
    [BepInDependency(RadialUI.RadialUIPlugin.Guid)]
    [BepInDependency(FileAccessPlugin.Guid)]
    [BepInDependency(AssetDataPlugin.Guid)]
    public partial class RuleSet5EPlugin : BaseUnityPlugin
    {
        // Plugin info        
        public const string Name = "RuleSet 5E Plug-In";
        public const string Guid = "org.lordashes.plugins.ruleset5e";
        public const string Version = "2.3.6.0";
        public const string Author = "XJ_Nekomancer";

        // Reference to plugin instance
        public static RuleSet5EPlugin Instance = null;

        // User configurations
        private string iconSelector = "type";

        // Character dictionary
        private Dictionary<string, Character> characters = new Dictionary<string, Character>();

        //XJ: Id + Character dictionary
        private  Dictionary<string, string> idMinis = new Dictionary<string, string>();

        //XJ: Id Bonus
        private  Dictionary<CreatureGuid, IdBonus> IdBonusList = new Dictionary<CreatureGuid, IdBonus>(); //XJ Remove Future

        // Last selected
        CreatureGuid lastSelectedMini = CreatureGuid.Empty; // XJ Remove Future

        // Private variables
        private Texture reactionStopIcon = null;
        private bool reactionStopContinue = false;
        //XJ: change: private int reactionRollTotal = 0;
        private string reactionRollTotal = "NoInfo";
        private bool reactionHalve = false;
        //XJ: To show more information about roll attack in reaction. And add Halve option
        //XJ:(2022/10/12)
        private bool dcAttack = false;
        private bool healSequence = false;
        //XJ: To add DC attack state
        private Vector2 smallScreenConversion = new Vector2(-1200, 40);        
        private bool pauseRender = false;       

        //XJ: (2022/12/14) MultiTarget Vars:
        private int numberOfSelectedTargets = 0;
        public List< CreatureBoardAsset> multiTargetAssets = new List<CreatureBoardAsset>() ;
        private int MultitargetAssetsIndex;
        public static Texture2D backgroundTexture;
        public static string  multiAttackType ="";
        public static Roll multiRoll;

        //XJ:(2023/08/25)

        private GameInput gameInputInstance = null;
        private MethodInfo gameInputDisable = null;
        private MethodInfo gameInputEnable = null;
        public bool globalKeyboardDisabled = false;

        public int uiLocX;
        public int uiLocY;

        // LA: 2023.12.22
        public bool fadeText = false;
        public bool useGeneralIcons = false;
        public static bool useJsonExtension = false;
        public static string locationPrefixFiles = "";
        public static string locationPrefixIcons = "";

        private Dictionary<string, Sprite> iconCache = new Dictionary<string, Sprite>();

        /// <summary>
        /// Function for initializing plugin
        /// This function is called once by TaleSpire
        /// </summary>


        void Awake()
        {
            UnityEngine.Debug.Log("RuleSet 5E Plugin: Active.");
            Instance = this;
            this.gameObject.hideFlags = HideFlags.HideAndDontSave;
            var harmony = new Harmony(Guid);
            harmony.PatchAll();

            AssetDataPlugin.Subscribe(RuleSet5EPlugin.Guid + ".BonusData", Callback); ////XJ: Bonus data
            AssetDataPlugin.Subscribe(RuleSet5EPlugin.Guid + ".Bubble", Callback2); ////XJ: Bubble on all players        

            if (diagnostics >= DiagnosticMode.high) { Debug.Log("RuleSet 5E Plugin: CurrentCulture:" + Thread.CurrentThread.CurrentCulture.ToString()); }

            // Read and apply configuration settings
            iconSelector = Config.Bind("Appearance", "Attack Icons Base On", "type").Value;
            string[] existence = Config.Bind("Appearance", "Dice Side Existance", "-100,0,0,45,0,0").Value.Split(',');//
            diceSideExistance = new Existence(new Vector3(float.Parse(existence[0]), float.Parse(existence[1]), float.Parse(existence[2])), new Vector3(float.Parse(existence[3]), float.Parse(existence[4]), float.Parse(existence[5])));
            string[] colorCode = Config.Bind("Appearance", "Dice Color", "0,0,0").Value.Split(',');
            //XJ: 2022/10/27 add  "CultureInfo.InvariantCulture.NumberFormat" to allow other cultural decimal separator.
            if (colorCode.Length == 3)
            {
                diceColor = new UnityEngine.Color(float.Parse(colorCode[0], CultureInfo.InvariantCulture.NumberFormat), float.Parse(colorCode[1], CultureInfo.InvariantCulture.NumberFormat), float.Parse(colorCode[2], CultureInfo.InvariantCulture.NumberFormat));
            }
            else if (colorCode.Length > 3)
            {
                diceColor = new UnityEngine.Color(float.Parse(colorCode[0], CultureInfo.InvariantCulture.NumberFormat), float.Parse(colorCode[1], CultureInfo.InvariantCulture.NumberFormat), float.Parse(colorCode[2], CultureInfo.InvariantCulture.NumberFormat), float.Parse(colorCode[3], CultureInfo.InvariantCulture.NumberFormat));
            }
            colorCode = Config.Bind("Appearance", "Dice Highlight Color", "1.0,1.0,0").Value.Split(',');
            if (colorCode.Length == 3)
            {
                diceHighlightColor = new UnityEngine.Color32((byte)(255 * float.Parse(colorCode[0], CultureInfo.InvariantCulture.NumberFormat)), (byte)(255 * float.Parse(colorCode[1], CultureInfo.InvariantCulture.NumberFormat)), (byte)(255 * float.Parse(colorCode[2], CultureInfo.InvariantCulture.NumberFormat)), 255);
            }
            else if (colorCode.Length > 3)
            {
                diceHighlightColor = new UnityEngine.Color32((byte)(255 * float.Parse(colorCode[0], CultureInfo.InvariantCulture.NumberFormat)), (byte)(255 * float.Parse(colorCode[1], CultureInfo.InvariantCulture.NumberFormat)), (byte)(255 * float.Parse(colorCode[2], CultureInfo.InvariantCulture.NumberFormat)), (byte)(255 * float.Parse(colorCode[3], CultureInfo.InvariantCulture.NumberFormat)));
            }

            missAnimation = Config.Bind("Appearance", "Miss Animation Name", "TLA_Wiggle").Value;
            deadAnimation = Config.Bind("Appearance", "Dead Animation Name", "TLA_Action_Knockdown").Value;
            fadeText = Config.Bind("Appearance", "Hide UI Menu Text When Not Under Mouse", false).Value;
            useGeneralIcons = Config.Bind("Appearance", "Use General Icons", true).Value;

            string[] smallScreenConversionString = Config.Bind("Settings", "Small Screen Offset", "-1200,40").Value.Split(',');
            smallScreenConversion = new Vector2(float.Parse(smallScreenConversionString[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(smallScreenConversionString[1], System.Globalization.CultureInfo.InvariantCulture));
            rollingSystem = Config.Bind("Settings", "Rolling Style", RollMode.automaticDice).Value;
            processSpeed = (Config.Bind("Settings", "Process Delay Percentage", 100).Value / 100);
            locationPrefixFiles = Config.Bind("Settings", "Remote Location Prefix For Dnd5E Files (Blank For Local Files)", "").Value;
            locationPrefixIcons = Config.Bind("Settings", "Remote Location Prefix For Icon Files (Blank For Local Files)", "").Value;
            useJsonExtension = Config.Bind("Settings", "Use Json Extension Instead Of Dnd5E", false).Value;

            //XJ (2022/10/14)
            changeBaseColors = Config.Bind("Auto Color Base Settings", "Auto Color Base", false).Value;
            npcColors = Config.Bind("Auto Color Base Settings", "Npc Colors", "2,13,1").Value.Split(',');
            pcColors = Config.Bind("Auto Color Base Settings", "PC Colors", "6,7,8").Value.Split(',');
            //XJ: Config AutoChangeBaseColors
            uiLocX = int.Parse(Config.Bind("Appearance", "UI Location X", "0").Value);
            uiLocY = int.Parse(Config.Bind("Appearance", "UI Location Y", "0").Value); 
            //XJ (2022/11/09) Level of detail log config
            diagnostics = Config.Bind("Troubleshooting", "Diagnostic Mode", DiagnosticMode.low).Value;

            if (diagnostics >= DiagnosticMode.high) { Debug.Log("RuleSet 5E Plugin: Dice Side Location = " + diceSideExistance.position); }

            if (diagnostics >= DiagnosticMode.high) { Debug.Log("RuleSet 5E Plugin: Speed = " + processSpeed + "x"); }

            RadialUI.RadialUIPlugin.RemoveCustomButtonOnCharacter("Attacks");

            backgroundTexture = FileAccessPlugin.Image.LoadTexture(locationPrefixIcons + "/" + RuleSet5EPlugin.Guid + "/RulesetBuilder.Toolbar.png");
            iconCache.Add("Attack", FileAccessPlugin.Image.LoadSprite(locationPrefixIcons + "/" + RuleSet5EPlugin.Guid + "/Attack.png"));
            iconCache.Add("Magic", FileAccessPlugin.Image.LoadSprite(locationPrefixIcons + "/" + RuleSet5EPlugin.Guid + "/Magic.png"));
            iconCache.Add("Saves", FileAccessPlugin.Image.LoadSprite(locationPrefixIcons + "/" + RuleSet5EPlugin.Guid + "/Saves.png"));
            iconCache.Add("Skills", FileAccessPlugin.Image.LoadSprite(locationPrefixIcons + "/" + RuleSet5EPlugin.Guid + "/Skills.png"));
            iconCache.Add("Healing", FileAccessPlugin.Image.LoadSprite(locationPrefixIcons + "/" + RuleSet5EPlugin.Guid + "/Healing.png"));

            RadialUI.RadialSubmenu.EnsureMainMenuItem(RuleSet5EPlugin.Guid + ".Attacks", RadialUI.RadialSubmenu.MenuType.character, "Scripted Attacks", iconCache["Attack"]);

            //XJ: add (2022/10/12)
            RadialUI.RadialSubmenu.EnsureMainMenuItem(RuleSet5EPlugin.Guid + ".AttacksDC", RadialUI.RadialSubmenu.MenuType.character, "Scripted DC Attacks", iconCache["Magic"]);
            //XJ Create new radial menu: DC Attacks.
            RadialUI.RadialSubmenu.EnsureMainMenuItem(RuleSet5EPlugin.Guid + ".Saves", RadialUI.RadialSubmenu.MenuType.character, "Saves", iconCache["Saves"]);
            RadialUI.RadialSubmenu.EnsureMainMenuItem(RuleSet5EPlugin.Guid + ".Skills", RadialUI.RadialSubmenu.MenuType.character, "Skills", iconCache["Skills"]);
            RadialUI.RadialSubmenu.EnsureMainMenuItem(RuleSet5EPlugin.Guid + ".Healing", RadialUI.RadialSubmenu.MenuType.character, "Healing", iconCache["Healing"]);

            reactionStopIcon = FileAccessPlugin.Image.LoadTexture(locationPrefixIcons + "/" + RuleSet5EPlugin.Guid + "/"+"ReactionStop.png");            
            Utility.PostOnMainPage(this.GetType());
        }

        /// <summary>
        /// Function for determining if view mode has been toggled and, if so, activating or deactivating Character View mode.
        /// This function is called periodically by TaleSpire.
        /// </summary>
        void Update()
        {
            if (Utility.isBoardLoaded())
            {
                if (callbackRollReady == null)
                {
                    callbackRollReady = NewDiceSet;
                    callbackRollResult = ResultDiceSet;
                    chatManager = GameObject.FindObjectOfType<ChatManager>();
                    StartCoroutine((IEnumerator)Executor());
                }
                
                if (Input.GetMouseButtonDown(0))
                {
                    //if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: ClickEvent"); }
                    Rect attackbonus = ScreenSizeAdjustment(new Rect((float)Screen.width / 2 - 70, 34, 40, 20), true);
                    if (Screen.height - Input.mousePosition.y > attackbonus.y & Screen.height - Input.mousePosition.y < attackbonus.y + attackbonus.height)
                    {
                        //if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Click y enter"); }
                        Rect damagebonus = ScreenSizeAdjustment(new Rect((float)Screen.width / 2 + 5, 34, 40, 20), true);
                        Rect skillbonus = ScreenSizeAdjustment(new Rect((float)Screen.width / 2 + 80, 34, 40, 20), true);
                        Rect acbonus = ScreenSizeAdjustment(new Rect((float)Screen.width / 2 + 165, 34, 40, 20), true);
                        if (Input.mousePosition.x > attackbonus.x & Input.mousePosition.x < attackbonus.x + attackbonus.width || Input.mousePosition.x > damagebonus.x & Input.mousePosition.x < damagebonus.x + damagebonus.width || Input.mousePosition.x > skillbonus.x & Input.mousePosition.x < skillbonus.x + skillbonus.width || Input.mousePosition.x > acbonus.x & Input.mousePosition.x < acbonus.x + acbonus.width)
                        {
                            //if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Click x + y enter"); }
                            DisableKeyboardEvents(true);
                        }
                        else { DisableKeyboardEvents(false); }
                    }
                    else { DisableKeyboardEvents(false); }

                    if (RuleSet5EPlugin.selectRuleMode == true)
                    {
                        CreatureBoardAsset lastClicked = null;
                        Unity.Mathematics.float3 pos;
                        PixelPickingManager.TryGetPickedCreature(out pos, out lastClicked);
                        if (lastClicked != null)
                        {
                            numberOfSelectedTargets = numberOfSelectedTargets + 1;
                            if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Last Clicked: " + lastClicked.CreatureId.ToString()); }
                            string targetMessage = "I'm target ";
                            string posttargetMessage = "";
                            int itinecount = 0;
                            int prefix = 0;
                            if (multiAttackType == "Heal") { lastClicked.SetGlow(true, Color.Lerp(Color.green, Color.black, 0.65f)); }
                            else { lastClicked.SetGlow(true, Color.Lerp(Color.red, Color.black, 0.30f)); }
                            multiTargetAssets.Add(lastClicked);
                            foreach (CreatureBoardAsset tempmultiasset in multiTargetAssets) { itinecount++; if (tempmultiasset.CreatureId == lastClicked.CreatureId) { prefix++; posttargetMessage = posttargetMessage + "," + itinecount.ToString(); } }
                            if (prefix > 1) { targetMessage = targetMessage + posttargetMessage.Trim(','); } else { targetMessage = targetMessage + numberOfSelectedTargets.ToString(); }
                            SingletonBehaviour<TextBubbleManager>.Instance.Hire().Setup(lastClicked.HookHead, targetMessage, Color.Lerp(Color.red, Color.yellow, 0.40f), new Color(0.4f, 0.3f, 0.5f, 1f));
                        }
                    }
                }
                if (RuleSet5EPlugin.selectRuleMode == true)
                {
                    if (Input.GetMouseButtonDown(2)) { numberOfSelectedTargets = 0; StartSequencePre(multiAttackType, multiRoll, instigator.CreatureId, null, null); RuleSet5EPlugin.selectRuleMode = false; }
                    if (Input.GetMouseButtonDown(1)) { foreach (CreatureBoardAsset tempmultiasset in multiTargetAssets) { tempmultiasset.SetGlow(false, Color.red); }; numberOfSelectedTargets = 0; multiTargetAssets.Clear(); MultitargetAssetsIndex = 0; RuleSet5EPlugin.selectRuleMode = false; }                    
                }          
            }
        }

        void OnGUI()
        {

            if (dolly != null)
            {
                //Debug.Log("Test Plugin: Rendering Aux Camera View");
                GUI.DrawTexture(new Rect(5,Screen.height / 2, Screen.width/4,Screen.height/4), auxCameraTexture, ScaleMode.ScaleToFit);
                 // GUI.DrawTexture(new Rect(5, (float)Screen.width / 2, 640, 480), auxCameraTexture, ScaleMode.ScaleToFit);
            }

            if (RuleSet5EPlugin.selectRuleMode == true)
            {
                GUIStyle gs3 = new GUIStyle();                              
                gs3.alignment = TextAnchor.UpperCenter;              
                GUI.DrawTexture(ScreenSizeAdjustment(new Rect((float)Screen.width/2 -  460f, 64f, 920f, 42f)), backgroundTexture);
                gs3.fontSize = 20;  
                GUI.Label(ScreenSizeAdjustment(new Rect(3f, 65f, (float)Screen.width, 30)), "<color=#db3b00> MultiSelectMode: ON | Targets: " + numberOfSelectedTargets.ToString()+ "</color>", gs3);
                gs3.fontSize = 16;
                GUI.Label(ScreenSizeAdjustment(new Rect(3f, 86f, (float)Screen.width, 30)), "<color=#Ffffff>To add Target: Press Left Mouse Button  | To continue: Press Middle Mouse Button | To Cancel: Press Right Mouse Button </color>", gs3);
            }
            if (messageContent != "")
            {
                GUIStyle gs1 = new GUIStyle();
                gs1.normal.textColor = Color.black;
                gs1.alignment = TextAnchor.UpperCenter;
                gs1.fontSize = 32;
                GUIStyle gs2 = new GUIStyle();
                gs2.normal.textColor = Color.yellow;
                gs2.alignment = TextAnchor.UpperCenter;
                gs2.fontSize = 32;
                GUI.Label(ScreenSizeAdjustment(new Rect(0f, 60f, (float)Screen.width, 55)), messageContent, gs1);
                GUI.Label(ScreenSizeAdjustment(new Rect(3f, 63f, (float)Screen.width, 55)), messageContent, gs2);                
            }
            if (reactionStopContinue)
            {
                //XJ:(2022/10/08) Added two new reactions: 
                GUIStyle gs2 = new GUIStyle();
                gs2.normal.textColor = Color.yellow;
                gs2.alignment = TextAnchor.UpperCenter;
                gs2.fontSize = 26;
                GUI.Label(ScreenSizeAdjustment(new Rect(((float)Screen.width / 2f) - 40f, 55, 80, 30)), "Roll: " + reactionRollTotal, gs2);
                if (GUI.Button(ScreenSizeAdjustment(new Rect(((float)Screen.width / 2f) - 130f, 85, 40, 30)), "Hit"))
                {
                    reactionStopContinue = false;
                    string message = "Forced Normal Hit Reaction Used";
                    lastResult["IsMax"] = false;
                    chatManager.SendChatMessageEx(message, message, message, instigator.CreatureId, LocalClient.Id.Value);
                    stateMachineState = StateMachineState.attackAttackHitReport;
                }
                if (GUI.Button(ScreenSizeAdjustment(new Rect(((float)Screen.width / 2f) - 220f, 85, 80, 30)), "Critical"))
                {
                    reactionStopContinue = false;
                    string message = "Forced Critical Hit Reaction Used";
                    lastResult["IsMax"] = true;
                    chatManager.SendChatMessageEx(message, message, message, instigator.CreatureId, LocalClient.Id.Value);
                    stateMachineState = StateMachineState.attackAttackHitReport;
                }
                if (GUI.Button(ScreenSizeAdjustment(new Rect(((float)Screen.width / 2f) - 85f, 85, 80, 30)), "Continue"))
                {
                    reactionStopContinue = false;
                    stateMachineState = StateMachineState.attackAttackDieRollReport;
                    //XJ: 2022/10/18
                    if (secureSuccess) { stateMachineState = StateMachineState.attackAttackHitReport; }
                    //XJ: If secure hit, change state.
                }
                if (GUI.Button(ScreenSizeAdjustment(new Rect(((float)Screen.width / 2f) + 5f, 85, 80, 30)), "Cancel"))
                {
                    reactionStopContinue = false;
                    string message = "Cancel Attack Reaction Used";
                    chatManager.SendChatMessageEx(message, message, message, instigator.CreatureId, LocalClient.Id.Value);
                    stateMachineState = StateMachineState.attackRollCleanup;
                }
                if (GUI.Button(ScreenSizeAdjustment(new Rect(((float)Screen.width / 2f) + 90f, 85, 40, 30)), "Miss"))
                {
                    reactionStopContinue = false;
                    string message = "Miss Reaction Used";
                    chatManager.SendChatMessageEx(message, message, message, instigator.CreatureId, LocalClient.Id.Value);
                    stateMachineState = StateMachineState.attackAttackMissReport;
                }
                if (GUI.Button(ScreenSizeAdjustment(new Rect(((float)Screen.width / 2f) + 140f, 85, 60, 30)), "Halve"))
                {
                    reactionStopContinue = false;
                    reactionHalve = true;
                    string message = "Halved Reaction Used";
                    chatManager.SendChatMessageEx(message, message, message, instigator.CreatureId, LocalClient.Id.Value);
                    stateMachineState = StateMachineState.attackAttackDieRollReport;
                    //XJ: 2022/10/18
                    if (secureSuccess) { stateMachineState = StateMachineState.attackAttackHitReport; }
                    //XJ: If secure hit, change state.
                    //XJ: Add force critical and halve damage options.
                }
            }

            if (Utility.isBoardLoaded())
            {
                if (pauseRender == false && PlayMode.CurrentStateId != PlayMode.Ids.Cutscene )
                {
                    RenderToolBarAddons();
                }
            }
        }

        //XJ:(2022/10/12)
        public void AttackDC(Roll roll, CreatureGuid cid, object obj, MapMenuItem mi)
        {
            if (diagnostics >= DiagnosticMode.low) { Debug.Log("RuleSet 5E Plugin: AttacksDC: " + roll.name); }
            lastRollRequest = new Roll(roll);
            if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: AttacksDC: " + lastRollRequest.name); }
            
            Roll find = lastRollRequest;
            while (true)
            {
                if (diagnostics >= DiagnosticMode.high) { Debug.Log("Damage Stack: " + find.name + " : " + find.type + " : " + find.roll); }
                find = find.link;
                if (find == null) { break; }
            }          
            if (instigator != null && victim != null) { dcAttack = true;  stateMachineState = StateMachineState.attackAttackRangeCheck; victim.SetGlow(true, Color.red); }
        }
        //XJ:To add DC attacks
        public void Attack(Roll roll, CreatureGuid cid, object obj, MapMenuItem mi)
        {
            if (diagnostics >= DiagnosticMode.low) { Debug.Log("RuleSet 5E Plugin: Attack: " + roll.name); }
            
            lastRollRequest = new Roll(roll);
            if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Attack: " + lastRollRequest.name); }            
            Roll find = lastRollRequest;
            while (true)
            {
                if (diagnostics >= DiagnosticMode.high) { Debug.Log("Damage Stack: " + find.name + " : " + find.type + " : " + find.roll); }
                find = find.link;
                if (find == null) { break; }
            }                    
            if (instigator != null && victim != null) { stateMachineState = StateMachineState.attackAttackRangeCheck; victim.SetGlow(true, Color.red); }
        }

        public void Skill(Roll roll, CreatureGuid cid, object obj, MapMenuItem mi)
        {
            if (diagnostics >= DiagnosticMode.low) { Debug.Log("RuleSet 5E Plugin: Save: " + roll.name); }
            lastRollRequest = roll;
            if (diagnostics >= DiagnosticMode.high) { Debug.Log("Roll: " + roll.roll); }        
            if (instigator != null) { stateMachineState = StateMachineState.skillRollSetup; victim.SetGlow(true, Color.Lerp(Color.green, Color.black, 0.76f)); }
        }

        public void Save(Roll roll, CreatureGuid cid, object obj, MapMenuItem mi)
        {
            if (diagnostics >= DiagnosticMode.low) { Debug.Log("RuleSet 5E Plugin: Skill: " + roll.name); }
            lastRollRequest = roll;
            if (diagnostics >= DiagnosticMode.high) { Debug.Log("Roll: " + roll.roll); }
            //CreaturePresenter.TryGetAsset(LocalClient.SelectedCreatureId, out instigator);
            if (instigator != null) { stateMachineState = StateMachineState.skillRollSetup; victim.SetGlow(true, Color.Lerp(Color.green, Color.black, 0.76f)); }
        }

        public void Heal(Roll roll, CreatureGuid cid, object obj, MapMenuItem mi)
        {
            if (diagnostics >= DiagnosticMode.low) { Debug.Log("RuleSet 5E Plugin: Heal: " + roll.name); }
            lastRollRequest = roll;
            if (diagnostics >= DiagnosticMode.high) { Debug.Log("Roll: " + roll.roll); }
            if (instigator != null && victim != null) { healSequence = true; stateMachineState = StateMachineState.attackAttackRangeCheck; victim.SetGlow(true, Color.Lerp(Color.green, Color.black, 0.76f)); }
        }

        //XJ:(2022/10/11) add:
        public void LoadDnd5(CreatureBoardAsset instigator)       
        {
            try
            {
                if (characters.ContainsKey(Utility.GetCharacterName(instigator.Name)))  // XJ:2022/11/15  Work with FileAccessPlugin
                {
                    if (diagnostics >= DiagnosticMode.high) { Debug.Log("RuleSet 5E Plugin: Loading stats from Dndn5:"); }
                    //XJ(2022/10/25) Add stats according with Stat Names.


                    if (instigator.Hp.Value.ToString() == instigator.Hp.Max.ToString())
                    {
                        CreatureManager.SetCreatureStatByIndex(instigator.CreatureId, new CreatureStat(int.Parse(characters[RuleSet5EPlugin.Utility.GetCharacterName(instigator)].hp), int.Parse(characters[RuleSet5EPlugin.Utility.GetCharacterName(instigator)].hp)), -1);
                    }
                    else
                    {
                        CreatureManager.SetCreatureStatByIndex(instigator.CreatureId, new CreatureStat(int.Parse(instigator.Hp.Value.ToString()), int.Parse(characters[RuleSet5EPlugin.Utility.GetCharacterName(instigator)].hp)), -1);
                    }

                    for (int i = 0; i < CampaignSessionManager.StatNames.Length; i++)
                    {
                        if (CampaignSessionManager.StatNames[i].ToUpper().Contains("AC")) { CreatureManager.SetCreatureStatByIndex(instigator.CreatureId, new CreatureStat(int.Parse(characters[RuleSet5EPlugin.Utility.GetCharacterName(instigator)].ac), 0), i); if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: AC CHANGED"); } }
                        if (CampaignSessionManager.StatNames[i].ToUpper().Contains("SPEED")) { CreatureManager.SetCreatureStatByIndex(instigator.CreatureId, new CreatureStat(int.Parse(characters[RuleSet5EPlugin.Utility.GetCharacterName(instigator)].speed), 0), i); if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: SPEED CHANGED"); } }
                        if (CampaignSessionManager.StatNames[i].ToUpper().Contains("STR")) { CreatureManager.SetCreatureStatByIndex(instigator.CreatureId, new CreatureStat(int.Parse(characters[RuleSet5EPlugin.Utility.GetCharacterName(instigator)].str), int.Parse(Math.Floor((float.Parse(characters[RuleSet5EPlugin.Utility.GetCharacterName(instigator)].str) - 10) / 2).ToString())), i); if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: STR CHANGED"); } }
                        if (CampaignSessionManager.StatNames[i].ToUpper().Contains("DEX")) { CreatureManager.SetCreatureStatByIndex(instigator.CreatureId, new CreatureStat(int.Parse(characters[RuleSet5EPlugin.Utility.GetCharacterName(instigator)].dex), int.Parse(Math.Floor((float.Parse(characters[RuleSet5EPlugin.Utility.GetCharacterName(instigator)].dex) - 10) / 2).ToString())), i); if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: DEX CHANGED"); } }
                        if (CampaignSessionManager.StatNames[i].ToUpper().Contains("CON")) { CreatureManager.SetCreatureStatByIndex(instigator.CreatureId, new CreatureStat(int.Parse(characters[RuleSet5EPlugin.Utility.GetCharacterName(instigator)].con), int.Parse(Math.Floor((float.Parse(characters[RuleSet5EPlugin.Utility.GetCharacterName(instigator)].con) - 10) / 2).ToString())), i); if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: CON CHANGED"); } }
                        if (CampaignSessionManager.StatNames[i].ToUpper().Contains("INT")) { CreatureManager.SetCreatureStatByIndex(instigator.CreatureId, new CreatureStat(int.Parse(characters[RuleSet5EPlugin.Utility.GetCharacterName(instigator)].Int), int.Parse(Math.Floor((float.Parse(characters[RuleSet5EPlugin.Utility.GetCharacterName(instigator)].Int) - 10) / 2).ToString())), i); if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: INT CHANGED"); } }
                        if (CampaignSessionManager.StatNames[i].ToUpper().Contains("WIS")) { CreatureManager.SetCreatureStatByIndex(instigator.CreatureId, new CreatureStat(int.Parse(characters[RuleSet5EPlugin.Utility.GetCharacterName(instigator)].wis), int.Parse(Math.Floor((float.Parse(characters[RuleSet5EPlugin.Utility.GetCharacterName(instigator)].wis) - 10) / 2).ToString())), i); if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: WIS CHANGED"); } }
                        if (CampaignSessionManager.StatNames[i].ToUpper().Contains("CHA")) { CreatureManager.SetCreatureStatByIndex(instigator.CreatureId, new CreatureStat(int.Parse(characters[RuleSet5EPlugin.Utility.GetCharacterName(instigator)].cha), int.Parse(Math.Floor((float.Parse(characters[RuleSet5EPlugin.Utility.GetCharacterName(instigator)].cha) - 10) / 2).ToString())), i); if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: CHA CHANGED"); } }
                    }

                    //XJ: (2022 / 10 / 9) add:
                    RuleSet5EPlugin.Instance.CustomBColor(instigator, (int)instigator.Hp.Value, (int)instigator.Hp.Max);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("RuleSet 5E Plugin:" + "!Error loading Dnd5 stats: " + e);
            }
        }

        public void LoadDnd5eJson(CreatureBoardAsset instigator)
        {
            string characterName = Utility.GetCharacterName(instigator.Name);
            if (!characters.ContainsKey(characterName))
            {
                if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Loading Character '/" + Utility.GetCharacterName(instigator.Name) + ".dnd5e'"); }
                string fileName = locationPrefixFiles + "/" + Utility.GetCharacterName(instigator.Name) + (useJsonExtension ? ".json" : ".dnd5e");
                if (!FileAccessPlugin.File.Exists(fileName))
                {
                    Debug.LogWarning("RuleSet 5E Plugin: " + "Cannot find '"+fileName+"'");
                    return;
                }

                try
                {
                    fileName = FileAccessPlugin.File.Find(fileName)[0];
                    if (diagnostics >= DiagnosticMode.low) { Debug.Log("RuleSet 5E Plugin: Loading Character '" + characterName + "' From '" + fileName + "'"); }
                    string json = FileAccessPlugin.File.ReadAllText(fileName);
                    if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Loading Character '" + characterName + "' Contains:\r\n" + json); }
                    characters.Add(characterName, JsonConvert.DeserializeObject<Character>(json));
                }
                catch (Exception e)
                {
                    Debug.LogWarning("RuleSet 5E Plugin: " + "Cannot read dnd5e file '" + fileName + "': " + e);
                }

                try
                {
                    //XJ Create new radial menu: DC Attack
                    foreach (Roll roll in characters[characterName].attacksDC)
                    {
                        if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Adding Character '" + characterName + "' attacksDC '" + roll.name + "'"); }
                        string rollIconSelector = PatchAssistant.GetField(roll, iconSelector) + ".png";

                        RadialUI.RadialSubmenu.CreateSubMenuItem(RuleSet5EPlugin.Guid + ".AttacksDC",
                          new MapMenu.ItemArgs()
                          {
                              CloseMenuOnActivate = true,
                              FadeName = fadeText,
                              Icon = (useGeneralIcons) ? iconCache["Magic"] : (FileAccessPlugin.File.Exists(locationPrefixIcons + "/" + RuleSet5EPlugin.Guid + "/" + rollIconSelector)) ? FileAccessPlugin.Image.LoadSprite(locationPrefixIcons + "/" + RuleSet5EPlugin.Guid + "/" + rollIconSelector) : iconCache["Magic"],
                              Title = roll.name,
                          },
                          // We need to use the HV callback version here in order to be able to specify the MapMenu.ItemArg object so that we can set the FadeName property
                          (hv, obj, mi) => StartSequencePre("AttackDC", roll, new CreatureGuid(RadialUIPlugin.GetLastRadialTargetCreature().ToString()), obj, mi),
                          () => { return Utility.CharacterCheck(characterName, roll.name); }
                        );
                    }

                    foreach (Roll roll in characters[characterName].attacks)
                    {
                        if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Adding Character '" + characterName + "' Attack '" + roll.name + "'"); }
                        string rollIconSelector = PatchAssistant.GetField(roll, iconSelector) + ".png";

                        RadialUI.RadialSubmenu.CreateSubMenuItem(RuleSet5EPlugin.Guid + ".Attacks",
                          new MapMenu.ItemArgs()
                          {
                              CloseMenuOnActivate = true,
                              FadeName = fadeText,
                              Icon = (useGeneralIcons) ? iconCache["Attack"] : (FileAccessPlugin.File.Exists(locationPrefixIcons + "/" + RuleSet5EPlugin.Guid + "/" + rollIconSelector)) ? FileAccessPlugin.Image.LoadSprite(locationPrefixIcons + "/" + RuleSet5EPlugin.Guid + "/" + rollIconSelector) : iconCache["Attack"],
                              Title = roll.name,
                          },
                          // We need to use the HV callback version here in order to be able to specify the MapMenu.ItemArg object so that we can set the FadeName property
                          (hv, obj, mi) => StartSequencePre("Attack", roll, new CreatureGuid(RadialUI.RadialUIPlugin.GetLastRadialTargetCreature()), obj, mi),
                          () => { return Utility.CharacterCheck(characterName, roll.name); }
                        );
                    }

                    foreach (Roll roll in characters[characterName].saves)
                    {
                        if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Adding Character '" + characterName + "' Save '" + roll.name + "'"); }

                        RadialUI.RadialSubmenu.CreateSubMenuItem(RuleSet5EPlugin.Guid + ".Saves",
                          new MapMenu.ItemArgs()
                          {
                              CloseMenuOnActivate = true,
                              FadeName = fadeText,
                              Icon = (useGeneralIcons) ? iconCache["Saves"] : (FileAccessPlugin.File.Exists(locationPrefixIcons + "/" + RuleSet5EPlugin.Guid + "/save_" + roll.name + ".png")) ? FileAccessPlugin.Image.LoadSprite(locationPrefixIcons + "/" + RuleSet5EPlugin.Guid + "/save_" + roll.name + ".png") : iconCache["Saves"],
                              Title = roll.name,
                          },
                          // We need to use the HV callback version here in order to be able to specify the MapMenu.ItemArg object so that we can set the FadeName property
                          (hv, obj, mi) => StartSequencePre("Save", roll, new CreatureGuid(RadialUI.RadialUIPlugin.GetLastRadialTargetCreature()), obj, mi),
                          () => { return Utility.CharacterCheck(characterName, roll.name); }
                        );
                    }

                    foreach (Roll roll in characters[characterName].skills)
                    {
                        Debug.Log("RuleSet 5E Plugin: Adding Character '" + characterName + "' Skill '" + roll.name + "'");

                        RadialUI.RadialSubmenu.CreateSubMenuItem(RuleSet5EPlugin.Guid + ".Skills",
                          new MapMenu.ItemArgs()
                          {
                              CloseMenuOnActivate = true,
                              FadeName = fadeText,
                              Icon = (useGeneralIcons) ? iconCache["Skills"] : (FileAccessPlugin.File.Exists(locationPrefixIcons + "/" + RuleSet5EPlugin.Guid + "/" + roll.name + ".png")) ? FileAccessPlugin.Image.LoadSprite(locationPrefixIcons + "/" + RuleSet5EPlugin.Guid + "/" + roll.name + ".png") : iconCache["Skills"],
                              Title = roll.name,
                          },
                          // We need to use the HV callback version here in order to be able to specify the MapMenu.ItemArg object so that we can set the FadeName property
                          (hv, obj, mi) => StartSequencePre("Skill", roll, new CreatureGuid(RadialUI.RadialUIPlugin.GetLastRadialTargetCreature()), obj, mi),
                          () => { return Utility.CharacterCheck(characterName, roll.name); }
                        );
                    }

                    foreach (Roll roll in characters[characterName].healing)
                    {
                        if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Adding Character '" + characterName + "' Healing '" + roll.name + "'"); }

                        RadialUI.RadialSubmenu.CreateSubMenuItem(RuleSet5EPlugin.Guid + ".Healing",
                          new MapMenu.ItemArgs()
                          {
                              CloseMenuOnActivate = true,
                              FadeName = fadeText,
                              Icon = (useGeneralIcons) ? iconCache["Healing"] : (FileAccessPlugin.File.Exists(locationPrefixIcons + "/" + RuleSet5EPlugin.Guid + "/" + roll.name + ".png") == true) ? FileAccessPlugin.Image.LoadSprite(locationPrefixIcons + "/" + RuleSet5EPlugin.Guid + "/" + roll.name + ".png") : iconCache["Healing"],
                              Title = roll.name,
                          },
                          // We need to use the HV callback version here in order to be able to specify the MapMenu.ItemArg object so that we can set the FadeName property
                          (hv, obj, mi) => StartSequencePre("Heal", roll, new CreatureGuid(RadialUI.RadialUIPlugin.GetLastRadialTargetCreature()), obj, mi),
                          () => { return Utility.CharacterCheck(characterName, roll.name); }
                        );
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("RuleSet 5E Plugin: Cannot read create radial menu entries. " + e);
                }

                try
                {
                    RuleSet5EPlugin.Instance.LoadDnd5(instigator);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("RuleSet 5E Plugin: Cannot set mini stats. " + e);
                }


                if (!idMinis.ContainsKey(instigator.CreatureId.ToString())) //XJ:(2022/10/25) If Mini not on the list add (id + name) to list.
                {
                    idMinis.Add(instigator.CreatureId.ToString(), instigator.Name.ToString());
                }
                else //XJ:(2022/10/25)  If Mini is on the list change name according to the id.
                {
                    idMinis[instigator.CreatureId.ToString()] = instigator.Name.ToString();
                }
            }
            else
            {
                if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Character '" + instigator.Name + "' Already Added."); }

                if (!idMinis.ContainsKey(instigator.CreatureId.ToString())) //XJ:(2022/10/25) If Mini not on the list add (id + name) to list.
                {
                    RuleSet5EPlugin.Instance.LoadDnd5(instigator);
                    idMinis.Add(instigator.CreatureId.ToString(), instigator.Name.ToString());
                }
                else if (idMinis[instigator.CreatureId.ToString()] != instigator.Name.ToString()) //XJ:(2022/10/25)  If Mini is on the list and name are changed then change name according to the id.
                {
                    RuleSet5EPlugin.Instance.LoadDnd5(instigator);
                    idMinis[instigator.CreatureId.ToString()] = instigator.Name.ToString();
                }
                //XJ: (2022/10/25) Only if mini are in the list and not change the mini name, not load stats.
            }            
        }

        private void RenderToolBarAddons() //XJ:(2022/11/27) Modified to use AssetData (Same bonuses for all players.)
        {            
            ControllerManager.KeyInputEnabled();  
            
          
            if (globalKeyboardDisabled == false) { GUI.FocusControl(null); } //XJ(2023/02/23) To avoid tab focus.            
            
            GUI.DrawTexture(ScreenSizeAdjustment(new Rect((float)Screen.width / 2f - 215f, 32f, 430f, 24f),true), backgroundTexture);
            reactionStop = GUI.Toggle(ScreenSizeAdjustment(new Rect((float)Screen.width / 2 - 215 + 10, 34, 40, 20), true), reactionStop, reactionStopIcon);
            bool tempAdv = GUI.Toggle(ScreenSizeAdjustment(new Rect((float)Screen.width / 2 - 215 + 50, 34, 30, 20), true), totalAdv, "+");
            bool tempDis = GUI.Toggle(ScreenSizeAdjustment(new Rect((float)Screen.width / 2 - 215 + 80, 34, 30, 20), true), totalDis, "-");
            //XJ:(2022/10/08)  Textfields allow 9 instead of 6 chars.            
            bool boolUseAttackBonusDie = GUI.Toggle(ScreenSizeAdjustment(new Rect((float)Screen.width / 2 - 100, 34, 25, 20), true), useAttackBonusDie, "A");            
            string strAmountAttackBonusDie = GUI.TextField(ScreenSizeAdjustment(new Rect((float)Screen.width / 2 - 70, 34, 40, 20), true), amountAttackBonusDie, 9);            
            bool boolUseDamageBonusDie = GUI.Toggle(ScreenSizeAdjustment(new Rect((float)Screen.width / 2 - 25, 34, 25, 20), true), useDamageBonusDie, "D");           
            string strAmountDamageBonusDie = GUI.TextField(ScreenSizeAdjustment(new Rect((float)Screen.width / 2 + 5, 34, 40, 20), true), amountDamageBonusDie, 9);
            bool boolUseSkillBonusDie = GUI.Toggle(ScreenSizeAdjustment(new Rect((float)Screen.width / 2 + 50, 34, 25, 20), true), useSkillBonusDie, "S");            
            string strAmountSkillBonusDie = GUI.TextField(ScreenSizeAdjustment(new Rect((float)Screen.width / 2 + 80, 34, 40, 20), true), amountSkillBonusDie, 9);
            bool boolUseACBonusDie = GUI.Toggle(ScreenSizeAdjustment(new Rect((float)Screen.width / 2 + 125, 34, 35, 20), true), useACBonusDie, "AC");
            string strAmountACBonusDie = GUI.TextField(ScreenSizeAdjustment(new Rect((float)Screen.width / 2 + 165, 34, 40, 20), true), amountACBonusDie, 9);
           
            int update = 0;
            if (tempDis != totalDis)
            {
                totalDis = tempDis;
                totalAdv = false;
                update = 1;
            }
            else if (tempAdv != totalAdv)
            {
                totalAdv = tempAdv;
                totalDis = false;
                update = 2;
            }
            if (useAttackBonusDie != boolUseAttackBonusDie) { useAttackBonusDie = boolUseAttackBonusDie; update = 3; }
            if (useDamageBonusDie != boolUseDamageBonusDie) { useDamageBonusDie = boolUseDamageBonusDie; update = 4; }
            if (useSkillBonusDie != boolUseSkillBonusDie) { useSkillBonusDie = boolUseSkillBonusDie; update = 5; }
            if (amountAttackBonusDie != strAmountAttackBonusDie) { amountAttackBonusDie = strAmountAttackBonusDie; update = 6; }
            if (amountDamageBonusDie != strAmountDamageBonusDie) { amountDamageBonusDie = strAmountDamageBonusDie; update = 7; }
            if (amountSkillBonusDie != strAmountSkillBonusDie) { amountSkillBonusDie = strAmountSkillBonusDie; update = 8; }
            if (useACBonusDie != boolUseACBonusDie) { useACBonusDie = boolUseACBonusDie; update = 9; }
            if (amountACBonusDie != strAmountACBonusDie) { amountACBonusDie = strAmountACBonusDie; update = 10; }
            if (update > 0)
            {
                if (diagnostics >= DiagnosticMode.high) { Debug.Log("RuleSet 5E Plugin: Toolbar Selection Changed (" + update + ")"); }
                CreatureBoardAsset asset;
                CreaturePresenter.TryGetAsset(LocalClient.SelectedCreatureId, out asset);
                if (asset != null)
                {
                    //XJ:2022/12/07 Future:
                    amountAttackBonusDie = Regex.Replace(amountAttackBonusDie, "[^0-9dD+-]", "");
                    amountDamageBonusDie = Regex.Replace(amountDamageBonusDie, "[^0-9dD+-]", "");
                    amountSkillBonusDie = Regex.Replace(amountSkillBonusDie, "[^0-9dD+-]", "");
                    amountACBonusDie = Regex.Replace(amountACBonusDie, "[^0-9dD+-]", "");

                    if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Valid Mini Selected For Update"); }

                    IdBonus idbonus = new IdBonus();

                    idbonus.name = Utility.GetCharacterName(asset.Name);
                    idbonus._useAttackBonusDie = useAttackBonusDie;
                    idbonus._useDamageBonusDie = useDamageBonusDie;
                    idbonus._useSkillBonusDie = useSkillBonusDie;
                    idbonus._useACBonusDie = useACBonusDie;
                    idbonus._amountAttackBonusDie = amountAttackBonusDie;
                    idbonus._amountDamageBonusDie = amountDamageBonusDie;
                    idbonus._amountSkillBonusDie = amountSkillBonusDie;
                    idbonus._amountACBonusDie = amountACBonusDie;
                    idbonus._useAdv = totalAdv;
                    idbonus._useDis = totalDis;

                    AssetDataPlugin.SetInfo(asset.CreatureId.ToString(), RuleSet5EPlugin.Guid + ".BonusData", idbonus);


                    if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Creature: " + asset.CreatureId.ToString()); }
                    if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin:" + idbonus.name.ToString()); }
                    if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin:" + idbonus._useAttackBonusDie.ToString()); }
                    if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin:" + idbonus._useDamageBonusDie.ToString()); }
                    if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin:" + idbonus._useSkillBonusDie.ToString()); }
                    if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin:" + idbonus._amountAttackBonusDie.ToString()); }
                    if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin:" + idbonus._amountDamageBonusDie.ToString()); }
                    if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin:" + idbonus._amountSkillBonusDie.ToString()); }
                    if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin:" + idbonus._useAdv.ToString()); }
                    if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin:" + idbonus._useDis.ToString()); }
                    if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin:" + idbonus._useACBonusDie.ToString()); }
                    if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin:" + idbonus._amountACBonusDie.ToString()); }
                }

            }
            if (dcAttack)
            {
                if (victim_totalAdv) { lastRollRequestTotal = RollTotal.advantage; }
                else if (victim_totalDis) { lastRollRequestTotal = RollTotal.disadvantage; }
                else { lastRollRequestTotal = RollTotal.normal; }

            }
            else
            { 
                if (totalAdv) { lastRollRequestTotal = RollTotal.advantage; }
                else if (totalDis) { lastRollRequestTotal = RollTotal.disadvantage; }
                else { lastRollRequestTotal = RollTotal.normal; }
            }
        }

        public Rect ScreenSizeAdjustment(Rect element, bool applySmallScreenConversion = false)
        {

            //if (applySmallScreenConversion && smallScreenConversion.x != 0 && smallScreenConversion.y != 0 && (Screen.width < 1920 || Screen.height < 1080))
            //{
            //    //    element.x = element.x + smallScreenConversion.x;
            //    //    element.y = element.y + smallScreenConversion.y;
            //    //}
            //    //else
            //    //{
            //    //    float scaleX = (float)Screen.width / 1920f;
            //    //    float scaleY = (float)Screen.height / 1080f;
            //    //    element.x = element.x * scaleX;
            //    //    element.y = element.y * scaleY;
            //   // element.y = element.y + 25;
            //    // element.y = GameSettings.UIScale * element.y;
            //}
            if (Screen.height > 1080) //(applySmallScreenConversion && Screen.height > 1080)
            {
                element.y = element.y + math.round( element.y + (Screen.height - 1080) / 22);               
            }

            element.y = element.y + uiLocY;
            element.x = element.x + uiLocX;
            return element;
        }
        public void StartSequencePre(string action, Roll roll, CreatureGuid cid, object obj, MapMenuItem mi) 
        {
            //XJ: Multitarget code:
            if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: StartSequencePre"); }
         
            CreaturePresenter.TryGetAsset(LocalClient.SelectedCreatureId, out instigator);
            if (multiTargetAssets.Count == 0) 
            {               
                CreaturePresenter.TryGetAsset(new CreatureGuid(RadialUI.RadialUIPlugin.GetLastRadialTargetCreature()), out victim);                
                if ((instigator.CreatureId == victim.CreatureId) && (action == "Attack" || action == "AttackDC"|| action == "Heal")) { selectRuleMode = true; multiTargetAssets.Clear(); MultitargetAssetsIndex = 0; multiRoll = roll; multiAttackType = action; } else { StartSequence(action, roll, cid, obj, mi); }
            } 
            else 
            {             
                if (MultitargetAssetsIndex < multiTargetAssets.Count)
                {
                    victim = multiTargetAssets[MultitargetAssetsIndex]; MultitargetAssetsIndex++; StartSequence(multiAttackType, multiRoll, instigator.CreatureId, obj, mi);
                }
                else { multiTargetAssets.Clear();MultitargetAssetsIndex = 0; multiRoll = null; multiAttackType = ""; }
            }
        }

        public void StartSequence(string action, Roll roll, CreatureGuid cid, object obj, MapMenuItem mi)
        {
            dcAttack = false;
            healSequence = false;            
            if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: StartSequence"); }
            if (instigator != null) { LoadBonus(instigator.CreatureId); }; //XJ: Correccion.
            if (victim != null) { LoadBonus(victim.CreatureId, true); };
            if (instigator != null && !characters.ContainsKey(Utility.GetCharacterName(instigator))) { LoadDnd5eJson(instigator); }
            if (victim != null && !characters.ContainsKey(Utility.GetCharacterName(victim))) { LoadDnd5eJson(victim); }
            if (characters.ContainsKey(Utility.GetCharacterName(victim)))
            {
                switch (action)
                {
                    case "Attack": Attack(roll, cid, obj, mi); break;
                    case "AttackDC": AttackDC(roll, cid, obj, mi); break;
                    case "Skill": Skill(roll, cid, obj, mi); break;
                    case "Save": Save(roll, cid, obj, mi); break;
                    case "Heal": Heal(roll, cid, obj, mi); break;
                }
            }
            else
            {               
                string messageInvalidtarget = "Invalid target: " + victim.Name.ToString();
                chatManager.SendChatMessageEx(messageInvalidtarget, messageInvalidtarget, messageInvalidtarget, instigator.CreatureId, LocalClient.Id.Value);               
                if (diagnostics >= DiagnosticMode.low) { Debug.Log("RuleSet 5E Plugin: Invalid target (" + victim.Name + "), does not have Dnd5e assigned"); }
                if (multiTargetAssets.Count != MultitargetAssetsIndex) { if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin:Invalid Target Multi"); }; StartSequencePre(multiAttackType, multiRoll, instigator.CreatureId, null, null); }
            }
          
        }
        private void Callback(AssetDataPlugin.DatumChange change) 
        {

        }
        private void Callback2(AssetDataPlugin.DatumChange change)
        {
            string changeString = (string)change.value;
            CreatureGuid creatureSpeaker;
            CreatureGuid.TryParse(changeString.Split('|')[0], out creatureSpeaker);
            CreaturePresenter.TryGetAsset(creatureSpeaker, out CreatureBoardAsset assetspeaker);
            if (diagnostics >= DiagnosticMode.high) { Debug.Log("RuleSet 5E Plugin: SpeakBubble"); };
            SpeakExtensions.SpeakExMessage(assetspeaker, changeString.Split('|')[1]);
        }

        //private void DisableKeyboardEvents(bool settings) //XJ: (2022/11/21)Disable keyboard when editing buff text.
        //{
        //    if (settings == true)
        //    {
        //        //
        //        // Turn off processing keyboard for non-text areas entry (e.g. prevent core TS keyboard events)
        //        //
        //        if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Disabling Keyboard Processing"); }                
        //        ControllerManager.GameInput.Disable();
        //    }
        //    else
        //    {
        //        //
        //        // Turn on processing keyboard for non-text areas entry (e.g. prevent core TS keyboard events)
        //        //
        //        if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Enabling Keyboard Processing"); }
        //        ControllerManager.GameInput.Enable();                
        //    }
        //}


        private void DisableKeyboardEvents(bool setting)
        {
            if (gameInputInstance == null || gameInputDisable == null || gameInputEnable == null)
            {
                try
                {
                    if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: GameInputEnabled()"); }
                    gameInputInstance = null;
                    gameInputDisable = null;
                    gameInputEnable = null;
                    gameInputInstance = (GameInput)(typeof(ControllerManager).GetRuntimeFields().Where(f => f.Name == "_gameInput").ToArray()[0].GetValue(null));
                    gameInputDisable = (typeof(GameInput).GetMethods().Where(m => m.Name == "Disable").ElementAt(0));
                    gameInputEnable = (typeof(GameInput).GetMethods().Where(m => m.Name == "Enable").ElementAt(0));
                }
                catch (Exception e)
                {
                   Debug.LogWarning("RuleSet 5E Plugin: GameInputEnabled exception:" + e.Message.ToString()); 
                }
            }
            if (!setting)
            {
                if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: GameInputEnabled() ENABLED"); }
                gameInputEnable.Invoke(gameInputInstance, new object[] { });
                globalKeyboardDisabled = false;
            }
            else
            {
                if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: GameInputEnabled() DISABLED"); }
                gameInputDisable.Invoke(gameInputInstance, new object[] { });
                globalKeyboardDisabled = true;                
            }
        }

        public void LoadBonus(CreatureGuid cid, bool victim =  false) //XJ:(2022/11/27) Modified to use AssetData (Same bonuses for all players.)
        {
            if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: LoadBonus"); }

            IdBonus idbonus = new IdBonus();         
   
       

            string json = AssetDataPlugin.ReadInfo(cid.ToString(), RuleSet5EPlugin.Guid + ".BonusData");
            if (json != null)
            {
                idbonus = JsonConvert.DeserializeObject<IdBonus>(json);
            }       
            
            if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: LoadBonus: " + cid.ToString()); }          
            if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Restoring " + idbonus._amountAttackBonusDie + "/" + idbonus._amountDamageBonusDie + "/" + idbonus._amountSkillBonusDie + "/"+idbonus._amountACBonusDie); }
            if (victim == false)
            {
                useAttackBonusDie = idbonus._useAttackBonusDie;
                useDamageBonusDie = idbonus._useDamageBonusDie;
                useSkillBonusDie = idbonus._useSkillBonusDie;
                useACBonusDie = idbonus._useACBonusDie;
                amountAttackBonusDie = idbonus._amountAttackBonusDie;
                amountDamageBonusDie = idbonus._amountDamageBonusDie;
                amountSkillBonusDie = idbonus._amountSkillBonusDie;
                amountACBonusDie = idbonus._amountACBonusDie;   
                totalAdv = idbonus._useAdv;
                totalDis = idbonus._useDis;
            }
            else 
            {
                pauseRender = true;
                victim_useSkillBonusDie = idbonus._useSkillBonusDie;
                victim_amountSkillBonusDie = idbonus._amountSkillBonusDie;
                if (int.TryParse(idbonus._amountACBonusDie, out int nf) & idbonus._useACBonusDie) { victim_amountACBonusDie = idbonus._amountACBonusDie; } else { victim_amountACBonusDie ="" ;} 
                victim_totalAdv = idbonus._useAdv;
                victim_totalDis = idbonus._useDis;
                pauseRender = false;
            }
        }
    }
}
