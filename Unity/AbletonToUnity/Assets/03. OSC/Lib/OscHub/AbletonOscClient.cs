using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

namespace yugop.connection {

    /// <summary>
    /// AbletonOSC（Remote Script）との OSC 送受信クライアント。
    /// クエリを Live（既定 11000）へ送信し、返信（既定 11001）を受信してメインスレッドで配信する。
    /// AbletonOSC の返信は 1 データグラムが大きくなる（クリップの全ノート等）ため、独自の受信処理を持つ。
    /// </summary>
    public sealed class AbletonOscClient : MonoBehaviour {

        public string liveHost = "127.0.0.1"; // AbletonOSC が動作するマシンのアドレス
        public int sendPort = 11000; // AbletonOSC の受信ポート
        public int receivePort = 11001; // AbletonOSC が返信を送ってくるポート
        public bool useDebugLog = false; // 受信メッセージを Console に出力する

        /// <summary>受信した OSC メッセージ 1 件（アドレスとパース済み引数）。</summary>
        public sealed class Message {
            public string Address; // OSC アドレス
            public object [ ] Args; // パース済み引数（int / float / double / bool / string）

            public int Count => Args.Length;

            /// <summary>引数を int として取得する（float / bool / 文字列からも変換）。</summary>
            public int GetInt(int index) {
                if (index < 0 || index >= Args.Length) {
                    return 0;
                }
                object value = Args [index];
                if (value is int i) return i;
                if (value is float f) return (int)f;
                if (value is double d) return (int)d;
                if (value is bool b) return b ? 1 : 0;
                if (value is string s && int.TryParse(s, out int parsed)) return parsed;
                return 0;
            }

            /// <summary>引数を float として取得する（int / bool / 文字列からも変換）。</summary>
            public float GetFloat(int index) {
                if (index < 0 || index >= Args.Length) {
                    return 0f;
                }
                object value = Args [index];
                if (value is float f) return f;
                if (value is double d) return (float)d;
                if (value is int i) return i;
                if (value is bool b) return b ? 1f : 0f;
                if (value is string s && float.TryParse(s, out float parsed)) return parsed;
                return 0f;
            }

            /// <summary>引数を string として取得する。</summary>
            public string GetString(int index) {
                if (index < 0 || index >= Args.Length) {
                    return "";
                }
                object value = Args [index];
                if (value is string s) return s;
                if (value == null) return "";
                return value.ToString();
            }
        }

        static readonly object [ ] emptyArgs = new object [0]; // 引数なしメッセージの共有インスタンス

        Socket sendSocket; // クエリ送信用 UDP ソケット
        Socket receiveSocket; // 返信受信用 UDP ソケット
        Thread receiveThread; // 受信ループスレッド
        volatile bool disposed; // 受信ループ停止フラグ
        readonly Queue<Message> mainThreadQueue = new Queue<Message>(); // 受信スレッド → メインスレッド受け渡しキュー
        readonly object queueLock = new object();
        readonly Dictionary<string, Action<Message>> handlers = new Dictionary<string, Action<Message>>(); // アドレス別ハンドラ
        readonly byte [ ] sendBuffer = new byte [512]; // 送信パケット組み立てバッファ

        void Awake() {
            openSockets();
        }

        void OnDestroy() {
            closeSockets();
        }

        void Update() {
            dispatchQueuedMessages();
        }

        /// <summary>OSC アドレスに対するハンドラを登録する（メインスレッドで呼ばれる）。</summary>
        public void AddHandler(string address, Action<Message> handler) {
            if (handlers.TryGetValue(address, out Action<Message> existing)) {
                handlers [address] = existing + handler;
            } else {
                handlers [address] = handler;
            }
        }

        /// <summary>引数なしのクエリを送信する。</summary>
        public void Send(string address) {
            sendPacket(address, null);
        }

