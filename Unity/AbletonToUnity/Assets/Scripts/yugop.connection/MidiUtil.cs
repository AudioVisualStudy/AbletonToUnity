using UnityEngine;

namespace yugop.connection {


    /// <summary>
    /// NoteOn/NoteOffイベントのデータ
    /// </summary>
    public struct MidiNote {
        public int Channel;      // 0-15
        public int Number;       // 0-127
        private string _string;  // キャッシュ用
        public int Velocity;     // 0-127

        /// <summary>
        /// ノート名を取得（例: "C3"）。未設定の場合は自動計算
        /// </summary>
        public string String {
            get {
                if ( string.IsNullOrEmpty ( _string ) ) {
                    _string = MIDIUtil.toNoteString ( Number );
                }
                return _string;
            }
            set {
                _string = value;
            }
        }

        /// <summary>
        /// コンストラクタ（ノート番号指定）
        /// </summary>
        public MidiNote ( int channel, int number, int velocity ) {
            Channel = channel;
            Number = number;
            _string = null;
            Velocity = velocity;
        }

        /// <summary>
        /// コンストラクタ（ノート名指定）
        /// </summary>
        public MidiNote ( int channel, string noteString, int velocity ) {
            Channel = channel;
            Number = MIDIUtil.toNoteNumber ( noteString );
            _string = noteString;
            Velocity = velocity;
        }

        /// <summary>
        /// ノート番号を指定してノート名を更新
        /// </summary>
        public void SetNumber ( int number ) {
            Number = number;
            _string = null; // キャッシュをクリア
        }

        /// <summary>
        /// ノート名を指定してノート番号を更新
        /// </summary>
        public void SetString ( string noteString ) {
            _string = noteString;
            Number = MIDIUtil.toNoteNumber ( noteString );
        }

        /// <summary>
        /// NoteOnかどうか（Velocity > 0）
        /// </summary>
        public bool IsNoteOn () {
            return Velocity > 0;
        }

        /// <summary>
        /// NoteOffかどうか（Velocity == 0）
        /// </summary>
        public bool IsNoteOff () {
            return Velocity == 0;
        }

        /// <summary>
        /// Velocityを0-1の範囲に正規化
        /// </summary>
        public float GetNormalizedVelocity () {
            return Velocity / 127f;
        }
    }


    /// <summary>
    /// PitchBendイベントのデータ
    /// </summary>
    public struct MidiPitchBend {
        public int Channel;     // 0-15
        private int _rawValue;  // 0 ~ 16383 / 中央値（ベンドなし）: 8192

        /// <summary>
        /// 生の値（0 ~ 16383）
        /// </summary>
        public int RawValue {
            get => _rawValue;
            set => _rawValue = value;
        }

        /// <summary>
        /// 正規化された値（-1.0 ~ 1.0）を自動計算
        /// </summary>
        public float Ratio {
            get => ( _rawValue - 8192f ) / 8192f;
        }

        /// <summary>
        /// コンストラクタ（生の値で指定）
        /// </summary>
        public MidiPitchBend ( int channel, int rawValue ) {
            Channel = channel;
            _rawValue = rawValue;
        }

        /// <summary>
        /// コンストラクタ（正規化値で指定）
        /// </summary>
        public MidiPitchBend ( int channel, float normalizedValue ) {
            Channel = channel;
            _rawValue = (int) ( normalizedValue * 8192f + 8192f );
        }

        /// <summary>
        /// 正規化値から生の値を設定
        /// </summary>
        public void SetFromNormalized ( float normalizedValue ) {
            _rawValue = (int) ( Mathf.Clamp ( normalizedValue, -1f, 1f ) * 8192f + 8192f );
        }

        /// <summary>
        /// センター位置（ベンドなし）かどうか
        /// </summary>
        public bool IsCenter () {
            return _rawValue == 8192;
        }

        /// <summary>
        /// センター位置にリセット
        /// </summary>
        public void ResetToCenter () {
            _rawValue = 8192;
        }

        /// <summary>
        /// 上方向へのベンドかどうか
        /// </summary>
        public bool IsBendUp () {
            return _rawValue > 8192;
        }

        /// <summary>
        /// 下方向へのベンドかどうか
        /// </summary>
        public bool IsBendDown () {
            return _rawValue < 8192;
        }
    }

    /// <summary>
    /// ControlChangeイベントのデータ
    /// </summary>
    public struct MidiControlChange {
        public int Channel { get; set; }        // 0-15
        public int ControlNumber { get; set; }  // 0-127
        public int ControlValue { get; set; }   // 0-127

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public MidiControlChange ( int channel, int controlNumber, int controlValue ) {
            Channel = channel;
            ControlNumber = controlNumber;
            ControlValue = controlValue;
        }

