using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Collections;
using UnityEngine;

//
// デバイス・チャンネルを指定し、現在のリアルタイム波形データをリングバッファに蓄積する。
// 公開 API: GetWaveData(channel, seconds, resolution) で float[] を取得できる。
// 学生には比較的ブラックボックスとして扱い、描画は WaveDrawer に任せる想定。
//
// 音声入力のレイテンシ:
// - Lasp の audioDataSlice は「直近1フレーム分」のデータのため、
//   取得時点で約1フレーム分の遅れがある。
// - 取得は Update で行う（Lasp は EarlyUpdate でバッファ更新のため、Update で読めばそのフレームの最新が取れる）。
//
public sealed class MultiWaveData : MonoBehaviour {
    #region Inspector

    [Header ( "デバイス（入力デバイス番号。0 = Lasp の入力デバイス一覧の先頭）" )]
    [SerializeField] int deviceIndex = 0;

    [Header ( "バッファ（最大で保持する秒数。WaveDrawer の表示秒数はこの範囲内）" )]
    [SerializeField, Range ( 0.1f, 10f )] float maxBufferSeconds = 5f;

    #endregion

    #region Constants

    const int MaxChannels = 8;

    #endregion

    #region Runtime state

    List<Lasp.AudioLevelTracker> trackers;
    List<float [ ]> ringBuffers;
    List<int> writeIndices;
    int sampleRate;
    int bufferLength;
    int channelCount;
    string deviceId;
    int lastDeviceIndex = -1;
    float lastMaxBufferSeconds = -1f;

    #endregion

    #region Public API

    // 有効チャンネル数（デバイスと MaxChannels の小さい方）
    public int ChannelCount => channelCount;

    // 指定チャンネルの直近 seconds 秒分を resolution 点にサンプリングした波形（過去→現在の振幅値）を返す。取得できない場合は null
    public float [ ] GetWaveData ( int channel, float seconds, int resolution ) {
        if ( ringBuffers == null || channel < 0 || channel >= ringBuffers.Count )
            return null;

        float [ ] buf = ringBuffers [ channel ];
        int wi = writeIndices [ channel ];
        if ( buf == null || buf.Length == 0 )
            return null;

        int sampleCount = Mathf.Max ( 1, Mathf.Min ( Mathf.CeilToInt ( seconds * sampleRate ), bufferLength ) );
        int res = Mathf.Clamp ( resolution, 32, 8192 );
        int effectiveRes = Mathf.Min ( res, sampleCount );
        if ( effectiveRes <= 0 )
            return null;

        float stepBack = effectiveRes > 1 ? ( sampleCount - 1 ) / ( float )( effectiveRes - 1 ) : 0f;
        float [ ] result = new float [ effectiveRes ];

        for ( int i = 0; i < effectiveRes; i++ ) {
            float rawIndex = wi - 1 - ( effectiveRes - 1 - i ) * stepBack;
            int sampleIndex = ( ( Mathf.RoundToInt ( rawIndex ) % bufferLength ) + bufferLength ) % bufferLength;
            result [ i ] = buf [ sampleIndex ];
        }

        return result;
    }

    #endregion

    #region MonoBehaviour

    // デバイスとリングバッファを構築する（Awake で行い、WaveDrawer の Start より先に ChannelCount を用意する）
    void Awake () {
        RebuildDeviceAndBuffers ();
    }

    // デバイス番号やバッファ秒数が変わったら再構築。Lasp は EarlyUpdate で更新済みなので、ここで audioDataSlice を読んでリングバッファに追記する。
    void Update () {
        if ( !Lasp.AudioSystem.InputDevices.Any () )
            return;
        if ( lastDeviceIndex != deviceIndex || lastMaxBufferSeconds != maxBufferSeconds )
            RebuildDeviceAndBuffers ();

        if ( trackers == null || trackers.Count == 0 )
            return;

        for ( int ch = 0; ch < trackers.Count; ch++ ) {
            if ( trackers [ ch ] == null || ringBuffers [ ch ] == null )
                continue;

            NativeSlice<float> slice = trackers [ ch ].audioDataSlice;
            float [ ] buf = ringBuffers [ ch ];
            int wi = writeIndices [ ch ];
            for ( int i = 0; i < slice.Length; i++ ) {
                buf [ wi ] = slice [ i ];
                wi = ( wi + 1 ) % bufferLength;
            }
            writeIndices [ ch ] = wi;
        }
    }