        /// <summary>int 引数 1 つのクエリを送信する。</summary>
        public void Send(string address, int arg0) {
            sendPacket(address, new object [ ] { arg0 });
        }

        /// <summary>int 引数 2 つのクエリを送信する。</summary>
        public void Send(string address, int arg0, int arg1) {
            sendPacket(address, new object [ ] { arg0, arg1 });
        }

        // 送受信ソケットを開き、受信スレッドを開始する
        void openSockets() {
            if (receiveSocket != null) {
                return;
            }

            sendSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            sendSocket.Connect(new IPEndPoint(IPAddress.Parse(liveHost), sendPort));

            receiveSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            receiveSocket.ReceiveTimeout = 100;
            receiveSocket.Bind(new IPEndPoint(IPAddress.Any, receivePort));

            disposed = false;
            receiveThread = new Thread(receiveLoop);
            receiveThread.Start();
            Debug.Log($"[AbletonOscClient] send to {liveHost}:{sendPort}, listen on {receivePort}");
        }

        // ソケットとスレッドを停止する
        void closeSockets() {
            disposed = true;
            if (receiveSocket != null) {
                receiveSocket.Close();
                receiveSocket = null;
            }
            if (receiveThread != null) {
                receiveThread.Join();
                receiveThread = null;
            }
            if (sendSocket != null) {
                sendSocket.Close();
                sendSocket = null;
            }
        }

        // 受信スレッド: データグラムをパースしてキューに積む
        void receiveLoop() {
            byte [ ] buffer = new byte [65536]; // ノート一覧など大きな返信に備えた受信バッファ
            List<Message> parsed = new List<Message>();
            while (!disposed) {
                try {
                    int received = receiveSocket.Receive(buffer);
                    if (disposed || received <= 0) {
                        continue;
                    }
                    parsed.Clear();
                    parseMessage(buffer, 0, received, parsed);
                    lock (queueLock) {
                        for (int i = 0; i < parsed.Count; i++) {
                            mainThreadQueue.Enqueue(parsed [i]);
                        }
                    }
                }
                catch (SocketException) {
                    // ReceiveTimeout による中断。何もしない
                }
                catch (Exception e) {
                    if (!disposed) {
                        Debug.LogWarning($"[AbletonOscClient] receive error: {e.Message}");
                    }
                }
            }
        }

        // キューのメッセージをメインスレッドでハンドラへ配信する
        void dispatchQueuedMessages() {
            while (true) {
                Message message;
                lock (queueLock) {
                    if (mainThreadQueue.Count == 0) {
                        return;
                    }
                    message = mainThreadQueue.Dequeue();
                }
                if (useDebugLog) {
                    Debug.Log($"[AbletonOscClient] recv {message.Address} args={message.Count}");
                }
                if (handlers.TryGetValue(message.Address, out Action<Message> handler)) {
                    try {
                        handler?.Invoke(message);
                    }
                    catch (Exception e) {
                        Debug.LogError($"[AbletonOscClient] handler error ({message.Address}): {e.Message}\n{e.StackTrace}");
                    }
                }
            }
        }

        // OSC パケットを組み立てて送信する（引数は int のみ対応）
        void sendPacket(string address, object [ ] args) {
            if (sendSocket == null) {
                return;
            }
            int offset = 0;
            writeString(sendBuffer, ref offset, address);
            StringBuilder tags = new StringBuilder(",");
            int argCount = args != null ? args.Length : 0;
            for (int i = 0; i < argCount; i++) {
                tags.Append('i');
            }
            writeString(sendBuffer, ref offset, tags.ToString());
            for (int i = 0; i < argCount; i++) {
                writeInt(sendBuffer, ref offset, (int)args [i]);
            }
            try {
                sendSocket.Send(sendBuffer, offset, SocketFlags.None);
            }
            catch (Exception e) {
                Debug.LogWarning($"[AbletonOscClient] send error ({address}): {e.Message}");
            }
        }

