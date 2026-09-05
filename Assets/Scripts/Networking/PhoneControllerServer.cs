using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using UnityEngine;

namespace PocketBlaster.Networking
{
    /// <summary>
    /// PhoneOrientationServer(生のTCP/WebSocket実装)をシーンに置くための薄いラッパー。
    /// スマホのブラウザ(webapp/index.html)が同一Wi-Fi内から https://&lt;このPCのIP&gt;:port/
    /// を開くと、このスクリプトがページを配信し、続けて開かれるWebSocket接続から
    /// ジャイロ値("orientation")・リロード操作("reload")・発射操作("shoot")を受け取る。
    ///
    /// httpsなのはiOS Safari等がhttpではDeviceOrientationEventを渡さないため
    /// (PhoneOrientationServer参照)。証明書は自己署名なので、スマホ側で初回に
    /// 「信頼して進む」操作が必要になる — LogConnectionInfoでその旨も案内する。
    /// </summary>
    public class PhoneControllerServer : MonoBehaviour
    {
        [SerializeField] private int port = 7777;

        public event Action<float, float, float> OnOrientation;
        public event Action OnReload;
        public event Action OnShoot;

        public bool IsConnected { get; private set; }
        public float LatestAlpha { get; private set; }
        public float LatestBeta { get; private set; }
        public float LatestGamma { get; private set; }
        public int Port => port;

        private PhoneOrientationServer _server;

        private void Awake()
        {
            var projectRoot = Path.Combine(Application.dataPath, "..");
            X509Certificate2 certificate;
            try
            {
                certificate = DevCertificate.LoadOrGenerate(projectRoot);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PhoneControllerServer] 開発用証明書の準備に失敗したため起動しません: {ex.Message}");
                enabled = false;
                return;
            }

            var indexHtmlPath = Path.Combine(Application.dataPath, "..", "webapp", "index.html");
            _server = new PhoneOrientationServer(port, indexHtmlPath, certificate);
            _server.Start();
            LogConnectionInfo(GetLocalIPv4Addresses());
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
                    case "shoot":
                        OnShoot?.Invoke();
                        break;
                }
            }
        }

        private void OnDestroy()
        {
            _server?.Stop();
        }

        private static List<IPAddress> GetLocalIPv4Addresses()
        {
            var result = new List<IPAddress>();
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var addr in host.AddressList)
                {
                    if (addr.AddressFamily == AddressFamily.InterNetwork)
                    {
                        result.Add(addr);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PhoneControllerServer] ローカルIPの取得に失敗: {ex.Message}");
            }
            return result;
        }

        private void LogConnectionInfo(List<IPAddress> localIps)
        {
            Debug.Log($"[PhoneControllerServer] port {port} で待ち受け中。" +
                      "スマホのブラウザで以下のいずれかを開いてください " +
                      "(自己署名証明書のため、初回は「安全でない接続」の警告を" +
                      "手動で許可して進む必要があります):");
            foreach (var addr in localIps)
            {
                Debug.Log($"  https://{addr}:{port}/");
            }
        }
    }
}
