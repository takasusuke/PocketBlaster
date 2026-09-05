using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using UnityEngine;

namespace PocketBlaster.Networking
{
    /// <summary>
    /// 生のTCPソケット上に最小限のHTTPS(静的ファイル配信)とRFC6455 WebSocket(wss)サーバーを
    /// 実装したもの。外部ライブラリなしでスマホのブラウザ(webapp/index.html)から
    /// ジャイロ値を受け取るために書いている。UnityのMonoBehaviourには依存しない
    /// (PhoneControllerServerがラップする)ので、Editor上のバッチ実行からも直接叩ける。
    ///
    /// TLS必須にしているのは、iOS Safari(および多くの最新ブラウザ)が「セキュアな
    /// コンテキスト(https)」でないとDeviceOrientationEventのセンサー値を一切渡さない
    /// ため — 同一Wi-Fi内のLAN IPへのhttp://では、権限ダイアログすら出ずに黙って
    /// 動かない(2026-09-05に実機で確認)。証明書は自己署名で、OSの証明書ストアには
    /// 触れずプロセス内だけで完結させている(SelfSignedCertificate参照)。
    /// </summary>
    public sealed class PhoneOrientationServer
    {
        private const string WebSocketMagicString = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        [Serializable]
        public struct InboundMessage
        {
            public string type;
            public double alpha;
            public double beta;
            public double gamma;
        }

        private readonly int _port;
        private readonly string _indexHtmlPath;
        private readonly X509Certificate2 _serverCertificate;
        private readonly ConcurrentQueue<InboundMessage> _inbox = new ConcurrentQueue<InboundMessage>();
        private TcpListener _listener;
        private Thread _acceptThread;
        private volatile bool _running;
        private volatile bool _clientConnected;

        public bool IsClientConnected => _clientConnected;

        public PhoneOrientationServer(int port, string indexHtmlPath, X509Certificate2 serverCertificate)
        {
            _port = port;
            _indexHtmlPath = indexHtmlPath;
            _serverCertificate = serverCertificate;
        }

        public void Start()
        {
            if (_running) return;
            _running = true;
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            _acceptThread = new Thread(AcceptLoop) { IsBackground = true };
            _acceptThread.Start();
        }

        public void Stop()
        {
            _running = false;
            try { _listener?.Stop(); } catch (Exception) { /* ソケット破棄時の例外は無視してよい */ }
        }

        public bool TryDequeue(out InboundMessage message) => _inbox.TryDequeue(out message);

        private void AcceptLoop()
        {
            while (_running)
            {
                TcpClient client;
                try
                {
                    client = _listener.AcceptTcpClient();
                }
                catch (Exception)
                {
                    // Stop()によるSocketException含む。running=falseならループを抜ける。
                    if (!_running) return;
                    continue;
                }

                var handlerThread = new Thread(() => HandleClient(client)) { IsBackground = true };
                handlerThread.Start();
            }
        }

        private void HandleClient(TcpClient client)
        {
            try
            {
                using (client)
                using (var rawStream = client.GetStream())
                using (var sslStream = new SslStream(rawStream, leaveInnerStreamOpen: false))
                {
                    // SslProtocols.Noneを指定すると、明示的なバージョン列挙に依存せず
                    // OS(Windowsの場合SChannel)がクライアントと合意できる最良のTLSバージョンを
                    // 選ぶ。ここで個別にTls12等を指定すると、ランタイムのAPI互換レベルによっては
                    // 新しいバージョン(Tls13)の列挙値自体が存在せずコンパイルエラーになる。
                    sslStream.AuthenticateAsServer(_serverCertificate, false, SslProtocols.None, false);
                    Stream stream = sslStream;

                    var headers = ReadHttpHeaders(stream, out _);
                    if (headers == null) return;

                    var isUpgrade = HeaderContainsValue(headers, "Upgrade", "websocket");
                    if (!isUpgrade)
                    {
                        ServeStaticHtml(stream);
                        return;
                    }

                    if (!headers.TryGetValue("Sec-WebSocket-Key", out var clientKey))
                    {
                        return;
                    }

                    CompleteHandshake(stream, clientKey);
                    _clientConnected = true;
                    try
                    {
                        ReadFrameLoop(stream);
                    }
                    finally
                    {
                        _clientConnected = false;
                    }
                }
            }
            catch (Exception)
            {
                _clientConnected = false;
            }
        }

        private static System.Collections.Generic.Dictionary<string, string> ReadHttpHeaders(Stream stream, out string requestLine)
        {
            requestLine = null;
            var headers = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var lineBuffer = new StringBuilder();
            var isFirstLine = true;
            int b;
            var previousWasCr = false;

            while ((b = stream.ReadByte()) != -1)
            {
                var c = (char)b;
                if (c == '\n' && previousWasCr)
                {
                    var line = lineBuffer.ToString(0, lineBuffer.Length - 1); // 末尾の\rを落とす
                    lineBuffer.Clear();

                    if (isFirstLine)
                    {
                        requestLine = line;
                        isFirstLine = false;
                    }
                    else if (line.Length == 0)
                    {
                        return headers; // ヘッダー終端の空行
                    }
                    else
                    {
                        var idx = line.IndexOf(':');
                        if (idx > 0)
                        {
                            var key = line.Substring(0, idx).Trim();
                            var value = line.Substring(idx + 1).Trim();
                            headers[key] = value;
                        }
                    }
                }
                else
                {
                    lineBuffer.Append(c);
                }
                previousWasCr = c == '\r';
            }

            return null; // 接続が途中で切れた
        }

