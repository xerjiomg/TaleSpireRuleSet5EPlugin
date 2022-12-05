# Rule Set 5E Plugin

This unofficial TaleSpire plugin for implementing some D&D 5E rule automation. Currently provides:

1. Four different modes of rolling: Manual on board, manual on the side, automated dice with dice cam, randomly generated
   dice values. Rolling style can be set on a player by player basis using the R2ModMan configuration for this plugin.

2. Three levels of player information in Chat using using unique messages. GM gets full information, attacker and victim
   get detailed infromation about their parts of the exchange and other players get minimal information.

3. Automated attack macros which roll attacks, compare them to target AC, roll damage, apply critial hits, immunities and
   resistances, and adjusts the target HP by the resulting amount.

4. Automated saves and skill roll.

5. Automated healing roll.

6. Roll with Advantage or Disadvantage using a toggle in the menu bar.

7. Roll with Attack Bonus (e.g. Bless), Damage Bonus (e.g. Hex) or Skill Bonus (e.g. Guidance)

8. Reaction pause with options to continue, cancel, forced hit, forced critical hit, halve and forced miss.

Video Demo: https://youtu.be/s-twNPYPtsY

This plugin, like all others, is free but if you want to donate, use: http://LordAshes.ca/TalespireDonate/Donate.php

## Change Log

```
2.0.0:
- A new functionality has been added to allow perform DC attacks (DC vs save rolls). Added a new radial button for it. 
- Now if "SnapToGrid" is selected in game, attack distance is take on number of cells in a grid. D&D 5e grid rules. (Work too in z-axis).
- Now auto-load mini's stats from Dnd5e files according to game stats's name.
- Now recognizes different decimal separators in the config file based on regional settings (e.g. Dice colors).
- Fixed a bug where critical attacks would not always hit the target.
- Fixed a bug where reactions would not trigger if an attack bonus was not added.
- Bonus attack modifiers can now be constant or negative.
- Fixed a bug where attack roll dice in manual mode were not automatically removed if bonus attack roll is selected.
- Negative roll dices are now allowed as bonus in skill and save rolls.
- Added "halve" and "crítical" options on reaction. Now, "hit" reaction force non critical hit.
- Different ranges and critical damage multipliers can now be set on attacks.
- On critical hits has been changed the damage multiplier by a damage dices multiplier.
- Attacks's range now take into account creature size.
- Changed the way to load Dnd5e files.
- Added new config option to activate and set up auto-change minis's base color according to minis hp (full, wounded, bloodied).
- Now, if bonus dices are marked and dont have bonus amount on textbox, not apply bonus and not crash.
- When Dnd5e file cant be loaded, showed a message on DebugLog intead crash.
- When the victim of one attack is a player, now receive: miss, success and damage all information.
- The message displayed in the reactions now contains more information about attack to choosse reaction. (Dice + modificator vs CA  or Save Throw + modificator vs DC).
- It is now possible to carry out attacks with guaranteed success.(e.g. Magic Misil).
- With dice roll mode distinct that "automatic generator" now allowed attacks moddifiers before dice rolls. (e.g. 5+1d20+2). 
- Change UI textbox's length to 9 instead 6 characters.
- Now vulnerabilities can added and take into account in the damage result.

1.9.2: Removed unnecessary diagnostics
1.9.1: Toolbar adjustment for smaller screens
1.9.0: Bug fix for post BR HF Integration update
1.8.1: Added missing Reaction Stop icon. No plugin change.
1.8.0: Added basic GUI scaling for resolution other than 1920x1080.
1.7.1: Corrected dependency on Chat Whisper plugin allowing player targeted messages. No plugin change.
1.7.0: Added Healing rolls
1.7.0: Fixed after BR update
1.6.0: Reaction stop toggle (stops after attack roll when checked)
1.6.0: Added reaction option for Continue, Cancel, Forced Hit, Forced Miss
1.5.0: Added Attack Bonus toggle (e.g. Bless) and amount. Remembered for character sheet character during session.
1.5.0: Added Damage Bonus toggle (e.g. Hex) and amount. Remembered for character sheet character during session.
1.5.0: Added Skill Bonus toggle (e.g. Guidance) and amount. Remembered for character sheet character during session.
1.5.0: Added support for link roll on skills and blank roll support for skill roll messages.
1.5.0: Added fix to make added tool bar options disappear when in Cinematic Mode.
1.4.1: Added Beyond Link Server LinkData files for "teaching" Beyonf Link Server how to make dnd5e files for the collected
       data. These files are only used if the user is using the Beyond Link via Chrome plugin to gether character sheet data
	   from external sources. The actualy plugin DLL has not changed (remains at version 1.4.0).
1.4.0: Added Reach and Range support including beyond range cancel and long range disadvantage.
1.4.0: Updated range attach from melee check to check all minis on the board.
1.4.0: Roll can now have a non dice roll calculation or be empty. If empty name and type will be displayed as message.
1.3.0: Added saves and skills. Updated sample character. Note: Previous versions will fail because previous versions used
       empty public rolls and provate sub-rolls to implemented Private mode. This plugin uses the type to determine the
       level of information shared with others.	   
1.2.0: Added support for rolls with advantage and disadvantage
1.1.0: Added R2ModMan setting for determining attack icons based on "type" or "name"
1.1.0: Added support for different attack animations. By default animation is based on roll type but the roll can included
       a property called info which can be the name of the animation to be used.
1.1.0: Added R2ModMan setting for miss animatation
1.1.0: Added R2ModMan setting for death animatation. If "remove", mini will be removed after death.
1.1.0: Added Melee attack distance check. Prevents melee attack if not in melee range.
1.1.0: Added Range attack distance check. Warns if in melee performing range attack. Disadvantage rolls not implemented yet.
1.1.0: Fixed display message duration bug.
1.0.1: Added dice color and highlight (to support future group rolls)
1.0.1: Buf fix for rolling system which prevented using anything but AutomaticDice
1.0.0: Initial release
```

