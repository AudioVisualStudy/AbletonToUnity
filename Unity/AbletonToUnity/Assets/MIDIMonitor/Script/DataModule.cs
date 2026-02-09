using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using yugop.connection;

public class DataModule : MonoBehaviour {

    public TextMeshProUGUI titleText;
    public TextMeshProUGUI dataNameText;
    public TextMeshProUGUI dataValueText;

    public CanvasGroup group;
    public Image bgImage;

    public MidiType type;

    public float startAlpha = 1f;
    public float endAlpha = 0.05f;

    public float fadeDuration = 0.5f;
    public float fadeDelay = 0.1f;

    Color color;
    public float saturation = 0.5f;
    public float brightness = 0.7f;

    // 位置を設定
    public void setUIPosition ( Vector2 position ) {
        RectTransform rectTransform = GetComponent<RectTransform> ();
        if ( rectTransform != null ) {
            rectTransform.anchoredPosition = position;
        }
    }


    // データを設定
    public void setData ( MidiType type, object data ) {
        this.type = type;

        switch ( type ) {

            case MidiType.NoteOn:
            case MidiType.NoteOff:

                if ( data is MidiNote note ) {
                    titleText.text = $"{type.ToString ()} : {note.String}";
                    dataNameText.text = $"String\nNumber\nVelocity";
                    dataValueText.text = $"{note.String}\n{note.Number}\n{note.Velocity}";

                    // 音階に応じた色を取得
                    color = GetColorByPitch ( note.Number );
                    bgImage.color = color;
                }
                break;

            case MidiType.PitchBend:
                if ( data is MidiPitchBend pb ) {
                    titleText.text = type.ToString ();
                    dataNameText.text = $"Ratio\nRaw Value";
                    dataValueText.text = $"{pb.Ratio:F2}\n{pb.RawValue}";

                    // 値に応じたグラデーション色を取得
                    color = GetColorByValue ( pb.Ratio );
                    bgImage.color = color;
                }
                break;

            case MidiType.ControlChange:
                if ( data is MidiControlChange cc ) {
                    titleText.text = type.ToString ();
                    dataNameText.text = $"CC Number\nCC Value";
                    dataValueText.text = $"{cc.ControlNumber}\n{cc.ControlValue}";

                    // 0-127を-1.0-1.0に正規化して色を取得
                    float normalizedValue = ( cc.ControlValue - 64f ) / 64f;
                    color = GetColorByValue ( normalizedValue );
                    bgImage.color = color;
                }
                break;



            case MidiType.ProgramChange:
                if ( data is MidiProgramChange pc ) {
                    titleText.text = type.ToString ();
                    dataNameText.text = $"nProgram";
                    dataValueText.text = $"{pc.ProgramNumber}";
                }
                break;
        }
    }

    // 音階番号から色を取得（色相を12分割）
    Color GetColorByPitch ( int noteNumber ) {
        // ノート番号を12で割った余りで音階を取得（0=C, 1=C#, 2=D, ... 11=B）
        int pitchClass = noteNumber % 12;
        // 色相を12分割（0-1の範囲、1色あたり1/12 ≒ 0.0833）
        float hue = pitchClass / 12f;
        // HSVからRGBに変換（彩度と明度は調整可能）
        Color pitchColor = Color.HSVToRGB ( hue, saturation, brightness );
        return pitchColor;
    }

    // 値から色を取得（-1.0: 青、0: 緑、1.0: 赤）色相グラデーション
    Color GetColorByValue ( float normalizedValue ) {
        // 値を -1.0 ~ 1.0 の範囲にクランプ
        normalizedValue = Mathf.Clamp ( normalizedValue, -1f, 1f );

        float hue;

        if ( normalizedValue < 0f ) {
            // -1.0 ~ 0.0: 青(240°)から緑(120°)へ
            float t = ( normalizedValue + 1f ); // 0.0 ~ 1.0 に変換
            hue = Mathf.Lerp ( 240f / 360f, 120f / 360f, t ); // 240° → 120°
        } else {
            // 0.0 ~ 1.0: 緑(120°)から赤(0°)へ
            float t = normalizedValue;
            hue = Mathf.Lerp ( 120f / 360f, 0f, t ); // 120° → 0°
        }

        // HSVからRGBに変換（彩度と明度は固定）
        Color resultColor = Color.HSVToRGB ( hue, saturation, brightness );

        return resultColor;
    }

    // フェードアウト開始
    public void startFadeOut () {
        group.alpha = startAlpha;
        StopAllCoroutines ();
        StartCoroutine ( fadeOutCoroutine ( fadeDuration, fadeDelay ) );
    }
    // フェードアウトのコルーチン
    IEnumerator fadeOutCoroutine ( float duration, float delay ) {

        yield return new WaitForSeconds ( delay );

        float elapsed = 0f;

        while ( elapsed < duration ) {
            elapsed += Time.deltaTime;
            float f = elapsed / duration;
            group.alpha = HEasing.Ease ( startAlpha, endAlpha, f, HEasingType.easeOutSine );

            yield return null;
        }
        group.alpha = endAlpha;
    }



}
