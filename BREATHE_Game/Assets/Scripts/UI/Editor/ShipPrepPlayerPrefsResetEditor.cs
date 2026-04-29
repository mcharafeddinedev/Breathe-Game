using UnityEditor;
using UnityEngine;
using Breathe.UI;

namespace Breathe.Editor
{
    [CustomEditor(typeof(ShipPrepPlayerPrefsReset))]
    sealed class ShipPrepPlayerPrefsResetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox(
                "Clears all PlayerPrefs and writes shipping audio defaults. Works in Edit Mode (prefs on disk) or Play Mode (also refreshes music / optional scene reload).",
                MessageType.Info);

            var t = (ShipPrepPlayerPrefsReset)target;
            if (GUILayout.Button("Run ship prep (reset PlayerPrefs)", GUILayout.Height(30f)))
                t.ExecuteShipPrep();
        }
    }
}
