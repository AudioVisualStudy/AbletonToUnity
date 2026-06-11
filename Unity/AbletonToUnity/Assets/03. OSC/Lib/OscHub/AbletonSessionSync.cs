using System.Collections.Generic;
using UnityEngine;

namespace yugop.connection {

    /// <summary>
    /// AbletonOSC へクエリとリスナー登録を行い、Live のセッション状態を OscHub.Session に同期する。
    /// 起動時に全トラック・クリップ・MIDI ノートをスキャンし、以後は再生スロットと再生位置を push 通知で追従する。
    /// </summary>
    [RequireComponent(typeof(AbletonOscClient))]
    public sealed class AbletonSessionSync : MonoBehaviour {

        public KeyCode rescanKey = KeyCode.R; // 手動再スキャンのキー
        public float scanTimeoutSeconds = 6f; // スキャン未完了時に打ち切るまでの秒数
        public float retryIntervalSeconds = 5f; // 未接続時にスキャンを再試行する間隔
        public bool useDebugLog = true; // 同期イベントを Console に出力する

        OscHub hub; // セッション状態の所有者
        AbletonOscClient client; // OSC 送受信クライアント
        LiveSessionState session; // OscHub.Session の参照
        int numTracks = -1; // スキャン中に受信したトラック数（未受信は -1）
        int numScenes = -1; // スキャン中に受信したシーン数（未受信は -1）
        int pendingReplies; // スキャン完了判定用の未応答クエリ数
        bool scanning; // スキャン進行中フラグ
        float scanStartedTime; // 最後にスキャンを開始した時刻
        readonly List<int> slotIndexListenTracks = new List<int>(); // playing_slot_index リスナー登録済みトラック
        readonly List<Vector2Int> positionListenClips = new List<Vector2Int>(); // playing_position リスナー登録済みクリップ
        readonly List<int> lastPlayingSlotPerTrack = new List<int>(); // 各トラックの最後に報告された playing_slot_index

        void Awake() {
            hub = GetComponent<OscHub>();
            client = GetComponent<AbletonOscClient>();
            registerHandlers();
        }

        void Start() {
            session = hub.Session;
            client.Send("/live/song/start_listen/is_playing");
            client.Send("/live/song/start_listen/tempo");
            StartScan();
        }

        void OnDestroy() {
            stopListeners();
            client.Send("/live/song/stop_listen/is_playing");
            client.Send("/live/song/stop_listen/tempo");
        }

        void Update() {
            if (Input.GetKeyDown(rescanKey)) {
                StartScan();
                return;
            }
            if (scanning && Time.unscaledTime - scanStartedTime > scanTimeoutSeconds) {
                Debug.LogWarning($"[OscHub] scan timeout (pending={pendingReplies}). Live / AbletonOSC の起動を確認してください");
                if (session.Tracks.Count > 0) {
                    finishScan();
                } else {
                    scanning = false;
                    pendingReplies = 0;
                }
                return;
            }
            if (!scanning && !session.IsReady && Time.unscaledTime - scanStartedTime > retryIntervalSeconds) {
                StartScan();
            }
        }

        /// <summary>セッション全体の再スキャンを開始する。</summary>
        public void StartScan() {
            scanning = true;
            scanStartedTime = Time.unscaledTime;
            numTracks = -1;
            numScenes = -1;
            pendingReplies = 0;
            lastPlayingSlotPerTrack.Clear();
            stopListeners();
            if (useDebugLog) {
                Debug.Log("[OscHub] scan start");
            }
            client.Send("/live/song/get/tempo");
            client.Send("/live/song/get/is_playing");
            sendQuery("/live/song/get/num_tracks");
            sendQuery("/live/song/get/num_scenes");
        }

        // クエリを送信し、未応答数を 1 増やす
        void sendQuery(string address) {
            pendingReplies++;
            client.Send(address);
        }

        void sendQuery(string address, int arg0) {
            pendingReplies++;
            client.Send(address, arg0);
        }

        void sendQuery(string address, int arg0, int arg1) {
            pendingReplies++;
            client.Send(address, arg0, arg1);
        }

        // 返信を 1 件処理済みとして数え、すべて揃ったらスキャンを完了する
        void resolveReply() {
            if (!scanning) {
                return;
            }
            pendingReplies--;
            if (pendingReplies <= 0) {
                finishScan();
            }
        }

        // スキャンを完了状態にする
        void finishScan() {
            scanning = false;
            pendingReplies = 0;
            session.EndSession();
            if (useDebugLog) {
                Debug.Log($"[OscHub] scan done: tracks={session.Tracks.Count} version={session.SessionVersion}");
            }
        }

