using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using System.Collections.Generic;
using Unity.Mathematics;



//用法例：
//float f = Mathf.Clamp01 ((Time.time - startTime) / easeTime);
//float tf = HEasing.easeOutCubic (0f, 1f, f);

public enum HEasingType {
    linear,
    clerp,
    spring,
    easeInQuad,
    easeOutQuad,
    easeInOutQuad,
    easeInCubic,
    easeOutCubic,
    easeInOutCubic,
    easeInQuart,
    easeOutQuart,
    easeInOutQuart,
    easeInQuint,
    easeOutQuint,
    easeInOutQuint,
    easeInSine,
    easeOutSine,
    easeInOutSine,
    easeInExpo,
    easeOutExpo,
    easeInOutExpo,
    easeInCirc,
    easeOutCirc,
    easeInOutCirc,
    easeInBounce,
    easeOutBounce,
    easeInOutBounce,
    easeInBack,
    easeOutBack,
    easeInOutBack,
    easeInElastic,
    easeOutElastic,
    easeInOutElastic
}




////public struct HVAnif<T> {
////    public bool alive { get; private set; }
////    public float ticker { get { return m_ticker > m_duration?1:m_ticker / m_duration; } }


////    public HVAnif(T from, T to, float duration, HEasingType easing = HEasingType.linear, System.Action callback = null) {
////        alive = true;
////        m_from = from;
////        m_to = to;
////        m_duration = duration;
////        m_ticker = 0;
////        m_easing = easing;
////        m_callback = callback;
////    }

////    public T Update() {
////        if(!alive) return m_to;
////        m_ticker += Time.deltaTime;
////        if(!alive || m_ticker > m_duration) {
////            alive = false;
////            m_callback?.Invoke();
////            m_callback = null;
////            return m_to;
////        } else {
////            return m_from + ( (dynamic)m_to - m_from ) * HEasing.Ease( m_ticker / m_duration, m_easing );
////        }
////    }

////    private T m_from;
////    private T m_to;
////    private float m_duration;
////    private float m_ticker;
////    private HEasingType m_easing;
////    private System.Action m_callback;
////}


public struct HAniFloat3 {
    public bool alive { get; private set; }
    public float ticker { get { return m_ticker > m_duration ? 1 : m_ticker / m_duration; } }


    public HAniFloat3(float3 from, float3 to, float duration, HEasingType easing = HEasingType.linear, System.Action callback = null) {
        alive = true;
        m_from = from;
        m_to = to;
        m_duration = duration;
        m_ticker = 0;
        m_easing = easing;
        m_callback = callback;
    }

    public float3 Update() {
        if(!alive) return m_to;
        m_ticker += Time.deltaTime;
        if(!alive || m_ticker > m_duration) {
            alive = false;
            m_callback?.Invoke();
            m_callback = null;
            return m_to;
        } else {
            return m_from + (m_to - m_from ) * HEasing.Ease( m_ticker / m_duration, m_easing );
        }
    }

    public float3 Update(float deltaTime) {
        if (!alive) return m_to;
        m_ticker += deltaTime;
        if (!alive || m_ticker > m_duration) {
            alive = false;
            m_callback?.Invoke();
            m_callback = null;
            return m_to;
        } else {
            return m_from + (m_to - m_from) * HEasing.Ease(m_ticker / m_duration, m_easing);
        }
    }

    public float3 UnscaleUpdate() {
        if (!alive) return m_to;
        m_ticker += Time.unscaledDeltaTime;
        if (!alive || m_ticker > m_duration) {
            alive = false;
            m_callback?.Invoke();
            m_callback = null;
            return m_to;
        } else {
            return m_from + (m_to - m_from) * HEasing.Ease(m_ticker / m_duration, m_easing);
        }
    }


    public void Clear() {
        alive = false;
    }

    private float3 m_from;
    private float3 m_to;
    private float m_duration;
    private float m_ticker;
    private HEasingType m_easing;
    private System.Action m_callback;
}




