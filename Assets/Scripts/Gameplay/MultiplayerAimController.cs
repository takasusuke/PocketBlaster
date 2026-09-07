using PocketBlaster.Aim;
using PocketBlaster.Audio;
using PocketBlaster.Meta;
using PocketBlaster.Networking;
using PocketBlaster.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace PocketBlaster.Gameplay
{
    /// <summary>
    /// マルチプレイヤーモード(2026-09-07、オーナー承認: 画面共有・移動オート・各自
    /// レティクル案)における、プレイヤー1人ぶんの狙い・弾薬・レティクル表示。
    /// `GyroReticleController`を継承・共用せず独立実装にしてある——移動が完全に
    /// 自動(`MultiplayerStageDirector`がカメラを動かす)なマルチプレイヤーには
    /// 「構える/構えない」の切り替え・リコイル・移動方向の入力といった、
    /// シングルプレイヤー用の複雑さが要らないため、1クラスに条件分岐を混在させる
    /// より別クラスの方が読みやすいと判断した。
    ///
    /// `MultiplayerStageDirector`が`PhoneControllerServer.OnPlayerConnected`を
    /// 受けて`AddComponent`し、直後に<see cref="Initialize"/>を呼ぶ(`PickupFactory`が
    /// `Pickup.Initialize`を呼ぶのと同じパターン)。狙撃判定そのものは
    /// `AimHitResolver`(GyroReticleControllerと共有)に委譲する。
    ///
    /// アイテム(Pickup)の効果は「誰が撃ったか」で振り分ける(2026-09-07、オーナー要望
    /// 「アイテムを実装して」)。弾薬回復(Reload)・最大弾薬数増加(AmmoUp)は撃った
    /// 本人の`AmmoState`へ直接反映する。体力回復(Health)は個人ではなく共有のHPプール
    /// (`MultiplayerStageDirector`が持つ)を回復するため、<see cref="OnHealthPickupCollected"/>
    /// で中継する——シングルプレイヤーの`StageDirector.OnHealthPickupCollected`と
    /// 同じ役割を、シングルトンではなくプレイヤーごとのインスタンスで担う形。
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class MultiplayerAimController : MonoBehaviour
    {
        [SerializeField] private int magazineSize = 6;
        [SerializeField] private float reloadDurationSeconds = 0.6f;
        [SerializeField] private int ammoUpAmount = 2;
        [SerializeField] private Camera aimCamera;
        [SerializeField] private LayerMask hitLayerMask = ~0;
        [SerializeField] private float maxHitDistance = 1000f;

        /// <summary>このプレイヤーが体力回復アイテムを撃った瞬間に中継される。
        /// `MultiplayerStageDirector`が購読し、共有HPプールを回復する。</summary>
        public event System.Action OnHealthPickupCollected;

        private static readonly Color AmmoPipFilledColor = new Color(1f, 0.75f, 0.15f);
        private static readonly Color AmmoPipEmptyColor = new Color(1f, 1f, 1f, 0.18f);

        private int _connectionId;
        private int _slot;
        private Color _playerColor;
        private PhoneControllerServer _server;
        private AmmoState _ammo;
        private AudioSource _audioSource;
        private AudioClip _shotClip;
        private AudioClip _hitClip;
        private AudioClip _missClip;
        private AudioClip _emptyClickClip;

        private bool _isCalibrated;
        private bool _isReloading;
        private float _reloadElapsed;
        private float _refBeta;
        private float _refGamma;
        private float _reticleScreenX;
        private float _reticleScreenY;

        private UIDocument _uiDocument;
        private PanelSettings _panelSettings;
        private VisualElement _reticle;
        private Label _calibrationLabel;
        private Label _ammoLabel;
        private VisualElement _reloadBarTrack;
        private VisualElement _reloadBarFill;
        private VisualElement _ammoPipsContainer;
        private VisualElement[] _ammoPips;

        private void Awake()
        {
            _ammo = new AmmoState(magazineSize);
            _audioSource = GetComponent<AudioSource>();
            _audioSource.volume = GameSettings.Current.SfxVolume;
            _shotClip = ProceduralSfx.CreateTone("sfx_mp_shot", 880f, 0.05f, 0.03f);
            _hitClip = ProceduralSfx.CreateTone("sfx_mp_hit", 440f, 0.15f, 0.1f);
            _missClip = ProceduralSfx.CreateTone("sfx_mp_miss", 220f, 0.08f, 0.06f);
            _emptyClickClip = ProceduralSfx.CreateTone("sfx_mp_empty", 120f, 0.05f, 0.02f);
        }

        /// <summary>`AddComponent`直後に`MultiplayerStageDirector`から呼ぶ初期化。</summary>
        public void Initialize(int connectionId, int slot, Color playerColor, PhoneControllerServer server)
        {
            _connectionId = connectionId;
            _slot = slot;
            _playerColor = playerColor;
            _server = server;
            _server.OnPlayerReload += HandleReload;
            _server.OnPlayerShoot += HandleShoot;

            BuildUi();
            SetReticleColor(_playerColor);
        }

        private void OnDestroy()
        {
            if (_server != null)
            {
                _server.OnPlayerReload -= HandleReload;
                _server.OnPlayerShoot -= HandleShoot;
            }
            if (_panelSettings != null) Destroy(_panelSettings);
        }

        private void Update()
        {
            if (_server == null || !_server.Players.TryGetValue(_connectionId, out var connection)) return;

            if (!_isCalibrated)
            {
                _calibrationLabel.style.display = DisplayStyle.Flex;
                _reticle.style.display = DisplayStyle.None;
                return;
            }
            _calibrationLabel.style.display = DisplayStyle.None;
            _reticle.style.display = DisplayStyle.Flex;

            var betaDelta = Mathf.DeltaAngle(_refBeta, connection.LatestBeta);
            var gammaDelta = Mathf.DeltaAngle(_refGamma, connection.LatestGamma);

            var halfW = Screen.width / 2f;
            var halfH = Screen.height / 2f;
            var x = Mathf.Clamp(halfW + gammaDelta * GameSettings.Current.HorizontalSensitivity, 0, Screen.width);
            var y = Mathf.Clamp(halfH + betaDelta * GameSettings.Current.VerticalSensitivity, 0, Screen.height);

            _reticleScreenX = x;
            _reticleScreenY = y;
            _reticle.style.left = x - _reticle.resolvedStyle.width / 2f;
            _reticle.style.top = y - _reticle.resolvedStyle.height / 2f;

            if (_isReloading)
            {
                const float barWidth = 56f;
                const float barHeight = 6f;
                _reloadBarTrack.style.display = DisplayStyle.Flex;
                _reloadBarTrack.style.left = x - barWidth / 2f;
                _reloadBarTrack.style.top = y + _reticle.resolvedStyle.height / 2f + 6f;
                var progress = Mathf.Clamp01(_reloadElapsed / reloadDurationSeconds);
                _reloadBarFill.style.width = Length.Percent(progress * 100f);
            }
            else
            {
                _reloadBarTrack.style.display = DisplayStyle.None;
            }

            _ammoLabel.text = _isReloading ? "リロード中..." : $"残弾 {_ammo.CurrentAmmo}/{_ammo.MagazineSize}";
            for (var i = 0; i < _ammoPips.Length; i++)
            {
                var isLoaded = !_isReloading && i < _ammo.CurrentAmmo;
                _ammoPips[i].style.backgroundColor = isLoaded ? AmmoPipFilledColor : AmmoPipEmptyColor;
            }
        }

        /// <summary>現在のレティクル位置からカメラの視線を飛ばしたレイ。</summary>
        private Ray? GetAimRay()
        {
            var cam = aimCamera != null ? aimCamera : Camera.main;
            if (cam == null) return null;
            var screenPoint = new Vector3(_reticleScreenX, Screen.height - _reticleScreenY, 0f);
            return cam.ScreenPointToRay(screenPoint);
        }

        private void HandleReload(int connectionId)
        {
            if (connectionId != _connectionId || _isReloading) return;
            StartCoroutine(ReloadRoutine());
        }

        private System.Collections.IEnumerator ReloadRoutine()
        {
            _isReloading = true;
            _reloadElapsed = 0f;
            while (_reloadElapsed < reloadDurationSeconds)
            {
                _reloadElapsed += Time.deltaTime;
                yield return null;
            }
            _ammo.Reload();
            // マルチプレイヤーには「構える/構えない」が無く、キャリブレーション目的の
            // 用途しか無いため、シングルプレイヤーと違い毎回無条件で基準を取り直す
            // (GyroReticleController.Recenter参照——ドリフト対策として同じ役割)。
            if (_server.Players.TryGetValue(_connectionId, out var connection))
            {
                _refBeta = connection.LatestBeta;
                _refGamma = connection.LatestGamma;
            }
            _isCalibrated = true;
            _isReloading = false;
        }

        private void HandleShoot(int connectionId)
        {
            if (connectionId != _connectionId) return;
            if (!_isCalibrated || _isReloading) return;

            if (!_ammo.Shoot())
            {
                _audioSource.PlayOneShot(_emptyClickClip);
                return;
            }
            _audioSource.PlayOneShot(_shotClip);

            var aimRay = GetAimRay();
            if (aimRay == null)
            {
                _audioSource.PlayOneShot(_missClip);
            }
            else
            {
                var result = AimHitResolver.TryHit(aimRay.Value, maxHitDistance, hitLayerMask, out var hitShootable);
                _audioSource.PlayOneShot(result == AimHitResolver.Result.Miss ? _missClip : _hitClip);
                if (hitShootable is Pickup pickup) ApplyPickupEffect(pickup.Type);
            }

            if (_ammo.CurrentAmmo == 0 && !_isReloading)
            {
                StartCoroutine(ReloadRoutine());
            }
        }

        /// <summary>撃った本人の効果として反映するか(Reload/AmmoUp)、共有HPプールへ
        /// 中継するか(Health)を振り分ける。</summary>
        private void ApplyPickupEffect(PickupType type)
        {
            switch (type)
            {
                case PickupType.Reload:
                    _ammo.Reload();
                    break;
                case PickupType.AmmoUp:
                    _ammo.IncreaseMagazineSize(ammoUpAmount);
                    break;
                case PickupType.Health:
                    OnHealthPickupCollected?.Invoke();
                    break;
            }
        }

        private void SetReticleColor(Color color)
        {
            _reticle.style.borderLeftColor = color;
            _reticle.style.borderRightColor = color;
            _reticle.style.borderTopColor = color;
            _reticle.style.borderBottomColor = color;
        }

        private void BuildUi()
        {
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            RuntimeLabelStyle.EnsureTheme(_panelSettings);
            _panelSettings.sortingOrder = 10;

            var uiDocumentGo = new GameObject($"MultiplayerAimUI_Player{_slot + 1}");
            uiDocumentGo.transform.SetParent(transform, false);
            _uiDocument = uiDocumentGo.AddComponent<UIDocument>();
            _uiDocument.panelSettings = _panelSettings;
            var root = _uiDocument.rootVisualElement;

            _reticle = new VisualElement();
            _reticle.style.position = Position.Absolute;
            _reticle.style.width = 32;
            _reticle.style.height = 32;
            _reticle.style.borderTopLeftRadius = 16;
            _reticle.style.borderTopRightRadius = 16;
            _reticle.style.borderBottomLeftRadius = 16;
            _reticle.style.borderBottomRightRadius = 16;
            _reticle.style.borderLeftWidth = 3;
            _reticle.style.borderRightWidth = 3;
            _reticle.style.borderTopWidth = 3;
            _reticle.style.borderBottomWidth = 3;
            root.Add(_reticle);

            _reloadBarTrack = new VisualElement();
            _reloadBarTrack.style.display = DisplayStyle.None;
            _reloadBarTrack.style.position = Position.Absolute;
            _reloadBarTrack.style.width = 56;
            _reloadBarTrack.style.height = 6;
            _reloadBarTrack.style.backgroundColor = new Color(0f, 0f, 0f, 0.5f);
            root.Add(_reloadBarTrack);

            _reloadBarFill = new VisualElement();
            _reloadBarFill.style.position = Position.Absolute;
            _reloadBarFill.style.left = 0;
            _reloadBarFill.style.top = 0;
            _reloadBarFill.style.bottom = 0;
            _reloadBarFill.style.width = Length.Percent(0);
            _reloadBarFill.style.backgroundColor = _playerColor;
            _reloadBarTrack.Add(_reloadBarFill);

            // 較正待ちの案内(プレイヤーごとに自分の色の枠内、画面の自分側に出す)。
            var isLeftSide = _slot == 0;
            _calibrationLabel = new Label(
                $"プレイヤー{_slot + 1}: スマホを画面中央に向けて「リロード」を押してください");
            _calibrationLabel.style.position = Position.Absolute;
            _calibrationLabel.style.bottom = 140;
            if (isLeftSide) _calibrationLabel.style.left = 12; else _calibrationLabel.style.right = 12;
            _calibrationLabel.style.width = 280;
            _calibrationLabel.style.color = Color.white;
            _calibrationLabel.style.fontSize = 14;
            _calibrationLabel.style.whiteSpace = WhiteSpace.Normal;
            _calibrationLabel.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
            _calibrationLabel.style.paddingLeft = 10;
            _calibrationLabel.style.paddingRight = 10;
            _calibrationLabel.style.paddingTop = 8;
            _calibrationLabel.style.paddingBottom = 8;
            _calibrationLabel.style.borderTopLeftRadius = 8;
            _calibrationLabel.style.borderTopRightRadius = 8;
            _calibrationLabel.style.borderBottomLeftRadius = 8;
            _calibrationLabel.style.borderBottomRightRadius = 8;
            RuntimeLabelStyle.ApplyDefaultFont(_calibrationLabel);
            root.Add(_calibrationLabel);

            // 残弾表示(プレイヤー1=左下、プレイヤー2=右下——お互いのHUDが重ならないように)。
            _ammoLabel = new Label();
            _ammoLabel.style.position = Position.Absolute;
            _ammoLabel.style.bottom = 24;
            if (isLeftSide) _ammoLabel.style.left = 24; else _ammoLabel.style.right = 24;
            _ammoLabel.style.color = _playerColor;
            _ammoLabel.style.fontSize = 28;
            _ammoLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _ammoLabel.style.backgroundColor = new Color(0f, 0f, 0f, 0.45f);
            _ammoLabel.style.paddingLeft = 14;
            _ammoLabel.style.paddingRight = 14;
            _ammoLabel.style.paddingTop = 6;
            _ammoLabel.style.paddingBottom = 6;
            _ammoLabel.style.borderTopLeftRadius = 10;
            _ammoLabel.style.borderTopRightRadius = 10;
            _ammoLabel.style.borderBottomLeftRadius = 10;
            _ammoLabel.style.borderBottomRightRadius = 10;
            RuntimeLabelStyle.ApplyDefaultFont(_ammoLabel);
            root.Add(_ammoLabel);

            _ammoPipsContainer = new VisualElement();
            _ammoPipsContainer.style.position = Position.Absolute;
            _ammoPipsContainer.style.bottom = 72;
            if (isLeftSide) _ammoPipsContainer.style.left = 24; else _ammoPipsContainer.style.right = 24;
            _ammoPipsContainer.style.flexDirection = FlexDirection.Row;
            root.Add(_ammoPipsContainer);

            _ammoPips = new VisualElement[Mathf.Max(magazineSize, 1)];
            for (var i = 0; i < _ammoPips.Length; i++)
            {
                var pip = new VisualElement();
                pip.style.width = 14;
                pip.style.height = 18;
                pip.style.marginLeft = 3;
                pip.style.marginRight = 3;
                pip.style.borderTopLeftRadius = 3;
                pip.style.borderTopRightRadius = 3;
                pip.style.borderBottomLeftRadius = 3;
                pip.style.borderBottomRightRadius = 3;
                pip.style.backgroundColor = AmmoPipFilledColor;
                _ammoPipsContainer.Add(pip);
                _ammoPips[i] = pip;
            }
        }
    }
}