        private static bool HeaderContainsValue(System.Collections.Generic.Dictionary<string, string> headers, string key, string expectedValue)
        {
            return headers.TryGetValue(key, out var value) &&
                   value.IndexOf(expectedValue, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void ServeStaticHtml(Stream stream)
        {
            string body;
            try
            {
                body = File.ReadAllText(_indexHtmlPath, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                body = "<html><body>webapp/index.html を読み込めませんでした: " + ex.Message + "</body></html>";
            }

            var bodyBytes = Encoding.UTF8.GetBytes(body);
            var header = "HTTP/1.1 200 OK\r\n" +
                         "Content-Type: text/html; charset=utf-8\r\n" +
                         "Content-Length: " + bodyBytes.Length + "\r\n" +
                         "Connection: close\r\n\r\n";
            var headerBytes = Encoding.ASCII.GetBytes(header);
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(bodyBytes, 0, bodyBytes.Length);
            stream.Flush();
        }

        private static void CompleteHandshake(Stream stream, string clientKey)
        {
            using var sha1 = SHA1.Create();
            var combined = clientKey + WebSocketMagicString;
            var hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(combined));
            var acceptKey = Convert.ToBase64String(hash);

            var response = "HTTP/1.1 101 Switching Protocols\r\n" +
                           "Upgrade: websocket\r\n" +
                           "Connection: Upgrade\r\n" +
                           "Sec-WebSocket-Accept: " + acceptKey + "\r\n\r\n";
            var responseBytes = Encoding.ASCII.GetBytes(response);
            stream.Write(responseBytes, 0, responseBytes.Length);
            stream.Flush();
        }

        private void ReadFrameLoop(Stream stream)
        {
            while (_running)
            {
                var payload = ReadOneFrame(stream, out var opcode);
                if (payload == null) return; // 接続終了

                switch (opcode)
                {
                    case 0x1: // text
                        var json = Encoding.UTF8.GetString(payload);
                        TryEnqueueJson(json);
                        break;
                    case 0x8: // close
                        return;
                    case 0x9: // ping -> pong
                        SendFrame(stream, 0xA, payload);
                        break;
                    default:
                        break; // pong・binaryは無視
                }
            }
        }

        private void TryEnqueueJson(string json)
        {
            try
            {
                var msg = JsonUtility.FromJson<InboundMessage>(json);
                if (!string.IsNullOrEmpty(msg.type))
                {
                    _inbox.Enqueue(msg);
                }
            }
            catch (Exception)
            {
                // 壊れたJSONは1件落とすだけにする(接続自体は切らない)
            }
        }

        private static byte[] ReadOneFrame(Stream stream, out byte opcode)
        {
            opcode = 0;
            var header = ReadExact(stream, 2);
            if (header == null) return null;

            opcode = (byte)(header[0] & 0x0F);
            var masked = (header[1] & 0x80) != 0;
            long len = header[1] & 0x7F;

            if (len == 126)
            {
                var ext = ReadExact(stream, 2);
                if (ext == null) return null;
                len = (ext[0] << 8) | ext[1];
            }
            else if (len == 127)
            {
                var ext = ReadExact(stream, 8);
                if (ext == null) return null;
                len = 0;
                for (var i = 0; i < 8; i++) len = (len << 8) | ext[i];
            }

            byte[] maskKey = null;
            if (masked)
            {
                maskKey = ReadExact(stream, 4);
                if (maskKey == null) return null;
            }

            var payload = len > 0 ? ReadExact(stream, (int)len) : Array.Empty<byte>();
            if (payload == null) return null;

            if (masked)
            {
                for (var i = 0; i < payload.Length; i++)
                {
                    payload[i] ^= maskKey[i % 4];
                }
            }

            return payload;
        }

        private static void SendFrame(Stream stream, byte opcode, byte[] payload)
        {
            // サーバーからクライアントへ送るフレームはRFC6455によりマスクしない。
            var len = payload.Length;
            using var ms = new MemoryStream();
            ms.WriteByte((byte)(0x80 | opcode)); // FIN=1

            if (len < 126)
            {
                ms.WriteByte((byte)len);
            }
            else if (len <= ushort.MaxValue)
            {
                ms.WriteByte(126);
                ms.WriteByte((byte)(len >> 8));
                ms.WriteByte((byte)len);
            }
            else
            {
                ms.WriteByte(127);
                for (var i = 7; i >= 0; i--) ms.WriteByte((byte)(len >> (i * 8)));
            }

            ms.Write(payload, 0, payload.Length);
            var bytes = ms.ToArray();
            stream.Write(bytes, 0, bytes.Length);
            stream.Flush();
        }

        private static byte[] ReadExact(Stream stream, int count)
        {
            var buffer = new byte[count];
            var offset = 0;
            while (offset < count)
            {
                var read = stream.Read(buffer, offset, count - offset);
                if (read <= 0) return null; // 接続が切れた
                offset += read;
            }
            return buffer;
        }
    }
}
