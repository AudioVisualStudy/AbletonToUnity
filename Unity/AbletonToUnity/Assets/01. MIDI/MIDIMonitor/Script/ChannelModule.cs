using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using yugop.connection;

public class ChannelModule : MonoBehaviour {
    public DataModule dataMoulePrefab;
    public TextMeshProUGUI titleText;

    public int Number;


    List<DataModule> dataModules = new List<DataModule> ();

    private float startX = 0f;
    private float startY = -90f;
    private float gridY = 260;


    // チャンネル番号を設定
    public void setChannelNumber ( int num ) {
        Number = num;
        titleText.text = $"Channel {Number}";
    }

    // 位置を設定
    public void setUIPosition ( Vector2 position ) {
        RectTransform rectTransform = GetComponent<RectTransform> ();
        if ( rectTransform != null ) {
            rectTransform.anchoredPosition = position;
        }
    }

    #region MIDIイベント受信処理

    //NotoOnイベント受信時
    public void onNoteOn ( MidiNote note ) {
        DataModule dataModule = GetOrCreateDataModule ( MidiType.NoteOn );
        dataModule.setData ( MidiType.NoteOn, note );
        dataModule.startFadeOut ();
    }

    //NoteOffイベント受信時
    public void onNoteOff ( MidiNote note ) {
        DataModule dataModule = GetOrCreateDataModule ( MidiType.NoteOff );
        dataModule.setData ( MidiType.NoteOff, note );
        dataModule.startFadeOut ();
    }

    //PitchBendイベント受信時
    public void onPitchBend ( MidiPitchBend pitchBend ) {
        DataModule dataModule = GetOrCreateDataModule ( MidiType.PitchBend );
        dataModule.setData ( MidiType.PitchBend, pitchBend );
        dataModule.startFadeOut ();
    }

    //ControlChangeイベント受信時
    public void onControlChange ( MidiControlChange controlChange ) {
        DataModule dataModule = GetOrCreateDataModule ( MidiType.ControlChange );
        dataModule.setData ( MidiType.ControlChange, controlChange );
        dataModule.startFadeOut ();
    }

    //ProgramChangeイベント受信時
    public void onProgramChange ( MidiProgramChange programChange ) {
        DataModule dataModule = GetOrCreateDataModule ( MidiType.ProgramChange );
        dataModule.setData ( MidiType.ProgramChange, programChange );
        dataModule.startFadeOut ();
    }

    #endregion

    DataModule GetOrCreateDataModule ( MidiType type ) {
        //既に表示しているモジュールで、同じタイプのものがあればそれを返す
        foreach ( var module in dataModules ) {
            if ( module.type == type ) {
                return module;
            }
        }

        //なければ新規作成して返す
        DataModule newModule = Instantiate ( dataMoulePrefab, transform );
        newModule.gameObject.name = "Module-" + newModule.type.ToString ();
        newModule.type = type;

        dataModules.Add ( newModule );
        arrangeDataModules ();
        return newModule;
    }

    void arrangeDataModules () {
        // typeの順番でソート
        var sortedModules = dataModules.OrderBy ( m => m.type ).ToList ();

        // 縦に並べる
        for ( int i = 0; i < sortedModules.Count; i++ ) {
            sortedModules [ i ].setUIPosition ( new Vector2 ( startX, startY - ( i * gridY ) ) );
        }
    }
}