using System;
using System.Collections.Generic;
using UnityEngine;

namespace DungeonDash
{
    [Serializable]
    public sealed class SaveData
    {
        public int coins = 25;
        public string characterId = "knight";
        public string equippedId;
        public List<Artifact> inventory = new();
        public string marketJson;

        const string Key = "DungeonDash.Save.v1";

        public static SaveData Load()
        {
            var json = PlayerPrefs.GetString(Key, string.Empty);
            var data = string.IsNullOrEmpty(json) ? new SaveData() : JsonUtility.FromJson<SaveData>(json);
            data.inventory ??= new List<Artifact>();
            return data;
        }

        public void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }
    }
}
