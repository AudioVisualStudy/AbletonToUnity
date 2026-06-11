using System.Collections.Generic;

namespace yugop.connection {

    /// <summary>クリップスロットの種別（/y-osc/session/clip の kind と一致）。</summary>
    public enum ClipKind {
        Empty = 0,
        Midi = 1,
        Audio = 2
    }

    /// <summary>Live Set の再生状態と BPM。</summary>
    public struct TransportInfo {
        public bool IsPlaying; // 再生中なら true
        public float Bpm; // 現在のテンポ
    }

    /// <summary>MIDI クリップ内の 1 ノート（静的配置・AbletonOSC）。</summary>
    public struct MidiClipNoteInfo {
        public int Pitch; // MIDI ノート番号
        public float StartBeat; // クリップ先頭からの開始位置（拍）
        public float DurationBeats; // ノート長（拍）
        public int Velocity; // ベロシティ
    }

    /// <summary>リアルタイムに発声した MIDI ノート（Y-OSC-NoteSender）。</summary>
    public struct LiveMidiNoteEvent {
        public int TrackIndex; // 親トラック index
        public int SlotIndex; // 親クリップ slot index
        public int Pitch; // MIDI ノート番号（0〜127）
        public int Velocity; // ベロシティ
        public float ReceivedAtUnscaledTime; // 受信時刻

        // Pitch から導出する音階名（例: C4）
        public string NoteName => MIDIUtil.toNoteString(Pitch);
    }

    /// <summary>セッションビューの 1 クリップスロット。</summary>
    public sealed class LiveClipSlotInfo {

        public int TrackIndex { get; internal set; } // 親トラック index
        public int SlotIndex { get; internal set; } // スロット index
        public ClipKind Kind { get; internal set; } // 空 / MIDI / Audio
        public string Name { get; internal set; } = ""; // クリップ名（空スロットは空文字）
        public float LengthBeats { get; internal set; } // クリップ長（拍）
        public bool IsPlaying { get; internal set; } // いま再生中なら true
        public float PlayingPositionBeats { get; internal set; } // 再生位置（拍）
        public bool PreferLivePosition { get; internal set; } // Live から位置 OSC を受信済みなら true
        public float LastPositionOscUnscaledTime { get; internal set; } // 最後の位置 OSC 受信時刻

        readonly List<MidiClipNoteInfo> midiNotes = new List<MidiClipNoteInfo>(); // 静的 MIDI ノート一覧
        readonly List<LiveMidiNoteEvent> recentLiveNotes = new List<LiveMidiNoteEvent>(); // リアルタイム発声履歴

        public IReadOnlyList<MidiClipNoteInfo> MidiNotes => midiNotes;
        public IReadOnlyList<LiveMidiNoteEvent> RecentLiveNotes => recentLiveNotes;

        const int maxRecentLiveNotes = 32; // 直近 live note の保持件数

        // MIDI ノート一覧をクリアする
        internal void ClearMidiNotes() {
            midiNotes.Clear();
        }

        // MIDI ノートを 1 件追加する
        internal void AddMidiNote(MidiClipNoteInfo note) {
            midiNotes.Add(note);
        }

        // リアルタイム発声ノートを 1 件追加する
        internal void AddRecentLiveNote(LiveMidiNoteEvent noteEvent) {
            recentLiveNotes.Add(noteEvent);
            if (recentLiveNotes.Count > maxRecentLiveNotes) {
                recentLiveNotes.RemoveAt(0);
            }
        }
    }

    /// <summary>セッションビューの 1 トラック。</summary>
    public sealed class LiveTrackInfo {

        public int Index { get; internal set; } // トラック index
        public string Name { get; internal set; } = ""; // トラック名

        readonly List<LiveClipSlotInfo> slots = new List<LiveClipSlotInfo>(); // クリップスロット一覧
        public IReadOnlyList<LiveClipSlotInfo> Slots => slots;

        // スロット数を確保し、指定 index のスロットを返す
        internal LiveClipSlotInfo EnsureSlot(int slotIndex) {
            while (slots.Count <= slotIndex) {
                LiveClipSlotInfo slot = new LiveClipSlotInfo {
                    TrackIndex = Index,
                    SlotIndex = slots.Count
                };
                slots.Add(slot);
            }
            return slots [slotIndex];
        }
    }

    /// <summary>Ableton Live セッションの OSC 同期状態。</summary>
    public sealed class LiveSessionState {

