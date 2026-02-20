using System.Collections.Generic;
using UnityEngine;

//
// メインクラス。MultiWaveData から GetWaveData で波形を取得し、LineRenderer で描画し続ける。
// 1) MultiWaveData から波形を取得 → 2) 時間軸を X、振幅を Y に変換 → 3) LineRenderer.SetPositions で描画
// インスペクタで DataSource に MultiWaveData を指定する。
//
public sealed class WaveDrawer : MonoBehaviour {


    [Header ( "波形データの取得するMultiWaveData。必須）" )]
    [SerializeField] MultiWaveData dataSource = null;

    [Header ( "波形データの取得内容（何秒分の波形データを、何点でサンプリングするか）" )]
    [SerializeField, Range ( 0.0001f, 5f )] float sampleSeconds = 0.01f;
    [SerializeField, Min ( 32 )] int drawResolution = 960;

    [Header ( "波形図の表示（幅・高さ・線幅・振幅の倍率。ワールド単位）" )]
    [SerializeField, Min ( 0.01f )] float displayWidth = 1920f;
    [SerializeField, Min ( 0.01f )] float displayHeight = 1080f;
    [SerializeField, Min ( 0.001f )] float lineWidth = 2f;

    [SerializeField, Min ( 0.001f )] float amplitude = 4f;

    [SerializeField] Material lineMaterial = null;

    [Header ( "描画するチャンネル（どのチャンネルを表示するか。インデックスがチャンネル番号に対応）" )]
    [SerializeField] bool [ ] drawChannel = new bool [ ] { true, true, false, false, false, false, false, false };

    [Header ( "色（チャンネルごと）" )]
    [SerializeField]
    Color [ ] channelColors = new Color [ ]
    {
        new Color(1f, 0.2f, 0.2f),
        new Color(0.2f, 0.8f, 0.2f),
        new Color(0.2f, 0.4f, 1f),
        new Color(1f, 0.9f, 0.2f),
        new Color(0.2f, 0.9f, 0.9f),
        new Color(1f, 0.4f, 0.9f),
        Color.white,
        new Color(0.6f, 0.6f, 0.6f)
    };



    List<LineRenderer> lineRenderers;
    Vector3 [ ] positionBuffer;


    // Start で一度だけ呼ぶ。チャンネル数分の LineRenderer を作成する（MultiWaveData は Awake で初期化済みの想定）
    void CreateLineRenderers () {
        if ( dataSource == null ) {
            Debug.LogError ( "[WaveDrawer] 必須: Data Source に MultiWaveData を指定してください。", this );
            return;
        }
        if ( dataSource.ChannelCount <= 0 ) {
            Debug.LogError ( "[WaveDrawer] MultiWaveData にチャンネルがありません。入力デバイスが選択されているか確認してください。", this );
            return;
        }
        if ( lineMaterial == null ) {
            Debug.LogError ( "[WaveDrawer] 必須: Line Material を指定してください。", this );
            return;
        }
        if ( lineRenderers != null && lineRenderers.Count > 0 )
            return;

        int channelCount = dataSource.ChannelCount;
        lineRenderers = new List<LineRenderer> ();
        for ( int ch = 0; ch < channelCount; ch++ ) {
            GameObject go = new GameObject ( $"Line_Ch{ch}" );
            go.transform.SetParent ( transform, false );
            LineRenderer lr = go.AddComponent<LineRenderer> ();
            lr.positionCount = 0;
            lr.useWorldSpace = false;
            lr.loop = false;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            if ( lineMaterial != null )
                lr.material = lineMaterial;
            lr.startColor = lr.endColor = ch < channelColors.Length ? channelColors [ ch ] : Color.white;
            lineRenderers.Add ( lr );
        }
    }

    void Start () {
        CreateLineRenderers ();
    }

    // dataSource から波形を取得し、各チャンネルの LineRenderer に描画する
    void LateUpdate () {
        if ( lineRenderers == null || lineRenderers.Count == 0 ) return;

        int channelCount = dataSource.ChannelCount;
        float halfH = displayHeight * 0.5f;

        // 1. 表示用バッファの確保（解像度分。1本分の頂点をチャンネル共通で使い回し）
        if ( positionBuffer == null || positionBuffer.Length < drawResolution )
            positionBuffer = new Vector3 [ drawResolution ];

        // 2. 各チャンネル: DrawChannel に応じて表示 ON/OFF → データ取得 → 座標計算 → SetPositions
        for ( int ch = 0; ch < channelCount && ch < lineRenderers.Count; ch++ ) {
            LineRenderer lr = lineRenderers [ ch ];
            bool show = ( drawChannel != null && ch < drawChannel.Length ) ? drawChannel [ ch ] : true;
            lr.enabled = show;
            if ( !lr.enabled ) continue;

            float [ ] data = dataSource.GetWaveData ( ch, sampleSeconds, drawResolution );
            if ( data == null || data.Length == 0 ) continue;

            int effectiveRes = data.Length;
            for ( int i = 0; i < effectiveRes; i++ ) {
                // X: 左端 -0.5*幅 ～ 右端 +0.5*幅 / Y: 振幅を表示高さの半分でスケール
                float x = ( effectiveRes > 1 ) ? ( ( float )i / ( effectiveRes - 1 ) - 0.5f ) * displayWidth : 0f;
                float y = data [ i ] * halfH * amplitude;
                positionBuffer [ i ] = new Vector3 ( x, y, 0f );
            }

            lr.positionCount = effectiveRes;
            lr.SetPositions ( positionBuffer );
        }
    }

    // 作成した LineRenderer 用 GameObject を破棄する
    void OnDestroy () {
        if ( lineRenderers != null ) {
            foreach ( LineRenderer lr in lineRenderers ) {
                if ( lr != null )
                    Destroy ( lr.gameObject );
            }
            lineRenderers.Clear ();
        }
    }

}
