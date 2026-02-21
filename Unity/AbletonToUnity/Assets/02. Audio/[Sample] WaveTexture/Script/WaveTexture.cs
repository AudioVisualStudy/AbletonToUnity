using System.Collections.Generic;
using UnityEngine;

// ----------------------------------------------------------------------------
// チャンネルごとの設定（インスペクタで「表示するか」「しきい値」を指定）
// ----------------------------------------------------------------------------
[System.Serializable]
public struct WaveTextureChannelSetting {
    [Tooltip ( "このチャンネルを表示する" )]
    public bool draw;
    [Tooltip ( "このチャンネルのレベルしきい値（この値以上で白/黒、未満でデフォルト色）" ), Min ( 0f )]
    public float levelThreshold;
}

// ----------------------------------------------------------------------------
// WaveTexture：オーディオ波形を白黒ストライプでビジュアライズするコンポーネント
// ----------------------------------------------------------------------------
// 【概要】
//   MultiWaveData から波形データを取得し、振幅の正負に応じて白/黒の縦ストライプとして描画します。
//   表示するチャンネルは「チャンネル」配列の「表示」チェックで選び、上から縦に帯状に並びます。
// 【描画のタイミング】
//   MultiWaveData は Update で波形を書き込むため、LateUpdate で読むと「同じフレームのデータ」を描画できます。
//   DefaultExecutionOrder(100) で、他のスクリプトより後に実行されるようにしています。
// ----------------------------------------------------------------------------
[DefaultExecutionOrder ( 100 )]
public sealed class WaveTexture : MonoBehaviour {
    // ----- インスペクタで設定する項目 -----

    [Header ( "波形データの取得元（MultiWaveData）。必須）" )]
    [SerializeField] MultiWaveData dataSource = null;

    [Header ( "波形データの取得内容（何秒分を、何本のストライプでサンプリングするか）" )]
    [SerializeField, Range ( 0.0001f, 5f )] float sampleSeconds = 0.01f;
    [SerializeField, Min ( 32 )] int drawResolution = 960;

    [Header ( "描画領域（ローカル座標。ストライプパターン全体がこの矩形内に描画される）" )]
    [SerializeField] Rect drawRect = new Rect ( -960f, -540f, 1920f, 1080f );

    [Header ( "描画用マテリアル（頂点色を表示する Unlit など）" )]
    [SerializeField] Material stripeMaterial = null;

    [Header ( "ストライプの色分け（レベル未満のときの色）" )]
    [SerializeField] Color defaultColor = Color.gray;

    [Header ( "チャンネル（最大8。表示にチェックを入れたチャンネルを上から縦に積んで表示。各チャンネルのレベルしきい値を指定可能）" )]
    [SerializeField] WaveTextureChannelSetting [ ] channels = new WaveTextureChannelSetting [ 8 ];

    [Header ( "オプション：描画エリアを上下に2分割（エリアA=上・エリアB=下、Bは白黒反転）" )]
    [SerializeField] bool splitAreaAB = false;

    [Header ( "デバッグ：メッシュ上に波形グラフを表示（各チャンネルの帯の中心に折れ線を表示）" )]
    [SerializeField] bool showWaveformOverlay = false;
    [SerializeField] Material waveformLineMaterial = null;
    [SerializeField, Min ( 0.001f )] float waveformLineWidth = 2f;
    [SerializeField, Min ( 0.001f )] float waveformAmplitudeScale = 1f;

    // 子オブジェクト名（ストライプ用メッシュと波形オーバーレイ用）
    const string MeshChildName = "StripesMesh";
    const string OverlayChildName = "WaveformOverlay";
    const int MaxWaveformOverlayChannels = 8;

    // ----- 内部で使う変数（スクリプトから参照するためメンバーとして保持） -----

    Mesh mesh;
    Transform meshChild;
    MeshFilter meshFilter;
    MeshRenderer meshRenderer;
    List<LineRenderer> waveformLines;
    Vector3 [ ] waveformPositions;   // 波形オーバーレイの頂点バッファ（使い回し）
    Vector3 [ ] vertices;            // ストライプメッシュの頂点
    Color [ ] colors;                // ストライプメッシュの頂点色
    int [ ] triangles;               // ストライプメッシュの三角形インデックス
    List<int> triangleList;        // メッシュに渡す用のリスト（SetTriangles で使用）
    int allocatedStripCount;       // いま何本分のストライプ用にバッファを確保しているか
    List<int> enabledChannels;    // 「表示」にチェックが入っているチャンネル番号のリスト