        TransportInfo transport = new TransportInfo { Bpm = 120f }; // 再生状態と BPM
        readonly List<LiveTrackInfo> tracks = new List<LiveTrackInfo>(); // トラック一覧
        bool isReady; // session/end 受信後 true
        int sessionVersion; // begin のたびにインクリメント
        string activeNoteImportKey = ""; // ノート受信中スロットのキー

        public TransportInfo Transport => transport;

        // 表示・進行用 BPM（無効値は 120 にフォールバック）
        public float EffectiveBpm => transport.Bpm > 1f ? transport.Bpm : 120f;

        public bool IsReady => isReady;
        public int SessionVersion => sessionVersion;
        public IReadOnlyList<LiveTrackInfo> Tracks => tracks;

        // リアルタイム MIDI ノートを親クリップへ格納する
        public LiveMidiNoteEvent? EmitLiveNote(int trackIndex, int slotIndex, int pitch, int velocity, float receivedAtUnscaledTime) {
            if (velocity <= 0) {
                return null;
            }
            LiveMidiNoteEvent noteEvent = new LiveMidiNoteEvent {
                TrackIndex = trackIndex,
                SlotIndex = slotIndex,
                Pitch = pitch,
                Velocity = velocity,
                ReceivedAtUnscaledTime = receivedAtUnscaledTime
            };
            LiveClipSlotInfo slot = FindClip(trackIndex, slotIndex);
            if (slot != null) {
                slot.AddRecentLiveNote(noteEvent);
            }
            return noteEvent;
        }

        // トラック index とスロット index からクリップを探す
        public LiveClipSlotInfo FindClip(int trackIndex, int slotIndex) {
            if (trackIndex < 0 || trackIndex >= tracks.Count) {
                return null;
            }
            LiveTrackInfo track = tracks [trackIndex];
            if (slotIndex < 0 || slotIndex >= track.Slots.Count) {
                return null;
            }
            return track.Slots [slotIndex];
        }

        // 再生中のクリップスロットを列挙する
        public IEnumerable<LiveClipSlotInfo> PlayingClips() {
            for (int trackIndex = 0; trackIndex < tracks.Count; trackIndex++) {
                LiveTrackInfo track = tracks [trackIndex];
                for (int slotIndex = 0; slotIndex < track.Slots.Count; slotIndex++) {
                    LiveClipSlotInfo slot = track.Slots [slotIndex];
                    if (slot.IsPlaying) {
                        yield return slot;
                    }
                }
            }
        }

        // 再生状態を更新する
        internal void SetPlaying(bool isPlaying) {
            transport.IsPlaying = isPlaying;
            if (transport.Bpm <= 1f) {
                transport.Bpm = 120f;
            }
        }

        // BPM を更新する
        internal void SetBpm(float bpm) {
            if (bpm > 1f) {
                transport.Bpm = bpm;
            }
        }

        // 再生中クリップが 1 つでもあるか
        public bool HasAnyPlayingClip() {
            for (int trackIndex = 0; trackIndex < tracks.Count; trackIndex++) {
                LiveTrackInfo track = tracks [trackIndex];
                for (int slotIndex = 0; slotIndex < track.Slots.Count; slotIndex++) {
                    if (track.Slots [slotIndex].IsPlaying) {
                        return true;
                    }
                }
            }
            return false;
        }

        // セッションスキャン開始時にトラック一覧を再構築する
        internal void BeginSession(int trackCount) {
            sessionVersion++;
            isReady = false;
            transport.IsPlaying = false;
            tracks.Clear();
            activeNoteImportKey = "";
            for (int trackIndex = 0; trackIndex < trackCount; trackIndex++) {
                tracks.Add(new LiveTrackInfo { Index = trackIndex });
            }
        }

        // トラック情報とスロット数を登録する
        internal void AddTrack(string name, int trackIndex, int slotCount) {
            if (trackIndex < 0 || trackIndex >= tracks.Count) {
                return;
            }
            LiveTrackInfo track = tracks [trackIndex];
            track.Name = name ?? "";
            track.Index = trackIndex;
            for (int slotIndex = 0; slotIndex < slotCount; slotIndex++) {
                LiveClipSlotInfo slot = track.EnsureSlot(slotIndex);
                slot.TrackIndex = trackIndex;
                slot.SlotIndex = slotIndex;
            }
        }