        /// <summary>
        /// コントロール値を0-1の範囲に正規化
        /// </summary>
        public float GetNormalizedValue () {
            return ControlValue / 127f;
        }

        /// <summary>
        /// 正規化値からコントロール値を設定
        /// </summary>
        public void SetFromNormalized ( float normalizedValue ) {
            ControlValue = (int) ( Mathf.Clamp01 ( normalizedValue ) * 127f );
        }

        /// <summary>
        /// 特定のコントロール番号かどうかチェック
        /// </summary>
        public bool IsControlNumber ( int number ) {
            return ControlNumber == number;
        }

        /// <summary>
        /// よく使われるコントロール番号の判定
        /// </summary>
        public bool IsModulationWheel () => ControlNumber == 1;
        public bool IsBreathController () => ControlNumber == 2;
        public bool IsVolume () => ControlNumber == 7;
        public bool IsPan () => ControlNumber == 10;
        public bool IsExpression () => ControlNumber == 11;
        public bool IsSustainPedal () => ControlNumber == 64;
    }

    /// <summary>
    /// ProgramChangeイベントのデータ
    /// </summary>
    public struct MidiProgramChange {
        public int Channel;         // 0-15
        public int ProgramNumber;   // 0-127

        /// <summary>
        /// コンストラクタ
        /// </summary>
        public MidiProgramChange ( int channel, int programNumber ) {
            Channel = channel;
            ProgramNumber = programNumber;
        }

        /// <summary>
        /// プログラム番号を1始まり（1-128）で取得
        /// </summary>
        public int GetProgramNumberOneBased () {
            return ProgramNumber + 1;
        }

        /// <summary>
        /// プログラム番号を1始まり（1-128）で設定
        /// </summary>
        public void SetProgramNumberOneBased ( int programNumber ) {
            ProgramNumber = programNumber - 1;
        }

        /// <summary>
        /// 特定のプログラム番号かどうかチェック
        /// </summary>
        public bool IsProgramNumber ( int number ) {
            return ProgramNumber == number;
        }
    }

    /// <summary>
    /// MIDIデータ関連のよく使うユーティリティ関数群
    /// </summary>
    /// 
    public static class MIDIUtil {


        /// <summary>
        /// MIDIノート番号をAbleton等で一般的に使われるノート名に変換する
        /// </summary>
        /// <param name="noteNumber">変換するMIDIノート番号</param>
        /// <returns>ノート名とオクターブを組み合わせた文字列（例: "C4", "A#5"）</returns>
        /// 
        public static string toNoteString ( int noteNumber ) {
            string [] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
            int octave = ( noteNumber / 12 ) - 2;
            string noteName = noteNames [ noteNumber % 12 ];
            return noteName + octave.ToString ();
        }

        /// <summary>
        /// ノート名をMIDIノート番号に変換する
        /// </summary>
        /// <param name="noteString">変換するノート名（例: "C4", "A#5"）</param>
        /// <returns>MIDIノート番号（0-127）。変換失敗時は-1を返す</returns>
        /// 
        public static int toNoteNumber ( string noteString ) {
            if ( string.IsNullOrEmpty ( noteString ) ) {
                Debug.LogWarning ( "Invalid note string: null or empty" );
                return -1;
            }

            noteString = noteString.Trim ().ToUpper ();

            string [] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

            // 音名部分とオクターブ部分を分離
            string noteName = "";
            string octaveString = "";

            for ( int i = 0; i < noteString.Length; i++ ) {
                char c = noteString [ i ];
                if ( char.IsDigit ( c ) || c == '-' ) {
                    octaveString = noteString.Substring ( i );
                    break;
                }
                noteName += c;
            }

            // 音名を0-11の値に変換
            int noteIndex = -1;
            for ( int i = 0; i < noteNames.Length; i++ ) {
                if ( noteNames [ i ] == noteName ) {
                    noteIndex = i;
                    break;
                }
            }

            if ( noteIndex == -1 ) {
                Debug.LogWarning ( $"Invalid note name: {noteName}" );
                return -1;
            }

            // オクターブを整数に変換
            if ( !int.TryParse ( octaveString, out int octave ) ) {
                Debug.LogWarning ( $"Invalid octave: {octaveString}" );
                return -1;
            }

            // MIDIノート番号を計算
            int noteNumber = ( octave + 2 ) * 12 + noteIndex;

            // 範囲チェック（0-127）
            if ( noteNumber < 0 || noteNumber > 127 ) {
                Debug.LogWarning ( $"Note number out of range (0-127): {noteNumber}" );
                return -1;
            }

            return noteNumber;
        }

    }

    //MIDIのイベントタイプの列挙型
    public enum MidiType {
        NoteOn = 0,
        NoteOff = 1,
        PitchBend = 2,
        ControlChange = 3,
        ProgramChange = 4
    }
}
