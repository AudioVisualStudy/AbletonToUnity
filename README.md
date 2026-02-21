# AbletonToUnity

Ableton Live の演奏やオーディオ入力を **Unity で視覚化**するためのサンプルプロジェクトです。  
「音声を波形として表示する」「MIDI を受信してオブジェクトを動かす」といった基礎を、初心者の学生でも追いやすい形でまとめています。

---

## 概要

- **1 MIDI 入力を視覚化する**：Ableton Live などから送られる MIDI（ノート・CC・ピッチベンドなど）を Unity で受け取り、**「このイベントが来たらこの処理をする」** と登録した関数に届ける。サンプルは **MIDI Monitor**（受信一覧）と **CubeVisualizer**（ドラムでキューブが動く）。
- **2 オーディオ入力を視覚化する**：PC のマイクやオーディオインターフェースから入力した音声を、Unity 上で**波形**として表示する。サンプルは **MultiWaveForm**（折れ線）と **WaveTexture**（白黒ストライプ）。

どちらも「**データの取得元を 1 つにまとめ、複数のビジュアライザーで使い回す**」構成にしています。

---

## プロジェクト構成

Unity プロジェクトは **`Unity/AbletonToUnity`** にあります。Assets の主なフォルダは次のとおりです。

```
Unity/AbletonToUnity/Assets/
├── 01. MIDI/                    # 1 MIDI 入力を視覚化する
│   ├── Lib/
│   │   ├── MidiHub/             # MIDI 受信の中核（DryWetMIDI を利用）
│   │   ├── DryWetMIDI/          # 外部ライブラリ（MIDI I/O）
│   │   └── HEasing/
│   ├── MIDIMonitor/             # サンプル：受信した MIDI を一覧表示
│   └── [Sample] CubeVisualizer/ # サンプル：ドラムのノートでキューブが動く
│
├── 02. Audio/                   # 2 オーディオ入力を視覚化する
│   ├── Lasp/                    # オーディオ入力プラグイン（リアルタイム取得）
│   ├── MultiWaveDataの使い方.md # 波形データ取得の API 解説（MultiWaveForm / WaveTexture 共通）
│   ├── [Sample] MultiWaveForm/  # サンプル：波形を折れ線で表示（MultiWaveData + WaveDrawer）
│   └── [Sample] WaveTexture/    # サンプル：波形を白黒ストライプで表示（MultiWaveData + WaveTexture）
│
├── Settings/                    # URP などのプロジェクト設定
└── TextMesh Pro/                # フォント・UI 用（MIDIMonitor 等で使用）
```

---

## 手法の整理（初心者向け）

### 1. MIDI 入力を視覚化する：「ポート受信 → リスナーに配信」

1. **DryWetMIDI**  
   PC の **仮想 MIDI ポート**（例：Ableton の送信先「AbletonToUnity」）から MIDI メッセージを受け取るために使っている外部ライブラリです。

2. **MidiHub**  
   DryWetMIDI で受信した MIDI を、**Unity のメインスレッド**で「NoteOn / NoteOff / ControlChange / PitchBend / ProgramChange」ごとに振り分け、**登録されたコールバック関数**に届けます。  
   - シーンに **1 つ** MidiHub を置き、**`MidiHub.Instance`** でどこからでも参照できます。  
   - 「このチャンネル・このノート番号のときだけ呼んでほしい」といった**フィルタ**も指定できます。

3. **サンプル（MIDI Monitor / CubeVisualizer）**  
   **MIDI Monitor** は受信した MIDI を一覧表示。**CubeVisualizer** は `Start` で `MidiHub.Instance.AddNoteOnListener(OnNoteOn)` のように**リスナーを登録**し、ノートが押されると `OnNoteOn(MidiNote note)` が呼ばれます。  
   - オブジェクトが消えるときは **OnDestroy** で **Remove〇〇Listener** して解除する必要があります。

**まとめ**  
「**仮想 MIDI ポート（Ableton など）→ MidiHub（受信・配信）→ 各スクリプトの登録した関数**」です。ビジュアライザーは「どのイベントで何をするか」だけを書けばよく、MIDI の低レベルな扱いは MidiHub に任せます。

---

### 2. オーディオ入力を視覚化する：「入力 → 蓄積 → 描画」

1. **Lasp**  
   Unity の実行中に、マイクやオーディオインターフェースから**リアルタイムで音声**を取得します。本プロジェクトでは Lasp を「音声の入り口」として使っています。

2. **MultiWaveData**  
   Lasp が取得した音声を、**一定時間分の波形データ（float の配列）** として**リングバッファ**に蓄えます。  
   - **リングバッファ**：古いデータを上書きしながら、直近 N 秒分を常に保持する仕組みです。  
   - 他のスクリプトからは **`GetWaveData(チャンネル, 秒数, 解像度)`** で「直近 ○ 秒分を ○ 点で」取得できます。  
   - シーンに **1 つだけ** MultiWaveData を置き、複数の描画コンポーネントから**同じデータを参照する**想定です。

