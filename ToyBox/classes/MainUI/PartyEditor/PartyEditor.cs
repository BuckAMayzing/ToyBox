﻿// Copyright < 2021 > Narria (github user Cabarius) - License: MIT
using Kingmaker;
using Kingmaker.Designers;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Parts;
using ModKit;
using ModKit.DataViewer;
using System;
using System.Collections.Generic;
using System.Linq;
using static ModKit.UI;

namespace ToyBox {
    public partial class PartyEditor {
        public static Settings Settings => Main.settings;

        private enum ToggleChoice {
            Classes,
            Stats,
            Facts,
            Features,
            Buffs,
            Abilities,
            Spells,
            AI,
            None,
        };
        private const int NarrowIndent = 413;

        private static ToggleChoice selectedToggle = ToggleChoice.None;
        private static int editingCharacterIndex = 0;
        private static UnitEntityData charToAdd = null;
        private static UnitEntityData charToRecruit = null;
        private static UnitEntityData charToRemove = null;
        private static UnitEntityData charToUnrecruit = null;
        private static bool editMultiClass = false;
        private static UnitEntityData multiclassEditCharacter = null;
        private static int respecableCount = 0;
        private static int recruitableCount = 0;
        private static int selectedSpellbook = 0;
        private static (string, string) nameEditState = (null, null);
        public static int selectedSpellbookLevel = 0;
        private static bool editSpellbooks = false;
        private static UnitEntityData spellbookEditCharacter = null;
        private static readonly Dictionary<string, int> statEditorStorage = new();
        public static Dictionary<string, Spellbook> SelectedSpellbook = new();
        private static UnitEntityData GetEditCharacter() {
            var characterList = CharacterPicker.GetCharacterList();
            if (characterList == null || characterList.Count == 0) return null;
            if (editingCharacterIndex >= characterList.Count) editingCharacterIndex = 0;
            return characterList[editingCharacterIndex];
        }

        public static void ResetGUI() {
            editingCharacterIndex = 0;
            selectedSpellbook = 0;
            selectedSpellbookLevel = 0;
            CharacterPicker.PartyFilterChoices = null;
            Main.settings.selectedPartyFilter = 0;
        }

        // This bit of kludge is added in order to tell whether our generic actions are being accessed from this screen or the Search n' Pick
        public static bool IsOnPartyEditor() => Main.settings.selectedTab == 2;