public struct HAniFloat{
    public bool alive { get; private set; }
    public float ticker { get { return m_ticker > m_duration ? 1 : m_ticker / m_duration; } }


    public HAniFloat(float from, float to, float duration, HEasingType easing = HEasingType.linear, System.Action callback = null) {
        alive = true;
        m_from = from;
        m_to = to;
        m_duration = duration;
        m_ticker = 0;
        m_easing = easing;
        m_callback = callback;
    }

    public float Update() {
        if(!alive) return m_to;
        m_ticker += Time.deltaTime;
        if(!alive || m_ticker > m_duration) {
            alive = false;
            m_callback?.Invoke();
            m_callback = null;
            return m_to;
        } else {
            return m_from + ( m_to - m_from ) * HEasing.Ease( m_ticker / m_duration, m_easing );
        }
    }

    public float Update(float deltaTime) {
        if (!alive) return m_to;
        m_ticker += deltaTime;
        if (!alive || m_ticker > m_duration) {
            alive = false;
            m_callback?.Invoke();
            m_callback = null;
            return m_to;
        } else {
            return m_from + (m_to - m_from) * HEasing.Ease(m_ticker / m_duration, m_easing);
        }
    }

    public float UnscaleUpdate() {
        if (!alive) return m_to;
        m_ticker += Time.unscaledDeltaTime;
        if (!alive || m_ticker > m_duration) {
            alive = false;
            m_callback?.Invoke();
            m_callback = null;
            return m_to;
        } else {
            return m_from + (m_to - m_from) * HEasing.Ease(m_ticker / m_duration, m_easing);
        }
    }


    public void Clear() {
        alive = false;
    }

    private float m_from;
    private float m_to;
    private float m_duration;
    private float m_ticker;
    private HEasingType m_easing;
    private System.Action m_callback;
}



public class HEasing {

    static public float Ease(float start, float end, float value, HEasingType easing) {
        switch(easing) {
            case HEasingType.linear: return HEasing.linear( start, end, value );
            case HEasingType.clerp: return HEasing.clerp( start, end, value );
            case HEasingType.spring: return HEasing.spring( start, end, value );
            case HEasingType.easeInQuad: return HEasing.easeInQuad( start, end, value );
            case HEasingType.easeOutQuad: return HEasing.easeOutQuad( start, end, value );
            case HEasingType.easeInOutQuad: return HEasing.easeInOutQuad( start, end, value );
            case HEasingType.easeInCubic: return HEasing.easeInCubic( start, end, value );
            case HEasingType.easeOutCubic: return HEasing.easeOutCubic( start, end, value );
            case HEasingType.easeInOutCubic: return HEasing.easeInOutCubic( start, end, value );
            case HEasingType.easeInQuart: return HEasing.easeInQuart( start, end, value );
            case HEasingType.easeOutQuart: return HEasing.easeOutQuart( start, end, value );
            case HEasingType.easeInOutQuart: return HEasing.easeInOutQuart( start, end, value );
            case HEasingType.easeInQuint: return HEasing.easeInQuint( start, end, value );
            case HEasingType.easeOutQuint: return HEasing.easeOutQuint( start, end, value );
            case HEasingType.easeInOutQuint: return HEasing.easeInOutQuint( start, end, value );
            case HEasingType.easeInSine: return HEasing.easeInSine( start, end, value );
            case HEasingType.easeOutSine: return HEasing.easeOutSine( start, end, value );
            case HEasingType.easeInOutSine: return HEasing.easeInOutSine( start, end, value );
            case HEasingType.easeInExpo: return HEasing.easeInExpo( start, end, value );
            case HEasingType.easeOutExpo: return HEasing.easeOutExpo( start, end, value );
            case HEasingType.easeInOutExpo: return HEasing.easeInOutExpo( start, end, value );
            case HEasingType.easeInCirc: return HEasing.easeInCirc( start, end, value );
            case HEasingType.easeOutCirc: return HEasing.easeOutCirc( start, end, value );
            case HEasingType.easeInOutCirc: return HEasing.easeInOutCirc( start, end, value );
            case HEasingType.easeInBounce: return HEasing.easeInBounce( start, end, value );
            case HEasingType.easeOutBounce: return HEasing.easeOutBounce( start, end, value );
            case HEasingType.easeInOutBounce: return HEasing.easeInOutBounce( start, end, value );
            case HEasingType.easeInBack: return HEasing.easeInBack( start, end, value );
            case HEasingType.easeOutBack: return HEasing.easeOutBack( start, end, value );
            case HEasingType.easeInOutBack: return HEasing.easeInOutBack( start, end, value );
            case HEasingType.easeInElastic: return HEasing.easeInElastic( start, end, value );
            case HEasingType.easeOutElastic: return HEasing.easeOutElastic( start, end, value );
            case HEasingType.easeInOutElastic: return HEasing.easeInOutElastic( start, end, value );
            default: return 0;
        }
    }
    static public float Ease(float value, HEasingType easing) {
        return HEasing.Ease( 0, 1, value, easing );
    }