## Install

Use R2ModMan or similar installer to install this plugin.

Set desired rolling mode, dice side area (if applicable), and speed using R2ModMan configuration for this plugin.

Create a Dnd5e file for each character (or foe type) that is to use this plugin. See the included Jon.Dnd5e file as an
example. While the format does support skills, they are currently not used.

## Usage

### Attacks & DC Attacks

```
1. Use the Dis, Normal and Adv selector in the top right of the screen to select the type of roll.
2. Select the mini that is attacking.
3. Right click the mini that is to be attacked.
4. Select the Scripted Attacks or Scripted DC Attacks menu and then the desired attack from the sub-menu.
5. The attack will be processed and the results displayed in speech bubbles with additional details in the chat.
```

### Saves And Skills

```
1. Use the Dis, Normal and Adv selector in the top right of the screen to select the type of roll.
2. Select the mini that is instigating the save or skill check.
3. Right click the mini to open the radial menu. Select either Saves or Skills.
5. Select the desired save or skill from the sub-selection.
```

### Reactions

```
1. Check the toggle beside the Reaction (hand) icon in the title bar
2. Activate an attack sequence (see above)
3. After the attack roll (and attack bonus roll if applicable) the sequence will stop with an indication of the
   roll total (but not any modifiers)
4. A number of reaction button will be displayed to continue or abort the sequence
5. If the victim uses a spell like Shield, the AC of the victim can be modified at this point prior to the hit/miss
   determination. Currently this is done (and undone) manually.
6. Selecting the Continue button will continue on with the sequence.
7. Selecting the Cancel button will cancel the sequence*.
8. Selecting the Hit button will jump to the hit scenario with non critical hit regardless of the attack total and victim AC*.
9. Selecting the Miss button will jump to the miss scenario regardless of the attack total and victim AC*.
10.Selecting the Critical button will jump to the hit scenario with critical hit regardless of the attack total and victim AC*.
11.Selecting the Halve button will coninue with the sequence but halve damage taken.
```

** Note: Choosing these options generates a indication in the chat that the option was used (to avoid cheating). 

## File Format

