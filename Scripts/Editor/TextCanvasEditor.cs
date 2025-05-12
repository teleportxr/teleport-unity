using UnityEditor;
using UnityEngine;

namespace teleport
{
    [CustomEditor(typeof(teleport.TextCanvas))]
	[HelpURL("https://docs.teleportvr.io/unity.html")]
	public class TextCanvasEditor : Editor
    {
        public override void OnInspectorGUI()
        {
			teleport.TextCanvas textCanvas = (teleport.TextCanvas)target;
			textCanvas.text=EditorGUILayout.TextArea(textCanvas.text);
			textCanvas.font=(Font)EditorGUILayout.ObjectField("Font",textCanvas.font,typeof(Font),false);
			textCanvas.size=EditorGUILayout.IntField("Size (px)",textCanvas.size);
			textCanvas.lineHeight=EditorGUILayout.FloatField("Line Height",textCanvas.lineHeight);
			textCanvas.colour=EditorGUILayout.ColorField("Colour",textCanvas.colour);
        }
    }
}