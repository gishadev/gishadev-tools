using System.Collections.Generic;
using System.Linq;
using gishadev.tools.Audio;
using gishadev.tools.Core;
using gishadev.tools.Pooling;
using UnityEditor;
using UnityEngine;

namespace gishadev.tools.Editor
{
    /// <summary>
    /// Clears empty references out of <see cref="AudioMasterSO"/> and <see cref="PoolDataSO"/>:
    /// <list type="bullet">
    /// <item>Empty slots: a <c>None</c> or missing clip/prefab inside an entry. Removing them changes nothing else, so it
    /// also runs on its own whenever assets are deleted (e.g. SFX Prep export dropping unticked clips).</item>
    /// <item>Empty entries: an entry left with no clips (audio) or no prefabs (pool). Removing them shifts the generated
    /// enums, so it only runs from the inspector button, which regenerates them.</item>
    /// </list>
    /// </summary>
    public static class EmptyEntriesCleaner
    {
        /// <summary>A collection and the reference array its entries need to be useful.</summary>
        private readonly struct Rule
        {
            public readonly string Collection;
            public readonly string Required;

            public Rule(string collection, string required)
            {
                Collection = collection;
                Required = required;
            }
        }

        private static readonly Rule[] AudioRules =
        {
            new(nameof(AudioMasterSO.MusicCollection), nameof(AudioData.AudioClips)),
            new(nameof(AudioMasterSO.SFXCollection), nameof(AudioData.AudioClips)),
        };

        private static readonly Rule[] PoolRules =
        {
            new(nameof(PoolDataSO.SFXPoolObjects), nameof(PoolObject.Prefabs)),
            new(nameof(PoolDataSO.VFXPoolObjects), nameof(PoolObject.Prefabs)),
            new(nameof(PoolDataSO.OtherPoolObjects), nameof(PoolObject.Prefabs)),
        };

        public static int CountEmptySlots(Object target) =>
            ReferenceArrays(new SerializedObject(target)).Sum(array => EmptyIndices(array).Count());

        /// <summary>Names (or positions) of the entries with nothing left in their clips/prefabs.</summary>
        public static List<string> EmptyEntries(Object target)
        {
            var result = new List<string>();
            var so = new SerializedObject(target);
            foreach (var rule in RulesFor(target))
            {
                var collection = so.FindProperty(Field(rule.Collection));
                for (var i = 0; collection != null && i < collection.arraySize; i++)
                {
                    if (IsEmptyEntry(collection.GetArrayElementAtIndex(i), rule))
                        result.Add(EntryLabel(collection, i, rule));
                }
            }

            return result;
        }

        /// <summary>Removes empty slots inside the entries. Returns how many went.</summary>
        public static int RemoveEmptySlots(Object target, bool undo = true)
        {
            var so = new SerializedObject(target);
            var removed = 0;
            foreach (var array in ReferenceArrays(so).ToList())
            {
                foreach (var i in EmptyIndices(array).OrderByDescending(i => i).ToList())
                {
                    array.DeleteArrayElementAtIndex(i);
                    removed++;
                }
            }

            if (removed > 0)
                Apply(so, undo);
            return removed;
        }

        /// <summary>Removes the empty slots, then the entries left with no clips/prefabs. Returns the entries removed.</summary>
        public static List<string> RemoveEmptySlotsAndEntries(Object target)
        {
            RemoveEmptySlots(target);
            var so = new SerializedObject(target);
            var removed = new List<string>();
            foreach (var rule in RulesFor(target))
            {
                var collection = so.FindProperty(Field(rule.Collection));
                for (var i = collection == null ? -1 : collection.arraySize - 1; i >= 0; i--)
                {
                    if (!IsEmptyEntry(collection.GetArrayElementAtIndex(i), rule))
                        continue;
                    removed.Insert(0, EntryLabel(collection, i, rule));
                    collection.DeleteArrayElementAtIndex(i);
                }
            }

            if (removed.Count > 0)
                Apply(so, true);
            return removed;
        }

