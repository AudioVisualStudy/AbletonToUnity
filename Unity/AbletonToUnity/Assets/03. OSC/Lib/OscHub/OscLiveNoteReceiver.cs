using UnityEngine;

namespace yugop.connection {

    /// <summary>
    /// Y-OSC-NoteSender からの /y-osc/note を受信し、OscHub.Session の親クリップへ格納する。
    /// </summary>
    [RequireComponent(typeof(AbletonOscClient))]
    public sealed class OscLiveNoteReceiver : MonoBehaviour {

        const string noteAddress = "/y-osc/note"; // リアルタイム Note On アドレス

        OscHub hub; // ノート配信先
        AbletonOscClient client; // OSC 受信クライアント

        void Awake() {
            hub = GetComponent<OscHub>();
            client = GetComponent<AbletonOscClient>();
            client.AddHandler(noteAddress, onNoteMessage);
        }

        // /y-osc/note（iiii: trackIndex, slotIndex, pitch, velocity）を処理する
        void onNoteMessage(AbletonOscClient.Message message) {
            if (message.Count < 4) {
                return;
            }
            int trackIndex = message.GetInt(0);
            int slotIndex = message.GetInt(1);
            int pitch = message.GetInt(2);
            int velocity = message.GetInt(3);
            if (velocity <= 0) {
                return;
            }
            hub.HandleLiveNote(trackIndex, slotIndex, pitch, velocity);
        }
    }

}
