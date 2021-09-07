﻿using UnityEngine;
using UnityModManagerNet;
using UnityEngine.UI;
using UnityEditor;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Kingmaker;
using Kingmaker.AI.Blueprints;
using Kingmaker.AI.Blueprints.Considerations;
using Kingmaker.AreaLogic.Cutscenes;
using Kingmaker.AreaLogic.Etudes;
using Kingmaker.Armies;
using Kingmaker.Armies.TacticalCombat;
using Kingmaker.Armies.TacticalCombat.Blueprints;
using Kingmaker.Armies.TacticalCombat.Brain;
using Kingmaker.Armies.TacticalCombat.Brain.Considerations;
using Kingmaker.BarkBanters;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Area;
using Kingmaker.Blueprints.CharGen;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.Credits;
using Kingmaker.Blueprints.Encyclopedia;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.Items;
using Kingmaker.Blueprints.Items.Armors;
using Kingmaker.Blueprints.Items.Components;
using Kingmaker.Blueprints.Items.Equipment;
using Kingmaker.Blueprints.Items.Shields;
using Kingmaker.Blueprints.Items.Weapons;
using Kingmaker.Blueprints.Quests;
using Kingmaker.Blueprints.Root;
using Kingmaker.Cheats;
using Kingmaker.Blueprints.Console;
using Kingmaker.Controllers.Rest;
using Kingmaker.Designers;
using Kingmaker.DialogSystem.Blueprints;
using Kingmaker.Dungeon.Blueprints;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.GameModes;
using Kingmaker.Globalmap.Blueprints;
using Kingmaker.Interaction;
using Kingmaker.Items;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules.Damage;
using Kingmaker.Tutorial;
using Kingmaker.UI;
using Kingmaker.UI.Common;
using Kingmaker.UI.LevelUp;
//using Kingmaker.UI.LevelUp.Phase;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Buffs.Blueprints;
using Kingmaker.UnitLogic.Class.LevelUp;
using Kingmaker.UnitLogic.Class.LevelUp.Actions;
using Kingmaker.UnitLogic.Customization;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.Utility;
using Kingmaker.Visual.Sound;
using Kingmaker.Assets.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine.SceneManagement;
using static ModKit.Utility.ReflectionCache;
using UnityModManager = UnityModManagerNet.UnityModManager;
using ModKit;
using Kingmaker.UnitLogic.ActivatableAbilities.Restrictions;
using Kingmaker.Kingdom.Settlements;
using Kingmaker.Blueprints.Classes.Prerequisites;
using Kingmaker.Kingdom.Settlements.BuildingComponents;

namespace ToyBox.Multiclass {
    public enum ProgressionPolicy {
        PrimaryClass = 0,
        Average = 1,
        Largest = 2,
        Sum = 3,
    };
    public class Mod {
        //public HashSet<Type> AbilityCasterCheckerTypes { get; } =
        //    new HashSet<Type>(Assembly.GetAssembly(typeof(IAbilityCasterChecker)).GetTypes()
        //        .Where(type => typeof(IAbilityCasterChecker).IsAssignableFrom(type) && !type.IsInterface));

        public HashSet<Type> ActivatableAbilityRestrictionTypes { get; } =
            new HashSet<Type>(Assembly.GetAssembly(typeof(ActivatableAbilityRestriction)).GetTypes()
                .Where(type => type.IsSubclassOf(typeof(ActivatableAbilityRestriction))));

        public HashSet<Type> BuildingRestrictionTypes { get; } =
            new HashSet<Type>(Assembly.GetAssembly(typeof(BuildingRestriction)).GetTypes()
                .Where(type => type.IsSubclassOf(typeof(BuildingRestriction))).Except(new[] { typeof(DLCRestriction) }));

        public HashSet<Type> EquipmentRestrictionTypes { get; } =
            new HashSet<Type>(Assembly.GetAssembly(typeof(EquipmentRestriction)).GetTypes()
                .Where(type => type.IsSubclassOf(typeof(EquipmentRestriction))));

