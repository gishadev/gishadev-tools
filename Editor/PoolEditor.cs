using gishadev.tools.Core;
using gishadev.tools.Pooling;
using UnityEditor;
using UnityEngine;

namespace gishadev.tools.Editor
{
    [CustomEditor(typeof(PoolDataSO))]
    public class PoolEditor : UnityEditor.Editor
    {
        private PoolDataSO _poolData;
        
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            EmptyEntriesCleaner.DrawInspectorBox(target);

            _poolData = (PoolDataSO) target;
            EditorGUILayout.Space();
            EditorDropAreaCreator<SFXPoolObject, GameObject>.Create(_poolData, _poolData.SFXPoolObjects);
            EditorGUILayout.Space();
            EditorDropAreaCreator<VFXPoolObject, GameObject>.Create(_poolData, _poolData.VFXPoolObjects);
            EditorGUILayout.Space();
            EditorDropAreaCreator<OtherPoolObject, GameObject>.Create(_poolData, _poolData.OtherPoolObjects);
            
            new PolishEditorStyles().CreateBigButton("GENERATE ENUMS", () =>
            {
                var enumsGen = (ScriptableObjectEnumsGenerator)target;
                enumsGen.OnCollectionChanged();
            });
            
            EditorUtility.SetDirty(_poolData);
        }
    }
}