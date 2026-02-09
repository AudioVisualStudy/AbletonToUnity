using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using yugop.connection;

public class MidiMonitor : MonoBehaviour {
    public ChannelModule channelModulePrefab;
    public CanvasScaler canvasScaler;
    public Transform mainPanel;
    public TextMeshProUGUI bpmText;

    [Header ( "表示スケール" )]
    [Range ( 0.1f, 3f )]
    public float scale = 1f;

    [Space ( 10 )]
    [Header ( "すべてのチャンネルをモニターする場合はチェック" )]
    [Space ( 10 )]
    public bool listenToAllChannels = true;

    [Space ( 10 )]
    [Header ( "特定のチャンネルに絞りたい時は上記のチェックを外して以下で複数指定" )]
    [Space ( 10 )]
    public List<int> listeningChannels = new List<int> { 0 };

    [Header ( "モニターしたいイベントをチェック" )]
    [Space ( 10 )]
    public bool _NoteOn = true;

    public bool _NoteOff = false;
    public bool _PitchBend = false;
    public bool _ControlChange = false;
    public bool _ProgramChange = false;

    [Space ( 10 )]
    [Header ( "拍を刻むチャンネルがあれば指定（NoteOn周期でBPM算出）" )]
    public int metronomeChannel = -1;

    [Header ( "BPM計算用の平均化サンプル数" )] public int bpmSampleCount = 4;

    // [HideInInspector]
    public static float BPM; // 現在のBPM値

    //チャンネルモジュール配置設定

    float xStart = 50f;
    float xGrid = 260;
    float yStart = -160f;

    private MidiHub hub;
    private List<int> monitorChannel = new List<int> ();

    List<ChannelModule> channelModules = new List<ChannelModule> ();

    // BPM計算用
    private Queue<float> metronomeTimes = new Queue<float> ();
    private float lastMetronomeTime = -1f;

    //初期設定
    void Start () {
        Application.targetFrameRate = 60;
        bpmText.gameObject.SetActive ( false );

        hub = MidiHub.Instance;

        // 監視対象のチャンネルリストを決定
        if ( listenToAllChannels ) {
            // 全チャンネル (0-15)
            monitorChannel.Clear ();
            for ( int i = 0; i < 16; i++ ) {
                monitorChannel.Add ( i );
            }
        } else {
            // 指定されたチャンネルのみ
            monitorChannel = new List<int> ( listeningChannels );
        }

        // 各チャンネルに対して、有効なイベントのリスナーのみを登録
        foreach ( int channel in monitorChannel ) {
            if ( _NoteOn ) hub.AddNoteOnListener ( OnNoteOn, channel );
            if ( _NoteOff ) hub.AddNoteOffListener ( OnNoteOff, channel );
            if ( _ControlChange ) hub.AddControlChangeListener ( OnControlChange, channel );
            if ( _PitchBend ) hub.AddPitchBendListener ( OnPitchBend, channel );
            if ( _ProgramChange ) hub.AddProgramChangeListener ( OnProgramChange, channel );
        }
    }

    // オブジェクト破棄時にリスナーを解除
    void OnDestroy () {
        if ( hub == null ) return;
        // 登録したリスナーのみを解除
        if ( _NoteOn ) hub.RemoveNoteOnListener ( OnNoteOn );
        if ( _NoteOff ) hub.RemoveNoteOffListener ( OnNoteOff );
        if ( _ControlChange ) hub.RemoveControlChangeListener ( OnControlChange );
        if ( _PitchBend ) hub.RemovePitchBendListener ( OnPitchBend );
        if ( _ProgramChange ) hub.RemoveProgramChangeListener ( OnProgramChange );
    }


    // 各イベントハンドラ（メインスレッドで実行される）

    // NoteOnイベント受信時
    void OnNoteOn ( MidiNote note ) {
        ChannelModule module = GetOrCreateModule ( note.Channel );
        module.onNoteOn ( note );

        // メトロノームチャンネルの場合、BPMを計算
        if ( metronomeChannel >= 0 && note.Channel == metronomeChannel ) {
            CalculateBPM ();
        }
    }

