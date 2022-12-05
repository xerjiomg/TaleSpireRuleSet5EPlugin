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
        public const string Version = "2.1.0.0";

        // Reference to plugin instance
        public static RuleSet5EPlugin Instance = null;

        // User configurations
        private string iconSelector = "type";

        // Character dictionary
        private Dictionary<string, Character> characters = new Dictionary<string, Character>();

        //XJ: Id + Character dictionary
        private  Dictionary<string, string> idMinis = new Dictionary<string, string>();

        //XJ: Id Bonus
        private  Dictionary<CreatureGuid, IdBonus> IdBonusList = new Dictionary<CreatureGuid, IdBonus>();

        // Last selected
        CreatureGuid lastSelectedMini = CreatureGuid.Empty;

        // Private variables
        private Texture reactionStopIcon = null;
        private bool reactionStopContinue = false;
        //XJ: change: private int reactionRollTotal = 0;
        private string reactionRollTotal = "NoInfo";
        private bool reactionHalve = false;
        //XJ: To show more information about roll attack in reaction. And add Halve option
        //XJ:(2022/10/12)
        private bool dcAttack = false;
        //XJ: To add DC attack state
        private Vector2 smallScreenConversion = new Vector2(-1200, 40);
        private bool pauseRender = false; 
        /// <summary>
        /// Function for initializing plugin
        /// This function is called once by TaleSpire
        /// </summary>


        void Awake()
        {
            UnityEngine.Debug.Log("RuleSet 5E Plugin: Active.");
            Instance = this;

            var harmony = new Harmony(Guid);
            harmony.PatchAll();

            AssetDataPlugin.Subscribe(RuleSet5EPlugin.Guid + ".BonusData", Callback); ////XJ: TEST REMOVE PLEASE
           

            if (diagnostics >= DiagnosticMode.high) { Debug.Log("RuleSet 5E Plugin: CurrentCulture:" + Thread.CurrentThread.CurrentCulture.ToString()); }

            // Read and apply configuration settings
            iconSelector = Config.Bind("Appearance", "Attack Icons Base On", "type").Value;
            string[] existence = Config.Bind("Appearance", "Dice Side Existance", "-100,0,0,45,0,0").Value.Split(',');
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

            string[] smallScreenConversionString = Config.Bind("Setting", "Small Screen Offset", "-1200,40").Value.Split(',');
            smallScreenConversion = new Vector2(float.Parse(smallScreenConversionString[0], System.Globalization.CultureInfo.InvariantCulture), float.Parse(smallScreenConversionString[1], System.Globalization.CultureInfo.InvariantCulture));
            rollingSystem = Config.Bind("Settings", "Rolling Style", RollMode.automaticDice).Value;
            processSpeed = (Config.Bind("Settings", "Process Delay Percentage", 100).Value / 100);

            //XJ (2022/10/14)
            changeBaseColors = Config.Bind("Auto Color Base Settings", "Auto Color Base", false).Value;
            npcColors = Config.Bind("Auto Color Base Settings", "Npc Colors", "2,13,1").Value.Split(',');
            pcColors = Config.Bind("Auto Color Base Settings", "PC Colors", "6,7,8").Value.Split(',');
            //XJ: Config AutoChangeBaseColors
            //XJ (2022/11/09) Level of detail log config
            diagnostics = Config.Bind("Troubleshooting", "Diagnostic Mode", DiagnosticMode.low).Value;

            if (diagnostics >= DiagnosticMode.high) { Debug.Log("RuleSet 5E Plugin: Dice Side Location = " + diceSideExistance.position); }

            if (diagnostics >= DiagnosticMode.high) { Debug.Log("RuleSet 5E Plugin: Speed = " + processSpeed + "x"); }

            RadialUI.RadialUIPlugin.RemoveCustomButtonOnCharacter("Attacks");

            RadialUI.RadialSubmenu.EnsureMainMenuItem(RuleSet5EPlugin.Guid + ".Attacks", RadialUI.RadialSubmenu.MenuType.character, "Scripted Attacks", FileAccessPlugin.Image.LoadSprite("Attack.png"));

            //XJ: add (2022/10/12)
            RadialUI.RadialSubmenu.EnsureMainMenuItem(RuleSet5EPlugin.Guid + ".AttacksDC", RadialUI.RadialSubmenu.MenuType.character, "Scripted DC Attacks", FileAccessPlugin.Image.LoadSprite("Magic.png"));
            //XJ Create new radial menu: DC Attacks.
            RadialUI.RadialSubmenu.EnsureMainMenuItem(RuleSet5EPlugin.Guid + ".Saves", RadialUI.RadialSubmenu.MenuType.character, "Saves", FileAccessPlugin.Image.LoadSprite("Saves.png"));
            RadialUI.RadialSubmenu.EnsureMainMenuItem(RuleSet5EPlugin.Guid + ".Skills", RadialUI.RadialSubmenu.MenuType.character, "Skills", FileAccessPlugin.Image.LoadSprite("Skills.png"));
            RadialUI.RadialSubmenu.EnsureMainMenuItem(RuleSet5EPlugin.Guid + ".Healing", RadialUI.RadialSubmenu.MenuType.character, "Healing", FileAccessPlugin.Image.LoadSprite("Healing.png"));
            //XJ: Add 2022/10/16)                        
            //RadialUI.RadialUIPlugin.AddCustomButtonGMSubmenu(RuleSet5EPlugin.Guid, new MapMenu.ItemArgs { Title = "Load Stats", CloseMenuOnActivate = true, Icon = FileAccessPlugin.Image.LoadSprite("Load.png"), Action = LoadDnd52 });
            // XJ Create new sub menu on GM radial button: dump dnd5 data into game variables.

            reactionStopIcon = FileAccessPlugin.Image.LoadTexture("/" + RuleSet5EPlugin.Guid + "/"+"ReactionStop.png");

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
                    if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: ClickEvent"); }
                    Rect attackbonus = ScreenSizeAdjustment(new Rect(1375, 5, 40, 20), true);                    
                    if (Screen.height - Input.mousePosition.y > attackbonus.y & Screen.height - Input.mousePosition.y < attackbonus.y + attackbonus.height) 
                    {
                        if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Click y enter"); }
                        Rect damagebonus = ScreenSizeAdjustment(new Rect(1450, 5, 40, 20), true);
                        Rect skillbonus = ScreenSizeAdjustment(new Rect(1525, 5, 40, 20), true);
                        if (Input.mousePosition.x > attackbonus.x & Input.mousePosition.x < attackbonus.x + attackbonus.width ||  Input.mousePosition.x > damagebonus.x & Input.mousePosition.x < damagebonus.x + damagebonus.width || Input.mousePosition.x > skillbonus.x & Input.mousePosition.x < skillbonus.x + skillbonus.width)
                        {
                            if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Click x + y enter"); }
                            DisableKeyboardEvents(true);
                        }
                        else { DisableKeyboardEvents(false); }
                    }
                    else { DisableKeyboardEvents(false); }
                }
                if (Input.GetMouseButtonUp(0)) { }          
            }
        }

        void OnGUI()
        {
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
                GUI.Label(ScreenSizeAdjustment(new Rect(0f, 40f, 1920, 30)), messageContent, gs1);
                GUI.Label(ScreenSizeAdjustment(new Rect(3f, 43f, 1920, 30)), messageContent, gs2);
            }

            if (reactionStopContinue)
            {
                //XJ:(2022/10/08) Added two new reactions: 
                GUIStyle gs2 = new GUIStyle();
                gs2.normal.textColor = Color.yellow;
                gs2.alignment = TextAnchor.UpperCenter;
                gs2.fontSize = 32;
                GUI.Label(ScreenSizeAdjustment(new Rect((1920f / 2f) - 40f, 35, 80, 30)), "Roll: " + reactionRollTotal, gs2);
                if (GUI.Button(ScreenSizeAdjustment(new Rect((1920f / 2f) - 130f, 70, 40, 30)), "Hit"))
                {
                    reactionStopContinue = false;
                    string message = "Forced Normal Hit Reaction Used";
                    lastResult["IsMax"] = false;
                    chatManager.SendChatMessageEx(message, message, message, instigator.CreatureId, LocalClient.Id.Value);
                    stateMachineState = StateMachineState.attackAttackHitReport;
                }
                if (GUI.Button(ScreenSizeAdjustment(new Rect((1920f / 2f) - 220f, 70, 80, 30)), "Critical"))
                {
                    reactionStopContinue = false;
                    string message = "Forced Critical Hit Reaction Used";
                    lastResult["IsMax"] = true;
                    chatManager.SendChatMessageEx(message, message, message, instigator.CreatureId, LocalClient.Id.Value);
                    stateMachineState = StateMachineState.attackAttackHitReport;
                }
                if (GUI.Button(ScreenSizeAdjustment(new Rect((1920f / 2f) - 85f, 70, 80, 30)), "Continue"))
                {
                    reactionStopContinue = false;
                    stateMachineState = StateMachineState.attackAttackDieRollReport;
                    //XJ: 2022/10/18
                    if (secureSuccess) { stateMachineState = StateMachineState.attackAttackHitReport; }
                    //XJ: If secure hit, change state.
                }
                if (GUI.Button(ScreenSizeAdjustment(new Rect((1920f / 2f) + 5f, 70, 80, 30)), "Cancel"))
                {
                    reactionStopContinue = false;
                    string message = "Cancel Attack Reaction Used";
                    chatManager.SendChatMessageEx(message, message, message, instigator.CreatureId, LocalClient.Id.Value);
                    stateMachineState = StateMachineState.attackRollCleanup;
                }
                if (GUI.Button(ScreenSizeAdjustment(new Rect((1920f / 2f) + 90f, 70, 40, 30)), "Miss"))
                {
                    reactionStopContinue = false;
                    string message = "Miss Reaction Used";
                    chatManager.SendChatMessageEx(message, message, message, instigator.CreatureId, LocalClient.Id.Value);
                    stateMachineState = StateMachineState.attackAttackMissReport;
                }
                if (GUI.Button(ScreenSizeAdjustment(new Rect((1920f / 2f) + 140f, 70, 60, 30)), "Halve"))
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
            dcAttack = true;
            Roll find = lastRollRequest;
            while (true)
            {
                if (diagnostics >= DiagnosticMode.high) { Debug.Log("Damage Stack: " + find.name + " : " + find.type + " : " + find.roll); }
                find = find.link;
                if (find == null) { break; }
            }
            //CreaturePresenter.TryGetAsset(LocalClient.SelectedCreatureId, out instigator);
            //CreaturePresenter.TryGetAsset(new CreatureGuid(RadialUI.RadialUIPlugin.GetLastRadialTargetCreature()), out victim);
            if (instigator != null && victim != null) { stateMachineState = StateMachineState.attackAttackRangeCheck; }
        }
        //XJ:To add DC attacks
        public void Attack(Roll roll, CreatureGuid cid, object obj, MapMenuItem mi)
        {
            if (diagnostics >= DiagnosticMode.low) { Debug.Log("RuleSet 5E Plugin: Attack: " + roll.name); }
            lastRollRequest = new Roll(roll);
            if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Attack: " + lastRollRequest.name); }
            dcAttack = false;
            Roll find = lastRollRequest;
            while (true)
            {
                if (diagnostics >= DiagnosticMode.high) { Debug.Log("Damage Stack: " + find.name + " : " + find.type + " : " + find.roll); }
                find = find.link;
                if (find == null) { break; }
            }
            //CreaturePresenter.TryGetAsset(LocalClient.SelectedCreatureId, out instigator);            
            //CreaturePresenter.TryGetAsset(new CreatureGuid(RadialUI.RadialUIPlugin.GetLastRadialTargetCreature()), out victim);            
            if (instigator != null && victim != null) { stateMachineState = StateMachineState.attackAttackRangeCheck; }
        }

        public void Skill(Roll roll, CreatureGuid cid, object obj, MapMenuItem mi)
        {
            if (diagnostics >= DiagnosticMode.low) { Debug.Log("RuleSet 5E Plugin: Save: " + roll.name); }
            lastRollRequest = roll;
            if (diagnostics >= DiagnosticMode.high) { Debug.Log("Roll: " + roll.roll); }
            //CreaturePresenter.TryGetAsset(LocalClient.SelectedCreatureId, out instigator);
            if (instigator != null) { stateMachineState = StateMachineState.skillRollSetup; }
        }

        public void Save(Roll roll, CreatureGuid cid, object obj, MapMenuItem mi)
        {
            if (diagnostics >= DiagnosticMode.low) { Debug.Log("RuleSet 5E Plugin: Skill: " + roll.name); }
            lastRollRequest = roll;
            if (diagnostics >= DiagnosticMode.high) { Debug.Log("Roll: " + roll.roll); }
            //CreaturePresenter.TryGetAsset(LocalClient.SelectedCreatureId, out instigator);
            if (instigator != null) { stateMachineState = StateMachineState.skillRollSetup; }
        }

        public void Heal(Roll roll, CreatureGuid cid, object obj, MapMenuItem mi)
        {
            if (diagnostics >= DiagnosticMode.low) { Debug.Log("RuleSet 5E Plugin: Heal: " + roll.name); }
            lastRollRequest = roll;
            if (diagnostics >= DiagnosticMode.high) { Debug.Log("Roll: " + roll.roll); }
            //CreaturePresenter.TryGetAsset(LocalClient.SelectedCreatureId, out instigator);
            //CreaturePresenter.TryGetAsset(new CreatureGuid(RadialUI.RadialUIPlugin.GetLastRadialTargetCreature()), out victim);
            if (instigator != null && victim != null) { stateMachineState = StateMachineState.healingRollStart; }
        }

        //XJ:(2022/10/11) add:
        public void LoadDnd5(CreatureBoardAsset instigator)
        // public void LoadDnd5(MapMenuItem ar1, object ar2)
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
                if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Loading Character '/" + Utility.GetCharacterName(instigator.Name) + ".Dnd5e'"); }
                if (FileAccessPlugin.File.Exists("/" + Utility.GetCharacterName(instigator.Name) + ".Dnd5e"))
                {                    
                    string fileName = "'/" + Utility.GetCharacterName(instigator.Name) + ".Dnd5e'"; 
                    try
                    {
                        fileName = FileAccessPlugin.File.Find("/" + Utility.GetCharacterName(instigator.Name) + ".Dnd5e")[0];
                        if (diagnostics >= DiagnosticMode.low) { Debug.Log("RuleSet 5E Plugin: Loading Character '" + characterName + "' From '" + fileName + "'"); }
                        string json = FileAccessPlugin.File.ReadAllText(fileName);
                        if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Loading Character '" + characterName + "' Contains:\r\n"+json); }
                        characters.Add(characterName, JsonConvert.DeserializeObject<Character>(json));
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning("RuleSet 5E Plugin: " + "Cannot read dnd5e file '"+fileName+"': " + e);
                    }

                    try
                    {
                        foreach (Roll roll in characters[characterName].attacksDC)
                        {
                            if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Adding Character '" + characterName + "' attacksDC '" + roll.name + "'"); }
                            string rollIconSelector = PatchAssistant.GetField(roll, iconSelector) + ".png";
                                                        
                            RadialUI.RadialSubmenu.CreateSubMenuItem(
                                                                        RuleSet5EPlugin.Guid + ".AttacksDC",
                                                                        roll.name,
                                                                        FileAccessPlugin.Image.LoadSprite("/" + RuleSet5EPlugin.Guid + "/" + rollIconSelector),
                                                                        (cid, obj, mi) => StartSequence("AttackDC", roll, cid, obj, mi),
                                                                        true,
                                                                        () => { return Utility.CharacterCheck(characterName, roll.name); }
                                                                    );
                        }
                        //XJ Create new radial menu: DC Attack

                        foreach (Roll roll in characters[characterName].attacks)
                        {
                            if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Adding Character '" + characterName + "' Attack '" + roll.name + "'"); }
                            string rollIconSelector = PatchAssistant.GetField(roll, iconSelector) + ".png";


                            RadialUI.RadialSubmenu.CreateSubMenuItem(
                                                                        RuleSet5EPlugin.Guid + ".Attacks",
                                                                        roll.name,
                                                                        FileAccessPlugin.Image.LoadSprite("/" + RuleSet5EPlugin.Guid + "/" + rollIconSelector),
                                                                        (cid, obj, mi) => StartSequence("Attack", roll, cid, obj, mi),
                                                                        true,
                                                                        () => { return Utility.CharacterCheck(characterName, roll.name); }
                                                                    );
                        }

                        foreach (Roll roll in characters[characterName].saves)
                        {
                            if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Adding Character '" + characterName + "' Save '" + roll.name + "'"); }

                            RadialUI.RadialSubmenu.CreateSubMenuItem(
                                                                        RuleSet5EPlugin.Guid + ".Saves",
                                                                        roll.name,
                                                                        (FileAccessPlugin.File.Exists("/" + RuleSet5EPlugin.Guid + "/save_" + roll.name + ".png") == true) ? FileAccessPlugin.Image.LoadSprite("/" + RuleSet5EPlugin.Guid + "/save_" + roll.name + ".png") : FileAccessPlugin.Image.LoadSprite("/" + RuleSet5EPlugin.Guid + "/" + "Saves.png"),
                                                                        (cid, obj, mi) => StartSequence("Save", roll, cid, obj, mi),
                                                                        true,
                                                                        () => { return Utility.CharacterCheck(characterName, roll.name); }
                                                                    );
                        }

                        foreach (Roll roll in characters[characterName].skills)
                        {
                            Debug.Log("RuleSet 5E Plugin: Adding Character '" + characterName + "' Skill '" + roll.name + "'");

                            RadialUI.RadialSubmenu.CreateSubMenuItem(
                                                                        RuleSet5EPlugin.Guid + ".Skills",
                                                                        roll.name,
                                                                        (FileAccessPlugin.File.Exists("/" + RuleSet5EPlugin.Guid + "/" + roll.name + ".png") == true) ? FileAccessPlugin.Image.LoadSprite("/" + RuleSet5EPlugin.Guid + "/" + roll.name + ".png") : FileAccessPlugin.Image.LoadSprite("/" + RuleSet5EPlugin.Guid + "/" + "Skills.png"),
                                                                        (cid, obj, mi) => StartSequence("Skill", roll, cid, obj, mi),
                                                                        true,
                                                                        () => { return Utility.CharacterCheck(characterName, roll.name); }
                                                                    );
                        }

                        foreach (Roll roll in characters[characterName].healing)
                        {
                            if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Adding Character '" + characterName + "' Healing '" + roll.name + "'"); }

                            RadialUI.RadialSubmenu.CreateSubMenuItem(
                                                                        RuleSet5EPlugin.Guid + ".Healing",
                                                                        roll.name,
                                                                        (FileAccessPlugin.File.Exists("/" + RuleSet5EPlugin.Guid + "/" + roll.name + ".png") == true) ? FileAccessPlugin.Image.LoadSprite("/" + RuleSet5EPlugin.Guid + "/" + roll.name + ".png") : FileAccessPlugin.Image.LoadSprite("/" + RuleSet5EPlugin.Guid + "/" + "Healing.png"),
                                                                        (cid, obj, mi) => StartSequence("Heal", roll, cid, obj, mi),
                                                                        true,
                                                                        () => { return Utility.CharacterCheck(characterName, roll.name); }
                                                                    );
                        }
                    }
                    catch(Exception e)
                    {
                        Debug.LogWarning("RuleSet 5E Plugin: Cannot read create radial menu entries. " + e);
                    }

                    try
                    {
                        RuleSet5EPlugin.Instance.LoadDnd5(instigator);
                    }
                    catch(Exception e)
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
            }
            else
            {
                if (diagnostics >= DiagnosticMode.ultra) { Debug.LogWarning("RuleSet 5E Plugin: Character '" + instigator.Name + "' Already Added."); }

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
            reactionStop = GUI.Toggle(ScreenSizeAdjustment(new Rect(1240, 5, 40, 20),true), reactionStop, reactionStopIcon);
            bool tempAdv = GUI.Toggle(ScreenSizeAdjustment(new Rect(1280, 5, 30, 20),true), totalAdv, "+");
            bool tempDis = GUI.Toggle(ScreenSizeAdjustment(new Rect(1310, 5, 30, 20),true), totalDis, "-");
            //XJ:(2022/10/08)  Textfields allow 9 instead of 6 chars.            
            bool boolUseAttackBonusDie = GUI.Toggle(ScreenSizeAdjustment(new Rect(1345, 5, 25, 20),true), useAttackBonusDie, "A");
            string strAmountAttackBonusDie = GUI.TextField(ScreenSizeAdjustment(new Rect(1375, 5, 40, 20), true), amountAttackBonusDie, 9);
            bool boolUseDamageBonusDie = GUI.Toggle(ScreenSizeAdjustment(new Rect(1420, 5, 25, 20), true), useDamageBonusDie, "D");
            string strAmountDamageBonusDie = GUI.TextField(ScreenSizeAdjustment(new Rect(1450, 5, 40, 20), true), amountDamageBonusDie, 9);
            bool boolUseSkillBonusDie = GUI.Toggle(ScreenSizeAdjustment(new Rect(1495, 5, 25, 20), true), useSkillBonusDie, "S");
            string strAmountSkillBonusDie = GUI.TextField(ScreenSizeAdjustment(new Rect(1525, 5, 40, 20), true), amountSkillBonusDie, 9);            
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
            if (update>0)
            {
                if (diagnostics >= DiagnosticMode.high) { Debug.Log("RuleSet 5E Plugin: Toolbar Selection Changed (" + update + ")"); }
                CreatureBoardAsset asset;
                CreaturePresenter.TryGetAsset(LocalClient.SelectedCreatureId, out asset);
                if (asset != null)
                {
                    if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Valid Mini Selected For Update"); }

                    IdBonus  idbonus =  new IdBonus();
                    
                    idbonus.name = Utility.GetCharacterName(asset.Name);                   
                    idbonus._useAttackBonusDie = useAttackBonusDie;            
                    idbonus._useDamageBonusDie = useDamageBonusDie;
                    idbonus._useSkillBonusDie = useSkillBonusDie;
                    idbonus._amountAttackBonusDie = amountAttackBonusDie;
                    idbonus._amountDamageBonusDie = amountDamageBonusDie;
                    idbonus._amountSkillBonusDie = amountSkillBonusDie;

                    AssetDataPlugin.SetInfo(asset.CreatureId.ToString(), RuleSet5EPlugin.Guid + ".BonusData", idbonus);
                   

                    if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Creature: " + asset.CreatureId.ToString()); }
                    if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin:" + idbonus.name.ToString()); }
                    if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin:" + idbonus._useAttackBonusDie.ToString()); }
                    if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin:" + idbonus._useDamageBonusDie.ToString()); }
                    if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin:" + idbonus._useSkillBonusDie.ToString()); }
                    if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin:" + idbonus._amountAttackBonusDie.ToString()); }
                    if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin:" + idbonus._amountDamageBonusDie.ToString()); }
                    if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin:" + idbonus._amountSkillBonusDie.ToString()); }
                }
                                                
            }
            if (totalAdv) { lastRollRequestTotal = RollTotal.advantage; }
            else if (totalDis) { lastRollRequestTotal = RollTotal.disadvantage; }
            else { lastRollRequestTotal = RollTotal.normal; }
        }

        public Rect ScreenSizeAdjustment(Rect element, bool applySmallScreenConversion = false)
        {
            if (applySmallScreenConversion && smallScreenConversion.x != 0 && smallScreenConversion.y != 0 && (Screen.width<1920 || Screen.height<1080))
            {
                element.x = element.x + smallScreenConversion.x;
                element.y = element.y + smallScreenConversion.y;
            }
            else
            {
                float scaleX = (float)Screen.width / 1920f;
                float scaleY = (float)Screen.height / 1080f;
                element.x = element.x * scaleX;
                element.y = element.y * scaleY;
            }
            return element;
        }
        public void StartSequence(string action, Roll roll, CreatureGuid cid, object obj, MapMenuItem mi)
        {
            CreaturePresenter.TryGetAsset(LocalClient.SelectedCreatureId, out instigator);
            CreaturePresenter.TryGetAsset(new CreatureGuid(RadialUI.RadialUIPlugin.GetLastRadialTargetCreature()), out victim);
            if (victim != null) { LoadBonus(victim.CreatureId,true); };
            if (instigator != null && !characters.ContainsKey(Utility.GetCharacterName(instigator))) { LoadDnd5eJson(instigator);}
            if (victim != null && !characters.ContainsKey(Utility.GetCharacterName(victim))){LoadDnd5eJson(victim);}

            if (characters.ContainsKey(Utility.GetCharacterName(victim)))
            {
                criticalImmunity = false;
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
                string messageInvalidtarget = "Invalid target: [" + victim.Name.ToString()+"]";
                chatManager.SendChatMessageEx(messageInvalidtarget, messageInvalidtarget, messageInvalidtarget, instigator.CreatureId, LocalClient.Id.Value);
                if (diagnostics >= DiagnosticMode.low) { Debug.Log("RuleSet 5E Plugin: Invalid target (" + victim.Name + "), does not have Dnd5e assigned"); }
            }
        }
        private void Callback(AssetDataPlugin.DatumChange change) //XJ: TEST REMOVE PLEASE
        {
            //IdBonus btemp = new IdBonus();
            //btemp = (IdBonus) change.value;

        }

        private void DisableKeyboardEvents(bool settings) //XJ: (2022/11/21)Disable keyboard when editing buff text.
        {
            if (settings == true)
            {
                //
                // Turn off processing keyboard for non-text areas entry (e.g. prevent core TS keyboard events)
                //
                if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Disabling Keyboard Processing"); } 
                
                ControllerManager.GameInput.Disable();
            }
            else
            {
                //
                // Turn on processing keyboard for non-text areas entry (e.g. prevent core TS keyboard events)
                //
                if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Enabling Keyboard Processing"); }
                ControllerManager.GameInput.Enable();                
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
            if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Restoring " + idbonus._amountAttackBonusDie + "/" + idbonus._amountDamageBonusDie + "/" + idbonus._amountSkillBonusDie); }
            if (victim == false)
            {
                useAttackBonusDie = idbonus._useAttackBonusDie;
                useDamageBonusDie = idbonus._useDamageBonusDie;
                useSkillBonusDie = idbonus._useSkillBonusDie;
                amountAttackBonusDie = idbonus._amountAttackBonusDie;
                amountDamageBonusDie = idbonus._amountDamageBonusDie;
                amountSkillBonusDie = idbonus._amountSkillBonusDie;
            }
            else 
            {
                pauseRender = true;
                victim_useSkillBonusDie = idbonus._useSkillBonusDie; 
                victim_amountSkillBonusDie = idbonus._amountSkillBonusDie;
                pauseRender = false;
            }
        }
    }
}
