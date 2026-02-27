# CLAUDE.md

このファイルは、リポジトリ内のコードを扱う際に Claude Code (claude.ai/code) へ向けたガイダンスを提供します。

## プロジェクト概要

AbletonToUnity は、Ableton Live のパフォーマンスをリアルタイムで可視化するデモ用の Unity プロジェクトです。2つのサブシステムで構成されています：
1. **MIDI** – バーチャル MIDI ポートを通じて Ableton から MIDI イベントを受信し、Unity コンポーネントへ配信する
2. **Audio** – マイク／オーディオインターフェースからライブ音声を取得し、波形をリアルタイムで描画する

カスタムソースコードはすべて `Unity/AbletonToUnity/Assets/` に置かれています。`Library/` フォルダは Unity が自動生成するキャッシュなので、直接編集しないでください。

## 開発環境のセットアップ

これは Unity プロジェクトなので、CLI のビルドコマンドはありません。開発は Unity Editor 上で行います：
- プロジェクトを開く：Unity Hub → Add → `Unity/AbletonToUnity/`
- シーンは `Assets/01. MIDI/` および `Assets/02. Audio/` サブディレクトリ以下にあります

**MIDI の前提条件**：`"AbletonToUnity"` という名前のバーチャル MIDI ポートがシステムに存在している必要があります（例：LoopMIDI で作成）。ポート名は `MidiHub` コンポーネントのインスペクタフィールド「受信するMIDIのポート名」で設定します。

## アーキテクチャ

### MIDI サブシステム (`Assets/01. MIDI/`)

**データフロー**：Ableton → バーチャル MIDI ポート → DryWetMIDI（バックグラウンドスレッド）→ `MidiHub` キュー → Unity メインスレッドコールバック → 各コンポーネント

- **`MidiHub.cs`** – シングルトン。DryWetMIDI 経由でバックグラウンドスレッドで MIDI を受信し、イベントをキューに積んで `Update()` でメインスレッドへ配信する。利用側は型付きリスナーを登録する：`AddNoteOnListener`、`AddNoteOffListener`、`AddControlChangeListener`、`AddPitchBendListener`、`AddProgramChangeListener`。各リスナーはチャンネルとノート番号／CC 番号でフィルタリング可能。
- **`MidiUtil.cs`** – 値型の定義：`MidiNote`（チャンネル、ノート 0–127、ベロシティ）、`MidiControlChange`（チャンネル、コントロールナンバー、値 0–127）、`MidiPitchBend`（チャンネル、生値 0–16383、正規化値 –1.0〜1.0）、`MidiProgramChange`。ノート番号と音名の相互変換および正規化ヘルパーを含む。

### Audio サブシステム (`Assets/02. Audio/`)

**データフロー**：マイク／インターフェース → Lasp `AudioLevelTracker` → `MultiWaveData` リングバッファ → `WaveDrawer` / `WaveTexture`（`LateUpdate` ごとに更新）

- **`MultiWaveData.cs`** – シーンシングルトン。チャンネルごとに `Lasp.AudioLevelTracker` を1つ生成し（最大8チャンネル）、サンプルをリングバッファに格納する。API：`GetWaveData(channel, seconds, resolution)` が `float[]` を返す。Lasp トラッカーの設定（自動ゲイン無効化、フィルタバイパス）にはリフレクションを使用。インスペクタの値が実行時に変更された場合はデバイス／バッファを再構築する。
- **`WaveDrawer.cs`** – チャンネルごとに `LineRenderer` で波形を描画する。`LateUpdate` のたびに `GetWaveData` を呼び出す。
- **`WaveTexture.cs`** – 動的メッシュ（色付きクワッド、チャンネルごとに縦帯）として波形を描画する。振幅がしきい値を超えると白／黒、未満はグレー。三角形は事前計算済みで、毎フレームは頂点の位置と色のみ更新する。

### サンプルシーン

| シーン | 場所 | 内容 |
|---|---|---|
| MidiMonitor | `Assets/01. MIDI/MIDIMonitor/` | 受信した全 MIDI イベントを一覧表示するデバッグ UI（BPM 表示付き） |
| CubeVisualizer | `Assets/01. MIDI/[Sample] CubeVisualizer/` | ドラムノートと CC 値で動く 3D キューブ（URP ChromaticAberration 使用） |
| MultiWaveForm | `Assets/02. Audio/[Sample] MultiWaveForm/` | ライン描画によるマルチチャンネル波形表示 |
| WaveTexture | `Assets/02. Audio/[Sample] WaveTexture/` | メッシュ描画によるストライプ状波形テクスチャ |

### 重要なパターン

- **スレッド安全性**：`MidiHub` は `lock` で保護された `Queue<Action>` を使って、DryWetMIDI のバックグラウンドスレッドから Unity のメインスレッドへ MIDI コールバックを受け渡す。
- **リスナーのクリーンアップ**：ダングリング参照を防ぐため、`OnDestroy` では必ず対応する `Remove*Listener`（例：`RemoveNoteOnListener`）を呼び出すこと。
- **リングバッファ**：`MultiWaveData` は最も古いサンプルを上書きする。バッファが保持する時間より多くの秒数を要求すると、古いデータが返される。
- **MultiWaveData でのリフレクション**：Lasp トラッカーのプロパティはパブリックセッターを持たないためリフレクション経由で設定している。Lasp パッケージのバージョン更新時は注意すること。

## 外部依存ライブラリ

- **DryWetMIDI** – MIDI I/O .NET ライブラリ。`Assets/01. MIDI/Lib/DryWetMIDI/` に同梱。
- **Lasp** – Keijiro 製のリアルタイム音声キャプチャプラグイン。`Assets/02. Audio/Lasp/` に同梱。
- **HEasing** – イージング関数ライブラリ。`Assets/01. MIDI/Lib/HEasing/` に同梱。
- **URP** – ポストプロセス用（CubeVisualizer の ChromaticAberration）。
