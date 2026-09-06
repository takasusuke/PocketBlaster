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
    /// ジャイロ値("orientation")・リロード操作("reload")・発射操作("shoot")・
    /// 足踏み検知("step"、PlayerLocomotion参照)・
    /// 一時停止("pause")・再挑戦("retry"、いずれもGameSession参照)を受け取る。
    /// 難易度モードは2026-09-06に起動画面(Title)側へ移したため、ここでは扱わない
    /// (GameSettings/GameSession参照)。
    ///
    /// httpsなのはiOS Safari等がhttpではDeviceOrientationEventを渡さないため
    /// (PhoneOrientationServer参照)。証明書は自己署名で、当初は「警告が出たら
    /// このまま進む」方式にしていたが、iOS Safariでは警告ページから進めず、
    /// Chrome/Braveではabout:blankに落ちるだけの挙動を実機で確認した(2026-09-05)。
    /// そのため証明書を構成プロファイルとしてインストールし「常に信頼する」設定に
    /// する方式に変更 — 別ポート(certificatePort)で証明書の公開部分だけを平文HTTPで
    /// 配布するCertificateDownloadServerを併走させる。
    /// </summary>
    public class PhoneControllerServer : MonoBehaviour
    {
        [SerializeField] private int port = 7777;
        [SerializeField] private int certificatePort = 7778;

        public event Action<float, float, float> OnOrientation;
        public event Action OnReload;
        public event Action OnShoot;
        public event Action OnStep;
        public event Action OnPauseToggleRequested;
        public event Action OnRetryRequested;

        public bool IsConnected { get; private set; }
        public float LatestAlpha { get; private set; }
        public float LatestBeta { get; private set; }
        public float LatestGamma { get; private set; }
        public int Port => port;

        private PhoneOrientationServer _server;
        private CertificateDownloadServer _certificateDownloadServer;

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

            _certificateDownloadServer = new CertificateDownloadServer(certificatePort, certificate);
            _certificateDownloadServer.Start();

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
                    case "step":
                        OnStep?.Invoke();
                        break;
                    case "pause":
                        OnPauseToggleRequested?.Invoke();
                        break;
                    case "retry":
                        OnRetryRequested?.Invoke();
                        break;
                }
            }
        }

        private void OnDestroy()
        {
            _server?.Stop();
            _certificateDownloadServer?.Stop();
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
            Debug.Log("[PhoneControllerServer] 初回のみ、証明書のインストールが必要です。" +
                      $"まずスマホのブラウザで以下を開いて証明書ファイルをダウンロードし、" +
                      "「設定」→「一般」→「VPNとデバイス管理」からプロファイルをインストール、" +
                      "続けて「設定」→「一般」→「情報」→「証明書信頼設定」で" +
                      "「PocketBlaster Dev Server」を完全に信頼する設定にしてください:");
            foreach (var addr in localIps)
            {
                Debug.Log($"  http://{addr}:{certificatePort}/  (証明書のダウンロード)");
            }

            Debug.Log($"[PhoneControllerServer] 証明書の信頼設定が済んだら、port {port} のコントローラー画面を開いてください:");
            foreach (var addr in localIps)
            {
                Debug.Log($"  https://{addr}:{port}/");
            }
        }
    }
}
