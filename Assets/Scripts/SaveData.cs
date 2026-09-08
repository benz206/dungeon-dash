using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonDash
{
    [Serializable]
    public sealed class SaveData
    {
        [Serializable]
        public sealed class CharacterSlot
        {
            public string characterId;
            public int coins = 25;
            public string equippedId;
            public List<Artifact> inventory = new();
            public RunCheckpoint run;
            public int tutorialActions;
            public int bestChamberCleared;
            public int lifetimeKills;

            public string GuildRank => bestChamberCleared >= 10 ? "CHAMPION"
                : bestChamberCleared >= 6 ? "WARDEN" : bestChamberCleared >= 3 ? "SCOUT" : "DELVER";
            public string NextGoal => bestChamberCleared >= 10 ? $"BEAT YOUR RECORD: {bestChamberCleared + 1:00}"
                : bestChamberCleared >= 6 ? "CLEAR 10 · EARN CHAMPION"
                : bestChamberCleared >= 3 ? "CLEAR 06 · EARN WARDEN" : "CLEAR 03 · EARN SCOUT";
        }

        public const int MaxSlots = 3;

        public List<CharacterSlot> slots = new();
        public int activeSlot;
        public bool guildCosmeticsOwned;
        public int cosmeticStyle;

        // Market state is shared across characters, not per-slot.
        public string marketJson;
        public int marketPendingCoinDelta;
        public bool marketAccountInitialized;
        public bool marketOnlineEnabled;
        public string marketPlayerId;
        public bool marketDeletionPending;
        public bool marketDataDeleted;

        static readonly string Key = Application.isEditor || Application.isBatchMode ||
            Array.Exists(Environment.GetCommandLineArgs(), argument => argument.StartsWith("--qa-", StringComparison.Ordinal))
                ? "DungeonDash.QA.Save.v2" : "DungeonDash.Save.v2";

        public static SaveData Load()
        {
            // v1 saves live under a different key and are intentionally not migrated:
            // a missing v2 save simply yields empty slots (fresh registry).
            var json = PlayerPrefs.GetString(Key, string.Empty);
            var data = string.IsNullOrEmpty(json) ? new SaveData() : JsonUtility.FromJson<SaveData>(json);
            data.slots ??= new List<CharacterSlot>();
            foreach (var slot in data.slots)
            {
                slot.inventory ??= new List<Artifact>();
                if (slot.run?.CanResume != true) continue;
                int knownClear = slot.run.wave - (slot.run.enemies.Count > 0 ? 1 : 0);
                slot.bestChamberCleared = Math.Max(slot.bestChamberCleared, knownClear);
                slot.lifetimeKills = Math.Max(slot.lifetimeKills, slot.run.kills);
            }
            return data;
        }

        public void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }

        public CharacterSlot CreateSlot(string characterId)
        {
            var slot = new CharacterSlot { characterId = characterId };
            slots.Add(slot);
            return slot;
        }

        public void DeleteSlot(int index)
        {
            if (index < 0 || index >= slots.Count) return;
            slots.RemoveAt(index);
            if (activeSlot >= slots.Count) activeSlot = Mathf.Max(0, slots.Count - 1);
        }

        public CharacterSlot ActiveSlotOrNull =>
            activeSlot >= 0 && activeSlot < slots.Count ? slots[activeSlot] : null;
    }
}