    static public float linear( float start , float end , float value ) {
        return math.lerp( start , end , value );
    }

    static public float clerp( float start , float end , float value ) {
        float min = 0.0f;
        float max = 360.0f;
        float half = math.abs( ( max - min ) * 0.5f );
        float retval = 0.0f;
        float diff = 0.0f;
        if( ( end - start ) < -half ) {
            diff = ( ( max - start ) + end ) * value;
            retval = start + diff;
        } else if( ( end - start ) > half ) {
            diff = -( ( max - end ) + start ) * value;
            retval = start + diff;
        } else retval = start + ( end - start ) * value;
        return retval;
    }

    static public float spring( float start , float end , float value ) {
        value = math.clamp( value,0,1 );
        value = ( math.sin( value * Mathf.PI * ( 0.2f + 2.5f * value * value * value ) ) * math.pow( 1f - value , 2.2f ) + value ) * ( 1f + ( 1.2f * ( 1f - value ) ) );
        return start + ( end - start ) * value;
    }

    static public float easeInQuad( float start , float end , float value ) {
        end -= start;
        return end * value * value + start;
    }

    static public float easeOutQuad( float start , float end , float value ) {
        end -= start;
        return -end * value * ( value - 2 ) + start;
    }

    static public float easeInOutQuad( float start , float end , float value ) {
        value /= .5f;
        end -= start;
        if( value < 1 ) return end * 0.5f * value * value + start;
        value--;
        return -end * 0.5f * ( value * ( value - 2 ) - 1 ) + start;
    }

    static public float easeInCubic( float start , float end , float value ) {
        end -= start;
        return end * value * value * value + start;
    }

    static public float easeOutCubic( float start , float end , float value ) {
        value--;
        end -= start;
        return end * ( value * value * value + 1 ) + start;
    }

    static public float easeInOutCubic( float start , float end , float value ) {
        value /= .5f;
        end -= start;
        if( value < 1 ) return end * 0.5f * value * value * value + start;
        value -= 2;
        return end * 0.5f * ( value * value * value + 2 ) + start;
    }

    static public float easeInQuart( float start , float end , float value ) {
        end -= start;
        return end * value * value * value * value + start;
    }

    static public float easeOutQuart( float start , float end , float value ) {
        value--;
        end -= start;
        return -end * ( value * value * value * value - 1 ) + start;
    }

    static public float easeInOutQuart( float start , float end , float value ) {
        value /= .5f;
        end -= start;
        if( value < 1 ) return end * 0.5f * value * value * value * value + start;
        value -= 2;
        return -end * 0.5f * ( value * value * value * value - 2 ) + start;
    }

    static public float easeInQuint( float start , float end , float value ) {
        end -= start;
        return end * value * value * value * value * value + start;
    }