        // 登録済みリスナーをすべて解除する（トランスポート系は除く）
        void stopListeners() {
            for (int i = 0; i < slotIndexListenTracks.Count; i++) {
                client.Send("/live/track/stop_listen/playing_slot_index", slotIndexListenTracks [i]);
            }
            slotIndexListenTracks.Clear();
            for (int i = 0; i < positionListenClips.Count; i++) {
                Vector2Int target = positionListenClips [i];
                client.Send("/live/clip/stop_listen/playing_position", target.x, target.y);
            }
            positionListenClips.Clear();
        }

        // AbletonOSC の返信アドレスに対するハンドラを一括登録する
        void registerHandlers() {
            client.AddHandler("/live/song/get/num_tracks", onNumTracks);
            client.AddHandler("/live/song/get/num_scenes", onNumScenes);
            client.AddHandler("/live/song/get/tempo", onTempo);
            client.AddHandler("/live/song/get/is_playing", onIsPlaying);
            client.AddHandler("/live/track/get/name", onTrackName);
            client.AddHandler("/live/clip_slot/get/has_clip", onHasClip);
            client.AddHandler("/live/clip/get/name", onClipName);
            client.AddHandler("/live/clip/get/length", onClipLength);
            client.AddHandler("/live/clip/get/is_midi_clip", onClipIsMidi);
            client.AddHandler("/live/clip/get/notes", onClipNotes);
            client.AddHandler("/live/track/get/playing_slot_index", onPlayingSlotIndex);
            client.AddHandler("/live/clip/get/playing_position", onPlayingPosition);
            client.AddHandler("/live/startup", onLiveStartup);
            client.AddHandler("/live/error", onLiveError);
        }

        // Live 再起動通知で再スキャンする
        void onLiveStartup(AbletonOscClient.Message message) {
            if (useDebugLog) {
                Debug.Log("[OscHub] /live/startup received");
            }
            StartScan();
        }

        // AbletonOSC 側のエラーを Console に出す
        void onLiveError(AbletonOscClient.Message message) {
            Debug.LogWarning($"[OscHub] live error: {message.GetString(0)}");
        }

        void onNumTracks(AbletonOscClient.Message message) {
            numTracks = message.GetInt(0);
            beginTrackScanIfReady();
            resolveReply();
        }

        void onNumScenes(AbletonOscClient.Message message) {
            numScenes = message.GetInt(0);
            beginTrackScanIfReady();
            resolveReply();
        }

        // トラック数とシーン数が揃ったらグリッドを作り、各トラック・スロットを問い合わせる
        void beginTrackScanIfReady() {
            if (!scanning || numTracks < 0 || numScenes < 0) {
                return;
            }
            session.BeginSession(numTracks);
            for (int trackIndex = 0; trackIndex < numTracks; trackIndex++) {
                session.AddTrack("", trackIndex, numScenes);
                sendQuery("/live/track/get/name", trackIndex);
                client.Send("/live/track/start_listen/playing_slot_index", trackIndex);
                slotIndexListenTracks.Add(trackIndex);
                for (int slotIndex = 0; slotIndex < numScenes; slotIndex++) {
                    sendQuery("/live/clip_slot/get/has_clip", trackIndex, slotIndex);
                }
            }
            if (useDebugLog) {
                Debug.Log($"[OscHub] grid: tracks={numTracks} scenes={numScenes}");
            }
        }

        void onTempo(AbletonOscClient.Message message) {
            session.SetBpm(message.GetFloat(0));
        }

        void onIsPlaying(AbletonOscClient.Message message) {
            bool isPlaying = message.GetInt(0) != 0;
            session.SetPlaying(isPlaying);
            if (!isPlaying) {
                session.StopAllClipPlayback();
            } else {
                for (int trackIndex = 0; trackIndex < session.Tracks.Count; trackIndex++) {
                    applyPlayingSlotForTrack(trackIndex);
                }
            }
            if (useDebugLog) {
                Debug.Log($"[OscHub] transport: {(isPlaying ? "play" : "stop")}");
            }
        }

        void onTrackName(AbletonOscClient.Message message) {
            int trackIndex = message.GetInt(0);
            string name = message.GetString(1);
            if (trackIndex >= 0 && trackIndex < session.Tracks.Count) {
                session.Tracks [trackIndex].Name = name;
            }
            resolveReply();
        }

        // クリップ有無の返信。あれば詳細クエリと位置リスナー登録、なければ空スロット確定
        void onHasClip(AbletonOscClient.Message message) {
            int trackIndex = message.GetInt(0);
            int slotIndex = message.GetInt(1);
            bool hasClip = message.GetInt(2) != 0;
            resolveReply();
            if (!hasClip) {
                session.SetClip(trackIndex, slotIndex, ClipKind.Empty, "");
                return;
            }
            sendQuery("/live/clip/get/name", trackIndex, slotIndex);
            sendQuery("/live/clip/get/length", trackIndex, slotIndex);
            sendQuery("/live/clip/get/is_midi_clip", trackIndex, slotIndex);
            client.Send("/live/clip/start_listen/playing_position", trackIndex, slotIndex);
            positionListenClips.Add(new Vector2Int(trackIndex, slotIndex));
        }

