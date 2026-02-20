using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Collections;
using UnityEngine;

//
// 多チャンネル音声入力の波形を、LineRenderer で X 軸中心に重ねて同時表示する。
// デバイスはインスペクタのインデックスで指定し、チャンネルはチェックボックスで選択する。
// 時間スケール（遡る秒数）と描画解像度を指定可能。チャンネルごとに色分け。
//
// 音声入力～描画のレイテンシ:
// - Lasp の audioDataSlice は「直近1フレーム分」のデータ（WaveformRenderer コメント参照）のため、
//   取得時点で約1フレーム分（60fps で約17ms、30fps で約33ms）の遅れがある。
// - 加えてオーディオドライバ／Lasp 内部バッファ（例: 256～1024 サンプル = 48kHz で約5～21ms）が乗る。
// - 合計でおおよそ 20～50ms 程度の遅れが発生し得る。取得は LateUpdate で行い同一フレーム内では可能な限り遅いタイミングで読むようにしている。
//
public sealed class MultiWaveForm : MonoBehaviour {
    #region Inspector

    [Header ( "Device" )]
    [Tooltip ( "Input device index (0 = first device in Lasp.AudioSystem.InputDevices)" )]
    [SerializeField] int _deviceIndex = 0;

    [Header ( "Channels" )]
    [Tooltip ( "Which channels to display (up to 8). Only indices < device ChannelCount are used." )]
    [SerializeField] bool [ ] _channelEnabled = new bool [ ] { true, true, false, false, false, false, false, false };

    [Header ( "Time & Resolution" )]
    [Tooltip ( "How many seconds of waveform to show (from current time backwards). 0.0001～5 sec." )]
    [SerializeField, Range ( 0.0001f, 5f )] float _timeScaleSeconds = 1f;

    [Tooltip ( "Number of points per line (LineRenderer position count)" )]
    [SerializeField, Min ( 32 )] int _resolution = 512;

    [Header ( "Display" )]
    [Tooltip ( "Overall width of the waveform diagram (world units). X axis extent. Default 1920." )]
    [SerializeField, Min ( 0.01f )] float _displayWidth = 1920f;

    [Tooltip ( "Overall height of the waveform diagram (world units). Y axis extent for full-scale signal. Default 1080." )]
    [SerializeField, Min ( 0.01f )] float _displayHeight = 1080f;

    [Tooltip ( "Amplitude multiplier. How many times to scale the current waveform (1 = default)." )]
    [SerializeField, Min ( 0.001f )] float _amplitude = 1f;

    [Tooltip ( "Line width for waveform (world units)" )]
    [SerializeField, Min ( 0.001f )] float _lineWidth = 2f;

    [Tooltip ( "Material used for all LineRenderers (e.g. Additive Line). If null, Sprites/Default is used." )]
    [SerializeField] Material _lineMaterial = null;

    [Header ( "Colors (per channel)" )]
    [SerializeField]
    Color [ ] _channelColors = new Color [ ]
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

    #endregion

    #region Constants

    const int MaxChannels = 8;

    #endregion

    #region Runtime state

    List<Lasp.AudioLevelTracker> _trackers;
    List<float [ ]> _ringBuffers;
    List<int> _writeIndices;
    List<LineRenderer> _lineRenderers;
    int _sampleRate;
    int _bufferLength;
    int _channelCount;
    string _deviceId;
    int _lastDeviceIndex = -1;
    float _lastTimeScale = -1f;
    int _lastResolution = -1;
    Vector3 [ ] _positionBuffer;

    #endregion

    #region MonoBehaviour

    void Start () {
        EnsureChannelArrays ();
        RebuildDeviceAndBuffers ();
    }

    void Update () {
        // デバイス・時間スケール・解像度の変更は Update で検知（LateUpdate より先に実行される）
        if ( !Lasp.AudioSystem.InputDevices.Any () )
            return;

        EnsureChannelArrays ();

        if ( _lastDeviceIndex != _deviceIndex || _lastTimeScale != _timeScaleSeconds || _lastResolution != _resolution )
            RebuildDeviceAndBuffers ();
    }