```
	{
     "NPC":true,
     "hp":"18",     
     "ac":"15",
     "str":"7",
     "dex":"16",
     "con":"12",
     "int":"14",
     "wis":"10",
     "cha":"10",
     "speed":"30",
     "attacks":[
          {
               "name":"Dagger",
               "type":"Melee",
               "range":"5/5",
               "roll":"1D20+5",
               "critrangemin":"20",
               "critmultip":"2",
               "link":{
                    "name":"Dagger",
                    "roll":"1d4+3",
                    "type":"piercing"
               }
          },
          {
               "name":"Dagger & Sneak Attack",
               "type":"Melee",
               "range":"5/5",
               "roll":"1D20+5",
               "critrangemin":"20",
               "critmultip":"2",
               "link":{
                    "name":"Dagger",
                    "roll":"1d4+3",
                    "type":"piercing",
                    "link":{
                         "name":"Sneak attack",
                         "roll":"1d4+3+1d6",
                         "type":"piercing"
                    }
               }
          },
          {
               "name":"Light Crossbow",
               "type":"Range",
               "range":"80/320",
               "roll":"1D20+5",
               "critrangemin":"20",
               "critmultip":"2",
               "link":{
                    "name":"Light Crossbow",
                    "roll":"1d8+3",
                    "type":"piercing"
               }
          },
          {
               "name":"Light Crossbow & Sneak Attack",
               "type":"Range",
               "range":"80/320",
               "roll":"1D20+5",
               "critrangemin":"20",
               "critmultip":"2",
               "link":{
                    "name":"Light Crossbow",
                    "roll":"1d8+3",
                    "type":"piercing",
                    "link":{
                         "name":"Sneak attack",
                         "roll":"1d8+3+1d6",
                         "type":"piercing"
                    } 
               }
          },
          {
               "name":"Rat Dagger Flurry",
               "type":"Range",
               "range":"20/60",
               "roll":"1D20+5",
               "critrangemin":"20",
               "critmultip":"2",
               "link":{
                    "name":"Rat Dagger Flurry",
                    "roll":"1d4+3",
                    "type":"piercing"
               }
          },
          {
               "name":"Rat Dagger Flurry & Sneak Attack",
               "type":"Range",
               "range":"20/60",
               "roll":"1D20+5",
               "critrangemin":"20",
               "critmultip":"2",
               "link":{
                    "name":"Rat Dagger Flurry",
                    "roll":"1d4+3",
                    "type":"piercing",
                    "link":{
                         "name":"Sneak attack",
                         "roll":"1d4+3+1d6",
                         "type":"piercing"
                    }
               }
          }
     ],
     "attacksDC":[
          {
               "name":"Burning Hands",
               "type":"Magic",
               "range":"15/15",
               "roll":"17/DEX/half",
               "link":{
                    "name":"Burning Hands",
                    "roll":"3d6",
                    "type":"fire"
               }
            },
			{
               "name":"Create Bonfire",
               "type":"Magic",
               "range":"60/60",
               "roll":"17/DEX/zero",
               "link":{
                    "name":"Create Bonfire",
                    "roll":"2d8",
                    "type":"fire"
				}
			},
            {
               "name":"Searing Smite",
               "type":"Magic",
               "range":"120/120",
               "roll":"100",
               "link":{
                    "name":"Searing Smite",
                    "roll":"1d6",
                    "type":"fire"
                }
            },
			           {
               "name":"Hold Person",
               "type":"Magic",
               "range":"60/60",
               "roll":"11/WIS/zero",
               "condition":"paralyzed",
               "link":{
                    "name":"Hold Person",
                    "roll":"0",
                    "type":""
                    }
            }
     ],
     "healing":[
	 	{
		"name": "Cure Wounds",
		"type": "Magic",
		"roll": "1D8+3",
		"link":	{
			"name": "Song Of Rest",
			"type": "Song",
			"roll": "1D6"
			}
		}
     ],
     "saves":[
          {
               "name":"STR",
               "type":"Public",
               "roll":"1D20-2"
          },
          {
               "name":"DEX",
               "type":"Public",
               "roll":"1D20+3"
          },
          {
               "name":"CON",
               "type":"Public",
               "roll":"1D20+1"
          },
          {
               "name":"INT",
               "type":"Public",
               "roll":"1D20+2"
          },
          {
               "name":"WIS",
               "type":"Public",
               "roll":"1D20+0"
          },
          {
               "name":"CHA",
               "type":"Public",
               "roll":"1D20+0"
          }
     ],
     "skills":[
          {
               "name":"Inititative",
               "type":"Public",
               "roll":"1D20+3"
          },
          {
               "name":"Acrobatics",
               "type":"Public",
               "roll":"1D20+5"
          },
          {
               "name":"Animal Handling",
               "type":"Public",
               "roll":"1D20+0"
          },
          {
               "name":"Arcana",
               "type":"Public",
               "roll":"1D20+2"
          },
          {
               "name":"Atlethics",
               "type":"Public",
               "roll":"1D20-2"
          },
          {
               "name":"Deception",
               "type":"Public",
               "roll":"1D20+0"
          },
          {
               "name":"History",
               "type":"Public",
               "roll":"1D20+2"
          },
          {
               "name":"Insigth",
               "type":"Public",
               "roll":"1D20+0"
          },
          {
               "name":"Intimidation",
               "type":"Public",
               "roll":"1D20+0"
          },
          {
               "name":"Investigation",
               "type":"Public",
               "roll":"1D20+2"
          },
          {
               "name":"Medicine",
               "type":"Public",
               "roll":"1D20+0"
          },
          {
               "name":"Nature",
               "type":"Public",
               "roll":"1D20+2"
          },
          {
               "name":"Perception",
               "type":"Public",
               "roll":"1D20+2"
          },
          {
               "name":"Performance",
               "type":"Public",
               "roll":"1D20+0"
          },
          {
               "name":"Persuasion",
               "type":"Public",
               "roll":"1D20+0"
          },
          {
               "name":"Religion",
               "type":"Public",
               "roll":"1D20+2"
          },
          {
               "name":"Sleight of Hand",
               "type":"Public",
               "roll":"1D20+3"
          },
          {
               "name":"Stealth",
               "type":"Public",
               "roll":"1D20+7"
          },
          {
               "name":"Survival",
               "type":"Public",
               "roll":"1D20+0"
          },
          {
               "name":"Deception [GM]",
               "type":"Secret,GM",
               "roll":"1D20+0"
          },
          {
               "name":"Insigth [GM]",
               "type":"Secret,GM",
               "roll":"1D20+0"
          },
          {
               "name":"Perception [GM]",
               "type":"Secret,GM",
               "roll":"1D20+0"
          }
     ],
     "immunity":[
	"poison"
     ],
     "resistance":[
          "lightning",
          "thunder",
          "bludgeoning",
          "piercing",
          "slashing"
     ],
     "vulnerability":[

     ]
}
```

