using System.Collections.Generic;
using System.Linq;
using gishadev.tools.Audio;
using gishadev.tools.Effects;
using gishadev.tools.Pooling;
using UnityEditor;
using UnityEngine;

namespace gishadev.tools.Editor
{
    /// <summary>Drop-down of the SFX pool's names (from the project's first PoolDataSO).</summary>
    [CustomPropertyDrawer(typeof(SFXPoolRef))]
    public class SFXPoolRefDrawer : SoundRefDrawer
    {
        protected override IReadOnlyList<string> GetNames()
        {
            var pool = FindAsset<PoolDataSO>();
            return pool != null && pool.SFXPoolObjects != null
                ? pool.SFXPoolObjects.Where(o => o != null).Select(o => o.Name).ToList()
                : null;
        }
    }

    /// <summary>Drop-down of the SFX bank's names (from the project's first AudioMasterSO).</summary>
    [CustomPropertyDrawer(typeof(SFXAudioRef))]
    public class SFXAudioRefDrawer : SoundRefDrawer
    {
        protected override IReadOnlyList<string> GetNames()
        {
            var bank = FindAsset<AudioMasterSO>();
            return bank != null && bank.SFXCollection != null
                ? bank.SFXCollection.Where(d => d != null).Select(d => d.Name).ToList()
                : null;
        }
    }

    /// <summary>
    /// A name picked from a list, with "(none)" first. A saved name the list no longer has is kept and shown as
    /// missing, so renaming an entry never silently re-points a field at another sound.
    /// </summary>
    public abstract class SoundRefDrawer : PropertyDrawer
    {
        private const string NoneLabel = "(none)";

        protected abstract IReadOnlyList<string> GetNames();

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var nameProperty = property.FindPropertyRelative("name");
            var names = GetNames();
            using var _ = new EditorGUI.PropertyScope(position, label, property);

            if (names == null)
            {
                // No pool/bank asset in the project: still editable by hand.
                nameProperty.stringValue = EditorGUI.TextField(position, label, nameProperty.stringValue);
                return;
            }

            var current = nameProperty.stringValue;
            var options = new List<string> { NoneLabel };
            options.AddRange(names);

            int selected = string.IsNullOrEmpty(current) ? 0 : options.IndexOf(current);
            if (selected < 0)
            {
                options.Add($"{current} (missing)");
                selected = options.Count - 1;
            }

            using var check = new EditorGUI.ChangeCheckScope();
            int picked = EditorGUI.Popup(position, label.text, selected, options.ToArray());
            if (!check.changed || picked == selected)
                return;

            nameProperty.stringValue = picked == 0 ? string.Empty : options[picked];
        }

        protected static T FindAsset<T>() where T : Object
        {
            var guid = AssetDatabase.FindAssets($"t:{typeof(T).Name}").FirstOrDefault();
            return guid != null ? AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)) : null;
        }
    }
}
