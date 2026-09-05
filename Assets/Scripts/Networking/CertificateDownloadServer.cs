using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace PocketBlaster.Networking
{
    /// <summary>
    /// 自己署名証明書の"公開部分だけ"(秘密鍵を含まない)を、暗号化なしの別ポートで
    /// 配布するためだけの最小サーバー。iOS Safariは自己署名証明書(特にホスト名では
    /// なく生のIPアドレス宛てのもの)への「このまま進む」操作が不安定なため、
    /// 証明書を構成プロファイルとして端末にインストールし「常に信頼する」設定にする
    /// 方式に切り替えた(2026-09-05、実機で警告ページから進めない事象を確認)。
    ///
    /// 配布用途のみなので暗号化は不要(公開鍵証明書は秘密情報ではない)。
    /// PhoneOrientationServerとは別ポートで待ち受け、プロトコルもTLSではなくHTTPのみ。
    /// </summary>
    public sealed class CertificateDownloadServer
    {
        private readonly int _port;
        private readonly byte[] _certificateDerBytes;
        private TcpListener _listener;
        private Thread _acceptThread;
        private volatile bool _running;

        public CertificateDownloadServer(int port, X509Certificate2 certificate)
        {
            _port = port;
            _certificateDerBytes = certificate.Export(X509ContentType.Cert); // 秘密鍵を含まない公開部分のみ
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
                using (var stream = client.GetStream())
                {
                    // リクエスト内容は問わず、どのパスへのGETでも証明書を返す(配布専用のため)。
                    var buffer = new byte[1024];
                    _ = stream.Read(buffer, 0, buffer.Length);

                    var header = "HTTP/1.1 200 OK\r\n" +
                                 "Content-Type: application/x-x509-ca-cert\r\n" +
                                 "Content-Disposition: attachment; filename=\"pocketblaster-dev-cert.cer\"\r\n" +
                                 "Content-Length: " + _certificateDerBytes.Length + "\r\n" +
                                 "Connection: close\r\n\r\n";
                    var headerBytes = Encoding.ASCII.GetBytes(header);
                    stream.Write(headerBytes, 0, headerBytes.Length);
                    stream.Write(_certificateDerBytes, 0, _certificateDerBytes.Length);
                    stream.Flush();
                }
            }
            catch (Exception)
            {
                // 配布用の使い捨て接続なので、失敗しても静かに諦めてよい。
            }
        }
    }
}
