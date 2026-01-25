using Melanchall.DryWetMidi.Common;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Multimedia;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace yugop.midi {

    /// <summary>
    /// DryWetMIDI経由でMIDIイベントを受信し、登録されたリスナーにMIDIイベントを配信するクラス。
    /// MIDIイベントはNoteOn、NoteOff、ControlChange、PitchBend、ProgramChangeをサポートし、
    /// チャンネルやノート番号などの条件でフィルタリングが可能。
    /// すべてのリスナーコールバックはUnityのメインスレッドで実行されます。
    /// </summary>

    public class MIDIHub : MonoBehaviour {

        [Header ( "受信するMIDIのポート名" )]
        public string MIDI_PortName = "AbletonToUnity";  // 受信するMIDIのポート名

        [Header ( "開始時にMIDIポートに接続するかどうか" )]
        public bool ConnectOnAwake = true;

        [Header ( "デバッグ出力のON/OFF" )]
        public bool useDebugLog = true;

        [Header ( "各MIDIイベントのデバッグ出力設定" )]
        public bool debugNoteOn = true;
        public bool debugNoteOff = true;
        public bool debugControlChange = true;
        public bool debugPitchBend = true;
        public bool debugProgramChange = false;
        public bool debugOtherEvents = false;


        public static MIDIHub Instance { get; private set; }
        private InputDevice Input;

        // メインスレッドで実行する処理のキュー
        private Queue<Action> mainThreadQueue = new Queue<Action> ();
        private object queueLock = new object ();

        // リスナー管理用の内部クラス
        private class MidiListener<T> {
            public Action<T> Callback { get; set; }
            public int? Channel { get; set; }
            public int? Note { get; set; }
            public int? ControlNumber { get; set; }

            public bool Matches ( int channel , int? note = null , int? controlNumber = null ) {
                if ( Channel.HasValue && Channel.Value != channel ) return false;
                if ( Note.HasValue && note.HasValue && Note.Value != note.Value ) return false;
                if ( ControlNumber.HasValue && controlNumber.HasValue && ControlNumber.Value != controlNumber.Value ) return false;
                return true;
            }
        }

        // 各イベントタイプのリスナーリスト
        private List<MidiListener<MidiNote>> noteOnListeners = new List<MidiListener<MidiNote>> ();
        private List<MidiListener<MidiNote>> noteOffListeners = new List<MidiListener<MidiNote>> ();
        private List<MidiListener<MidiControlChange>> controlChangeListeners = new List<MidiListener<MidiControlChange>> ();
        private List<MidiListener<MidiPitchBend>> pitchBendListeners = new List<MidiListener<MidiPitchBend>> ();
        private List<MidiListener<MidiProgramChange>> programChangeListeners = new List<MidiListener<MidiProgramChange>> ();

        // オブジェクト生成時にMIDIポートの接続を開始
        void Awake () {
            // シングルトン設定
            Instance = this;
            // MIDIポートに接続
            if ( ConnectOnAwake ) {
                startConnection ();
            }

        }

        // オブジェクト破棄時にMIDIポートの接続を解除
        void OnDestroy () {
            endConnection ();
        }

        void Update () {
            // メインスレッドキューの処理
            lock ( queueLock ) {
                while ( mainThreadQueue.Count > 0 ) {
                    var action = mainThreadQueue.Dequeue ();
                    try {
                        action?.Invoke ();
                    } catch ( Exception e ) {
                        Debug.LogError ( $"[MIDIHub] Error executing queued action: {e.Message}\n{e.StackTrace}" );
                    }
                }
            }
        }

        // MIDIポートの接続開始
        public void startConnection () {
            Input = InputDevice.GetByName ( MIDI_PortName );
            Input.EventReceived += OnEventReceived;
            Input.StartEventsListening ();
            Debug.Log ( $"Connected to MIDI Port: {MIDI_PortName}" );
        }

        // MIDIポートの接続解除
        public void endConnection () {
            if ( Input != null ) {
                Input.EventReceived -= OnEventReceived;
                Input.Dispose ();
            }
        }


        //DryWetMIDIからのイベント受信時（バックグラウンドスレッドで呼ばれる）
        private void OnEventReceived ( object sender , MidiEventReceivedEventArgs e ) {

            switch ( e.Event ) {
                case NoteOnEvent noteOn when noteOn.Velocity > 0:
                    showDebug ( $" [NoteOn] Channel = {( int ) noteOn.Channel} / Note = {noteOn.NoteNumber} (\"{MIDIUtil.toNoteString ( noteOn.NoteNumber )}\") / Velocity = {noteOn.Velocity}" , debugNoteOn );
                    // メインスレッドで実行
                    EnqueueMainThreadAction ( () => InvokeNoteOnListeners ( noteOn ) );
                    break;

                case NoteOffEvent noteOff:
                    showDebug ( $" [NoteOff] Channel = {( int ) noteOff.Channel} / Note = {noteOff.NoteNumber} (\"{MIDIUtil.toNoteString ( noteOff.NoteNumber )}\")" , debugNoteOff );
                    // メインスレッドで実行
                    EnqueueMainThreadAction ( () => InvokeNoteOffListeners ( noteOff.Channel , noteOff.NoteNumber , 0 ) );
                    break;

                case NoteOnEvent noteOn when noteOn.Velocity == 0:
                    showDebug ( $" [NoteOff] Channel = {( int ) noteOn.Channel} / Note = {MIDIUtil.toNoteString ( noteOn.NoteNumber )}" , debugNoteOff );
                    // メインスレッドで実行
                    EnqueueMainThreadAction ( () => InvokeNoteOffListeners ( noteOn.Channel , noteOn.NoteNumber , 0 ) );
                    break;

                case ControlChangeEvent cc:
                    showDebug ( $" [ControlChange] Channel = {( int ) cc.Channel} / Number = {cc.ControlNumber} / Value = {cc.ControlValue}" , debugControlChange );
                    // メインスレッドで実行
                    EnqueueMainThreadAction ( () => InvokeControlChangeListeners ( cc ) );
                    break;

                case PitchBendEvent pb:
                    showDebug ( $" [PitchBend] Channel = {( int ) pb.Channel} / Value = {pb.PitchValue}" , debugPitchBend );
                    // メインスレッドで実行
                    EnqueueMainThreadAction ( () => InvokePitchBendListeners ( pb ) );
                    break;

                case ProgramChangeEvent pc:
                    showDebug ( $" [ProgramChange] Channel = {( int ) pc.Channel} / Program = {pc.ProgramNumber}" , debugProgramChange );
                    // メインスレッドで実行
                    EnqueueMainThreadAction ( () => InvokeProgramChangeListeners ( pc ) );
                    break;

                default:
                    showDebug ( $" [Other] {e.Event.GetType ().Name}" , debugOtherEvents );
                    break;
            }
        }

        // メインスレッドで処理を実行するためのヘルパーメソッド
        private void EnqueueMainThreadAction ( Action action ) {
            lock ( queueLock ) {
                mainThreadQueue.Enqueue ( action );
            }
        }


        #region リスナー登録/解除メソッド

        /// <summary>
        /// NoteOnイベントのリスナーを登録
        /// </summary>
        /// <param name="callback">コールバック関数</param>
        /// <param name="channel">チャンネル指定（0-15、nullの場合は全チャンネル）</param>
        /// <param name="note">ノート番号指定（0-127、nullの場合は全ノート）</param>
        public void AddNoteOnListener ( Action<MidiNote> callback , int? channel = null , int? note = null ) {
            noteOnListeners.Add ( new MidiListener<MidiNote> {
                Callback = callback ,
                Channel = channel ,
                Note = note
            } );
        }

        /// <summary>
        /// NoteOnイベントのリスナーを登録（ノート名指定版）
        /// </summary>
        /// <param name="callback">コールバック関数</param>
        /// <param name="channel">チャンネル指定（0-15、nullの場合は全チャンネル）</param>
        /// <param name="noteString">ノート名指定（例: "C3", "A#4"、nullの場合は全ノート）</param>
        public void AddNoteOnListener ( Action<MidiNote> callback , int? channel , string noteString ) {
            int? note = null;
            if ( !string.IsNullOrEmpty ( noteString ) ) {
                int noteNum = MIDIUtil.toNoteNumber ( noteString );
                if ( noteNum >= 0 ) {
                    note = noteNum;
                } else {
                    Debug.LogWarning ( $"Invalid note string: {noteString}. Listener will accept all notes." );
                }
            }
            AddNoteOnListener ( callback , channel , note );
        }

        /// <summary>
        /// NoteOnイベントのリスナーを解除
        /// </summary>
        public void RemoveNoteOnListener ( Action<MidiNote> callback ) {
            noteOnListeners.RemoveAll ( l => l.Callback == callback );
        }

        /// <summary>
        /// NoteOffイベントのリスナーを登録
        /// </summary>
        /// <param name="callback">コールバック関数</param>
        /// <param name="channel">チャンネル指定（0-15、nullの場合は全チャンネル）</param>
        /// <param name="note">ノート番号指定（0-127、nullの場合は全ノート）</param>
        public void AddNoteOffListener ( Action<MidiNote> callback , int? channel = null , int? note = null ) {
            noteOffListeners.Add ( new MidiListener<MidiNote> {
                Callback = callback ,
                Channel = channel ,
                Note = note
            } );
        }

        /// <summary>
        /// NoteOffイベントのリスナーを登録（ノート名指定版）
        /// </summary>
        /// <param name="callback">コールバック関数</param>
        /// <param name="channel">チャンネル指定（0-15、nullの場合は全チャンネル）</param>
        /// <param name="noteString">ノート名指定（例: "C3", "A#4"、nullの場合は全ノート）</param>
        public void AddNoteOffListener ( Action<MidiNote> callback , int? channel , string noteString ) {
            int? note = null;
            if ( !string.IsNullOrEmpty ( noteString ) ) {
                int noteNum = MIDIUtil.toNoteNumber ( noteString );
                if ( noteNum >= 0 ) {
                    note = noteNum;
                } else {
                    Debug.LogWarning ( $"Invalid note string: {noteString}. Listener will accept all notes." );
                }
            }
            AddNoteOffListener ( callback , channel , note );
        }

        /// <summary>
        /// NoteOffイベントのリスナーを解除
        /// </summary>
        public void RemoveNoteOffListener ( Action<MidiNote> callback ) {
            noteOffListeners.RemoveAll ( l => l.Callback == callback );
        }

        /// <summary>
        /// PitchBendイベントのリスナーを登録
        /// </summary>
        /// <param name="callback">コールバック関数</param>
        /// <param name="channel">チャンネル指定（0-15、nullの場合は全チャンネル）</param>
        public void AddPitchBendListener ( Action<MidiPitchBend> callback , int? channel = null ) {
            pitchBendListeners.Add ( new MidiListener<MidiPitchBend> {
                Callback = callback ,
                Channel = channel
            } );
        }

        /// <summary>
        /// PitchBendイベントのリスナーを解除
        /// </summary>
        public void RemovePitchBendListener ( Action<MidiPitchBend> callback ) {
            pitchBendListeners.RemoveAll ( l => l.Callback == callback );
        }


        /// <summary>
        /// ControlChangeイベントのリスナーを登録
        /// </summary>
        /// <param name="callback">コールバック関数</param>
        /// <param name="channel">チャンネル指定（0-15、nullの場合は全チャンネル）</param>
        /// <param name="controlNumber">コントロール番号指定（nullの場合は全コントロール）</param>
        public void AddControlChangeListener ( Action<MidiControlChange> callback , int? channel = null , int? controlNumber = null ) {
            controlChangeListeners.Add ( new MidiListener<MidiControlChange> {
                Callback = callback ,
                Channel = channel ,
                ControlNumber = controlNumber
            } );
        }

        /// <summary>
        /// ControlChangeイベントのリスナーを解除
        /// </summary>
        public void RemoveControlChangeListener ( Action<MidiControlChange> callback ) {
            controlChangeListeners.RemoveAll ( l => l.Callback == callback );
        }

        /// <summary>
        /// ProgramChangeイベントのリスナーを登録
        /// </summary>
        /// <param name="callback">コールバック関数</param>
        /// <param name="channel">チャンネル指定（0-15、nullの場合は全チャンネル）</param>
        public void AddProgramChangeListener ( Action<MidiProgramChange> callback , int? channel = null ) {
            programChangeListeners.Add ( new MidiListener<MidiProgramChange> {
                Callback = callback ,
                Channel = channel
            } );
        }

        /// <summary>
        /// ProgramChangeイベントのリスナーを解除
        /// </summary>
        public void RemoveProgramChangeListener ( Action<MidiProgramChange> callback ) {
            programChangeListeners.RemoveAll ( l => l.Callback == callback );
        }

        #endregion


        #region リスナー呼び出しメソッド（メインスレッドで実行される）

        private void InvokeNoteOnListeners ( NoteOnEvent noteOn ) {
            int channel = ( int ) noteOn.Channel;
            var eventData = new MidiNote ( channel , noteOn.NoteNumber , noteOn.Velocity );

            foreach ( var listener in noteOnListeners ) {
                if ( listener.Matches ( channel , noteOn.NoteNumber ) ) {
                    listener.Callback?.Invoke ( eventData );
                }
            }
        }

        private void InvokeNoteOffListeners ( FourBitNumber channel , SevenBitNumber noteNumber , int velocity ) {
            int ch = ( int ) channel;
            var eventData = new MidiNote ( ch , noteNumber , velocity );

            foreach ( var listener in noteOffListeners ) {
                if ( listener.Matches ( ch , noteNumber ) ) {
                    listener.Callback?.Invoke ( eventData );
                }
            }
        }

        private void InvokeControlChangeListeners ( ControlChangeEvent cc ) {
            int channel = ( int ) cc.Channel;
            var eventData = new MidiControlChange ( channel , cc.ControlNumber , cc.ControlValue );

            foreach ( var listener in controlChangeListeners ) {
                if ( listener.Matches ( channel , controlNumber: cc.ControlNumber ) ) {
                    listener.Callback?.Invoke ( eventData );
                }
            }
        }

        private void InvokePitchBendListeners ( PitchBendEvent pb ) {
            int channel = ( int ) pb.Channel;
            var eventData = new MidiPitchBend ( channel , pb.PitchValue );

            foreach ( var listener in pitchBendListeners ) {
                if ( listener.Matches ( channel ) ) {
                    listener.Callback?.Invoke ( eventData );
                }
            }
        }

        private void InvokeProgramChangeListeners ( ProgramChangeEvent pc ) {
            int channel = ( int ) pc.Channel;
            var eventData = new MidiProgramChange ( channel , pc.ProgramNumber );

            foreach ( var listener in programChangeListeners ) {
                if ( listener.Matches ( channel ) ) {
                    listener.Callback?.Invoke ( eventData );
                }
            }
        }

        #endregion


        //デバッグの表示
        void showDebug ( string text , bool isEnabled = true ) {
            if ( useDebugLog && isEnabled ) {
                Debug.Log ( text );
            }
        }
    }
}