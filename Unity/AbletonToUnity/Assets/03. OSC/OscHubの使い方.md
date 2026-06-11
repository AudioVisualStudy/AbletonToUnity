# OscHub の使い方

OscHub は **AbletonOSC**（セッション同期）と **Y-OSC-NoteSender**（リアルタイム MIDI）を集約するコンポーネントです。  
`LiveSessionState` が **全体 → トラック → クリップ → ノート** の単一ツリーを保持します。

---

## 1. Ableton Live 側

### 1.1 AbletonOSC（セッション情報）

1. AbletonOSC をコントロールサーフェスに設定（UDP 11000 / 11001）
2. Live 下部に `AbletonOSC: Listening for OSC on port 11000` を確認

### 1.2 Y-OSC-NoteSender（リアルタイム MIDI）

1. [`Assets/03. OSC/Lib/Y-OscNoteSender/Distribution/`](Lib/Y-OscNoteSender/Distribution/) の **2 ファイル**を User Library にコピー  
   `Documents/Ableton/User Library/Presets/MIDI Effects/Max MIDI Effect/`
2. Live を再起動（またはブラウザを更新）
3. User Library → MIDI Effects → Max MIDI Effect → **Y-OSC-NoteSender** を各 MIDI トラックに配置
4. 楽器トラックでは **Instrument の前**（MIDI Effect スロット）
5. 送信先は `127.0.0.1:11001`（デバイス既定）

| Distribution 同梱 | 役割 |
|-------------------|------|
| `Y-OSC-NoteSender.amxd` | 静的 Max MIDI Effect（Python 不要） |
| `y_osc_note_sender.js` | amxd と同じフォルダに置く sidecar |

詳細は `Distribution/README.txt` を参照。

---

## 2. Unity 側

1. シーンに空の GameObject を作成し、**OscHub** をアタッチする（`AbletonOscClient` / `AbletonSessionSync` / `OscLiveNoteReceiver` は自動追加）
2. **OscHub はシーンに 1 つ**。`OscHub.Instance` で参照する

```csharp
using yugop.connection;

OscHub hub = OscHub.Instance;
LiveSessionState session = hub.Session;
```

### データモデル

| レイヤ | 型 | データ源 |
|--------|-----|---------|
| 全体 | `TransportInfo`, `Session.IsReady` | AbletonOSC |
| トラック | `LiveTrackInfo` | AbletonOSC |
| クリップ | `LiveClipSlotInfo` | AbletonOSC |
| 静的ノート | `MidiClipNoteInfo`（ピアノロール） | AbletonOSC `/live/clip/get/notes` |
| リアルタイムノート | `LiveMidiNoteEvent` | Y-OSC-NoteSender `/y-osc/note` |

リアルタイムノートは `Session.EmitLiveNote` 経由で親クリップの `RecentLiveNotes` に格納されます。

### NoteOn リスナー

```csharp
void Start() {
    OscHub.Instance.AddNoteOnListener(OnNoteOn);
}

void OnNoteOn(MidiNote note) {
    Debug.Log($"TrackID={note.TrackIndex} ClipID={note.SlotIndex} {note.String} vel={note.Velocity}");
}

void OnDestroy() {
    if (OscHub.Instance != null) {
        OscHub.Instance.RemoveNoteOnListener(OnNoteOn);
    }
}
```

| フィールド | 意味 |
|-----------|------|
| `TrackIndex` / `SlotIndex` | 親トラック・クリップ |
| `TrackName` / `ClipName` | 名前（Session から解決） |
| `Number` / `String` | MIDI 番号・音階名 |

### セッション再スキャン

```csharp
OscHub.Instance.StartScan();
```

Inspector の `AbletonSessionSync.rescanKey`（既定 R）でも再スキャンできます。

---

## 3. OSC プロトコル（リアルタイム MIDI）

| アドレス | 型 | 内容 |
|---------|-----|------|
| `/y-osc/note` | `iiii` | `trackIndex`, `slotIndex`, `pitch`, `velocity` |

送信先: `127.0.0.1:11001`

---

## 4. 動作確認

本プロジェクトには **OscSessionVisualizer** サンプル（Shapes 依存）は同梱していません。  
OscHub をシーンに置き、Console ログで動作を確認してください。

1. シーンに **OscHub** を 1 つ配置して **Play**
2. Live で AbletonOSC と Y-OSC-NoteSender を設定
3. Live でクリップを再生し、MIDI を入力する

成功時の Console 例:

```
[OscHub] scan done: tracks=3 version=1
[OscHub] live note : C3 , Vel=100  /  TrackID=0 (MainPiano) , ClipID=0 (NoName)
```

### 反応しないとき

- Live 下部の AbletonOSC メッセージを確認
- ポート 11000 / 11001 の競合・ファイアウォールを確認
- Y-OSC-NoteSender が対象トラックの Instrument 前にあるか確認
- Console の `[OscHub]` ログを確認
