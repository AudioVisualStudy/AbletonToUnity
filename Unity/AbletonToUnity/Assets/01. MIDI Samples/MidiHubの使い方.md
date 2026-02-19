# MidiHub の使い方

MidiHub は、Ableton Live などから送られてくる MIDI を Unity で受け取り、**「このイベントが来たらこの処理をしてね」** と登録した関数に届けるためのコンポーネントです。  
このドキュメントでは、**イベントの登録・解除**と、**関数に渡される MIDI 情報の型**（MidiNote など）を解説します。

---

## 前提：MidiHub の準備

1. シーンに空の GameObject を作成し、**MidiHub** コンポーネントをアタッチする。
2. Inspector の **「受信するMIDIのポート名」** に、Ableton などが送信先にしている仮想 MIDI ポート名（例: `AbletonToUnity`）を入力する。
3. MidiHub は **シーンに1つ** あればよく、どこからでも `MidiHub.Instance` で参照できる。

```csharp
using yugop.connection;

// どこからでも MidiHub にアクセスできる
MidiHub hub = MidiHub.Instance;
```

---

## 1. イベントの登録・解除

### 1.1 基本的な流れ

1. **Start（または Awake）** で「どのイベントが来たら、どの関数を実行するか」を **登録** する。
2. 登録した関数は、MIDI が届いたときに Unity 側で普通に呼ばれるので、`Transform` や `Debug.Log` など、いつも使っている Unity の機能をそのまま使ってよい。
3. オブジェクトが消えるときは **OnDestroy** で必ず **解除** する。解除しないと、消えたオブジェクトの処理が残って不具合の原因になる。

### 1.2 登録・解除のメソッド一覧

| 受け取りたいイベント | 登録するメソッド | 解除するメソッド | 関数に渡されるデータの型 |
|----------------------|------------------|------------------|--------------------------|
| ノート（鍵盤）が押された     | `AddNoteOnListener`   | `RemoveNoteOnListener`   | `MidiNote` |
| ノート（鍵盤）が離された     | `AddNoteOffListener`  | `RemoveNoteOffListener`  | `MidiNote` |
| コントロールチェンジ | `AddControlChangeListener` | `RemoveControlChangeListener` | `MidiControlChange` |
| ピッチベンド         | `AddPitchBendListener`   | `RemovePitchBendListener`   | `MidiPitchBend` |
| プログラムチェンジ   | `AddProgramChangeListener` | `RemoveProgramChangeListener` | `MidiProgramChange` |

### 1.3 登録の書き方（例：ノート（鍵盤）が押されたとき）

**全部のチャンネル・全部のノート（鍵盤）** の「押された」を受け取る場合：

```csharp
void Start() {
    MidiHub.Instance.AddNoteOnListener(OnNoteOn);
}

// MIDI でノート（鍵盤）が押されると、この関数が呼ばれる
void OnNoteOn(MidiNote note) {
    Debug.Log($"押された: {note.String} 強さ={note.Velocity}");
}

void OnDestroy() {
    if (MidiHub.Instance != null) {
        MidiHub.Instance.RemoveNoteOnListener(OnNoteOn);
    }
}
```

**特定のチャンネルだけ** 受け取る場合（Unity ではチャンネルは 0～15。Ableton の 1～16 より 1 小さい）：

```csharp
// チャンネル 0 の「押された」だけ受け取る
MidiHub.Instance.AddNoteOnListener(OnNoteOn, 0);
```

**特定の音だけ** 受け取る場合（ノート（鍵盤）番号 0～127、または "C4" のような名前）：

```csharp
// ノート（鍵盤）番号 60（中央のド）だけ
MidiHub.Instance.AddNoteOnListener(OnNoteOn, null, 60);

// ノート（鍵盤）名で指定（"C4", "A#3" など）
MidiHub.Instance.AddNoteOnListener(OnNoteOn, 0, "C4");
```

### 1.4 解除の書き方

登録したときの **同じ関数** を指定して解除する。Remove の引数には、登録した関数名を書く。

```csharp
void OnDestroy() {
    if (MidiHub.Instance == null) return;
    MidiHub.Instance.RemoveNoteOnListener(OnNoteOn);
    MidiHub.Instance.RemoveNoteOffListener(OnNoteOff);
    // 登録したものは、すべて対応する Remove で解除する
}
```

