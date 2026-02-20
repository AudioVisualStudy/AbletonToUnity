# MultiWaveData の使い方（API 解説）

**MultiWaveData** は、マイクやオーディオインターフェースから入ってきた音声を「波形の数値」として蓄えておくコンポーネントです。  
このコンポーネントを **1 つ用意して、複数の描画や処理で使い回す** ことを前提にした解説です。

---

## セットアップ（使い回す前の準備）

### 1. シーンに 1 つだけ置く

- 空の GameObject を作成する（または既存のオブジェクトを使う）。
- その GameObject に **MultiWaveData** コンポーネントを **1 つ** アタッチする。

### 2. インスペクタで設定する

- **デバイス**  
  使う入力デバイスの番号（0 が多くの環境で「規定のマイク」）。  
  再生開始時にコンソールに `[MultiWaveData] 0: デバイス名` のように出る番号と対応する。
- **バッファ（最大で保持する秒数）**  
  何秒分までの音声を蓄えるか。ここで指定した秒数より長い「過去」のデータは取得できない。

### 3. ほかのコンポーネントから参照する

- 波形を描画する **WaveDrawer** の「Data Source」に、この MultiWaveData がアタッチされている GameObject（または MultiWaveData コンポーネント）を指定する。
- 自作スクリプトから使う場合は、`SerializeField` で MultiWaveData を参照するか、`GetComponent<MultiWaveData>()` で取得する。

ここまでで「1 つの MultiWaveData を、複数のコンポーネントで使い回す」準備ができています。

---

## 公開 API（ほかのスクリプトから使えるもの）

MultiWaveData が用意している「外から呼んでよい」プロパティとメソッドは次の 2 つです。

---

### 1. ChannelCount（チャンネル数）

**形（シグネチャ）**

```csharp
public int ChannelCount
```

**説明**

- 現在使っている入力デバイスの **有効なチャンネル数** を返します。
- 例：ステレオなら `2`、モノラルなら `1`。最大 8 まで（内部で 8 チャンネルで打ち切っています）。

**いつ使うか**

- 波形を描画するときに「何本の線（何チャンネル分）を描くか」を決めるとき。
- チャンネルごとにループを回すときの上限（`for (int ch = 0; ch < dataSource.ChannelCount; ch++)` など）。

**例**

```csharp
MultiWaveData dataSource = GetComponent<MultiWaveData>();
int count = dataSource.ChannelCount;  // 例: 2
```

---

### 2. GetWaveData（波形データを取得する）

**形（シグネチャ）**

```csharp
public float[] GetWaveData(int channel, float seconds, int resolution)
```

**引数**

| 名前       | 型    | 説明（初心者向け） |
|------------|--------|----------------------|
| channel   | int   | どのチャンネルのデータが欲しいか。0, 1, 2 …。 |
| seconds   | float | **何秒分**の波形が欲しいか。例：`0.01f` なら直近 0.01 秒分。 |
| resolution| int   | その何秒分を **何個の点** で表すか。例：512 なら 512 個の float が並んだ配列になる。 |

**戻り値**

- 成功したとき：**float の配列**。  
  - 配列の **前の方** ＝ 少し過去の音  
  - 配列の **後ろの方** ＝ 直近の音  
  という並びになっています。
- 取得できないとき：**null**（チャンネル番号がおかしい、まだデータが足りない、など）。

**いつ使うか**

- 波形を線で描画するとき（WaveDrawer が毎フレームこれでデータを取って LineRenderer に渡している）。
- 自作で「直近 ○ 秒分の波形」を使って何か計算したり表示したりするとき。

**注意**

- `seconds` は、MultiWaveData のインスペクタで設定した「バッファ（最大で保持する秒数）」より大きくしないこと。それより長い分はデータがないため、実質「最大保持秒数」で打ち切られます。
- `resolution` は 32 ～ 8192 の範囲に内部で収められます。大きいほど滑らかだが重くなりやすいです。

**例**

```csharp
// チャンネル 0 の、直近 0.01 秒分を 960 点で取得
float[] wave = dataSource.GetWaveData(0, 0.01f, 960);

if (wave != null) {
    // 例: 配列の長さだけ何かする
    for (int i = 0; i < wave.Length; i++) {
        float amplitude = wave[i];  // -1～1 付近の振幅値
        // ...
    }
}
```

---

## 使い回すときのポイント

1. **参照は 1 か所で管理**  
   デバイスやバッファの設定は MultiWaveData のインスペクタだけ触ればよい。WaveDrawer や自作スクリプトは「Data Source」などでその MultiWaveData を参照するだけ。どのチャンネルを表示するかは **WaveDrawer** の「Draw Channel」で決める。

2. **GetWaveData は何度呼んでもよい**  
   同じフレーム内で、複数のコンポーネントが同じチャンネルの `GetWaveData` を呼んでも問題ない。読み取り専用のように扱われ、内部のバッファは 1 つなので「同じ入力」をみんなで共有できる。

3. **初期化のタイミング**  
   MultiWaveData は **Awake** でデバイスとバッファを用意する。  
   参照する側（例：WaveDrawer）は **Start** で LineRenderer を作る想定なので、Start の時点ではすでに `ChannelCount` や `GetWaveData` が使える状態になっています。

4. **null チェック**  
   `GetWaveData` は失敗すると `null` を返す。初心者のうちは「取得したら null でないか確認してから使う」と安全です。

---

## まとめ（API 一覧）

| 名前 | 種類 | 説明 |
|------|------|------|
| **ChannelCount** | プロパティ (int) | 有効なチャンネル数（最大 8）。 |
| **GetWaveData(channel, seconds, resolution)** | メソッド (float[]) | 指定チャンネルの直近 ○ 秒分を ○ 点で取得。取れないときは null。 |

**使い回しの前提**  
1 つの MultiWaveData をシーンに 1 つ置き、WaveDrawer や自作スクリプトから参照して、同じ波形データを共有して使う。
