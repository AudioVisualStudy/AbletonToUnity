using System;
using System.Collections.Generic;
using UnityEngine;

namespace yugop.connection {

    /// <summary>
    /// AbletonOSC（セッション同期）と Y-OSC-NoteSender（リアルタイム MIDI）を集約する OSC ハブ。
    /// </summary>
    [RequireComponent(typeof(AbletonOscClient))]
    [RequireComponent(typeof(AbletonSessionSync))]
    [RequireComponent(typeof(OscLiveNoteReceiver))]
    public class OscHub : MonoBehaviour {

        public bool useDebugLog = true; // Console へリアルタイム MIDI を出力する

        public static OscHub Instance { get; private set; }

        public LiveSessionState Session { get; private set; } = new LiveSessionState(); // Live セッション状態

        AbletonSessionSync sessionSync; // AbletonOSC 同期

        // リスナー管理用の内部クラス
        class MidiListener {
            public Action<MidiNote> Callback;
            public int? Note;

            public bool Matches(int note) {
                if (Note.HasValue && Note.Value != note) {
                    return false;
                }
                return true;
            }
        }

        readonly List<MidiListener> noteOnListeners = new List<MidiListener>();

        void Awake() {
            Instance = this;
            sessionSync = GetComponent<AbletonSessionSync>();
        }

        void OnDestroy() {
            if (Instance == this) {
                Instance = null;
            }
        }

        /// <summary>セッション全体の再スキャンを開始する。</summary>
        public void StartScan() {
            if (sessionSync != null) {
                sessionSync.StartScan();
            }
        }

        /// <summary>リアルタイム MIDI ノートを Session ツリーへ格納しリスナーへ配信する。</summary>
        public void HandleLiveNote(int trackIndex, int slotIndex, int pitch, int velocity) {
            if (velocity <= 0) {
                return;
            }
            float receivedAt = Time.unscaledTime;
            LiveMidiNoteEvent? noteEvent = Session.EmitLiveNote(trackIndex, slotIndex, pitch, velocity, receivedAt);
            if (!noteEvent.HasValue) {
                return;
            }
            invokeNoteOnListeners(trackIndex, slotIndex, pitch, velocity);
        }

        void invokeNoteOnListeners(int trackIndex, int slotIndex, int pitch, int velocity) {
            string trackName = "";
            string clipName = "";
            if (trackIndex >= 0 && trackIndex < Session.Tracks.Count) {
                trackName = Session.Tracks [trackIndex].Name ?? "";
            }
            LiveClipSlotInfo slot = Session.FindClip(trackIndex, slotIndex);
            if (slot != null) {
                clipName = slot.Name ?? "";
            }

            MidiNote eventData = new MidiNote(0, pitch, velocity);
            eventData.TrackName = trackName;
            eventData.TrackIndex = trackIndex;
            eventData.SlotIndex = slotIndex;
            eventData.ClipName = clipName;

            if (useDebugLog) {
                string clipLabel = string.IsNullOrEmpty(clipName) ? "NoName" : clipName;
                Debug.Log(
                    $"[OscHub] live note : {eventData.String} , Vel={velocity}  /  " +
                    $"TrackID={trackIndex} ({trackName}) , ClipID={slotIndex} ({clipLabel})"
                );
            }

            for (int i = 0; i < noteOnListeners.Count; i++) {
                MidiListener listener = noteOnListeners [i];
                if (listener.Matches(pitch)) {
                    listener.Callback?.Invoke(eventData);
                }
            }
        }

        /// <summary>
        /// NoteOn リスナーを登録する（OSC 経路ではチャンネル引数は互換用で無視する）。
        /// </summary>
        public void AddNoteOnListener(Action<MidiNote> callback, int? channel = null, int? note = null) {
            noteOnListeners.Add(new MidiListener {
                Callback = callback,
                Note = note
            });
        }

        /// <summary>
        /// NoteOn リスナーを登録する（ノート名指定版）。
        /// </summary>
        public void AddNoteOnListener(Action<MidiNote> callback, int? channel, string noteString) {
            int? note = null;
            if (!string.IsNullOrEmpty(noteString)) {
                int noteNum = MIDIUtil.toNoteNumber(noteString);
                if (noteNum >= 0) {
                    note = noteNum;
                }
            }
            AddNoteOnListener(callback, channel, note);
        }

        /// <summary>
        /// NoteOn リスナーを解除する。
        /// </summary>
        public void RemoveNoteOnListener(Action<MidiNote> callback) {
            noteOnListeners.RemoveAll(listener => listener.Callback == callback);
        }

        /// <summary>
        /// MidiHub 互換の NoteOff API（M4L からは送信されないスタブ）。
        /// </summary>
        public void AddNoteOffListener(Action<MidiNote> callback, int? channel = null, int? note = null) {
        }

        public void AddNoteOffListener(Action<MidiNote> callback, int? channel, string noteString) {
        }

        public void RemoveNoteOffListener(Action<MidiNote> callback) {
        }
    }

}
