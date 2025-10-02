using UnityEditor;
using UnityEngine;

namespace Utils.Editor
{
    [CustomEditor(typeof(ImageToColumnGenManager))]
    public class ImageToColumnGenManagerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            ImageToColumnGenManager component = (ImageToColumnGenManager)target;

            if (GUILayout.Button("Generate Columns"))
            {
                component.GenerateColumns();
            }
        }
    }
}