    // ----------------------------------------------------------------------------
    // ストライプ描画用のメッシュと子オブジェクトを用意する
    // ----------------------------------------------------------------------------
    void EnsureMeshAndComponents () {
        if ( mesh != null ) return;

        mesh = new Mesh ();
        mesh.name = "WaveTextureStripes";

        // メッシュは子オブジェクトで描画するため、親に MeshFilter/MeshRenderer が残っていれば削除する
        var parentFilter = GetComponent<MeshFilter> ();
        var parentRenderer = GetComponent<MeshRenderer> ();
        if ( parentFilter != null ) Destroy ( parentFilter );
        if ( parentRenderer != null ) Destroy ( parentRenderer );

        // 子オブジェクト「StripesMesh」を探す or 作成
        Transform t = transform.Find ( MeshChildName );
        if ( t == null ) {
            GameObject child = new GameObject ( MeshChildName );
            child.transform.SetParent ( transform, false );
            child.transform.localPosition = Vector3.zero;
            child.transform.localRotation = Quaternion.identity;
            child.transform.localScale = Vector3.one;
            t = child.transform;
        }
        meshChild = t;

        meshFilter = meshChild.GetComponent<MeshFilter> ();
        if ( meshFilter == null )
            meshFilter = meshChild.gameObject.AddComponent<MeshFilter> ();
        meshFilter.sharedMesh = mesh;

        meshRenderer = meshChild.GetComponent<MeshRenderer> ();
        if ( meshRenderer == null )
            meshRenderer = meshChild.gameObject.AddComponent<MeshRenderer> ();
        if ( stripeMaterial != null )
            meshRenderer.sharedMaterial = stripeMaterial;
    }

    // ----------------------------------------------------------------------------
    // ストライプ本数に合わせて頂点・色・三角形のバッファを確保する
    // totalStrips : 描画するストライプの総本数（チャンネル数 × 1チャンネルあたりの本数）
    // ----------------------------------------------------------------------------
    void AllocateBuffers ( int totalStrips ) {
        if ( totalStrips <= 0 ) totalStrips = 1;
        if ( allocatedStripCount >= totalStrips ) return;

        allocatedStripCount = totalStrips;
        int vertexCount = totalStrips * 4;   // ストライプ1本 = 四角形 = 頂点4個
        int triangleCount = totalStrips * 6;  // 四角形1枚 = 三角形2つ = インデックス6個

        vertices = new Vector3 [ vertexCount ];
        colors = new Color [ vertexCount ];
        triangles = new int [ triangleCount ];
        triangleList = new List<int> ( triangleCount );

        // 表がカメラ側（-Z）を向くよう、-Z から見て反時計回りになる頂点順で三角形を定義
        for ( int i = 0; i < totalStrips; i++ ) {
            int v = i * 4;
            int ti = i * 6;
            triangles [ ti + 0 ] = v + 0;
            triangles [ ti + 1 ] = v + 3;
            triangles [ ti + 2 ] = v + 2;
            triangles [ ti + 3 ] = v + 0;
            triangles [ ti + 4 ] = v + 2;
            triangles [ ti + 5 ] = v + 1;
        }

        mesh.Clear ();
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = triangles;
    }

    // ----------------------------------------------------------------------------
    // 波形オーバーレイ用の LineRenderer を、チャンネル数ぶんだけ子オブジェクトとして用意する
    // ----------------------------------------------------------------------------
    void EnsureWaveformOverlay ( int channelCount ) {
        if ( !showWaveformOverlay || waveformLineMaterial == null ) return;
        int need = Mathf.Min ( channelCount, MaxWaveformOverlayChannels );
        if ( need <= 0 ) return;

        waveformLines ??= new List<LineRenderer> ();

        Transform parent = transform.Find ( OverlayChildName );
        if ( parent == null ) {
            GameObject go = new GameObject ( OverlayChildName );
            go.transform.SetParent ( transform, false );
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            parent = go.transform;
        }

        // 足りない分だけ LineRenderer を追加
        while ( waveformLines.Count < need ) {
            int idx = waveformLines.Count;
            GameObject go = new GameObject ( "Line_" + idx );
            go.transform.SetParent ( parent, false );
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            var lr = go.AddComponent<LineRenderer> ();
            lr.useWorldSpace = false;
            lr.loop = false;
            lr.startWidth = lr.endWidth = waveformLineWidth;
            lr.material = waveformLineMaterial;
            lr.startColor = lr.endColor = new Color ( 1f, 0.3f, 0.3f, 0.9f );
            lr.positionCount = 0;
            waveformLines.Add ( lr );
        }
    }