3. **描画コンポーネント（MultiWaveForm：WaveDrawer / WaveTexture）**  
   MultiWaveData の `GetWaveData` で取得した配列を、**毎フレーム**「座標」や「メッシュの頂点」に変換して描画します。  
   - **MultiWaveForm**：WaveDrawer が LineRenderer で波形を**折れ線**として表示。チャンネルごとに色分け可能。  
   - **WaveTexture**：波形の振幅に応じて**白黒の縦ストライプ**を Mesh で描画。複数チャンネルを縦に帯状に並べられます。

**まとめ**  
「**Lasp（入力）→ MultiWaveData（蓄積）→ WaveDrawer / WaveTexture（描画）**」という 3 段階になっています。データの形は「時間順の振幅値の配列」で、描画側はそれをどう見せるかだけを担当します。

---

## サンプルの使い方（最短）

### 1. MIDI 入力を視覚化する

**MIDI Monitor**（受信した MIDI を一覧表示）

1. シーン **`[Scene] MidiMonitor.unity`** を開く。  
2. MidiHub の **「受信するMIDIのポート名」** に、Ableton などが送信する仮想 MIDI ポート名を入力。  
3. **Play** して、MIDI を送ると一覧に表示されます。

**CubeVisualizer**（ドラムのノートでキューブが動く）

1. Ableton などで **仮想 MIDI ポート**（例：LoopMIDI で作った「AbletonToUnity」）に MIDI を送る設定にする。  
2. シーン **`[Scene] CubeVisualizer.unity`** を開く。  
3. MidiHub の **「受信するMIDIのポート名」** に、そのポート名を入力。  
4. **Play** して、Ableton でドラムなどを鳴らすと、ノートに反応してキューブが動きます。  
5. MIDI の登録方法は **`Assets/01. MIDI/MidiHubの使い方.md`** を参照。

---

### 2. オーディオ入力を視覚化する

**MultiWaveForm**（波形を折れ線で表示）

1. シーン **`[Scene] MultiWaveForm.unity`** を開く。  
2. **Play** して、マイクなどから音を入れる。  
3. 同じ GameObject に **MultiWaveData**（音声を蓄える）と **WaveDrawer**（折れ線で描く）が付いていれば、波形が表示されます。  
4. 詳しくは **`Assets/02. Audio/[Sample] MultiWaveForm/MultiWaveFormサンプルについて.md`** と **`Assets/02. Audio/MultiWaveDataの使い方.md`** を参照。

**WaveTexture**（波形を白黒ストライプで表示）

1. シーン **`[Scene] WaveTexture.unity`** を開く。  
2. **MultiWaveData** でデータ源を、**WaveTexture** で描画領域・チャンネル・しきい値などを指定。  
3. 表示するチャンネルは「チャンネル」配列の「表示」にチェックを入れたものだけが、上から縦に帯状に並びます。

---

## 必要な環境

- **Unity**（本プロジェクトの推奨バージョンに合わせてください）
- **オーディオ**：マイクまたはオーディオインターフェース（OS でマイク許可が必要な場合あり）
- **MIDI**：仮想 MIDI ポート（例：LoopMIDI、Ableton の「Track / Send to」でそのポートを指定）

---

## 用語の整理

| 用語 | 説明（このプロジェクトでの意味） |
|------|----------------------------------|
| **Lasp** | Unity 用のオーディオ入力プラグイン。入力デバイスからリアルタイムで波形を取得する。 |
| **リングバッファ** | 直近 N 秒分のデータを保持し、新しいデータで古いデータを上書きするバッファ。MultiWaveData が使用。 |
| **GetWaveData** | MultiWaveData の API。「チャンネル・秒数・解像度」を指定すると、その条件の波形（float[]）を返す。 |
| **MidiHub** | MIDI を受信し、登録したリスナー（関数）にイベントを届けるコンポーネント。シングルトン。 |
| **DryWetMIDI** | MIDI の送受信を行う .NET ライブラリ。MidiHub が内部で利用。 |

---

## まとめ

- **1 MIDI 入力を視覚化する**：仮想ポート → MidiHub（DryWetMIDI）→ Add〇〇Listener で登録した関数にイベントが届く。サンプルは **MIDI Monitor** と **CubeVisualizer**。  
- **2 オーディオ入力を視覚化する**：Lasp → MultiWaveData（リングバッファ）→ GetWaveData で配列を取得 → **MultiWaveForm**（WaveDrawer）/ **WaveTexture** が描画。  
- どちらも「**データの入り口を 1 つにまとめ、複数のビジュアライザーで共有する**」構成になっており、初心者の方はまずこの流れを押さえると読みやすくなります。  

各サンプルフォルダ内の説明用 .md と、インスペクタのヘッダ・ツールチップもあわせて参照してください。