        void onClipName(AbletonOscClient.Message message) {
            LiveClipSlotInfo slot = session.FindClip(message.GetInt(0), message.GetInt(1));
            if (slot != null) {
                slot.Name = message.GetString(2);
            }
            resolveReply();
        }

        void onClipLength(AbletonOscClient.Message message) {
            session.SetClipLength(message.GetInt(0), message.GetInt(1), message.GetFloat(2));
            resolveReply();
        }

        // MIDI / Audio 種別の返信。MIDI ならノート一覧を問い合わせる
        void onClipIsMidi(AbletonOscClient.Message message) {
            int trackIndex = message.GetInt(0);
            int slotIndex = message.GetInt(1);
            bool isMidi = message.GetInt(2) != 0;
            LiveClipSlotInfo slot = session.FindClip(trackIndex, slotIndex);
            if (slot != null) {
                slot.Kind = isMidi ? ClipKind.Midi : ClipKind.Audio;
            }
            resolveReply();
            if (isMidi) {
                sendQuery("/live/clip/get/notes", trackIndex, slotIndex);
            }
        }

        // ノート一覧の返信（track, clip の後に pitch, start, duration, velocity, mute の繰り返し）
        void onClipNotes(AbletonOscClient.Message message) {
            int trackIndex = message.GetInt(0);
            int slotIndex = message.GetInt(1);
            LiveClipSlotInfo slot = session.FindClip(trackIndex, slotIndex);
            resolveReply();
            if (slot == null) {
                return;
            }
            slot.ClearMidiNotes();
            for (int offset = 2; offset + 4 < message.Count; offset += 5) {
                bool muted = message.GetInt(offset + 4) != 0;
                if (muted) {
                    continue;
                }
                MidiClipNoteInfo note = new MidiClipNoteInfo {
                    Pitch = message.GetInt(offset),
                    StartBeat = message.GetFloat(offset + 1),
                    DurationBeats = message.GetFloat(offset + 2),
                    Velocity = message.GetInt(offset + 3)
                };
                slot.AddMidiNote(note);
            }
            if (useDebugLog) {
                Debug.Log($"[OscHub] notes: [{trackIndex}:{slotIndex}] count={slot.MidiNotes.Count}");
            }
        }

        // 再生スロット変更の通知。最後のスロット index を記録し、再生中なら反映する
        void onPlayingSlotIndex(AbletonOscClient.Message message) {
            int trackIndex = message.GetInt(0);
            int playingIndex = message.GetInt(1);
            if (trackIndex < 0) {
                return;
            }
            while (lastPlayingSlotPerTrack.Count <= trackIndex) {
                lastPlayingSlotPerTrack.Add(-1);
            }
            lastPlayingSlotPerTrack [trackIndex] = playingIndex;
            if (trackIndex >= session.Tracks.Count) {
                return;
            }
            applyPlayingSlotForTrack(trackIndex);
            if (useDebugLog) {
                Debug.Log($"[OscHub] playing slot: track={trackIndex} slot={playingIndex} transport={(session.Transport.IsPlaying ? "play" : "stop")}");
            }
        }

        // トラックの lastPlayingSlotPerTrack を Session に反映する（transport 再生中のみ）
        void applyPlayingSlotForTrack(int trackIndex) {
            if (!session.Transport.IsPlaying || trackIndex < 0 || trackIndex >= session.Tracks.Count) {
                return;
            }
            int playingIndex = -1;
            if (trackIndex < lastPlayingSlotPerTrack.Count) {
                playingIndex = lastPlayingSlotPerTrack [trackIndex];
            }
            LiveTrackInfo track = session.Tracks [trackIndex];
            for (int slotIndex = 0; slotIndex < track.Slots.Count; slotIndex++) {
                LiveClipSlotInfo slot = track.Slots [slotIndex];
                if (playingIndex >= 0 && slotIndex == playingIndex) {
                    session.SetClipPlaying(trackIndex, slotIndex, slot.Kind, slot.Name);
                } else if (slot.IsPlaying) {
                    session.SetClipStopped(trackIndex, slotIndex);
                }
            }
        }

        // 再生位置の通知（拍）。transport 再生中かつ該当スロット再生中のみ補間アンカーを更新する
        void onPlayingPosition(AbletonOscClient.Message message) {
            if (!session.Transport.IsPlaying) {
                return;
            }
            int trackIndex = message.GetInt(0);
            int slotIndex = message.GetInt(1);
            LiveClipSlotInfo slot = session.FindClip(trackIndex, slotIndex);
            if (slot == null || !slot.IsPlaying) {
                return;
            }
            float positionBeats = message.GetFloat(2);
            session.SetClipPosition(trackIndex, slotIndex, positionBeats, Time.unscaledTime);
        }
    }

}