    void Start () {
        if ( dataSource == null ) {
            Debug.LogError ( "[WaveTexture] 必須: Data Source に MultiWaveData を指定してください。", this );
            return;
        }
        if ( dataSource.ChannelCount <= 0 ) {
            Debug.LogError ( "[WaveTexture] MultiWaveData にチャンネルがありません。", this );
            return;
        }
        if ( stripeMaterial == null ) {
            Debug.LogError ( "[WaveTexture] 必須: Stripe Material を指定してください。", this );
            return;
        }

        EnsureMeshAndComponents ();
        enabledChannels = new List<int> ();
        AllocateBuffers ( drawResolution * 8 );
    }

    // ----------------------------------------------------------------------------
    // 毎フレーム：波形データを取得し、ストライプメッシュとオーバーレイを更新する
    // MultiWaveData が Update でデータを書くので、LateUpdate で読むと同フレームのデータを描画できる
    // ----------------------------------------------------------------------------
    void LateUpdate () {
        if ( dataSource == null || mesh == null ) return;
        int channelCount = dataSource.ChannelCount;
        if ( channelCount <= 0 ) return;

        enabledChannels.Clear ();
        for ( int ch = 0; ch < channelCount && ch < channels.Length; ch++ )
            if ( channels [ ch ].draw ) enabledChannels.Add ( ch );
        int numEnabled = enabledChannels.Count;
        if ( numEnabled == 0 ) return;

        // 先頭チャンネルの波形を取得（本数 n の基準にも使う）
        float [ ] data0 = dataSource.GetWaveData ( enabledChannels [ 0 ], sampleSeconds, drawResolution );
        if ( data0 == null || data0.Length == 0 ) return;

        int n = data0.Length;
        int stripsPerChannel = splitAreaAB ? n * 2 : n;  // 上下分割時は1チャンネルあたり2倍のストライプ
        int requiredStrips = numEnabled * stripsPerChannel;
        if ( requiredStrips > allocatedStripCount )
            AllocateBuffers ( requiredStrips );

        // 描画領域をチャンネル数で縦に分割したときの1帯の高さ
        float xMin = drawRect.xMin;
        float xMax = drawRect.xMax;
        float fullYMin = drawRect.yMin;
        float bandHeight = drawRect.height / numEnabled;
        float inv = ( n > 1 ) ? 1f / ( n - 1 ) : 0f;

        int stripOffset = 0;
        for ( int k = 0; k < numEnabled; k++ ) {
            int ch = enabledChannels [ k ];
            float [ ] data = ( k == 0 ) ? data0 : dataSource.GetWaveData ( ch, sampleSeconds, drawResolution );
            if ( data == null || data.Length == 0 ) continue;

            // このチャンネルの帯のY範囲（上から k 番目。上ほどYが大きい）
            float bandYMin = fullYMin + ( numEnabled - 1 - k ) * bandHeight;
            float bandYMax = bandYMin + bandHeight;
            float bandYCenter = ( bandYMin + bandYMax ) * 0.5f;

            // このフレームのこのチャンネルのピーク振幅（レベル）
            float channelLevel = 0f;
            for ( int i = 0; i < n; i++ ) {
                float absA = Mathf.Abs ( data [ i ] );
                if ( absA > channelLevel ) channelLevel = absA;
            }

            float th = ch < channels.Length ? channels [ ch ].levelThreshold : 0.01f;

            // ストライプ1本ずつ：波形の各サンプルに対応する四角形の頂点・色を書き込む
            for ( int i = 0; i < n; i++ ) {
                float t0 = ( n > 1 ) ? i * inv : 0f;
                float t1 = ( n > 1 ) ? Mathf.Min ( ( i + 1 ) * inv, 1f ) : 1f;
                float x0 = Mathf.Lerp ( xMin, xMax, t0 );
                float x1 = Mathf.Lerp ( xMin, xMax, t1 );

                float a = data [ i ];
                Color cA, cB;
                if ( channelLevel >= th ) {
                    cA = a >= 0f ? Color.white : Color.black;
                    cB = a >= 0f ? Color.black : Color.white;
                } else {
                    cA = cB = defaultColor;
                }

                if ( splitAreaAB ) {
                    // エリアA（上）：bandYCenter ～ bandYMax
                    int vA = ( stripOffset + i ) * 4;
                    vertices [ vA + 0 ] = new Vector3 ( x0, bandYCenter, 0f );
                    vertices [ vA + 1 ] = new Vector3 ( x1, bandYCenter, 0f );
                    vertices [ vA + 2 ] = new Vector3 ( x1, bandYMax, 0f );
                    vertices [ vA + 3 ] = new Vector3 ( x0, bandYMax, 0f );
                    colors [ vA + 0 ] = colors [ vA + 1 ] = colors [ vA + 2 ] = colors [ vA + 3 ] = cA;
                    // エリアB（下）：bandYMin ～ bandYCenter
                    int vB = ( stripOffset + n + i ) * 4;
                    vertices [ vB + 0 ] = new Vector3 ( x0, bandYMin, 0f );
                    vertices [ vB + 1 ] = new Vector3 ( x1, bandYMin, 0f );
                    vertices [ vB + 2 ] = new Vector3 ( x1, bandYCenter, 0f );
                    vertices [ vB + 3 ] = new Vector3 ( x0, bandYCenter, 0f );
                    colors [ vB + 0 ] = colors [ vB + 1 ] = colors [ vB + 2 ] = colors [ vB + 3 ] = cB;
                } else {
                    int v = ( stripOffset + i ) * 4;
                    vertices [ v + 0 ] = new Vector3 ( x0, bandYMin, 0f );
                    vertices [ v + 1 ] = new Vector3 ( x1, bandYMin, 0f );
                    vertices [ v + 2 ] = new Vector3 ( x1, bandYMax, 0f );
                    vertices [ v + 3 ] = new Vector3 ( x0, bandYMax, 0f );
                    colors [ v + 0 ] = colors [ v + 1 ] = colors [ v + 2 ] = colors [ v + 3 ] = cA;
                }
            }
            stripOffset += stripsPerChannel;
        }

        // メッシュに頂点・色・三角形を渡して反映（確保済みサイズで渡す）
        int vertexCount = allocatedStripCount * 4;
        int triCount = requiredStrips * 6;
        mesh.SetVertices ( vertices, 0, vertexCount );
        mesh.SetColors ( colors, 0, vertexCount );
        triangleList.Clear ();
        for ( int i = 0; i < triCount; i++ )
            triangleList.Add ( triangles [ i ] );
        mesh.SetTriangles ( triangleList, 0 );
        mesh.RecalculateBounds ();

        if ( showWaveformOverlay && waveformLineMaterial != null && numEnabled > 0 ) {
            EnsureWaveformOverlay ( numEnabled );
            if ( waveformPositions == null || waveformPositions.Length < n )
                waveformPositions = new Vector3 [ Mathf.Max ( n, drawResolution ) ];
            float halfBandH = bandHeight * 0.5f * waveformAmplitudeScale;
            for ( int k = 0; k < numEnabled && k < waveformLines.Count; k++ ) {
                float bandYCenter = fullYMin + ( numEnabled - 1 - k ) * bandHeight + bandHeight * 0.5f;
                float [ ] data = ( k == 0 ) ? data0 : dataSource.GetWaveData ( enabledChannels [ k ], sampleSeconds, drawResolution );
                if ( data == null || data.Length == 0 ) { waveformLines [ k ].enabled = false; continue; }
                for ( int i = 0; i < n; i++ ) {
                    float tx = ( n > 1 ) ? i * inv : 0f;
                    float x = Mathf.Lerp ( drawRect.xMin, drawRect.xMax, tx );
                    float y = bandYCenter + data [ i ] * halfBandH;
                    waveformPositions [ i ] = new Vector3 ( x, y, -0.01f );
                }
                waveformLines [ k ].positionCount = n;
                waveformLines [ k ].SetPositions ( waveformPositions );
                waveformLines [ k ].enabled = true;
            }
            for ( int k = numEnabled; k < waveformLines.Count; k++ )
                waveformLines [ k ].enabled = false;
        } else if ( waveformLines != null ) {
            for ( int i = 0; i < waveformLines.Count; i++ )
                waveformLines [ i ].enabled = false;
        }
    }

    void OnDestroy () {
        if ( mesh != null ) {
            if ( Application.isPlaying ) Destroy ( mesh );
            else DestroyImmediate ( mesh );
        }
        var overlayRoot = transform.Find ( OverlayChildName );
        if ( overlayRoot != null ) Destroy ( overlayRoot.gameObject );
        waveformLines?.Clear ();
    }
}