        // クリップスロットの種別と名前を更新する
        internal void SetClip(int trackIndex, int slotIndex, ClipKind kind, string clipName) {
            LiveClipSlotInfo slot = GetOrCreateSlot(trackIndex, slotIndex);
            if (slot == null) {
                return;
            }
            slot.Kind = kind;
            slot.Name = clipName ?? "";
            if (kind == ClipKind.Empty) {
                slot.LengthBeats = 0f;
                slot.IsPlaying = false;
                slot.PlayingPositionBeats = 0f;
                slot.ClearMidiNotes();
            }
        }

        // クリップ長（拍）を更新する
        internal void SetClipLength(int trackIndex, int slotIndex, float lengthBeats) {
            LiveClipSlotInfo slot = GetOrCreateSlot(trackIndex, slotIndex);
            if (slot == null || lengthBeats <= 0f) {
                return;
            }
            slot.LengthBeats = lengthBeats;
        }

        // セッションスキャン完了を記録する
        internal void EndSession() {
            isReady = true;
            if (transport.Bpm <= 1f) {
                transport.Bpm = 120f;
            }
        }

        // クリップ再生停止を記録する
        internal void SetClipStopped(int trackIndex, int slotIndex) {
            LiveClipSlotInfo slot = FindClip(trackIndex, slotIndex);
            if (slot == null) {
                return;
            }
            slot.IsPlaying = false;
            slot.PreferLivePosition = false;
            slot.PlayingPositionBeats = 0f;
            slot.LastPositionOscUnscaledTime = 0f;
            if (!HasAnyPlayingClip()) {
                transport.IsPlaying = false;
            }
        }

        // 全クリップの再生フラグを落とす
        internal void ClearAllPlaying() {
            ClearPlayingFlags();
        }

        // 全スロットの再生フラグと再生位置アンカーをクリアする（transport は変更しない）
        internal void StopAllClipPlayback() {
            for (int trackIndex = 0; trackIndex < tracks.Count; trackIndex++) {
                LiveTrackInfo track = tracks [trackIndex];
                for (int slotIndex = 0; slotIndex < track.Slots.Count; slotIndex++) {
                    LiveClipSlotInfo slot = track.Slots [slotIndex];
                    slot.IsPlaying = false;
                    slot.PreferLivePosition = false;
                    slot.PlayingPositionBeats = 0f;
                    slot.LastPositionOscUnscaledTime = 0f;
                }
            }
        }

        // 再生開始したクリップをマークする
        internal void SetClipPlaying(int trackIndex, int slotIndex, ClipKind kind, string clipName) {
            LiveClipSlotInfo slot = GetOrCreateSlot(trackIndex, slotIndex);
            if (slot == null) {
                return;
            }
            bool wasPlaying = slot.IsPlaying;
            slot.IsPlaying = true;
            transport.IsPlaying = true;
            if (!wasPlaying) {
                slot.PreferLivePosition = false;
                slot.PlayingPositionBeats = 0f;
                slot.LastPositionOscUnscaledTime = 0f;
            }
            if (kind != ClipKind.Empty) {
                slot.Kind = kind;
            }
            if (!string.IsNullOrEmpty(clipName)) {
                slot.Name = clipName;
            }
        }

        // MIDI ノート受信開始時に対象スロットを準備する
        internal void BeginClipNotes(int trackIndex, int slotIndex, int noteCount, float lengthBeats) {
            LiveClipSlotInfo slot = GetOrCreateSlot(trackIndex, slotIndex);
            if (slot == null) {
                return;
            }
            slot.ClearMidiNotes();
            if (lengthBeats > 0f) {
                slot.LengthBeats = lengthBeats;
            }
            activeNoteImportKey = MakeSlotKey(trackIndex, slotIndex);
        }

        // MIDI ノートを 1 件追加する（時刻は拍）
        internal void AddClipNote(int trackIndex, int slotIndex, int pitch, float startBeats, float durationBeats, int velocity) {
            LiveClipSlotInfo slot = GetOrCreateSlot(trackIndex, slotIndex);
            if (slot == null) {
                return;
            }
            if (durationBeats <= 0f) {
                return;
            }
            MidiClipNoteInfo note = new MidiClipNoteInfo {
                Pitch = pitch,
                StartBeat = startBeats,
                DurationBeats = durationBeats,
                Velocity = velocity
            };
            slot.AddMidiNote(note);
        }