        public static void ActionsGUI(UnitEntityData ch) {
            var player = Game.Instance.Player;
            Space(25);
            if (!player.PartyAndPets.Contains(ch) && player.AllCharacters.Contains(ch)) {
                ActionButton("Add", () => { charToAdd = ch; }, Width(150));
                Space(25);
            }
            else if (player.ActiveCompanions.Contains(ch)) {
                ActionButton("Remove", () => { charToRemove = ch; }, Width(150));
                Space(25);
            }
            else if (!player.AllCharacters.Contains(ch)) {
                recruitableCount++;
                ActionButton("Recruit".cyan(), () => { charToRecruit = ch; }, Width(150));
                Space(25);
            }
            if (player.AllCharacters.Contains(ch) && !ch.IsStoryCompanion()) {
                ActionButton("Unrecruit".cyan(), () => { charToUnrecruit = ch; charToRemove = ch; }, Width(150));
                Space(25);

            }
//            else
//                Space(178);
            if (RespecHelper.GetRespecableUnits().Contains(ch)) {
                respecableCount++;
                ActionButton("Respec".cyan(), () => { Actions.ToggleModWindow(); RespecHelper.Respec(ch); }, Width(150));
            }
            else {
                Space(153);
            }
#if false
            Space(25);
            ActionButton("Log Caster Info", () => CasterHelpers.GetOriginalCasterLevel(ch.Descriptor),
                AutoWidth());
#endif
            Label("", AutoWidth());
        }
        public static void OnGUI() {
            var player = Game.Instance.Player;
            if (player == null) return;
            charToAdd = null;
            charToRecruit = null;
            charToRemove = null;
            charToUnrecruit = null;
            var characterListFunc = CharacterPicker.OnFilterPickerGUI();
            var characterList = characterListFunc.func();
            var mainChar = GameHelper.GetPlayerCharacter();
            if (characterListFunc.name == "Nearby") {
                Slider("Nearby Distance", ref CharacterPicker.nearbyRange, 1f, 200, 25, 0, " meters", Width(250));
                characterList = characterList.OrderBy((ch) => ch.DistanceTo(mainChar)).ToList();
            }
            Space(20);
            var chIndex = 0;
            recruitableCount = 0;
            respecableCount = 0;
            var selectedCharacter = GetEditCharacter();
            var isWide = IsWide;
            if (Main.IsInGame) {
                using (HorizontalScope()) {
                    Label($"Party Level ".cyan() + $"{Game.Instance.Player.PartyLevel}".orange().bold(), AutoWidth());
                    Space(25);
#if false   // disabled until we fix performance
                    var encounterCR = CheatsCombat.GetEncounterCr();
                    if (encounterCR > 0) {
                        UI.Label($"Encounter CR ".cyan() + $"{encounterCR}".orange().bold(), UI.AutoWidth());
                    }
#endif
                }
            }
            List<Action> todo = new();
            foreach (var ch in characterList) {
                var classData = ch.Progression.Classes;
                // TODO - understand the difference between ch.Progression and ch.Descriptor.Progression
                var progression = ch.Descriptor.Progression;
                var xpTable = progression.ExperienceTable;
                var level = progression.CharacterLevel;
                var mythicLevel = progression.MythicLevel;
                var spellbooks = ch.Spellbooks.ToList();
                var spellCount = spellbooks.Sum((sb) => sb.GetAllKnownSpells().Count());
                var isOnTeam = player.AllCharacters.Contains(ch);
                using (HorizontalScope()) {
                    var name = ch.CharacterName;
                    if (Game.Instance.Player.AllCharacters.Contains(ch)) {
                        if (isWide) {
                            if (EditableLabel(ref name, ref nameEditState, 200, n => n.orange().bold(), MinWidth(100), MaxWidth(400))) {
                                ch.Descriptor.CustomName = name;
                                Main.SetNeedsResetGameUI();
                            }
                        }
                        else
                            if (EditableLabel(ref name, ref nameEditState, 200, n => n.orange().bold(), Width(230))) {
                            ch.Descriptor.CustomName = name;
                            Main.SetNeedsResetGameUI();
                        }
                    }
                    else {
                        if (isWide)
                            Label(ch.CharacterName.orange().bold(), MinWidth(100), MaxWidth(400));
                        else
                            Label(ch.CharacterName.orange().bold(), Width(230));
                    }
                    Space(5);
                    var distance = mainChar.DistanceTo(ch); ;
                    Label(distance < 1 ? "" : distance.ToString("0") + "m", Width(75));
                    Space(5);
                    int nextLevel;
                    for (nextLevel = level; progression.Experience >= xpTable.GetBonus(nextLevel + 1) && xpTable.HasBonusForLevel(nextLevel + 1); nextLevel++) { }
                    if (nextLevel <= level || !isOnTeam)
                        Label((level < 10 ? "   lvl" : "   lv").green() + $" {level}", Width(90));
                    else
                        Label((level < 10 ? "  " : "") + $"{level} > " + $"{nextLevel}".cyan(), Width(90));
                    // Level up code adapted from Bag of Tricks https://www.nexusmods.com/pathfinderkingmaker/mods/2
                    if (player.AllCharacters.Contains(ch)) {
                        if (xpTable.HasBonusForLevel(nextLevel + 1)) {
                            ActionButton("+1", () => {
                                progression.AdvanceExperienceTo(xpTable.GetBonus(nextLevel + 1), true);
                            }, Width(63));
                        }
                        else { Label("max", Width(63)); }
                    }
                    else { Space(66); }
                    Space(10);
                    var nextML = progression.MythicExperience;
                    if (nextML <= mythicLevel || !isOnTeam)
                        Label((mythicLevel < 10 ? "  my" : "  my").green() + $" {mythicLevel}", Width(90));
                    else
                        Label((level < 10 ? "  " : "") + $"{mythicLevel} > " + $"{nextML}".cyan(), Width(90));
                    if (player.AllCharacters.Contains(ch)) {
                        if (progression.MythicExperience < 10) {
                            ActionButton("+1", () => {
                                progression.AdvanceMythicExperience(progression.MythicExperience + 1, true);
                            }, Width(63));
                        }
                        else { Label("max", Width(63)); }
                    }
                    else { Space(66); }
                    Space(30);
                    Wrap(IsNarrow, NarrowIndent, 0);
                    var prevSelectedChar = selectedCharacter;
                    var showClasses = ch == selectedCharacter && selectedToggle == ToggleChoice.Classes;
                    if (DisclosureToggle($"{classData.Count} Classes", ref showClasses, 140)) {
                        if (showClasses) {
                            selectedCharacter = ch; selectedToggle = ToggleChoice.Classes; Mod.Trace($"selected {ch.CharacterName}");
                        }
                        else { selectedToggle = ToggleChoice.None; }
                    }
                    var showStats = ch == selectedCharacter && selectedToggle == ToggleChoice.Stats;
                    if (DisclosureToggle("Stats", ref showStats,95)) {
                        if (showStats) { selectedCharacter = ch; selectedToggle = ToggleChoice.Stats; }
                        else { selectedToggle = ToggleChoice.None; }
                    }
                    //var showFacts = ch == selectedCharacter && selectedToggle == ToggleChoice.Facts;
                    //if (UI.DisclosureToggle("Facts", ref showFacts, 125)) {
                    //    if (showFacts) { selectedCharacter = ch; selectedToggle = ToggleChoice.Facts; }
                    //    else { selectedToggle = ToggleChoice.None; }
                    //}
                    var showFeatures = ch == selectedCharacter && selectedToggle == ToggleChoice.Features;
                    if (DisclosureToggle("Features", ref showFeatures, 125)) {
                        if (showFeatures) { selectedCharacter = ch; selectedToggle = ToggleChoice.Features; }
                        else { selectedToggle = ToggleChoice.None; }
                    }
                    Wrap(!IsWide, NarrowIndent, 0);
                    var showBuffs = ch == selectedCharacter && selectedToggle == ToggleChoice.Buffs;
                    if (DisclosureToggle("Buffs", ref showBuffs, 90)) {
                        if (showBuffs) { selectedCharacter = ch; selectedToggle = ToggleChoice.Buffs; }
                        else { selectedToggle = ToggleChoice.None; }
                    }
                    var showAbilities = ch == selectedCharacter && selectedToggle == ToggleChoice.Abilities;
                    if (DisclosureToggle("Abilities", ref showAbilities, 125)) {
                        if (showAbilities) { selectedCharacter = ch; selectedToggle = ToggleChoice.Abilities; }
                        else { selectedToggle = ToggleChoice.None; }
                    }
                    var showSpells = ch == selectedCharacter && selectedToggle == ToggleChoice.Spells;
                    if (DisclosureToggle($"{spellCount} Spells", ref showSpells,150)) {
                        if (showSpells) { selectedCharacter = ch; selectedToggle = ToggleChoice.Spells; }
                        else { selectedToggle = ToggleChoice.None; }
                    }
                    var showAI = ch == selectedCharacter && selectedToggle == ToggleChoice.AI;
                    ReflectionTreeView.DetailToggle("Ins", ch, ch, 75);
                    Wrap(!isWide, NarrowIndent - 20); 
                    ActionsGUI(ch);
                    if (prevSelectedChar != selectedCharacter) {
                        selectedSpellbook = 0;
                    }
                }
                if (!isWide) Div(00, 10);
                5.space();
                ReflectionTreeView.DetailsOnGUI(ch);
                //if (!UI.IsWide && (selectedToggle != ToggleChoice.Stats || ch != selectedCharacter)) {
                //    UI.Div(20, 20);
                //}
                if (selectedCharacter != spellbookEditCharacter) {
                    editSpellbooks = false;
                    spellbookEditCharacter = null;
                }
                if (selectedCharacter != multiclassEditCharacter) {
                    editMultiClass = false;
                    multiclassEditCharacter = null;
                }
                if (ch == selectedCharacter && selectedToggle == ToggleChoice.Classes) {
                    OnClassesGUI(ch, classData, selectedCharacter);
                }
                if (ch == selectedCharacter && selectedToggle == ToggleChoice.Stats) {
                    OnStatsGUI(ch);
                }
                //if (ch == selectedCharacter && selectedToggle == ToggleChoice.Facts) {
                //    todo = FactsEditor.OnGUI(ch, ch.Facts.m_Facts);
                //}
                if (ch == selectedCharacter && selectedToggle == ToggleChoice.Features) {
                    todo = FactsEditor.OnGUI(ch, ch.Progression.Features.Enumerable.ToList());
                }
                if (ch == selectedCharacter && selectedToggle == ToggleChoice.Buffs) {
                    todo = FactsEditor.OnGUI(ch, ch.Descriptor.Buffs.Enumerable.ToList());
                }
                if (ch == selectedCharacter && selectedToggle == ToggleChoice.Abilities) {
                    todo = FactsEditor.OnGUI(ch, ch.Descriptor.Abilities.Enumerable.ToList());
                }
                if (ch == selectedCharacter && selectedToggle == ToggleChoice.Spells) {
                    todo = OnSpellsGUI(ch, spellbooks);
                }
                if (selectedCharacter != GetEditCharacter()) {
                    editingCharacterIndex = characterList.IndexOf(selectedCharacter);
                }
                chIndex += 1;
            }
            Space(25);
            if (recruitableCount > 0) {
                Label($"{recruitableCount} character(s) can be ".orange().bold() + " Recruited".cyan() + ". This allows you to add non party NPCs to your party as if they were mercenaries".green());
            }
            if (respecableCount > 0) {
                Label($"{respecableCount} character(s)  can be ".orange().bold() + "Respecced".cyan() + ". Pressing Respec will close the mod window and take you to character level up".green());
                Label("WARNING".yellow().bold() + " The Respec UI is ".orange() + "Non Interruptable".yellow().bold() + " please save before using".orange());
            }
            if (recruitableCount > 0 || respecableCount > 0) {
                Label("WARNING".yellow().bold() + " these features are ".orange() + "EXPERIMENTAL".yellow().bold() + " and uses unreleased and likely buggy code.".orange());
                Label("BACK UP".yellow().bold() + " before playing with this feature.You will lose your mythic ranks but you can restore them in this Party Editor.".orange());
            }
            Space(25);
            foreach (var action in todo)
                action();
            if (charToAdd != null) { UnitEntityDataUtils.AddCompanion(charToAdd); }
            if (charToRecruit != null) { UnitEntityDataUtils.RecruitCompanion(charToRecruit); }
            if (charToRemove != null) { UnitEntityDataUtils.RemoveCompanion(charToRemove); }
            if (charToUnrecruit != null) { charToUnrecruit.Ensure<UnitPartCompanion>().SetState(CompanionState.None); charToUnrecruit.Remove<UnitPartCompanion>(); }
        }
    }
}