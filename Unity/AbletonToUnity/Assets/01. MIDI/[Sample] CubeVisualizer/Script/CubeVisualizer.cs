using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using yugop.connection;

public class CubeVisualizer : MonoBehaviour {

    [Header ( "キューブのプレハブと最大数。Clapで1個から順に表示が増える（最大20）" )]
    public GameObject cubePrefab;
    [Range ( 1, 20 )]
    public int maxCubeCount = 20;

    [Header ( "ドラムのMIDIチャンネル（1～16）とKick,HiHut,Clapのノート番号" )]
    [Range ( 1, 16 )]
    public int drumChannel = 1;
    [Range ( 0, 127 )]
    public int NoteNum_Kick = 36;
    [Range ( 0, 127 )]
    public int NoteNum_HiHut = 46;
    [Range ( 0, 127 )]
    public int NoteNum_Clap = 39;

    // --- 回転・位置運動 ---
    [Header ( "回転：キックで加わる角速度の強さ（度/秒）" )]
    public float kickStrength = 180f;
    [Header ( "回転・位置：運動の減衰。0に近いほど早く止まる、1に近いほど長く残る" )]
    [Range ( 0.8f, 0.999f )]
    public float velocityDecay = 0.98f;
    [Header ( "位置：各キューブの中心点が原点からずれる最大距離" )]
    public float centerOffsetRadius = 0.5f;
    [Header ( "位置：中心への引力の強さ（大きいほど早く収束）" )]
    public float positionAttractionStrength = 5f;
    [Header ( "位置：キックで加わる並進の強さ" )]
    public float positionKickStrength = 2f;

    // --- プロポーション変化 ---
    [Header ( "プロポーション：各軸スケールの乱数範囲。体積は常に1に正規化" )]
    public float proportionMin = 0.01f;
    public float proportionMax = 2f;
    [Header ( "プロポーション：極端な値の出やすさ。0=一様、1=小さい/大きいが出やすい" )]
    [Range ( 0f, 1f )]
    public float proportionExtremeness = 0.5f;
    [Header ( "プロポーション：各軸の最大スケール。超えたら全体をスケールダウン" )]
    public float proportionMaxScale = 2f;
    [Header ( "プロポーション：連続類似を避ける。分布の差がこの値未満なら再抽選（0で無効）" )]
    public float proportionMinDifference = 0.3f;
    [Header ( "プロポーション：目標値への近づく速さ（0～1）" )]
    [Range ( 0.01f, 1f )]
    public float proportionSmoothSpeed = 0.1f;

    // --- キューブ振動（CCの量に比例・毎フレームランダムに位置・回転を加算） ---
    [Header ( "振動：CC値の指数カーブ。1=線形、>1で小さい値は繊細・大きい値はよりダイナミック" )]
    [Range ( 0.5f, 3f )]
    public float vibrationExponent = 2f;
    [Header ( "振動：位置の揺れ幅（CC=127で毎フレームこの半径内でランダム加算）" )]
    public float cubeVibrationPositionRadius = 0.05f;
    [Header ( "振動：回転の揺れ幅（度）。CC=127で各軸この角度までランダム加算" )]
    public float cubeVibrationRotationDegrees = 2f;

    // --- Global Volume：Chromatic AberrationをCC値に連動 ---
    [Header ( "ポストプロセス：Global VolumeのChromatic AberrationをCC値に連動（0～最大値）" )]
    public Volume globalVolume;
    [Tooltip ( "CC=127のときのChromatic Aberrationの最大値（通常は1）" )]
    [Range ( 0.01f, 1f )]
    public float chromaticAberrationMax = 1f;

    // Cubeの動き管理用パラメータ
    Transform [ ] cube;
    Vector3 [ ] position; //位置
    Vector3 [ ] positionVelocity; //速度
    Quaternion [ ] rotation; //回転
    Vector3 [ ] rotationVelocity; //角速度
    Vector3 [ ] gravityCenter; //重力中心

    // 表示するキューブ数（1で開始、Clapで増加、最大 maxCubeCount）
    int visibleCubeCount;
    // プロポーション用
    Vector3 targetProportion = Vector3.one;
    // キューブ振動：CCの正規化値に指数をかけた強さ（0～1）。未受信時は0
    float vibrationIntensity;
    // Global VolumeのChromatic Aberration（CC連動用）
    ChromaticAberration chromaticAberration;
    MidiHub midiHub;


