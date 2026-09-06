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
    /// 一時停止("pause")・再挑戦("retry")・起動画面へ戻る("title"、
    /// オーナー要望2026-09-06:「練習モードから起動画面に戻るボタンをスマホに配置して」。
    /// いずれもGameSession参照)・
    /// 構えの開始/終了("aim_start"/"aim_end"、IsAiming参照)を受け取る。
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
    ///
    /// シーンをまたいで生き続ける永続シングルトン(2026-09-06、オーナー報告:「再挑戦すると、
    /// スマホとの接続が切れてしまいました」への対応)。以前は各シーンの"GyroAimTestRig"に
    /// 直接AddComponentしていたため、`GameSession`の「再挑戦」(シーンリロード)や
    /// 起動画面からステージへの遷移のたびにこのMonoBehaviourごと破棄・再生成され、
    /// 生きていたTCP/WebSocket接続まで道連れで切れていた。`GetOrCreate()`経由でのみ
    /// 取得させ、`DontDestroyOnLoad`で1個だけ生き残らせることで、シーン遷移をまたいで
    /// 同じ接続を保持できるようにした。
    /// </summary>
    public class PhoneControllerServer : MonoBehaviour
    {
        [SerializeField] private int port = 7777;
        [SerializeField] private int certificatePort = 7778;

        public static PhoneControllerServer Instance { get; private set; }

        /// <summary>
        /// 生きている永続インスタンスを返す。無ければ専用のGameObjectを新規作成する。
        /// シーン(GyroAimTestRig等)に直接AddComponentしない — シーン側のGameObjectは
        /// シーン遷移で破棄されるため、この永続インスタンスとは別物になる。
        /// </summary>
        public static PhoneControllerServer GetOrCreate()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("PhoneControllerServer(Persistent)");
            return go.AddComponent<PhoneControllerServer>();
        }

        public event Action<float, float, float> OnOrientation;
        public event Action OnReload;
        public event Action OnShoot;
        public event Action OnStep;
        public event Action OnPauseToggleRequested;
        public event Action OnRetryRequested;
        /// <summary>
        /// 起動画面(Title)へ戻る要求(オーナー要望、2026-09-06:「練習モードから起動画面に
        /// 戻るボタンをスマホに配置して」)。練習モードに限らず、どのシーンでも
        /// GameSessionが購読して即座にシーン遷移する(ステージクリア/ゲームオーバー後の
        /// 自動遷移とは別の、プレイヤーが任意のタイミングで戻れる手動操作)。
        /// </summary>
        public event Action OnReturnToTitleRequested;

        /// <summary>
        /// スマホ側の「構える」ボタンが構え状態の間true(オーナー要望、2026-09-06:
        /// 「『構える』ボタンを新たに配置して、構えている間は照準を動かして、構えて
        /// いない間は移動する」)。スマホの傾きは1系統しか無いため、狙い(照準)と
        /// 移動(歩き回り)のどちらに使うかをこのボタンで明示的に切り替える設計にした。
        /// GyroReticleControllerが照準の有効/無効に、PlayerLocomotionが移動方向の
        /// 入力切り替えに、それぞれこれを見る。
        ///
        /// 当初は「押している間だけ構える」長押し方式だったが、オーナー要望(2026-09-06:
        /// 「構える、構えないは長押しではなくボタンによる切り替え式にして」)によりタップ
        /// 切り替え式に変更した。ここでの見え方は変わらない——"aim_start"/"aim_end"を
        /// 受け取ってtrue/falseにするだけのレベル値で、webapp側の送信タイミングが
        /// 「押している間」から「押した瞬間ごとの切り替え」に変わっただけ。
        /// </summary>
        public bool IsAiming { get; private set; }

        public bool IsConnected { get; private set; }
        public float LatestAlpha { get; private set; }
        public float LatestBeta { get; private set; }
        public float LatestGamma { get; private set; }
        public int Port => port;

        private PhoneOrientationServer _server;
        private CertificateDownloadServer _certificateDownloadServer;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // 万が一2個目が作られても(例えば旧シーンの生成コードが残っていた場合)、
                // 新しい方を即座に破棄して既存の接続を保持する。
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

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
                    case "title":
                        OnReturnToTitleRequested?.Invoke();
                        break;
                    case "aim_start":
                        IsAiming = true;
                        break;
                    case "aim_end":
                        IsAiming = false;
                        break;
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
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
