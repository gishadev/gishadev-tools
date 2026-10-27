using gishadev.tools.Core;
using gishadev.tools.Audio;
using UnityEditor;
using UnityEngine;

namespace gishadev.tools.Editor
{
    [CustomEditor(typeof(AudioMasterSO))]
    public class AudioEditor : UnityEditor.Editor
    {
        private AudioMasterSO _audioMasterSO;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            EmptyEntriesCleaner.DrawInspectorBox(target);

            _audioMasterSO = (AudioMasterSO)target;
            EditorGUILayout.Space();
            EditorDropAreaCreator<MusicData, AudioClip>.Create(_audioMasterSO, _audioMasterSO.MusicCollection);
            EditorGUILayout.Space();
            EditorDropAreaCreator<SFXData, AudioClip>.Create(_audioMasterSO, _audioMasterSO.SFXCollection);

            new PolishEditorStyles().CreateBigButton("GENERATE ENUMS", () =>
            {
                var enumsGen = (ScriptableObjectEnumsGenerator)target;
                enumsGen.OnCollectionChanged();
            });
            
            EditorUtility.SetDirty(_audioMasterSO);
        }
    }
}