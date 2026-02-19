using UnityEngine;
using yugop.connection;

public class CubeVisualizer : MonoBehaviour {

    [Header ( "キューブのプレハブと生成数。群の中心は原点" )]
    public GameObject cubePrefab;
    public int cubeCount = 5;

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

    // --- カメラ振動（CCの量に比例・指数カーブ） ---
    [Header ( "カメラ：元位置からの振動の最大半径（CC=127でこの量）" )]
    public float cameraShakeMaxRadius = 0.5f;

    [Header ( "カメラ：変化の指数。1=線形、>1で小さい値は繊細・大きい値はよりダイナミック" )]
    [Range ( 0.5f, 3f )]
    public float cameraShakeExponent = 2f;

    [Header ( "カメラ：位置揺れのXYZ係数。1=等倍、2でその軸だけ2倍動く" )]
    public Vector3 cameraShakePositionScale = Vector3.one;

    [Header ( "カメラ：Z軸まわり回転の揺れ幅（度）。CC=127でこの角度まで" )]
    public float cameraShakeMaxRollDegrees = 5f;

    [Header ( "カメラ：基準位置" )]
    public Vector3 cameraBasePosition = new Vector3 ( 0f, 0f, -4f );

    // 回転・位置用
    Transform [ ] cubes;
    Vector3 [ ] angularVelocities;
    Vector3 [ ] cubeCenters;
    Vector3 [ ] positionVelocities;
    // プロポーション用
    Vector3 targetProportion = Vector3.one;
    // カメラ振動：CCの正規化値（0～1）。未受信時は0
    float cameraShakeIntensity;
    Quaternion cameraBaseRotation;
    MidiHub midiHub;

    void Start () {
        Application.targetFrameRate = 60;

        midiHub = MidiHub.Instance; //MidiHubインスタンスへの参照
        midiHub.startConnection (); //MIDIポートに接続

        midiHub.AddNoteOnListener ( onDrum, 1 );    //CH1（ドラム）にリスナー関数を追加
        midiHub.AddNoteOnListener ( onBass, 2 ); //CH2（シンセベース）にリスナー関数を追加
        midiHub.AddControlChangeListener ( onControlChange ); //コントロールチェンジにリスナー関数を追加

        if ( Camera.main != null )
            cameraBaseRotation = Camera.main.transform.rotation;

        generateCubes ();//キューブを生成し、パラメータを初期化

    }

    //キューブを生成し、パラメータを初期化
    void generateCubes () {
        if ( cubePrefab != null && cubeCount > 0 ) {
            cubes = new Transform [ cubeCount ];
            angularVelocities = new Vector3 [ cubeCount ];
            cubeCenters = new Vector3 [ cubeCount ];
            positionVelocities = new Vector3 [ cubeCount ];
            targetProportion = cubePrefab.transform.localScale;
            for ( int i = 0; i < cubeCount; i++ ) {
                GameObject go = Instantiate ( cubePrefab, Vector3.zero, Quaternion.identity, transform );
                go.name = cubePrefab.name + "_" + i;
                cubes [ i ] = go.transform;
                cubeCenters [ i ] = Random.insideUnitSphere * centerOffsetRadius;
                cubes [ i ].localPosition = cubeCenters [ i ];
            }
        }
    }

    //ドラムが鳴ったとき
    void onDrum ( MidiNote note ) {
        switch ( note.Number ) {
            case 36: //キック
                ApplyAcceleration ( kickStrength );
                break;
            case 46: //ハイハットOPEN
                ChangeProportion ();
                break;
        }
    }

    //シンセベースが鳴ったとき
    void onBass ( MidiNote note ) {
    
    }

    // MIDIコントロールチェンジ時。指数カーブで小さい値は繊細に、大きい値はダイナミックに。
    void onControlChange ( MidiControlChange controlChange ) {
        float normalized = controlChange.GetNormalizedValue ();
        cameraShakeIntensity = Mathf.Pow ( normalized, cameraShakeExponent );
    }




    // キックで角速度・並進を加え、中心点をランダム更新。Update で減衰。
    public void ApplyAcceleration ( float strength ) {
        if ( angularVelocities == null ) return;
        for ( int i = 0; i < angularVelocities.Length; i++ ) {
            Vector3 randomDir = Random.onUnitSphere;
            angularVelocities [ i ] += randomDir * strength;
            if ( positionVelocities != null && i < positionVelocities.Length )
                positionVelocities [ i ] += Random.onUnitSphere * positionKickStrength;
            if ( cubeCenters != null && i < cubeCenters.Length )
                cubeCenters [ i ] = Random.insideUnitSphere * centerOffsetRadius;
        }
    }

    // 目標プロポーションを更新。前回と十分違う分布になるまで再抽選。
    public void ChangeProportion () {
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




    //毎フレームの処理
    void Update () {
        if ( cubes == null || angularVelocities == null ) return;
        float dt = Time.deltaTime;

        for ( int i = 0; i < cubes.Length; i++ ) {
            if ( cubes [ i ] == null ) continue;
            Transform t = cubes [ i ];

            // 回転・位置運動
            t.Rotate ( angularVelocities [ i ] * dt, Space.World );
            angularVelocities [ i ] *= velocityDecay;

            if ( cubeCenters != null && positionVelocities != null && i < cubeCenters.Length ) {
                Vector3 toCenter = cubeCenters [ i ] - t.localPosition;
                positionVelocities [ i ] += toCenter * ( positionAttractionStrength * dt );
                t.localPosition += positionVelocities [ i ] * dt;
                positionVelocities [ i ] *= velocityDecay;
            }

            // プロポーション変化
            t.localScale = Vector3.Lerp ( t.localScale, targetProportion, proportionSmoothSpeed );
        }

        // カメラ：CCの量に比例して位置とZ軸回転を揺らす
        if ( Camera.main != null ) {
            float radius = cameraShakeIntensity * cameraShakeMaxRadius;
            Vector3 offset = Random.insideUnitSphere * radius;
            offset.x *= cameraShakePositionScale.x;
            offset.y *= cameraShakePositionScale.y;
            offset.z *= cameraShakePositionScale.z;
            float rollDeg = Random.Range ( -1f, 1f ) * cameraShakeMaxRollDegrees * cameraShakeIntensity;
            Camera.main.transform.position = cameraBasePosition + offset;
            Camera.main.transform.rotation = cameraBaseRotation * Quaternion.Euler ( 0f, 0f, rollDeg );
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



}