        // OSC メッセージ（またはバンドル）を再帰的にパースする
        static void parseMessage(byte [ ] buffer, int offset, int length, List<Message> output) {
            int end = offset + length;
            string address = readString(buffer, ref offset, end);
            if (string.IsNullOrEmpty(address)) {
                return;
            }

            if (address == "#bundle") {
                offset += 8; // タイムタグは読み飛ばす
                while (offset + 4 <= end) {
                    int elementLength = readInt(buffer, ref offset);
                    if (elementLength <= 0 || offset + elementLength > end) {
                        break;
                    }
                    parseMessage(buffer, offset, elementLength, output);
                    offset += elementLength;
                }
                return;
            }

            if (offset >= end || buffer [offset] != (byte)',') {
                output.Add(new Message { Address = address, Args = emptyArgs });
                return;
            }

            string typeTags = readString(buffer, ref offset, end);
            List<object> args = new List<object>(typeTags.Length - 1);
            for (int i = 1; i < typeTags.Length; i++) {
                char tag = typeTags [i];
                switch (tag) {
                    case 'i':
                        args.Add(readInt(buffer, ref offset));
                        break;
                    case 'f':
                        args.Add(readFloat(buffer, ref offset));
                        break;
                    case 'd':
                        args.Add(readDouble(buffer, ref offset));
                        break;
                    case 's':
                    case 'S':
                        args.Add(readString(buffer, ref offset, end));
                        break;
                    case 'T':
                        args.Add(true);
                        break;
                    case 'F':
                        args.Add(false);
                        break;
                    case 'N':
                        args.Add(null);
                        break;
                    case 'b': {
                            int blobLength = readInt(buffer, ref offset);
                            offset += align4(blobLength);
                            args.Add(null);
                            break;
                        }
                    default:
                        i = typeTags.Length;
                        break;
                }
            }
            output.Add(new Message { Address = address, Args = args.ToArray() });
        }

        // 4 バイト境界へ切り上げる
        static int align4(int length) {
            return (length + 3) & ~3;
        }

        // null 終端文字列を読み、オフセットを 4 バイト境界へ進める
        static string readString(byte [ ] buffer, ref int offset, int end) {
            int start = offset;
            while (offset < end && buffer [offset] != 0) {
                offset++;
            }
            string result = Encoding.UTF8.GetString(buffer, start, offset - start);
            offset = start + align4(offset - start + 1);
            return result;
        }

        // ビッグエンディアン int32 を読む
        static int readInt(byte [ ] buffer, ref int offset) {
            int value = (buffer [offset] << 24) | (buffer [offset + 1] << 16) | (buffer [offset + 2] << 8) | buffer [offset + 3];
            offset += 4;
            return value;
        }

        // ビッグエンディアン float32 を読む
        static float readFloat(byte [ ] buffer, ref int offset) {
            int raw = readInt(buffer, ref offset);
            return BitConverter.Int32BitsToSingle(raw);
        }

        // ビッグエンディアン float64 を読む
        static double readDouble(byte [ ] buffer, ref int offset) {
            long high = (uint)readInt(buffer, ref offset);
            long low = (uint)readInt(buffer, ref offset);
            long raw = (high << 32) | low;
            return BitConverter.Int64BitsToDouble(raw);
        }

        // null 終端文字列を書き、オフセットを 4 バイト境界へ進める
        static void writeString(byte [ ] buffer, ref int offset, string value) {
            int written = Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, offset);
            int padded = align4(written + 1);
            for (int i = written; i < padded; i++) {
                buffer [offset + i] = 0;
            }
            offset += padded;
        }

        // ビッグエンディアン int32 を書く
        static void writeInt(byte [ ] buffer, ref int offset, int value) {
            buffer [offset] = (byte)(value >> 24);
            buffer [offset + 1] = (byte)(value >> 16);
            buffer [offset + 2] = (byte)(value >> 8);
            buffer [offset + 3] = (byte)value;
            offset += 4;
        }
    }

}
