using BepInEx;
using Bounce.Unmanaged;
using GameChat.UI;
using HarmonyLib;
using Spaghet.Compiler.Ops;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using UnityEngine;

namespace LordAshes //XJ: (2022/11/27)  Add: Replace(" ","\x255") to avoid player's name with spaces.
{
    public partial class RuleSet5EPlugin : BaseUnityPlugin
    {
        [HarmonyPatch(typeof(UIChatMessageManager), "AddChatMessage")]
        public static class PatchAddChatMessage
        {
            public static bool Prefix(ref string creatureName, Texture2D icon, ref string chatMessage, UIChatMessageManager.IChatFocusable focus = null)
            {
                if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Patch: Checking Message Content"); }
                if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Creature Name" + creatureName + " | ChatMessage: " + chatMessage); }
                if (chatMessage != null)
                {                    
                    chatMessage = chatMessage.Replace("(Whisper)", "").Trim();
                    if (chatMessage.StartsWith("[") && chatMessage.Contains("]"))
                    {
                        creatureName = chatMessage.Substring(0, chatMessage.IndexOf("]"));
                        creatureName = creatureName.Substring(1);
                        if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Patch: Speaker Changed To '" + creatureName + "'"); }
                        chatMessage = chatMessage.Substring(chatMessage.IndexOf("]") + 1);
                    }
                }
                return true;
            }
        }
    }

    /// <summary>
    /// Extension methods
    /// </summary>
    public static class SpeakExtensions
    {
        /// <summary>
        /// SpawnAt methods sets or clears the forced spawn position and orientation
        /// </summary>
        /// <param name="creature">Speaking creature</param>
        /// <param name="text">Content to be spoken</param>

        public static void SpeakEx(this CreatureBoardAsset creature, string text)
        {    
                AssetDataPlugin.SendInfo(RuleSet5EPlugin.Guid + ".Bubble",creature.CreatureId.ToString() +"|"+ text); ////XJ(2023/02/23) All players can see Bubbles.
        }
        public static void SpeakExMessage(this CreatureBoardAsset creature, string text)
        {
            if(LordAshes.RuleSet5EPlugin.rollingSystem != RuleSet5EPlugin.RollMode.manual_side)
            {
                creature.Speak(text);              
            }
            else
            {
                RuleSet5EPlugin.Instance.StartCoroutine(RuleSet5EPlugin.Instance.DisplayMessage(RuleSet5EPlugin.Utility.GetCharacterName(creature)+": "+text,3f));
            }
        }

        /// <summary>
        /// Method to send, potentially different, chat messages to players, owner and GM
        /// </summary>
        /// <param name="chatManager">Insatnce of Chat Manager (not used since SendChatMessage is static</param>
        /// <param name="playersMessage">Message to be sent to all players (including owner and GM)</param>
        /// <param name="ownerMessage">Message to be sent to owner only</param>
        /// <param name="gmMessage">Message to be sent to GM only</param>
        /// <param name="speaker">Guid of the speaker</param>
        public static void SendChatMessageEx(this ChatManager chatManager, string playersMessage, string ownerMessage, string gmMessage, CreatureGuid subject, NGuid speaker)
        {
            List<PlayerGuid> gms = RuleSet5EPlugin.Utility.FindGMs();
            List<PlayerGuid> owners = RuleSet5EPlugin.Utility.FindOwners(subject);
            if (gmMessage!=null)
            {
                foreach (PlayerGuid gmName in gms)
                {
                    if (LordAshes.RuleSet5EPlugin.diagnostics >= LordAshes.RuleSet5EPlugin.DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Chat Extension: Sending Chat Message To GM '" + gmName + "' Content: " + gmMessage.Replace("\r\n", "|")); }
                    // ChatManager.SendChatMessage("/w " + gmName.Replace(" ","\x255") + " " + gmMessage, speaker);                    
                    ChatManager.SendChatMessageToGms(gmMessage, speaker);
                }
            }
            if(ownerMessage!=null)
            {
                foreach (PlayerGuid ownerName in owners)
                {
                    if (!gms.Contains(ownerName))
                    {
                        if (LordAshes.RuleSet5EPlugin.diagnostics >= LordAshes.RuleSet5EPlugin.DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Chat Extension: Sending Chat Message To Owner '" + ownerName + "' Content: " + gmMessage.Replace("\r\n", "|")); }
                        // ChatManager.SendChatMessage("/w " + ownerName.Replace(" ","\x255") + " " + ownerMessage, speaker);
                        
                        ChatManager.SendChatMessageToPlayer(ownerMessage, ownerName, speaker);
                    }
                }
            }
            if (playersMessage != null)
            {
                foreach (PlayerGuid pid in CampaignSessionManager.PlayersInfo.Keys)
                {
                    //string playerName = CampaignSessionManager.GetPlayerName(pid);
                    if (!gms.Contains(pid) && !owners.Contains(pid))
                    {
                        if (LordAshes.RuleSet5EPlugin.diagnostics >= LordAshes.RuleSet5EPlugin.DiagnosticMode.ultra) { Debug.Log("RuleSet 5E Plugin: Chat Extension: Sending Chat Message To Player '" + CampaignSessionManager.GetPlayerName(pid) + "' Content: " + playersMessage.Replace("\r\n", "|")); }
                        //ChatManager.SendChatMessage("/w "+ CampaignSessionManager.GetPlayerName(pid).Replace(" ","\x255") + " "+playersMessage, speaker);
                        ChatManager.SendChatMessageToPlayer(playersMessage,pid, speaker);  
                    }
                }
            }
        }
    }
}
