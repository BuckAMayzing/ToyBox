﻿using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Designers;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.UnitLogic;
using Alignment = Kingmaker.Enums.Alignment;
using ModKit;
using static ModKit.UI;
using ModKit.Utility;
using ToyBox.classes.Infrastructure;
using Kingmaker.PubSubSystem;
using Kingmaker.Blueprints;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.UnitLogic.Progression.Paths;
using static Kingmaker.Utility.UnitDescription.UnitDescription;
#if Wrath
using Kingmaker.Blueprints.Classes.Spells;
using ToyBox.Multiclass;
#endif

namespace ToyBox {
    public partial class PartyEditor {
        public static void OnClassesGUI(UnitEntityData ch, List<(BlueprintCareerPath path, int level)> careerPaths, UnitEntityData selectedCharacter) {
#if Wrath
            Div(100, 20);
            using (HorizontalScope()) {
                Space(100);
                Toggle("Multiple Classes On Level-Up", ref Settings.toggleMulticlass);
                if (Settings.toggleMulticlass) {
                    Space(40);
                    if (DisclosureToggle("Config".orange().bold(), ref editMultiClass)) {
                        multiclassEditCharacter = selectedCharacter;
                    }
                    Space(53);
                    Label("Experimental - See 'Level Up + Multiclass' for more options and info".green());
                }
            }
#endif
            using (HorizontalScope()) {
#if Wrath
                Space(100);
                ActionToggle("Allow Levels Past 20",
                    () => {
                        var hasValue = Settings.perSave.charIsLegendaryHero.TryGetValue(ch.HashKey(), out var isLegendaryHero);
                        return hasValue && isLegendaryHero;
                    },
                    (val) => {
                        if (Settings.perSave.charIsLegendaryHero.ContainsKey(ch.HashKey())) {
                            Settings.perSave.charIsLegendaryHero[ch.HashKey()] = val;
                            Settings.SavePerSaveSettings();
                        }
                        else {
                            Settings.perSave.charIsLegendaryHero.Add(ch.HashKey(), val);
                            Settings.SavePerSaveSettings();
                        }
                    },
                    0f,
                    AutoWidth());
                Space(380);
                Label("Tick this to let your character exceed the level 20 level cap like the Legend mythic path".green());
#endif
            }
            Div(100, 20);

            if (editMultiClass) {
#if Wrath
                MulticlassPicker.OnGUI(ch);
#endif
            }
            else {
                var prog = ch.Descriptor().Progression;
                using (HorizontalScope()) {
                    using (HorizontalScope(Width(600))) {
                        Space(100);
                        Label("Character Level".cyan(), Width(250));
                        ActionButton("<", () => prog.CharacterLevel = Math.Max(0, prog.CharacterLevel - 1), AutoWidth());
                        Space(25);
                        Label("level".green() + $": {prog.CharacterLevel}", Width(100f));
                        ActionButton(">", () => prog.CharacterLevel = Math.Min(
#if Wrath
                                                    prog.MaxCharacterLevel, 
#elif RT
                                                    int.MaxValue, // TODO: is this right?
#endif
                                                    prog.CharacterLevel + 1),
                                     AutoWidth());
                    }
                    ActionButton("Reset", () => ch.resetClassLevel(), Width(150));
                    Space(23);
                    using (VerticalScope()) {
                        Label("This directly changes your character level but will not change exp or adjust any features associated with your character. To do a normal level up use +1 Lvl above.  This gets recalculated when you reload the game.  ".green());
                    }
                }
                using (HorizontalScope()) {
                    using (HorizontalScope(Width(600))) {
                        Space(100);
                        Label("Experience".cyan(), Width(250));
                        Space(25);
                        int tmpExp = prog.Experience;
                        IntTextField(ref tmpExp, null, Width(150f));
                        prog.Experience = tmpExp;
                    }
                }
                using (HorizontalScope()) {
                    using (HorizontalScope(Width(781))) {
                        Space(100);
                        ActionButton("Adjust based on Level", () => {
                            var xpTable = prog.ExperienceTable;
                            prog.Experience = xpTable.GetBonus(prog.CharacterLevel);
                        }, AutoWidth());
                        Space(27);
                    }
                    Label("This sets your experience to match the current value of character level".green());
                }
#if false
                Div(100, 25);
                using (HorizontalScope()) {
                    using (HorizontalScope(Width(600))) {
                        Space(100);
                        Label("Mythic Level".cyan(), Width(250));
                        ActionButton("<", () => prog.MythicLevel = Math.Max(0, prog.MythicLevel - 1), AutoWidth());
                        Space(25);
                        Label("my lvl".green() + $": {prog.MythicLevel}", Width(100f));
                        ActionButton(">", () => prog.MythicLevel = Math.Min(10, prog.MythicLevel + 1), AutoWidth());
                    }
                    Space(181);
                    Label("This directly changes your mythic level but will not adjust any features associated with your character. To do a normal mythic level up use +1 my above".green());
                }

                using (HorizontalScope()) {
                    using (HorizontalScope(Width(600))) {
                        Space(100);
                        Label("Experience".cyan(), Width(250));
                        Space(25);
                        int tmpMythicExp = prog.MythicExperience;
                        IntTextField(ref tmpMythicExp, null, Width(150f));
                        if (0 <= tmpMythicExp && tmpMythicExp <= 10) {
                            prog.MythicExperience = tmpMythicExp;
                        } // If Mythic experience is 0, entering any number besides 1 is > 10, meaning the number would be overwritten with; this is to prevent that
                        else if (tmpMythicExp % 10 == 0) {
                            prog.MythicExperience = tmpMythicExp / 10;
                        }
                    }
                }
                using (HorizontalScope()) {
                    using (HorizontalScope(Width(781))) {
                        Space(100);
                        ActionButton("Adjust based on Level", () => {
                            prog.MythicExperience = prog.MythicLevel;
                        }, AutoWidth());
                        Space(27);
                    }
                    Label("This sets your mythic experience to match the current value of mythic level. Note that mythic experience is 1 point per level".green());
                }
#endif
                var classCount = careerPaths.Count();
#if Wrath
                var gestaltCount = classData.Count(cd => !cd.CharacterClass.IsMythic && ch.IsClassGestalt(cd.CharacterClass));
                var mythicCount = classData.Count(x => x.CharacterClass.IsMythic);
                var mythicGestaltCount = classData.Count(cd => cd.CharacterClass.IsMythic && ch.IsClassGestalt(cd.CharacterClass));
#endif
                foreach (var cd in careerPaths) {
                    var showedGestalt = false;
                    Div(100, 20);
                    using (HorizontalScope()) {
                        Space(100);
                        using (VerticalScope(Width(250))) {
                            var className = cd.path.Name;
                                Label(className.orange(), Width(250));
                        }
//                        ActionButton("<", () => cd.level = Math.Max(0, cd.level - 1), AutoWidth());
                        Space(25);
                        Label("level".green() + $": {cd.level}", Width(100f));
                        var maxLevel = 20;
                        //ActionButton(">", () => cd.level = Math.Min(maxLevel, cd.level + 1), AutoWidth());
                        Space(23);
#if Wrath
                        if (ch.IsClassGestalt(cd.CharacterClass)
                            || !cd.CharacterClass.IsMythic && classCount - gestaltCount > 1
                            || cd.CharacterClass.IsMythic && mythicCount - mythicGestaltCount > 1
                            ) {
                            ActionToggle(
                                "gestalt".grey(),
                                () => ch.IsClassGestalt(cd.CharacterClass),
                                (v) => {
                                    ch.SetClassIsGestalt(cd.CharacterClass, v);
                                    ch.Progression.UpdateLevelsForGestalt();
                                },
                                125
                                );
                            showedGestalt = true;
                        }
                        else Space(125);
                        Space(27);
                        using (VerticalScope()) {
                            if (showedGestalt) {
                                if (showedGestalt) {
                                    Label("this flag lets you not count this class in computing character level".green());
                                    DivLast();
                                }
                            }
                            Label(cd.CharacterClass.Description.StripHTML().green(), AutoWidth());
                        }
#endif
                    }
                }
            }
        }
    }
}