    //初期化
    void Start () {
        Application.targetFrameRate = 60;

        midiHub = MidiHub.Instance; //MidiHubインスタンスへの参照
        midiHub.startConnection (); //MIDIポートに接続

        midiHub.AddNoteOnListener ( onDrum, drumChannel );  //ドラムのMIDIチャンネルにリスナー関数を追加
        midiHub.AddControlChangeListener ( onControlChange );  //コントロールチェンジにリスナー関数を追加

        generateCubes ();//キューブを生成し、パラメータを初期化

        SetupChromaticAberration ();  // Global VolumeのChromatic AberrationをCC連動用に取得
    }



    // 最大数だけキューブを生成し、最初は1個だけ表示
    void generateCubes () {
        if ( cubePrefab != null && maxCubeCount > 0 ) {
            cube = new Transform [ maxCubeCount ];
            rotationVelocity = new Vector3 [ maxCubeCount ];
            gravityCenter = new Vector3 [ maxCubeCount ];
            positionVelocity = new Vector3 [ maxCubeCount ];
            position = new Vector3 [ maxCubeCount ];
            rotation = new Quaternion [ maxCubeCount ];
            targetProportion = cubePrefab.transform.localScale;
            for ( int i = 0; i < maxCubeCount; i++ ) {
                GameObject go = Instantiate ( cubePrefab, Vector3.zero, Quaternion.identity, transform );
                go.name = cubePrefab.name + "_" + i;
                cube [ i ] = go.transform;
                gravityCenter [ i ] = Random.insideUnitSphere * centerOffsetRadius;
                cube [ i ].localPosition = gravityCenter [ i ];
                position [ i ] = gravityCenter [ i ];
                rotation [ i ] = cube [ i ].rotation;
            }
            visibleCubeCount = 1;
            for ( int i = 0; i < maxCubeCount; i++ )
                cube [ i ].gameObject.SetActive ( i < visibleCubeCount );
        }
    }
    // ドラムが鳴った時。楽器の種類（note.Number）によって処理を分岐する
    void onDrum ( MidiNote note ) {
        if ( note.Number == NoteNum_Kick ) {
            // キックでキューブを回転、移動
            MoveCubes ( kickStrength );
        } else if ( note.Number == NoteNum_HiHut ) {
            // ハイハットでキューブのプロポーションを変更
            ChangeCubeProportion ();
        } else if ( note.Number == NoteNum_Clap ) {
            // クラップでキューブを1個増やす
            AddVisibleCube ();
        } else {
            Debug.LogWarning ( "不明なノート番号: " + note.Number );
        }
    }


    // MIDIコントロールチェンジ時（つまみを回した時）
    // 指数カーブで振動強度を更新（各キューブの振動量に使用）。Chromatic Aberrationは正規化値に比例で0～最大値。
    void onControlChange ( MidiControlChange controlChange ) {
        float normalized = controlChange.GetNormalizedValue ();
        vibrationIntensity = Mathf.Pow ( normalized, vibrationExponent );

        //GlobalVolumeのchromaticAberrationエフェクトの適用量を変化させる
        if ( chromaticAberration != null )
            chromaticAberration.intensity.Override ( normalized * chromaticAberrationMax );
    }


    //////////////////////////////////////////////////////////////////////////////////////////////////////////

    // キックで角速度・並進を加え、中心点をランダム更新。Update で減衰。
    public void MoveCubes ( float strength ) {
        if ( rotationVelocity == null ) return;
        for ( int i = 0; i < rotationVelocity.Length; i++ ) {
            Vector3 randomDir = Random.onUnitSphere;
            rotationVelocity [ i ] += randomDir * strength;
            if ( positionVelocity != null && i < positionVelocity.Length )
                positionVelocity [ i ] += Random.onUnitSphere * positionKickStrength;
            if ( gravityCenter != null && i < gravityCenter.Length )
                gravityCenter [ i ] = Random.insideUnitSphere * centerOffsetRadius;
        }
    }

    // 目標プロポーションを更新。前回と十分違う分布になるまで再抽選。
    public void ChangeCubeProportion () {
        const int maxTries = 30;
        Vector3 prev = targetProportion;
        for ( int i = 0; i < maxTries; i++ ) {
            Vector3 candidate = GenerateOneProportion ();
            if ( proportionMinDifference <= 0f || ProportionDistributionDistance ( candidate, prev ) >= proportionMinDifference ) {
                targetProportion = candidate;
                return;
            }
        }
        targetProportion = GenerateOneProportion ();
    }

    // 表示キューブ数を1つ増やす（最大 maxCubeCount まで）。Clapで呼ぶ。
    void AddVisibleCube () {
        visibleCubeCount = Mathf.Min ( visibleCubeCount + 1, maxCubeCount );
        if ( cube != null && visibleCubeCount <= cube.Length )
            cube [ visibleCubeCount - 1 ].gameObject.SetActive ( true );
    }