```
"NPC" indicates a mini is considered an ally (npc=false) or foe (npc=true). It should be noted that a better name for
      this property would have been foe but for backwards compatibility the name will not be changed. If an NPC is not
	  hostile, its NPC value should be false (not true). This will prevent it from giving allies disadvantage in combat
	  if it is adjactent and the ally is using a ranged attack.

"ac" sets the Armor Class.

"reach" indicated the number of feet that the character can make melee attacks and threaten locations from.

"attacks" is an array of Roll objects which define possible attacks the user can make.

"attacksDC" is an array of Roll objects which define possible DC attacks the user can make.

"name" is a Roll object that determines the name of the attack whch will be displayed in the radial menu.
"type" is a Roll obejct that determines the type of attack typically unarmed, melee, range and magic.
       For saves and skills it determine how the results are shared using the following options:
	     Public - Everyone sees the speech bubble and chat message with results. Owner and GM sees breakdown.
		 Private - Everyone sees the speech bubble and chat message of the skill used but not the result.
		           Onwer and GM get results with breakdown. 
		 Secret - No speech bubble or chat message to everyone. Owner and GM sees breakdown.
		 Private, GM - Everyone sees the speech bubble and chat message of the skill used but not the result.
		               Only GM gets results with breakdown.
		 Secret, GM - No speech bubble or chat message to everyone. Only GM sees results with breakdown.
"roll" is a Roll object that determines the roll that is made when this attack is selected. Uses the #D#+# or #D#-# format.
       It should be noted that the number before D is not optional. For example, 1D20 cannot be abbreviated with D20. Also can 
       use "100" value to carry out attacks with guaranteed success.
"range" indicates the end of short range and maximum distance separated by a slash. Used for range and mele attack. For melee
        attacks this value is ignored and the reach value is used if range not exist.
"info" is a optional string parameter that determines extra information for the roll. For attacks, this holds the name of
       the animation that is to be played. If not specified for attack, the attack type is used to determine the animation.

"critrangemin" is the value from which the attack roll is considered a critical hit. (e.g. Improved Critical, 19-20)

"critmultip" is the multiplier of the amount of damage dice that are rolled on a critical hit. (e.g. Brutal Critical, x3 damage rolls on critical)

"link" is a Roll object links to the Roll damage object. This follows the same rules as a Roll object except the type
       determines the damage type and the roll rolls the weapon damage. The link in a Roll damage object can be used
	   to add additional damage (of the same or different type). This is typically used for things like a sword of flame
	   (where the weapon damage type and the bonus damage type are different) or to add extra damage like sneak damage.

"skills" is a Roll object that determines the skill to be rolled and how the results are displayed. The name proeprty
         indicates the name of the skill and is used in the output results. Type is one of "public", "private" (or "owner")
		 or "secret" (or "GM"). Public rolls are displayed as speech bubbles and chat messages for everyone to see.
		 Private rolls appear on a message board that is displayed only for the owner of the mini and the GM. The contents
		 is displayed for a configurable amount of time on the message board but can be dismissed by clicking on the message
		 board. Secret rolls also show up on the message board but only for the GM. The roll property is used to determines
		 the dice and modifier used to make the skill check. If the roll is empty, the roll name will be displayed as a
		 comment. The link property can be used to link to additions rolls which are automatically processed. The link
		 property for skills is typically used to display a comment for the public but display the roll results for the
		 owner and/or GM.
		 
"healing" is a Roll object that determines how much the victim is healed. Uses hierarchy rolls like damage on attack rolls.
          Healing rolls are always pubic and enumerate the different sources of damage and well as the total.

"immunity" is a list of strings representing damage types from which the user takes no damage. When the damage type
           of an attack against the user matches an immunity (exactly) the damage is reduced to 0.
		   
"resistance" is a list of strings representing damage types from which the user takes 1/2 damage. When the damage type
           of an attack against the user matches a resistance (exactly) the damage is reduced to 1/2.
				
"vulnerability" is a list of strings representing damage types from which the user takes 2x damage. When the damage type
           of an attack against the user matches a vulnerability (exactly) the damage is doubled.

Note: Immunity, vulnerability and resistance is only applied to the portion of damage that matches the damage type. If an attack does
      multiple types of damage, the plugin will correctly apply immunity, vulnerability and resistance to only the mathcing damage type.

Note(2):Other fields are optional and can be used to automatically assign stats to your minis according to the "name stats" you have configured in Talespire.
```
	  	  