    static public float easeOutQuint( float start , float end , float value ) {
        value--;
        end -= start;
        return end * ( value * value * value * value * value + 1 ) + start;
    }

    static public float easeInOutQuint( float start , float end , float value ) {
        value /= .5f;
        end -= start;
        if( value < 1 ) return end * 0.5f * value * value * value * value * value + start;
        value -= 2;
        return end * 0.5f * ( value * value * value * value * value + 2 ) + start;
    }

    static public float easeInSine( float start , float end , float value ) {
        end -= start;
        return -end * math.cos( value * ( Mathf.PI * 0.5f ) ) + end + start;
    }

    static public float easeOutSine( float start , float end , float value ) {
        end -= start;
        return end * math.sin( value * ( Mathf.PI * 0.5f ) ) + start;
    }

    static public float easeInOutSine( float start , float end , float value ) {
        end -= start;
        return -end * 0.5f * ( math.cos( Mathf.PI * value ) - 1 ) + start;
    }

    static public float easeInExpo( float start , float end , float value ) {
        end -= start;
        return end * math.pow( 2 , 10 * ( value - 1 ) ) + start;
    }

    static public float easeOutExpo( float start , float end , float value ) {
        end -= start;
        return end * ( -math.pow( 2 , -10 * value ) + 1 ) + start;
    }

    static public float easeInOutExpo( float start , float end , float value ) {
        value /= .5f;
        end -= start;
        if( value < 1 ) return end * 0.5f * math.pow( 2 , 10 * ( value - 1 ) ) + start;
        value--;
        return end * 0.5f * ( -math.pow( 2 , -10 * value ) + 2 ) + start;
    }

    static public float easeInCirc( float start , float end , float value ) {
        end -= start;
        return -end * ( math.sqrt( 1 - value * value ) - 1 ) + start;
    }

    static public float easeOutCirc( float start , float end , float value ) {
        value--;
        end -= start;
        return end * math.sqrt( 1 - value * value ) + start;
    }

    static public float easeInOutCirc( float start , float end , float value ) {
        value /= .5f;
        end -= start;
        if( value < 1 ) return -end * 0.5f * ( math.sqrt( 1 - value * value ) - 1 ) + start;
        value -= 2;
        return end * 0.5f * ( math.sqrt( 1 - value * value ) + 1 ) + start;
    }

    /* GFX47 MOD START */
    static public float easeInBounce( float start , float end , float value ) {
        end -= start;
        float d = 1f;
        return end - easeOutBounce( 0 , end , d - value ) + start;
    }
    /* GFX47 MOD END */

    /* GFX47 MOD START */
    //static public  float bounce(float start, float end, float value){
    static public float easeOutBounce( float start , float end , float value ) {
        value /= 1f;
        end -= start;
        if( value < ( 1 / 2.75f ) ) {
            return end * ( 7.5625f * value * value ) + start;
        } else if( value < ( 2 / 2.75f ) ) {
            value -= ( 1.5f / 2.75f );
            return end * ( 7.5625f * ( value ) * value + .75f ) + start;
        } else if( value < ( 2.5 / 2.75 ) ) {
            value -= ( 2.25f / 2.75f );
            return end * ( 7.5625f * ( value ) * value + .9375f ) + start;
        } else {
            value -= ( 2.625f / 2.75f );
            return end * ( 7.5625f * ( value ) * value + .984375f ) + start;
        }
    }
    /* GFX47 MOD END */

    /* GFX47 MOD START */
    static public float easeInOutBounce( float start , float end , float value ) {
        end -= start;
        float d = 1f;
        if( value < d * 0.5f ) return easeInBounce( 0 , end , value * 2 ) * 0.5f + start;
        else return easeOutBounce( 0 , end , value * 2 - d ) * 0.5f + end * 0.5f + start;
    }
    /* GFX47 MOD END */