    //////////////////////////////////////////////////////////////////////////////////////////////////////////

    //毎フレームの処理
    void Update () {
        if ( cube == null || rotationVelocity == null ) return;
        float dt = Time.deltaTime;

        for ( int i = 0; i < cube.Length; i++ ) {
            if ( cube [ i ] == null ) continue;

            // 回転・位置運動：基準（振動前）の位置・回転のみを更新
            rotation [ i ] *= Quaternion.Euler ( rotationVelocity [ i ] * dt );
            rotationVelocity [ i ] *= velocityDecay;

            if ( gravityCenter != null && positionVelocity != null && position != null && i < gravityCenter.Length ) {
                Vector3 toCenter = gravityCenter [ i ] - position [ i ];
                positionVelocity [ i ] += toCenter * ( positionAttractionStrength * dt );
                position [ i ] += positionVelocity [ i ] * dt;
                positionVelocity [ i ] *= velocityDecay;
            }

            // プロポーション変化
            cube [ i ].localScale = Vector3.Lerp ( cube [ i ].localScale, targetProportion, proportionSmoothSpeed );

            // キューブ振動：基準位置・回転に乱数オフセットを加えて表示
            float posRadius = vibrationIntensity * cubeVibrationPositionRadius;
            Vector3 vibrationPosOffset = Random.insideUnitSphere * posRadius;
            position [ i ] += vibrationPosOffset;
            cube [ i ].localPosition = position [ i ];

            float rotAmount = vibrationIntensity * cubeVibrationRotationDegrees;
            Quaternion vibrationRotOffset = Quaternion.Euler (
                Random.Range ( -1f, 1f ) * rotAmount,
                Random.Range ( -1f, 1f ) * rotAmount,
                Random.Range ( -1f, 1f ) * rotAmount
            );
            rotation [ i ] *= vibrationRotOffset;
            cube [ i ].rotation = rotation [ i ];
        }
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
    // 以下サブ関数
    // 乱数でプロポーションを1つ生成（体積1・最大スケール制限あり）
    Vector3 GenerateOneProportion () {
        float t0 = RandomTowardExtremes ();
        float t1 = RandomTowardExtremes ();
        float t2 = RandomTowardExtremes ();
        float x = Mathf.Lerp ( proportionMin, proportionMax, t0 );
        float y = Mathf.Lerp ( proportionMin, proportionMax, t1 );
        float z = Mathf.Lerp ( proportionMin, proportionMax, t2 );
        float volume = x * y * z;
        if ( volume <= 0.0001f ) return targetProportion;
        float k = 1f / Mathf.Pow ( volume, 1f / 3f );
        Vector3 p = new Vector3 ( x * k, y * k, z * k );
        float maxAxis = Mathf.Max ( p.x, p.y, p.z );
        if ( maxAxis > proportionMaxScale )
            p *= proportionMaxScale / maxAxis;
        return p;
    }

    // 一様乱数を指数で極端な値が出やすく再分布
    float RandomTowardExtremes () {
        float u = Random.Range ( 0f, 1f );
        float dist = 2f * Mathf.Abs ( u - 0.5f );
        float power = Mathf.Lerp ( 1f, 0.02f, proportionExtremeness );
        float w = Mathf.Pow ( dist, power );
        return 0.5f + Mathf.Sign ( u - 0.5f ) * ( w * 0.5f );
    }

    // 3軸を昇順に並べたベクトル（分布の比較用）
    static Vector3 SortedProportion ( Vector3 p ) {
        float a = p.x, b = p.y, c = p.z;
        if ( a > b ) { var t = a; a = b; b = t; }
        if ( b > c ) { var t = b; b = c; c = t; }
        if ( a > b ) { var t = a; a = b; b = t; }
        return new Vector3 ( a, b, c );
    }

    // ソート後の L2 距離。0 に近いほど類似。
    static float ProportionDistributionDistance ( Vector3 a, Vector3 b ) {
        Vector3 sa = SortedProportion ( a ), sb = SortedProportion ( b );
        return Vector3.Distance ( sa, sb );
    }

    // Global Volumeのプロファイルを複製し、Chromatic Aberration参照を取得（アセットを直接いじらないため）
    void SetupChromaticAberration () {
        if ( globalVolume == null || globalVolume.profile == null ) return;
        globalVolume.profile = Instantiate ( globalVolume.profile );
        if ( globalVolume.profile.TryGet ( out chromaticAberration ) ) {
            chromaticAberration.active = true;
            chromaticAberration.intensity.overrideState = true;
            chromaticAberration.intensity.Override ( 0f );
        }
    }

}