## Limitations

```
1. While the plugin does expose the characters dictionary (so other plugins can modify it) this plugin reads the contents
   of the Dnd5E files at start up and does not provide any interactive methods to change the settings. For example, a new
   resistance gained through a spell would not be reflected.    
2. The attack sequence does not provide an option for reactions to be used to modify the attack sequence. For example,
   if the user casts a Shield spell to temporarily increase AC or uses a effects of a Warding Flare.
3. Currently does not support damage reduction such as that given by the Heavy Armor Master feat.
4. Advantage and disadvantage rolls are more likely to cause dice to roll out of dice cam view.
5. Scale of 5' is assumed.
6. Saves currently use the Skill Bonus toggle and amount as opposed to the Attack Bonus toggle and amount.
```

### Work-Around: Changing Specifications

Sometimes a character will frequently change its statistics which affect attacks. For example, a Barbarian gains resistance
to physical attacks when raging but not when he/she is not raging. Since the plugin does not provide an interactive way to
change a charcters specifications (inclduing resistance) while running it may seem that such a character design is not
supported. However, there are a couple work arounds to get such characters working with this plugin.

#### Damage Changes

Changes to damage such as a rogue attack with and without sneak or barbarian adding damage from rage or not can be solved
by making two (or more) attack entries and using the appropriate one. This fills up the radial menu quickly if you have
many combinations but allows such a character to be used with this plugin.

#### Immunity And Resistance Changes

To create a character that can change immunities and/or resistances, such as a barbarian, create two copies of the same
character with slightly different names. For example, "Garth" and "Garth (Rage)". Set the appropriated immunities and
resistances for version. In game, one can switch between the different modes by renaming the character. Since the plugin
looks up the character sheet associated with the mini name, it is possible to access multiple version of a character just
by renaming the character.

#### DC Attacks

The Roll field should have 3 values splited by "/"
-First value refers to DC of attack
-Second value refers to the first three characters of the stat saving throw (e.g. CON for constitution saving throw)
-The third value refers to damage received if the saving throw is not successful. Take values "half" if damage half reduced on a failure saving throw or "zero" if no damage.
Moreover the roll field can be set as ["roll":"100"] to carry out attacks with guaranteed success (Secure Success) (e.g. Magic Missile)

#### Tips

-You can use reactions to solve many situations: Ignore the attack roll and decide if it's "hit", "critical", "miss" or "halve".
-You can create invisible creatures to deal extra damage. Using DC attacks or Attacks with secure success (Roll:"100").
-You can use DC attacks to simulate traps, including the DC and the damage of the trap.
-You can use DC attacks without damage to determine if a condition or other effects are set on the victim. (The chat will show: "No  damage".)
-So that the way to measure distances between characters are the same for all players, them should have snap to grid enabled or disabled at the same time. 
-There are monsters that are resistant to non-magical piercing, slashing, and/or bludgeoning damage. If the weapon which you are going to attack them is magical and deals one of these damages, you can add "magic" in front of the damage type in order to deals the total damage instead halve damage (e.g. "type" = "magic piercing").

#### Base Colors

https://ibb.co/L0p22Jk