    // NoteOffイベント受信時
    void OnNoteOff ( MidiNote note ) {
        ChannelModule module = GetOrCreateModule ( note.Channel );
        module.onNoteOff ( note );
    }

    // ControlChangeイベント受信時
    void OnControlChange ( MidiControlChange cc ) {
        ChannelModule module = GetOrCreateModule ( cc.Channel );
        module.onControlChange ( cc );
    }

    // PitchBendイベント受信時
    void OnPitchBend ( MidiPitchBend pb ) {
        ChannelModule module = GetOrCreateModule ( pb.Channel );
        module.onPitchBend ( pb );
    }

    // ProgramChangeイベント受信時
    void OnProgramChange ( MidiProgramChange pc ) {
        ChannelModule module = GetOrCreateModule ( pc.Channel );
        module.onProgramChange ( pc );
    }

    // BPM計算
    void CalculateBPM () {
        float currentTime = Time.time;

        // 初回または最初のタイミング
        if ( lastMetronomeTime < 0f ) {
            lastMetronomeTime = currentTime;
            return;
        }

        // 前回のNoteOnからの経過時間（秒）
        float interval = currentTime - lastMetronomeTime;
        lastMetronomeTime = currentTime;

        // 異常値をフィルタリング（0.1秒未満や10秒以上は無視）
        if ( interval < 0.1f || interval > 10f ) {
            return;
        }

        // インターバルをキューに追加
        metronomeTimes.Enqueue ( interval );

        // サンプル数を超えた場合は古いデータを削除
        if ( metronomeTimes.Count > bpmSampleCount ) {
            metronomeTimes.Dequeue ();
        }

        // 十分なサンプル数が溜まってからBPMを計算・表示
        if ( metronomeTimes.Count < bpmSampleCount ) {
            return;
        }

        // 平均インターバルを計算
        float averageInterval = 0f;
        foreach ( float time in metronomeTimes ) {
            averageInterval += time;
        }

        averageInterval /= metronomeTimes.Count;

        // BPM = 60 / 1拍の秒数（小数点2桁で丸める）
        BPM = Mathf.Round ( ( 60f / averageInterval ) * 100f ) / 100f;
        setBPMText ( BPM );
    }


    // チャンネルモジュールを取得または新規作成
    ChannelModule GetOrCreateModule ( int chNum ) {
        // 既存のChannelModuleを検索
        ChannelModule existingModule = null;
        foreach ( ChannelModule module in channelModules ) {
            if ( module.Number == chNum ) {
                existingModule = module;
                break;
            }
        }

        if ( existingModule != null ) {
            // 既にインスタンス化されている場合はそれを返す
            return existingModule;
        }

        // 新しいChannelModuleをインスタンス化
        ChannelModule newModule = Instantiate ( channelModulePrefab, mainPanel );
        newModule.gameObject.name = "Channel " + chNum;
        newModule.setChannelNumber ( chNum );


        // リストに追加
        channelModules.Add ( newModule );

        // チャンネルモジュールを並び替え
        arrangeChannelModules ();

        return newModule;
    }


    //チャンネルモジュールを、Number順に並び替え
    void arrangeChannelModules () {
        // channelNumberでソート
        channelModules.Sort ( ( a, b ) => a.Number.CompareTo ( b.Number ) );

        // 横並びで配置
        for ( int i = 0; i < channelModules.Count; i++ ) {
            Vector3 position = new Vector3 ( xStart + i * xGrid, yStart, 0f );
            channelModules [ i ].setUIPosition ( position );
        }
    }

    // BPM表示を更新
    void setBPMText ( float bpm ) {
        bpmText.gameObject.SetActive ( bpm > 0f );
        bpmText.text = "Current estimated BPM : " + bpm.ToString ( "F2" );
    }

    void Update () {
        // スケールを適用
        canvasScaler.scaleFactor = scale;
    }
}