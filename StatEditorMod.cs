using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace SunHavenStatEditorMod
{
    [BepInPlugin("stateditor", "Stat Editor Mod", "2.0.0")]
    public class StatEditorMod : BaseUnityPlugin
    {
        internal static ManualLogSource logger;

        private ConfigEntry<KeyboardShortcut> toggleKey;
        private ConfigEntry<float> maxBonusValue;
        private ConfigEntry<string> configBonuses;
        private readonly Dictionary<string, float> appliedConfigBonuses = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> warnedStats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private IDictionary lastStatBonuses;
        private float nextBonusApplyCheck;

        private static bool showGUI = false;
        private static Rect windowRect = new Rect(40, 40, 560, 640);
        private const int windowID = 41723;
        private static Vector2 scrollPos = Vector2.zero;
        private static string searchFilter = "";
        private static string goldInput = "";
        private static CursorLockMode prevCursorLock;
        private static bool prevCursorVisible;

        // Stat rows
        private class StatRow
        {
            public string Name;
            public object EnumValue;
            public string BonusInput = "";
        }

        private static readonly List<StatRow> rows = new List<StatRow>();
        private static bool rowsInitialized = false;
        private static bool typesCached = false;

        // Cached reflection targets
        private static Type statTypeType;
        private static Type playerType;
        private static object playerInstance;
        private static MethodInfo getMyStatMethod;
        private static MethodInfo disableInputMethod;
        private static MethodInfo enableInputMethod;
        private static PropertyInfo allowArrowKeysProp;
        private static bool prevAllowArrowKeys = true;
        private static IDictionary statBonuses;   // Dictionary<StatType, float>
        private static PropertyInfo coinsProp;    // worldData.coins
        private static object coinsTarget;        // worldData object

        private void Awake()
        {
            logger = Logger;
            logger.LogInfo("Stat Editor Mod v2 initializing...");

            toggleKey = Config.Bind("Settings", "Toggle Key", new KeyboardShortcut(KeyCode.F2),
                "Key to open/close the stat editor window");
            maxBonusValue = Config.Bind("Settings", "Max Bonus Value", 99999f,
                "Maximum absolute value allowed for stat bonus inputs");
            configBonuses = Config.Bind("Bonuses", "Stat Bonuses", "",
                "Permanent stat bonuses applied automatically once in-game, as a fallback if the GUI is unusable. " +
                "Format: Name=Value pairs separated by semicolons, e.g. Health=500;AttackDamage=100;GoldGain=25. " +
                "Set a bonus to 0 or remove the pair to clear it.");

            try
            {
                CacheGameTypes();
            }
            catch (Exception ex)
            {
                logger.LogError($"Stat Editor Mod: failed to resolve game types: {ex.Message}");
            }

            logger.LogInfo("Stat Editor Mod loaded.");
        }

        private void Update()
        {
            if (Time.realtimeSinceStartup >= nextBonusApplyCheck)
            {
                nextBonusApplyCheck = Time.realtimeSinceStartup + 1f;
                TryApplyConfigBonuses();
            }

            if (toggleKey.Value.IsDown())
            {
                if (showGUI) CloseEditor(); else OpenEditor();
            }
        }

        private void OnGUI()
        {
            if (!showGUI)
            {
                if (GUI.Button(new Rect(20, 20, 130, 30), "Stat Editor (F2)"))
                    OpenEditor();
                return;
            }

            windowRect = GUI.Window(windowID, windowRect, DrawWindow, "Sun Haven Stat Editor — by Aliester Eroan");
        }

        private void OpenEditor()
        {
            showGUI = true;
            EnsureData();
            LoadCurrentValues();
            prevCursorLock = Cursor.lockState;
            prevCursorVisible = Cursor.visible;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Block game input while the editor is open (same mechanism as the dev console)
            disableInputMethod?.Invoke(null, new object[] { "statEditor" });
            if (allowArrowKeysProp != null)
            {
                prevAllowArrowKeys = (bool)allowArrowKeysProp.GetValue(null);
                allowArrowKeysProp.SetValue(null, false);
            }
        }

        private void CloseEditor()
        {
            showGUI = false;
            Cursor.lockState = prevCursorLock;
            Cursor.visible = prevCursorVisible;

            enableInputMethod?.Invoke(null, new object[] { "statEditor" });
            allowArrowKeysProp?.SetValue(null, prevAllowArrowKeys);
        }

        private void DrawWindow(int id)
        {
            // Close button top-right; drag from the rest of the title bar
            if (GUI.Button(new Rect(windowRect.width - 32, 4, 26, 22), "X"))
            {
                CloseEditor();
                GUIUtility.ExitGUI();
            }
            GUI.DragWindow(new Rect(0, 0, windowRect.width - 36, 28));

            GUILayout.BeginVertical();
            GUILayout.Space(30);

            if (!rowsInitialized)
            {
                GUILayout.Label("Game data not available (not in a save?).");
                GUILayout.EndVertical();
                return;
            }

            // Search filter
            GUILayout.BeginHorizontal();
            GUILayout.Label("Filter:", GUILayout.Width(45));
            string newFilter = GUILayout.TextField(searchFilter, GUILayout.Width(220));
            if (newFilter != searchFilter)
            {
                searchFilter = newFilter;
                scrollPos = Vector2.zero;
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Refresh", GUILayout.Width(70)))
                LoadCurrentValues();
            GUILayout.EndHorizontal();
            GUILayout.Space(5);

            // Scrollable stat list
            scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.Height(windowRect.height - 190));

            string filter = searchFilter ?? "";
            foreach (var row in rows)
            {
                if (filter.Length > 0 && row.Name.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                GUILayout.BeginHorizontal();
                GUILayout.Label(row.Name, GUILayout.Width(180));

                float current = GetCurrentStat(row);
                GUILayout.Label($"cur {current:0.##}", GUILayout.Width(80));

                GUILayout.Label("bonus:", GUILayout.Width(50));
                row.BonusInput = GUILayout.TextField(row.BonusInput, GUILayout.Width(80));

                if (GUILayout.Button("+", GUILayout.Width(24))) AdjustBonus(row, 1f);
                if (GUILayout.Button("-", GUILayout.Width(24))) AdjustBonus(row, -1f);
                if (GUILayout.Button("CLR", GUILayout.Width(44))) ClearBonus(row);

                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();

            GUILayout.Space(8);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Apply Bonuses", GUILayout.Height(30)))
                ApplyBonuses();
            if (GUILayout.Button("Reset All Bonuses", GUILayout.Height(30)))
                ResetBonuses();
            GUILayout.EndHorizontal();

            GUILayout.Space(8);
            GUILayout.Label("Gold:");
            GUILayout.BeginHorizontal();
            goldInput = GUILayout.TextField(goldInput, GUILayout.Width(160));
            if (GUILayout.Button("Set Gold", GUILayout.Width(80)))
                ApplyGold();
            GUILayout.EndHorizontal();

            GUILayout.Space(4);
            GUILayout.Label("'cur' = live total. Bonus adds on top — set 0 to remove, negative to reduce.");

            GUILayout.EndVertical();
        }

        private float GetCurrentStat(StatRow row)
        {
            try
            {
                if (playerInstance == null || getMyStatMethod == null) return 0f;
                return (float)getMyStatMethod.Invoke(playerInstance, new object[] { row.EnumValue });
            }
            catch { return 0f; }
        }

        private void AdjustBonus(StatRow row, float delta)
        {
            float.TryParse(row.BonusInput, NumberStyles.Float, CultureInfo.InvariantCulture, out float v);
            row.BonusInput = (v + delta).ToString("0.##", CultureInfo.InvariantCulture);
        }

        private void ClearBonus(StatRow row)
        {
            try
            {
                RefreshSaveData();
                statBonuses?.Remove(row.EnumValue);
                row.BonusInput = "";
            }
            catch (Exception ex)
            {
                logger.LogError($"Stat Editor: ClearBonus failed: {ex.Message}");
            }
        }

        private void ApplyBonuses()
        {
            try
            {
                if (statBonuses == null) { RefreshSaveData(); if (statBonuses == null) return; }
                int applied = 0;
                foreach (var row in rows)
                {
                    if (string.IsNullOrWhiteSpace(row.BonusInput)) continue;
                    if (!float.TryParse(row.BonusInput, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)) continue;
                    value = Mathf.Clamp(value, -maxBonusValue.Value, maxBonusValue.Value);
                    if (Mathf.Approximately(value, 0f))
                        statBonuses.Remove(row.EnumValue);
                    else
                        statBonuses[row.EnumValue] = value;
                    applied++;
                }
                logger.LogInfo($"Stat Editor: applied {applied} stat bonuses");
            }
            catch (Exception ex)
            {
                logger.LogError($"Stat Editor: ApplyBonuses failed: {ex.Message}");
            }
        }

        private void ResetBonuses()
        {
            try
            {
                RefreshSaveData();
                if (statBonuses == null) return;
                statBonuses.Clear();
                foreach (var row in rows) row.BonusInput = "";
                logger.LogInfo("Stat Editor: cleared all stat bonuses");
            }
            catch (Exception ex)
            {
                logger.LogError($"Stat Editor: ResetBonuses failed: {ex.Message}");
            }
        }

        private void ApplyGold()
        {
            try
            {
                if (!int.TryParse(goldInput, NumberStyles.Integer, CultureInfo.InvariantCulture, out int gold)) return;
                RefreshSaveData();
                if (coinsProp != null && coinsTarget != null)
                {
                    coinsProp.SetValue(coinsTarget, gold);
                    logger.LogInfo($"Stat Editor: set gold to {gold}");
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Stat Editor: ApplyGold failed: {ex.Message}");
            }
        }

        private void TryApplyConfigBonuses()
        {
            try
            {
                string cfg = configBonuses?.Value;
                if (string.IsNullOrWhiteSpace(cfg))
                {
                    // Config cleared — remove anything we previously applied from it
                    // (but only if the bonus dict is still the same save's — a new save keeps its own bonuses)
                    if (appliedConfigBonuses.Count > 0)
                    {
                        RefreshSaveData();
                        if (ReferenceEquals(statBonuses, lastStatBonuses))
                            foreach (var name in appliedConfigBonuses.Keys)
                                RemoveConfigBonus(name);
                        appliedConfigBonuses.Clear();
                        lastStatBonuses = statBonuses;
                    }
                    return;
                }

                // Parse desired state: Name=Value;Name=Value
                var desired = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
                foreach (var pair in cfg.Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    int eq = pair.IndexOf('=');
                    if (eq <= 0) continue;
                    string name = pair.Substring(0, eq).Trim();
                    if (float.TryParse(pair.Substring(eq + 1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
                        desired[name] = Mathf.Clamp(v, -maxBonusValue.Value, maxBonusValue.Value);
                }

                // Skip if nothing changed since last apply
                if (desired.Count == appliedConfigBonuses.Count)
                {
                    bool same = true;
                    foreach (var kv in desired)
                        if (!appliedConfigBonuses.TryGetValue(kv.Key, out float old) || !Mathf.Approximately(old, kv.Value)) { same = false; break; }
                    if (same) return;
                }

                RefreshSaveData();
                if (statBonuses == null || statTypeType == null) return; // not in a save yet — retry next tick

                // Save/character switched — the bonus dict is a new object, re-apply everything
                if (!ReferenceEquals(statBonuses, lastStatBonuses))
                {
                    lastStatBonuses = statBonuses;
                    appliedConfigBonuses.Clear();
                }

                // Remove bonuses no longer in config
                foreach (var name in new List<string>(appliedConfigBonuses.Keys))
                {
                    if (!desired.ContainsKey(name))
                    {
                        RemoveConfigBonus(name);
                        appliedConfigBonuses.Remove(name);
                    }
                }

                // Set current desired values
                foreach (var kv in desired)
                {
                    if (TryGetStatEnum(kv.Key, out object stat))
                    {
                        if (Mathf.Approximately(kv.Value, 0f))
                            statBonuses.Remove(stat);
                        else
                            statBonuses[stat] = kv.Value;
                        appliedConfigBonuses[kv.Key] = kv.Value;
                    }
                    else if (warnedStats.Add(kv.Key))
                    {
                        logger.LogWarning($"Stat Editor: unknown stat name in config: '{kv.Key}'");
                    }
                }
                logger.LogInfo($"Stat Editor: applied {desired.Count} config stat bonuses");
            }
            catch (Exception ex)
            {
                logger.LogError($"Stat Editor: TryApplyConfigBonuses failed: {ex.Message}");
            }
        }

        private void RemoveConfigBonus(string name)
        {
            if (statBonuses == null) return;
            if (TryGetStatEnum(name, out object stat))
                statBonuses.Remove(stat);
        }

        private bool TryGetStatEnum(string name, out object stat)
        {
            stat = null;
            if (statTypeType == null) return false;
            foreach (var n in Enum.GetNames(statTypeType))
            {
                if (string.Equals(n, name, StringComparison.OrdinalIgnoreCase))
                {
                    stat = Enum.Parse(statTypeType, n);
                    return true;
                }
            }
            return false;
        }

        private void EnsureData()
        {
            if (rowsInitialized) return;
            try
            {
                CacheGameTypes();
                LoadCurrentValues();
                rowsInitialized = rows.Count > 0;
            }
            catch (Exception ex)
            {
                logger.LogError($"Stat Editor: EnsureData failed: {ex.Message}");
            }
        }

        private void LoadCurrentValues()
        {
            try
            {
                RefreshSaveData();
                if (rows.Count == 0) BuildRows();

                foreach (var row in rows)
                {
                    float bonus = 0f;
                    if (statBonuses != null && statBonuses.Contains(row.EnumValue))
                        bonus = Convert.ToSingle(statBonuses[row.EnumValue], CultureInfo.InvariantCulture);
                    row.BonusInput = bonus.ToString("0.##", CultureInfo.InvariantCulture);
                }

                if (coinsProp != null && coinsTarget != null)
                    goldInput = Convert.ToInt32(coinsProp.GetValue(coinsTarget), CultureInfo.InvariantCulture).ToString();
            }
            catch (Exception ex)
            {
                logger.LogError($"Stat Editor: LoadCurrentValues failed: {ex.Message}");
            }
        }

        private void BuildRows()
        {
            rows.Clear();
            if (statTypeType == null) return;
            foreach (var name in Enum.GetNames(statTypeType))
            {
                rows.Add(new StatRow
                {
                    Name = name,
                    EnumValue = Enum.Parse(statTypeType, name)
                });
            }
            logger.LogInfo($"Stat Editor: built {rows.Count} stat rows");
        }

        private void RefreshSaveData()
        {
            try
            {
                // Player.Instance (static) — needed for GetMyStat; re-resolve if destroyed
                if (playerType != null && (playerInstance == null || playerInstance is UnityEngine.Object uo && uo == null))
                {
                    playerInstance = null;
                    var instProp = playerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                    if (instProp != null) playerInstance = instProp.GetValue(null);
                    if (playerInstance == null)
                    {
                        var instField = playerType.GetField("Instance", BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                        if (instField != null) playerInstance = instField.GetValue(null);
                    }
                }

                // GameSave -> CurrentSave -> characterData -> StatBonuses / worldData -> coins
                var gameSaveType = AccessTools.TypeByName("Wish.GameSave");
                if (gameSaveType == null) return;
                var singletonType = AccessTools.TypeByName("Wish.SingletonBehaviour`1");
                if (singletonType == null) return;
                var generic = singletonType.MakeGenericType(gameSaveType);
                var instanceProp = generic.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceProp == null) return;
                var gameSave = instanceProp.GetValue(null);
                if (gameSave == null) return;

                var currentSaveProp = gameSaveType.GetProperty("CurrentSave");
                var currentSave = currentSaveProp?.GetValue(gameSave);
                if (currentSave == null) return;

                var charDataProp = currentSave.GetType().GetProperty("characterData");
                var charData = charDataProp?.GetValue(currentSave);
                if (charData != null)
                {
                    var sbProp = charData.GetType().GetProperty("StatBonuses");
                    statBonuses = sbProp?.GetValue(charData) as IDictionary;
                }

                var worldDataProp = currentSave.GetType().GetProperty("worldData");
                var worldData = worldDataProp?.GetValue(currentSave);
                if (worldData != null)
                {
                    coinsTarget = worldData;
                    coinsProp = worldData.GetType().GetProperty("coins");
                }
            }
            catch (Exception ex)
            {
                logger.LogError($"Stat Editor: RefreshSaveData failed: {ex.Message}");
            }
        }

        private void CacheGameTypes()
        {
            if (typesCached) return;
            typesCached = true;

            statTypeType = AccessTools.TypeByName("Wish.StatType");
            playerType = AccessTools.TypeByName("Wish.Player");

            // Wish.PlayerInput static helpers — same input gate the dev console uses
            var playerInputType = AccessTools.TypeByName("Wish.PlayerInput");
            if (playerInputType != null)
            {
                disableInputMethod = playerInputType.GetMethod("DisableInput", BindingFlags.Public | BindingFlags.Static);
                enableInputMethod = playerInputType.GetMethod("EnableInput", BindingFlags.Public | BindingFlags.Static);
                allowArrowKeysProp = playerInputType.GetProperty("AllowArrowKeys", BindingFlags.Public | BindingFlags.Static);
            }
            if (disableInputMethod == null || enableInputMethod == null)
                logger.LogWarning("Stat Editor: Wish.PlayerInput DisableInput/EnableInput not found — game input will not be blocked");

            if (playerType != null && statTypeType != null)
            {
                getMyStatMethod = playerType.GetMethod("GetMyStat", new Type[] { statTypeType });
                if (getMyStatMethod == null)
                    logger.LogWarning("Stat Editor: Wish.Player.GetMyStat not found");
            }
            else
            {
                logger.LogWarning("Stat Editor: Wish.Player or Wish.StatType not found");
            }
        }
    }
}