        /// <summary>The inspector box: what's empty, and a button that clears it.</summary>
        public static void DrawInspectorBox(Object target)
        {
            var slots = CountEmptySlots(target);
            var entries = EmptyEntries(target);
            if (slots == 0 && entries.Count == 0)
                return;

            var parts = new List<string>();
            if (slots > 0)
                parts.Add($"{slots} empty slot(s) (None or missing)");
            if (entries.Count > 0)
                parts.Add($"{entries.Count} entr{(entries.Count == 1 ? "y" : "ies")} with nothing in it: {string.Join(", ", entries.Take(6))}" +
                          (entries.Count > 6 ? "..." : ""));

            EditorGUILayout.HelpBox(string.Join("\n", parts), MessageType.Warning);
            if (!GUILayout.Button(entries.Count > 0 ? "Remove empty slots and entries" : "Remove empty slots"))
                return;

            if (entries.Count > 0)
            {
                if (!EditorUtility.DisplayDialog("Remove empty entries",
                        $"Remove {entries.Count} entr{(entries.Count == 1 ? "y" : "ies")} with no clips/prefabs?\n\n" +
                        string.Join("\n", entries) + "\n\nThe generated enums are regenerated, so entries after them " +
                        "shift. Fields saved as SFXPoolRef / SFXAudioRef keep pointing at the right sound.",
                        "Remove", "Cancel"))
                    GUIUtility.ExitGUI();

                RemoveEmptySlotsAndEntries(target);
                ((ScriptableObjectEnumsGenerator)target).OnCollectionChanged();
            }
            else
            {
                RemoveEmptySlots(target);
            }

            GUIUtility.ExitGUI();
        }

        private static IEnumerable<Rule> RulesFor(Object target) => target switch
        {
            AudioMasterSO => AudioRules,
            PoolDataSO => PoolRules,
            _ => Enumerable.Empty<Rule>(),
        };

        private static bool IsEmptyEntry(SerializedProperty entry, Rule rule)
        {
            var required = entry.FindPropertyRelative(Field(rule.Required));
            return required == null || Enumerable.Range(0, required.arraySize)
                .All(i => IsEmpty(required.GetArrayElementAtIndex(i)));
        }

        private static string EntryLabel(SerializedProperty collection, int index, Rule rule)
        {
            var name = collection.GetArrayElementAtIndex(index).FindPropertyRelative(Field("Name"))?.stringValue;
            return string.IsNullOrEmpty(name) ? $"{rule.Collection}[{index}]" : name;
        }

        /// <summary>Every array of object references in the asset, nested ones included.</summary>
        private static IEnumerable<SerializedProperty> ReferenceArrays(SerializedObject so)
        {
            var paths = new List<string>();
            var it = so.GetIterator();
            while (it.Next(true))
            {
                if (it.isArray && it.propertyType == SerializedPropertyType.Generic && it.arrayElementType.StartsWith("PPtr<"))
                    paths.Add(it.propertyPath);
            }

            return paths.Select(so.FindProperty);
        }

        private static IEnumerable<int> EmptyIndices(SerializedProperty array) =>
            Enumerable.Range(0, array.arraySize).Where(i => IsEmpty(array.GetArrayElementAtIndex(i)));

        // A missing reference (deleted asset) reads as null too.
        private static bool IsEmpty(SerializedProperty element) => element.objectReferenceValue == null;

        private static void Apply(SerializedObject so, bool undo)
        {
            if (undo)
                so.ApplyModifiedProperties();
            else
                so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssetIfDirty(so.targetObject);
        }

        private static string Field(string property) => $"<{property}>k__BackingField";

        /// <summary>When assets are deleted, clears the slots that pointed at them in every audio master and pool asset.</summary>
        private class DeletionWatcher : AssetPostprocessor
        {
            private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
            {
                if (deleted.Length == 0)
                    return;

                // After the import finishes: assets shouldn't be edited inside the postprocess callback.
                EditorApplication.delayCall += () =>
                {
                    var guids = AssetDatabase.FindAssets($"t:{nameof(AudioMasterSO)}")
                        .Concat(AssetDatabase.FindAssets($"t:{nameof(PoolDataSO)}"));
                    foreach (var guid in guids)
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                        var removed = asset != null ? RemoveEmptySlots(asset, undo: false) : 0;
                        if (removed > 0)
                            Debug.Log($"gishadev tools: removed {removed} empty slot(s) from {path} after assets were deleted.", asset);
                    }
                };
            }
        }
    }
}