### 1.5 他のイベントの登録例

- **ノート（鍵盤）が離された（NoteOff）**  
  `AddNoteOffListener(OnNoteOff, channel)` のように、NoteOn と同様にチャンネル・ノート（鍵盤）で絞れる。
- **コントロールチェンジ（CC）**  
  `AddControlChangeListener(OnCC, channel, controlNumber)`  
  `controlNumber` を指定すると、その CC 番号（例: 7＝ボリューム）だけ受け取れる。
- **ピッチベンド**  
  `AddPitchBendListener(OnPitchBend, channel)`  
  チャンネルだけ指定する。
- **プログラムチェンジ**  
  `AddProgramChangeListener(OnProgramChange, channel)`  
  チャンネルだけ指定する。

### 1.6 補足：チャンネル番号について

Ableton Live では MIDI チャンネルは **1～16** で表示・指定しますが、Unity（MidiHub）では **0～15** で扱います。  
対応関係は次のとおりです。

| Ableton Live | Unity（MidiHub） |
|--------------|------------------|
| チャンネル 1 | チャンネル 0     |
| チャンネル 2 | チャンネル 1     |
| …            | …                |
| チャンネル 16| チャンネル 15    |

Ableton のチャンネル番号から **1 を引く**と Unity のチャンネル番号になります。

---

## 2. MIDI情報を表すクラス

登録した関数には、イベントの種類に応じて **MIDI の情報が入った型** が渡されます。ここでは、それらを一覧にし、それぞれのプロパティを解説します。

### 2.1 一覧

| 型の名前 | どんなときに渡されるか | 主な用途 |
|----------|------------------------|----------|
| **MidiNote** | ノート（鍵盤）が押された / 離された | 鍵盤の音・強さ |
| **MidiControlChange** | つまみやペダルなどが動いた | ボリューム・パン・モジュレーションなど |
| **MidiPitchBend** | ピッチベンド（音の高さを滑らかに変える） | ホイールやレバー |
| **MidiProgramChange** | 音色が切り替わった | プログラム番号（1～128 の音色） |

---

### 2.2 MidiNote（ノート（鍵盤）が押された / 離された）

**1つの鍵盤の情報**（どの音か・どれくらいの強さか）が入った型です。

#### プロパティ

| プロパティ | 型 | 説明 |
|------------|-----|------|
| **Channel** | `int` | MIDI チャンネル（0～15） |
| **Number**  | `int` | ノート（鍵盤）番号（0～127）。中央のドは 60（C4） |
| **Velocity**| `int` | 強さ（0～127）。押した強さ。離したときは多くの場合 0 |
| **String** | `string` | ノート（鍵盤）名（例: `"C4"`, `"A#3"`）。Number から自動で決まる |

#### よく使うメソッド

| メソッド | 戻り値 | 説明 |
|----------|--------|------|
| **IsNoteOn()**  | `bool` | 押されたときなら true |
| **IsNoteOff()** | `bool` | 離されたときなら true |
| **GetNormalizedVelocity()** | `float` | 強さを 0.0～1.0 に変換（127 → 1.0） |

#### 使用例

```csharp
void OnNoteOn(MidiNote note) {
    Debug.Log($"押された: {note.String} 強さ={note.Velocity}");
    float strength = note.GetNormalizedVelocity();
    transform.localScale = Vector3.one * (0.5f + strength * 0.5f);
    if (note.Number == 60) { /* 中央のドのとき */ }
}
```

---

### 2.3 MidiControlChange（つまみ・ペダルなど）

**コントロールチェンジ**（CC）の情報です。どのコントロール番号が、どの値になったかが入ります。

#### プロパティ

| プロパティ | 型 | 説明 |
|------------|-----|------|
| **Channel** | `int` | MIDI チャンネル（0～15） |
| **ControlNumber** | `int` | コントロール番号（0～127）。例: 7＝ボリューム、10＝パン、64＝サスティンペダル |
| **ControlValue** | `int` | 値（0～127） |

#### よく使うメソッド