        public HashSet<Type> PrerequisiteTypes { get; } =
            new HashSet<Type>(Assembly.GetAssembly(typeof(Prerequisite)).GetTypes()
                .Where(type => type.IsSubclassOf(typeof(Prerequisite))));

        public HashSet<BlueprintCharacterClass> AppliedMulticlassSet { get; internal set; }
            = new HashSet<BlueprintCharacterClass>();

        public HashSet<BlueprintProgression> UpdatedProgressions { get; internal set; }
            = new HashSet<BlueprintProgression>();

        public LevelUpController LevelUpController { get; internal set; }

        public HashSet<MethodBase> LockedPatchedMethods { get; internal set; } = new HashSet<MethodBase>();

        public bool IsLevelLocked { get; internal set; }

        public Scene ActiveScene => SceneManager.GetActiveScene();

        public BlueprintCharacterClass[] CharacterClasses => Game.Instance.BlueprintRoot.Progression.CharacterClasses.ToArray();

        public BlueprintCharacterClass[] MythicClasses => Game.Instance.BlueprintRoot.Progression.CharacterMythics.ToArray();

        public BlueprintCharacterClass[] AllClasses => CharacterClasses.Concat(MythicClasses).ToArray();

        public BlueprintScriptableObject LibraryObject => typeof(ResourcesLibrary).GetFieldValue<BlueprintScriptableObject>("s_LoadedResources");//("s_LibraryObject");

        public Player Player => Game.Instance.Player;
    }

    public static class MulticlassUtils {
        public static Settings settings = Main.settings;
        public static Player player = Game.Instance.Player;
        public static UnityModManager.ModEntry.ModLogger modLogger = ModKit.Logger.modLogger;

        public static bool IsCharGen(this LevelUpState state) {
            return state.Mode == LevelUpState.CharBuildMode.CharGen;
        }

        public static bool IsLevelUp(this LevelUpState state) {
            return state.Mode == LevelUpState.CharBuildMode.LevelUp;
        }

        public static bool IsPreGen(this LevelUpState state) {
            return state.IsPregen;
        }

        public static HashSet<string> GetMulticlassSet(this UnitEntityData ch) {
            if (ch.HashKey() == null) return null;
            return Main.settings.selectedMulticlassSets.GetValueOrDefault(ch.HashKey(), new HashSet<string>());
        }
        public static void SetMulticlassSet(this UnitEntityData ch, HashSet<string> multiclassSet) {
            if (ch.HashKey() == null) return;
            Main.settings.selectedMulticlassSets[ch.HashKey()] = multiclassSet;
        }
        public static HashSet<string> GetMulticlassSet(this UnitDescriptor ch) {
            if (ch.HashKey() == null) return null;
            return Main.settings.selectedMulticlassSets.GetValueOrDefault(ch.HashKey(), new HashSet<string>());
        }
        public static void SetMulticlassSet(this UnitDescriptor ch, HashSet<string> multiclassSet) {
            if (ch.HashKey() == null) return;
            Main.settings.selectedMulticlassSets[ch.HashKey()] = multiclassSet;
        }
        public static HashSet<string> SelectedMulticlassSet(this UnitDescriptor unit, LevelUpState state) {
            HashSet<string> selectedMulticlassSet;
            if (!state.IsCharGen()) {
                modLogger.Log($"SelectedMulticlassSet - in game - {unit.CharacterName}");
                if (unit.Unit != null) {
                    selectedMulticlassSet = unit.Unit.Descriptor.GetMulticlassSet();
                }
                else {
                    selectedMulticlassSet = unit.GetMulticlassSet();
                }
            }
            else {
                modLogger.Log("SelectedMulticlassSet - chargen");
                selectedMulticlassSet = Main.settings.charGenMulticlassSet;
            }
            return selectedMulticlassSet;
        }

    }
}