    // 作成したトラッカー用 GameObject を破棄する
    void OnDestroy () {
        if ( trackers != null ) {
            foreach ( Lasp.AudioLevelTracker t in trackers ) {
                if ( t != null && t.gameObject != null )
                    Destroy ( t.gameObject );
            }
            trackers.Clear ();
        }
    }

    #endregion

    #region Helpers

    // InputDevices 要素の表示名を取得する（Name があればそれ、なければ ID）。dynamic を避けリフレクションで取得
    static string GetDeviceDisplayName ( object dev ) {
        if ( dev == null ) return "(null)";
        System.Type t = dev.GetType ();
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
        var nameProp = t.GetProperty ( "Name", flags );
        var idProp = t.GetProperty ( "ID", flags );
        object nameVal = nameProp?.GetValue ( dev );
        object idVal = idProp?.GetValue ( dev );
        string s = ( nameVal ?? idVal )?.ToString ();
        return string.IsNullOrEmpty ( s ) ? dev.ToString () : s;
    }

    // Lasp のトラッカーを生波形取得用に設定する（リフレクションで Bypass / AutoGain オフなど）
    static void ApplyRawWaveformSettings ( Lasp.AudioLevelTracker tracker ) {
        if ( tracker == null ) return;
        System.Type t = tracker.GetType ();
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;

        FieldInfo filterType = t.GetField ( "_filterType", flags );
        if ( filterType != null ) filterType.SetValue ( tracker, 0 ); // 0 = Bypass

        FieldInfo autoGain = t.GetField ( "_autoGain", flags );
        if ( autoGain != null ) autoGain.SetValue ( tracker, false );

        FieldInfo smoothFall = t.GetField ( "_smoothFall", flags );
        if ( smoothFall != null ) smoothFall.SetValue ( tracker, false );
    }

    // デバイス選択に合わせてトラッカーとリングバッファを作り直す
    void RebuildDeviceAndBuffers () {
        // Lasp の InputDevices 要素型はアセンブリ内で公開されていないため var で受ける
        var devices = Lasp.AudioSystem.InputDevices.ToList ();
        if ( devices.Count == 0 ) {
            lastDeviceIndex = deviceIndex;
            lastMaxBufferSeconds = maxBufferSeconds;
            return;
        }

        // インデックス：デバイス名 をログ表示
        for ( int i = 0; i < devices.Count; i++ ) {
            string name = GetDeviceDisplayName ( devices [ i ] );
            Debug.Log ( $"[MultiWaveData] {i}: {name}" );
        }

        int safeIndex = Mathf.Clamp ( deviceIndex, 0, devices.Count - 1 );
        var dev = devices [ safeIndex ];
        deviceId = dev.ID;
        channelCount = Mathf.Min ( dev.ChannelCount, MaxChannels );
        sampleRate = dev.SampleRate > 0 ? dev.SampleRate : 48000;
        bufferLength = Mathf.Max ( 4, Mathf.CeilToInt ( maxBufferSeconds * sampleRate ) );
        lastDeviceIndex = deviceIndex;
        lastMaxBufferSeconds = maxBufferSeconds;

        // 採用するデバイス名とチャンネル数を明示
        Debug.Log ( $"[MultiWaveDataの使用デバイス] インデックス :  {safeIndex} / デバイス名 : {GetDeviceDisplayName ( dev )} / チャンネル数 :  {channelCount}" );

        if ( trackers != null ) {
            foreach ( Lasp.AudioLevelTracker t in trackers ) {
                if ( t != null && t.gameObject != null )
                    Destroy ( t.gameObject );
            }
        }

        trackers = new List<Lasp.AudioLevelTracker> ();
        ringBuffers = new List<float [ ]> ();
        writeIndices = new List<int> ();

        for ( int ch = 0; ch < channelCount; ch++ ) {
            GameObject go = new GameObject ( $"WaveformTracker_Ch{ch}" );
            go.transform.SetParent ( transform, false );
            Lasp.AudioLevelTracker tracker = go.AddComponent<Lasp.AudioLevelTracker> ();
            tracker.deviceID = deviceId;
            tracker.channel = ch;
            ApplyRawWaveformSettings ( tracker );
            trackers.Add ( tracker );
            ringBuffers.Add ( new float [ bufferLength ] );
            writeIndices.Add ( 0 );
        }
    }

    #endregion
}