    static public float easeInBack( float start , float end , float value ) {
        end -= start;
        value /= 1;
        float s = 1.70158f;
        return end * ( value ) * value * ( ( s + 1 ) * value - s ) + start;
    }

    static public float easeOutBack( float start , float end , float value ) {
        float s = 1.70158f;
        end -= start;
        value = ( value ) - 1;
        return end * ( ( value ) * value * ( ( s + 1 ) * value + s ) + 1 ) + start;
    }

    static public float easeInOutBack( float start , float end , float value ) {
        float s = 1.70158f;
        end -= start;
        value /= .5f;
        if( ( value ) < 1 ) {
            s *= ( 1.525f );
            return end * 0.5f * ( value * value * ( ( ( s ) + 1 ) * value - s ) ) + start;
        }
        value -= 2;
        s *= ( 1.525f );
        return end * 0.5f * ( ( value ) * value * ( ( ( s ) + 1 ) * value + s ) + 2 ) + start;
    }

    static public float punch( float amplitude , float value ) {
        float s = 9;
        if( value == 0 ) {
            return 0;
        } else if( value == 1 ) {
            return 0;
        }
        float period = 1 * 0.3f;
        s = period / ( 2 * Mathf.PI ) * math.asin( 0 );
        return ( amplitude * math.pow( 2 , -10 * value ) * math.sin( ( value * 1 - s ) * ( 2 * Mathf.PI ) / period ) );
    }

    /* GFX47 MOD START */
    static public float easeInElastic( float start , float end , float value ) {
        end -= start;

        float d = 1f;
        float p = d * .3f;
        float s = 0;
        float a = 0;

        if( value == 0 ) return start;

        if( ( value /= d ) == 1 ) return start + end;

        if( a == 0f || a < math.abs( end ) ) {
            a = end;
            s = p / 4;
        } else {
            s = p / ( 2 * Mathf.PI ) * math.asin( end / a );
        }

        return -( a * math.pow( 2 , 10 * ( value -= 1 ) ) * math.sin( ( value * d - s ) * ( 2 * Mathf.PI ) / p ) ) + start;
    }
    /* GFX47 MOD END */

    /* GFX47 MOD START */
    //static public  float elastic(float start, float end, float value){
    static public float easeOutElastic( float start , float end , float value ) {
        /* GFX47 MOD END */
        //Thank you to rafael.marteleto for fixing this as a port over from Pedro's UnityTween
        end -= start;

        float d = 1f;
        float p = d * .3f;
        float s = 0;
        float a = 0;

        if( value == 0 ) return start;

        if( ( value /= d ) == 1 ) return start + end;

        if( a == 0f || a < math.abs( end ) ) {
            a = end;
            s = p * 0.25f;
        } else {
            s = p / ( 2 * Mathf.PI ) * math.asin( end / a );
        }

        return ( a * math.pow( 2 , -10 * value ) * math.sin( ( value * d - s ) * ( 2 * Mathf.PI ) / p ) + end + start );
    }

    /* GFX47 MOD START */
    static public float easeInOutElastic( float start , float end , float value ) {
        end -= start;

        float d = 1f;
        float p = d * .3f;
        float s = 0;
        float a = 0;

        if( value == 0 ) return start;

        if( ( value /= d * 0.5f ) == 2 ) return start + end;

        if( a == 0f || a < math.abs( end ) ) {
            a = end;
            s = p / 4;
        } else {
            s = p / ( 2 * Mathf.PI ) * math.asin( end / a );
        }

        if( value < 1 ) return -0.5f * ( a * math.pow( 2 , 10 * ( value -= 1 ) ) * math.sin( ( value * d - s ) * ( 2 * Mathf.PI ) / p ) ) + start;
        return a * math.pow( 2 , -10 * ( value -= 1 ) ) * math.sin( ( value * d - s ) * ( 2 * Mathf.PI ) / p ) * 0.5f + end + start;
    }
    /* GFX47 MOD END */


}