        // 描画用の再生位置（最後の OSC 位置から BPM で補間）
        public float GetInterpolatedPositionBeats(LiveClipSlotInfo slot, float nowUnscaledTime) {
            if (slot == null || !slot.IsPlaying) {
                return 0f;
            }
            if (!slot.PreferLivePosition || slot.LastPositionOscUnscaledTime <= 0f) {
                return slot.PlayingPositionBeats;
            }
            float elapsed = nowUnscaledTime - slot.LastPositionOscUnscaledTime;
            if (elapsed < 0f) {
                elapsed = 0f;
            }
            float advanced = slot.PlayingPositionBeats + (EffectiveBpm / 60f) * elapsed;
            return WrapBeat(advanced, slot.LengthBeats);
        }

        // 拍位置をループ長で折り返す
        static float WrapBeat(float beat, float lengthBeats) {
            if (lengthBeats <= 0f) {
                return beat;
            }
            float wrapped = beat % lengthBeats;
            if (wrapped < 0f) {
                wrapped += lengthBeats;
            }
            return wrapped;
        }

        // MIDI ノート受信完了を記録する
        internal void EndClipNotes(int trackIndex, int slotIndex) {
            string key = MakeSlotKey(trackIndex, slotIndex);
            if (activeNoteImportKey == key) {
                activeNoteImportKey = "";
            }
            LiveClipSlotInfo slot = FindClip(trackIndex, slotIndex);
            if (slot == null || slot.LengthBeats > 0f) {
                return;
            }
            float maxEndBeat = 0f;
            for (int noteIndex = 0; noteIndex < slot.MidiNotes.Count; noteIndex++) {
                MidiClipNoteInfo note = slot.MidiNotes [noteIndex];
                float endBeat = note.StartBeat + note.DurationBeats;
                if (endBeat > maxEndBeat) {
                    maxEndBeat = endBeat;
                }
            }
            if (maxEndBeat > 0f) {
                slot.LengthBeats = maxEndBeat;
            }
        }

        // ノート受信中のスロットを返す
        LiveClipSlotInfo FindActiveNoteImportSlot() {
            if (string.IsNullOrEmpty(activeNoteImportKey)) {
                return null;
            }
            string [ ] parts = activeNoteImportKey.Split(':');
            if (parts.Length != 2) {
                return null;
            }
            if (!int.TryParse(parts [0], out int trackIndex)) {
                return null;
            }
            if (!int.TryParse(parts [1], out int slotIndex)) {
                return null;
            }
            return FindClip(trackIndex, slotIndex);
        }

        // 再生位置（拍）を更新する
        internal void SetClipPosition(int trackIndex, int slotIndex, float positionBeats, float receivedAtUnscaledTime) {
            LiveClipSlotInfo slot = FindClip(trackIndex, slotIndex);
            if (slot == null || !slot.IsPlaying) {
                return;
            }
            transport.IsPlaying = true;
            slot.PlayingPositionBeats = WrapBeat(positionBeats, slot.LengthBeats);
            slot.LastPositionOscUnscaledTime = receivedAtUnscaledTime;
            slot.PreferLivePosition = true;
        }

        // 全スロットの再生フラグを落とす
        void ClearPlayingFlags() {
            for (int trackIndex = 0; trackIndex < tracks.Count; trackIndex++) {
                LiveTrackInfo track = tracks [trackIndex];
                for (int slotIndex = 0; slotIndex < track.Slots.Count; slotIndex++) {
                    track.Slots [slotIndex].IsPlaying = false;
                }
            }
        }

        // トラック index とスロット index からスロットを取得または作成する
        LiveClipSlotInfo GetOrCreateSlot(int trackIndex, int slotIndex) {
            while (tracks.Count <= trackIndex) {
                tracks.Add(new LiveTrackInfo { Index = tracks.Count });
            }
            LiveTrackInfo track = tracks [trackIndex];
            track.Index = trackIndex;
            LiveClipSlotInfo slot = track.EnsureSlot(slotIndex);
            slot.TrackIndex = trackIndex;
            slot.SlotIndex = slotIndex;
            return slot;
        }

        // ノート受信辞書用のキーを作る
        static string MakeSlotKey(int trackIndex, int slotIndex) {
            return trackIndex.ToString() + ":" + slotIndex.ToString();
        }
    }
}
