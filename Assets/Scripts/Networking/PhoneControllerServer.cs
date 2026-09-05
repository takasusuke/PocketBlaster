using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace PocketBlaster.Networking
{
    /// <summary>
    /// PhoneOrientationServer(生のTCP/WebSocket実装)をシーンに置くための薄いラッパー。
    /// スマホのブラウザ(webapp/index.html)が同一Wi-Fi内から http://&lt;このPCのIP&gt;:port/
    /// を開くと、このスクリプトがページを配信し、続けて開かれるWebSocket接続から
    /// ジャイロ値("orientation")とリロード操作("reload")を受け取る。
    /// </summary>
    public class PhoneControllerServer : MonoBehaviour
    {
        [SerializeField] private int port = 7777;

        public event Action<float, float, float> OnOrientation;
        public event Action OnReload;

        public bool IsConnected { get; private set; }
        public float LatestAlpha { get; private set; }
        public float LatestBeta { get; private set; }
        public float LatestGamma { get; private set; }
        public int Port => port;

        private PhoneOrientationServer _server;

        private void Awake()
        {
            var indexHtmlPath = Path.Combine(Application.dataPath, "..", "webapp", "index.html");
            _server = new PhoneOrientationServer(port, indexHtmlPath);
            _server.Start();
            LogConnectionInfo();
        }

        private void Update()
        {
            if (_server == null) return;

            IsConnected = _server.IsClientConnected;

            while (_server.TryDequeue(out var msg))
            {
                switch (msg.type)
                {
                    case "orientation":
                        LatestAlpha = (float)msg.alpha;
                        LatestBeta = (float)msg.beta;
                        LatestGamma = (float)msg.gamma;
                        OnOrientation?.Invoke(LatestAlpha, LatestBeta, LatestGamma);
                        break;
                    case "reload":
                        OnReload?.Invoke();
                        break;
                }
            }
        }

        private void OnDestroy()
        {
            _server?.Stop();
        }

        private void LogConnectionInfo()
        {
            Debug.Log($"[PhoneControllerServer] port {port} で待ち受け中。" +
                      $"スマホのブラウザで以下のいずれかを開いてください:");
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var addr in host.AddressList)
                {
                    if (addr.AddressFamily == AddressFamily.InterNetwork)
                    {
                        Debug.Log($"  http://{addr}:{port}/");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PhoneControllerServer] ローカルIPの取得に失敗: {ex.Message}");
            }
        }
    }
}