| メソッド | 戻り値 | 説明 |
|----------|--------|------|
| **GetNormalizedValue()** | `float` | 値を 0.0～1.0 に変換（127 → 1.0） |
| **IsModulationWheel()** | `bool` | モジュレーションホイール（CC#1）なら true |
| **IsVolume()** | `bool` | ボリューム（CC#7）なら true |
| **IsPan()** | `bool` | パン（CC#10）なら true |
| **IsExpression()** | `bool` | エクスプレッション（CC#11）なら true |
| **IsSustainPedal()** | `bool` | サスティンペダル（CC#64）なら true |
| **IsControlNumber(int)** | `bool` | 指定した番号と一致するなら true |

#### 使用例

```csharp
void OnCC(MidiControlChange cc) {
    if (cc.IsVolume()) {
        float vol = cc.GetNormalizedValue(); // 0～1
        audioSource.volume = vol;
    }
    if (cc.IsSustainPedal()) {
        bool pedalOn = cc.ControlValue >= 64;
        // ペダルが踏まれているかどうか
    }
}
```

---

### 2.4 MidiPitchBend（ピッチベンド）

**ピッチベンド**（音の高さを滑らかに上げ下げする）の情報です。中央が 0、上下で -1.0～1.0 のように扱えます。

#### プロパティ

| プロパティ | 型 | 説明 |
|------------|-----|------|
| **Channel** | `int` | MIDI チャンネル（0～15） |
| **RawValue** | `int` | 生の値（0～16383）。中央（ベンドなし）は 8192 |
| **Ratio** | `float` | -1.0～1.0 に正規化した値。0 が中央、正で上方向、負で下方向 |

#### よく使うメソッド

| メソッド | 戻り値 | 説明 |
|----------|--------|------|
| **IsCenter()** | `bool` | 中央（ベンドなし）なら true |
| **IsBendUp()** | `bool` | 上方向にベンドしているなら true |
| **IsBendDown()** | `bool` | 下方向にベンドしているなら true |

#### 使用例

```csharp
void OnPitchBend(MidiPitchBend pb) {
    // -1～1 の値で何かを動かす
    float amount = pb.Ratio;
    transform.position = basePosition + Vector3.right * amount * 2f;
    if (pb.IsCenter()) { /* ベンドなしに戻った */ }
}
```

---

### 2.5 MidiProgramChange（音色の切り替え）

**プログラムチェンジ**（音色を切り替える）の情報です。どのプログラム番号（音色）に変わったかが入ります。

#### プロパティ

| プロパティ | 型 | 説明 |
|------------|-----|------|
| **Channel** | `int` | MIDI チャンネル（0～15） |
| **ProgramNumber** | `int` | プログラム番号（0～127）。多くの機器では 1～128 で表示されるので、その場合は GetProgramNumberOneBased() を使う |

#### よく使うメソッド

| メソッド | 戻り値 | 説明 |
|----------|--------|------|
| **GetProgramNumberOneBased()** | `int` | 1～128 の番号で取得（機器の表示に合わせたいとき） |
| **SetProgramNumberOneBased(int)** | - | 1～128 の番号で設定するとき |
| **IsProgramNumber(int)** | `bool` | 指定した番号と一致するなら true |

#### 使用例

```csharp
void OnProgramChange(MidiProgramChange pc) {
    int soundNumber = pc.GetProgramNumberOneBased(); // 1～128
    Debug.Log($"音色が {soundNumber} に変わった");
    if (pc.IsProgramNumber(0)) { /* プログラム 0（多くの機器では 1 番） */ }
}
```

---

## 3. まとめ

- **登録**: `Start` などで「このイベントが来たらこの関数を実行して」と `Add〇〇Listener(関数, チャンネル, …)` で伝える。
- **解除**: オブジェクトが消えるときに `OnDestroy` で `Remove〇〇Listener(同じ関数)` を呼んで、登録を外す。
- **渡されるデータ**: イベントごとに **MidiNote**（鍵盤）、**MidiControlChange**（つまみ・ペダル）、**MidiPitchBend**（ピッチベンド）、**MidiProgramChange**（音色）のいずれかが渡される。それぞれプロパティで中身を参照できる。

この流れを守れば、MidiHub を安全に利用できる。
