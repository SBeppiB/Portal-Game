using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Portals.Save
{
    /// <summary>Slot listing metadata (safe to show in menus without loading full state).</summary>
    public sealed class SaveSlotInfo
    {
        public int slot;
        public bool exists;
        public bool valid;
        public string saveName;
        public string sceneName;
        public DateTime savedAt;
        public int version;
    }

    /// <summary>
    /// Persistent multi-slot save system. Saves live in a dedicated directory
    /// (persistentDataPath/Saves) - never mixed with DebugLogs. Writes are atomic:
    /// the payload is serialised to a .tmp file first and then swapped over the
    /// destination, so an interrupted save cannot corrupt an existing file.
    /// Missing, corrupt and older-version files are handled gracefully.
    /// </summary>
    public static class SaveSystem
    {
        public const int CurrentVersion = 1;
        public const int SlotCount = 5;

        private static string _directoryOverride;

        /// <summary>Directory holding slot_N.json files.</summary>
        public static string SaveDirectory =>
            _directoryOverride ?? Path.Combine(Application.persistentDataPath, "Saves");

        /// <summary>Redirects save IO (tests, custom installs).</summary>
        public static void SetDirectoryOverride(string directory) => _directoryOverride = directory;

        public static string SlotPath(int slot) => Path.Combine(SaveDirectory, $"slot_{slot}.json");

        public static bool IsValidSlot(int slot) => slot >= 0 && slot < SlotCount;

        public static bool SlotExists(int slot) =>
            IsValidSlot(slot) && File.Exists(SlotPath(slot));

        /// <summary>
        /// Serialises and atomically writes a slot. Returns false (with a reason)
        /// on any failure - success is only reported when the file is on disk.
        /// </summary>
        public static bool Save(int slot, SaveGameData data, out string error)
        {
            error = null;
            if (data == null) { error = "Save data is null."; return false; }
            if (!IsValidSlot(slot)) { error = $"Invalid slot {slot}."; return false; }

            try
            {
                Directory.CreateDirectory(SaveDirectory);

                data.slot = slot;
                data.version = CurrentVersion;
                if (string.IsNullOrEmpty(data.savedAtIso))
                    data.savedAtIso = DateTime.Now.ToString("o");

                string path = SlotPath(slot);
                string tempPath = path + ".tmp";
                string json = JsonUtility.ToJson(data, true);

                // 1) fully write the temp file, 2) atomically swap it into place.
                File.WriteAllText(tempPath, json);
                if (File.Exists(path))
                    File.Replace(tempPath, path, null);
                else
                    File.Move(tempPath, path);

                if (!File.Exists(path))
                {
                    error = "Save file missing after write.";
                    return false;
                }
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                try { if (File.Exists(SlotPath(slot) + ".tmp")) File.Delete(SlotPath(slot) + ".tmp"); } catch { }
                Debug.LogWarning($"[SaveSystem] Save to slot {slot} failed: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Reads and validates a slot. Returns null when the slot is missing,
        /// corrupt, unreadable, from a newer format, or failed migration -
        /// never throws for bad input.
        /// </summary>
        public static SaveGameData Load(int slot)
        {
            if (!IsValidSlot(slot)) return null;
            string path = SlotPath(slot);
            if (!File.Exists(path)) return null;

            try
            {
                string json = File.ReadAllText(path);
                if (string.IsNullOrWhiteSpace(json))
                {
                    Debug.LogWarning($"[SaveSystem] Slot {slot} is empty.");
                    return null;
                }

                SaveGameData data = JsonUtility.FromJson<SaveGameData>(json);
                if (data == null || string.IsNullOrEmpty(data.sceneName))
                {
                    Debug.LogWarning($"[SaveSystem] Slot {slot} is not a valid save file.");
                    return null;
                }

                if (data.version > CurrentVersion)
                {
                    Debug.LogWarning(
                        $"[SaveSystem] Slot {slot} uses format v{data.version} (supported up to v{CurrentVersion}); refusing to load.");
                    return null;
                }

                if (data.version < CurrentVersion && !Migrate(data))
                {
                    Debug.LogWarning($"[SaveSystem] Slot {slot} (v{data.version}) could not be migrated.");
                    return null;
                }

                data.slot = slot;
                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] Slot {slot} is corrupt: {e.Message}");
                return null;
            }
        }

        /// <summary>Upgrades older payloads to the current schema. False = unloadable.</summary>
        private static bool Migrate(SaveGameData data)
        {
            // v1 is the initial format; future versions migrate step by step:
            // while (data.version < CurrentVersion) { switch (data.version) {...} }
            return data.version >= 1;
        }

        /// <summary>Menu-facing slot summary; exists=true even for corrupt files.</summary>
        public static SaveSlotInfo GetSlotInfo(int slot)
        {
            var info = new SaveSlotInfo { slot = slot, exists = SlotExists(slot) };
            if (!info.exists) return info;

            SaveGameData data = Load(slot);
            if (data == null) return info; // exists but invalid

            info.valid = true;
            info.saveName = data.saveName;
            info.sceneName = data.sceneName;
            info.version = data.version;
            DateTime.TryParse(data.savedAtIso, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime at);
            info.savedAt = at;
            return info;
        }

        /// <summary>Highest slot number with a valid save, or -1.</summary>
        public static int MostRecentValidSlot()
        {
            int best = -1;
            DateTime bestTime = DateTime.MinValue;
            for (int i = 0; i < SlotCount; i++)
            {
                SaveGameData data = Load(i);
                if (data == null) continue;
                if (!DateTime.TryParse(data.savedAtIso, null,
                        System.Globalization.DateTimeStyles.RoundtripKind, out DateTime at))
                    at = DateTime.MinValue;
                if (at >= bestTime)
                {
                    bestTime = at;
                    best = i;
                }
            }
            return best;
        }

        public static bool DeleteSlot(int slot)
        {
            if (!SlotExists(slot)) return false;
            try { File.Delete(SlotPath(slot)); return true; }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveSystem] Delete slot {slot} failed: {e.Message}");
                return false;
            }
        }

        /// <summary>Test hook: clears the directory override.</summary>
        public static void ClearDirectoryOverride() => _directoryOverride = null;
    }
}
