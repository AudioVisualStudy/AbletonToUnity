using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MultiChannelWaveformViewer))]
public sealed class MultiChannelWaveformViewerEditor : Editor
{
    SerializedProperty _deviceIndex;
    SerializedProperty _channelEnabled;
    SerializedProperty _timeScaleSeconds;
    SerializedProperty _resolution;
    SerializedProperty _displayWidth;
    SerializedProperty _displayHeight;
    SerializedProperty _amplitude;
    SerializedProperty _lineWidth;
    SerializedProperty _lineMaterial;
    SerializedProperty _channelColors;

    void OnEnable()
    {
        _deviceIndex = serializedObject.FindProperty("_deviceIndex");
        _channelEnabled = serializedObject.FindProperty("_channelEnabled");
        _timeScaleSeconds = serializedObject.FindProperty("_timeScaleSeconds");
        _resolution = serializedObject.FindProperty("_resolution");
        _displayWidth = serializedObject.FindProperty("_displayWidth");
        _displayHeight = serializedObject.FindProperty("_displayHeight");
        _amplitude = serializedObject.FindProperty("_amplitude");
        _lineWidth = serializedObject.FindProperty("_lineWidth");
        _lineMaterial = serializedObject.FindProperty("_lineMaterial");
        _channelColors = serializedObject.FindProperty("_channelColors");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("Device", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_deviceIndex, new GUIContent("Device Index", "0 = first device in Lasp.AudioSystem.InputDevices"));

        // 実行時のみ: デバイス一覧を表示
        if (Application.isPlaying && Lasp.AudioSystem.InputDevices != null)
        {
            var list = System.Linq.Enumerable.ToList(Lasp.AudioSystem.InputDevices);
            if (list.Count > 0)
            {
                var idx = Mathf.Clamp(_deviceIndex.intValue, 0, list.Count - 1);
                var dev = list[idx];
                EditorGUILayout.HelpBox(
                    $"Current: [{idx}] {dev.Name}\nChannels: {dev.ChannelCount}, SampleRate: {dev.SampleRate} Hz",
                    MessageType.None);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Device list is available at runtime. Use index 0, 1, 2... for the first, second, third device.", MessageType.Info);
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Channels", EditorStyles.boldLabel);
        if (_channelEnabled.isArray && _channelEnabled.arraySize > 0)
        {
            for (var i = 0; i < _channelEnabled.arraySize; i++)
            {
                var el = _channelEnabled.GetArrayElementAtIndex(i);
                EditorGUILayout.PropertyField(el, new GUIContent($"Channel {i}"));
            }
        }
        else
        {
            EditorGUILayout.PropertyField(_channelEnabled, true);
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Time & Resolution", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_timeScaleSeconds);
        EditorGUILayout.PropertyField(_resolution);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Display", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_displayWidth);
        EditorGUILayout.PropertyField(_displayHeight);
        EditorGUILayout.PropertyField(_amplitude);
        EditorGUILayout.PropertyField(_lineWidth);
        EditorGUILayout.PropertyField(_lineMaterial);
        EditorGUILayout.PropertyField(_channelColors, true);

        serializedObject.ApplyModifiedProperties();
    }
}