    // 同一フレーム内で可能な限り遅いタイミングで取得・描画し、見かけのレイテンシを少しでも抑える
    void LateUpdate () {
        if ( !Lasp.AudioSystem.InputDevices.Any () || _trackers == null || _trackers.Count == 0 )
            return;

        // 各チャンネルの audioDataSlice をリングバッファに追加
        for ( var ch = 0; ch < _trackers.Count; ch++ ) {
            if ( _trackers [ ch ] == null || _ringBuffers [ ch ] == null )
                continue;

            var slice = _trackers [ ch ].audioDataSlice;
            var buf = _ringBuffers [ ch ];
            var wi = _writeIndices [ ch ];
            for ( var i = 0; i < slice.Length; i++ ) {
                buf [ wi ] = slice [ i ];
                wi = ( wi + 1 ) % _bufferLength;
            }
            _writeIndices [ ch ] = wi;
        }

        // リングバッファから解像度分サンプリングして LineRenderer を更新
        UpdateLineRenderers ();
    }

    void OnDestroy () {
        if ( _trackers != null ) {
            foreach ( var t in _trackers ) {
                if ( t != null && t.gameObject != null )
                    Destroy ( t.gameObject );
            }
            _trackers.Clear ();
        }

        if ( _lineRenderers != null ) {
            foreach ( var lr in _lineRenderers ) {
                if ( lr != null && lr.gameObject != null && lr.gameObject != gameObject )
                    Destroy ( lr.gameObject );
            }
            _lineRenderers.Clear ();
        }
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Lasp AudioLevelTracker を生波形取得用に設定する。
    /// Filter=Bypass, AutoGain=off, SmoothFall=off にし、audioDataSlice ができるだけ加工されないようにする。
    /// </summary>
    static void ApplyRawWaveformSettings ( Lasp.AudioLevelTracker tracker ) {
        if ( tracker == null ) return;
        var t = tracker.GetType ();
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

        var filterType = t.GetField ( "_filterType", flags );
        if ( filterType != null ) filterType.SetValue ( tracker, 0 ); // 0 = Bypass

        var autoGain = t.GetField ( "_autoGain", flags );
        if ( autoGain != null ) autoGain.SetValue ( tracker, false );

        var smoothFall = t.GetField ( "_smoothFall", flags );
        if ( smoothFall != null ) smoothFall.SetValue ( tracker, false );
    }

    void EnsureChannelArrays () {
        if ( _channelEnabled == null || _channelEnabled.Length < MaxChannels ) {
            var next = new bool [ MaxChannels ];
            for ( var i = 0; i < MaxChannels; i++ )
                next [ i ] = _channelEnabled != null && i < _channelEnabled.Length && _channelEnabled [ i ];
            _channelEnabled = next;
        }

        if ( _channelColors == null || _channelColors.Length < MaxChannels ) {
            var defaults = new Color [ ]
            {
                new Color(1f, 0.2f, 0.2f), new Color(0.2f, 0.8f, 0.2f),
                new Color(0.2f, 0.4f, 1f), new Color(1f, 0.9f, 0.2f),
                new Color(0.2f, 0.9f, 0.9f), new Color(1f, 0.4f, 0.9f),
                Color.white, new Color(0.6f, 0.6f, 0.6f)
            };
            var next = new Color [ MaxChannels ];
            for ( var i = 0; i < MaxChannels; i++ )
                next [ i ] = _channelColors != null && i < _channelColors.Length ? _channelColors [ i ] : defaults [ i ];
            _channelColors = next;
        }
    }

    void RebuildDeviceAndBuffers () {
        var devices = Lasp.AudioSystem.InputDevices.ToList ();
        if ( devices.Count == 0 ) {
            _lastDeviceIndex = _deviceIndex;
            _lastTimeScale = _timeScaleSeconds;
            _lastResolution = _resolution;
            return;
        }

        var safeIndex = Mathf.Clamp ( _deviceIndex, 0, devices.Count - 1 );
        var dev = devices [ safeIndex ];
        _deviceId = dev.ID;
        _channelCount = Mathf.Min ( dev.ChannelCount, MaxChannels );
        _sampleRate = dev.SampleRate > 0 ? dev.SampleRate : 48000;
        _bufferLength = Mathf.Max ( 4, Mathf.CeilToInt ( _timeScaleSeconds * _sampleRate ) );
        _lastDeviceIndex = _deviceIndex;
        _lastTimeScale = _timeScaleSeconds;
        _lastResolution = _resolution;

        // 既存のトラッカーを破棄
        if ( _trackers != null ) {
            foreach ( var t in _trackers ) {
                if ( t != null && t.gameObject != null )
                    Destroy ( t.gameObject );
            }
        }

        _trackers = new List<Lasp.AudioLevelTracker> ();
        _ringBuffers = new List<float [ ]> ();
        _writeIndices = new List<int> ();

        for ( var ch = 0; ch < _channelCount; ch++ ) {
            var go = new GameObject ( $"WaveformTracker_Ch{ch}" );
            go.transform.SetParent ( transform, false );
            var tracker = go.AddComponent<Lasp.AudioLevelTracker> ();
            tracker.deviceID = _deviceId;
            tracker.channel = ch;
            ApplyRawWaveformSettings ( tracker );
            _trackers.Add ( tracker );
            _ringBuffers.Add ( new float [ _bufferLength ] );
            _writeIndices.Add ( 0 );
        }

        // LineRenderer をチャンネルごとに作成（有効なチャンネルのみ表示）
        if ( _lineRenderers != null ) {
            foreach ( var lr in _lineRenderers ) {
                if ( lr != null && lr.gameObject != null && lr.gameObject != gameObject )
                    Destroy ( lr.gameObject );
            }
        }

        _lineRenderers = new List<LineRenderer> ();
        for ( var ch = 0; ch < _channelCount; ch++ ) {
            var go = new GameObject ( $"Line_Ch{ch}" );
            go.transform.SetParent ( transform, false );
            var lr = go.AddComponent<LineRenderer> ();
            lr.positionCount = _resolution;
            lr.useWorldSpace = false;
            lr.loop = false;
            lr.startWidth = _lineWidth;
            lr.endWidth = _lineWidth;
            if ( _lineMaterial != null )
                lr.material = _lineMaterial;
            else {
                var shader = Shader.Find ( "Sprites/Default" );
                if ( shader != null )
                    lr.material = new Material ( shader );
            }
            lr.startColor = lr.endColor = _channelColors [ ch ];
            _lineRenderers.Add ( lr );
        }

        _positionBuffer = new Vector3 [ _resolution ];
    }

    void UpdateLineRenderers () {
        if ( _ringBuffers == null || _lineRenderers == null || _positionBuffer == null )
            return;

        var res = Mathf.Clamp ( _resolution, 32, 8192 );
        // 表示点数をバッファ長以下に制限する。バッファをラップして同じサンプルを繰り返すと
        // ラップ境界で不連続（縦線のジャンプ）が発生するため、1周期分だけ描画する。
        var effectiveRes = Mathf.Min ( res, _bufferLength );
        if ( effectiveRes <= 0 ) return;
        var stepBack = effectiveRes > 1 ? ( _bufferLength - 1 ) / ( effectiveRes - 1 ) : 0;

        for ( var ch = 0; ch < _lineRenderers.Count && ch < _ringBuffers.Count; ch++ ) {
            var enabled = ch < _channelEnabled.Length && _channelEnabled [ ch ];
            var lr = _lineRenderers [ ch ];
            if ( lr == null ) continue;

            lr.enabled = enabled;
            if ( !enabled ) continue;

            var buf = _ringBuffers [ ch ];
            var wi = _writeIndices [ ch ];

            // 過去から現在へ。バッファをラップせず連続した範囲だけ参照する
            var halfH = _displayHeight * 0.5f;
            for ( var i = 0; i < effectiveRes; i++ ) {
                var rawIndex = wi - 1 - ( effectiveRes - 1 - i ) * stepBack;
                var sampleIndex = ( ( rawIndex % _bufferLength ) + _bufferLength ) % _bufferLength;
                var x = ( effectiveRes > 1 ) ? ( ( float )i / ( effectiveRes - 1 ) - 0.5f ) * _displayWidth : 0f;
                var y = buf [ sampleIndex ] * halfH * _amplitude;
                _positionBuffer [ i ] = new Vector3 ( x, y, 0f );
            }

            lr.startWidth = lr.endWidth = _lineWidth;
            lr.positionCount = effectiveRes;
            lr.SetPositions ( _positionBuffer );

            if ( ch < _channelColors.Length ) {
                lr.startColor = lr.endColor = _channelColors [ ch ];
            }
        }
    }

    #endregion
}
