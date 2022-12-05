using BepInEx;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace LordAshes
{
    public partial class RuleSet5EPlugin : BaseUnityPlugin
    {
        public class Character
        {
            public bool NPC { get; set; } = false;
            public int reach { get; set; } = 5;
            public List<Roll> attacks { get; set; } = new List<Roll>();
            //XJ:(2022/10/12)
            public List<Roll> attacksDC { get; set; } = new List<Roll>();
            //XJ: to add DC attacks
            public List<Roll> saves { get; set; } = new List<Roll>();
            public List<Roll> skills { get; set; } = new List<Roll>();
            public List<Roll> healing { get; set; } = new List<Roll>();
            public List<string> resistance { get; set; } = new List<string>();
            //XJ: Add
            public List<string> vulnerability { get; set; } = new List<string>();
            //XJ: To allow vulnerabilities
            public List<string> immunity { get; set; } = new List<string>();
            //XJ:(2022/10/11)
            public string ac { get; set; } = "8";
            public string hp { get; set; } = "10";
            public string str { get; set; } = "10";
            public string dex { get; set; } = "10";
            public string con { get; set; } = "10";
            public string Int { get; set; } = "10";
            public string wis { get; set; } = "10";
            public string cha { get; set; } = "10";
            public string speed { get; set; } = "30";
            //XJ: To allow automatic stats Load           
            //public bool _usingAttackBonus { get; set; } = false;
            //public bool _usingDamageBonus { get; set; } = false;
            //public bool _usingSkillBonus { get; set; } = false;
            //public string _usingAttackBonusAmount { get; set; } = "";
            //public string _usingDamageBonusAmount { get; set; } = "";
            //public string _usingSkillBonusAmonunt { get; set; } = "";
        }

        public class Roll
        {
            public string name { get; set; } = "";
            public string type { get; set; } = "";
            public string roll { get; set; } = "100";
            //XJ:(2020/10/05) Added new stat about crit range.
            public string critrangemin { get; set; } = "20";
            //XJ
            //XJ:(2020/10/08) Added new stat about crit multiplier.
            public string critmultip { get; set; } = "2";
            //XJ
            public string range { get; set; } = "0/0";
            public string info { get; set; } = "";
            public string futureUse_icon { get; set; } = "Melee";
            public Roll link { get; set; } = null;

            public Roll()
            {

            }

            public Roll(Roll source)
            {
                if (diagnostics >= DiagnosticMode.ultra) { Debug.Log("Copying Roll Stats To New Roll Object"); }
                this.name = source.name;
                this.type = source.type;
                this.roll = source.roll;
                this.critrangemin = source.critrangemin;
                this.critmultip = source.critmultip;
                this.range = source.range;
                this.info = source.info;
                this.futureUse_icon = source.futureUse_icon;
                if (source.link == null) { this.link = null; } else { this.link = new Roll(source.link); }
            }
        }

        //XJ:2022/11/27 Structure Store bonuses.
        public class IdBonus
        {
        
            public string name { get; set; } = "";
            public bool _useAttackBonusDie { get; set; } =false;
            public bool _useDamageBonusDie { get; set; } = false;
            public bool _useSkillBonusDie { get; set; } = false;
            public string _amountAttackBonusDie { get; set; } = "";
            public string _amountDamageBonusDie { get; set; } = "";
            public string _amountSkillBonusDie { get; set; } = "";
        }
    }
}
