using PocketBlaster.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace PocketBlaster.Gameplay
{
    /// <summary>
    /// ScorePopupEffect.SpawnAtが生成する、加点を表示して浮かび上がりながら消える
    /// 1個の演出インスタンス。UIDocumentは実行時生成のためRuntimeLabelStyleで
    /// フォントを明示する(でないとテキストが描画されない、GyroReticleController等と同じ理由)。
    /// </summary>
    public class ScorePopupBehaviour : MonoBehaviour
    {
        private const float LifetimeSeconds = 1f;
        private const float RiseScreenPixels = 70f;

        private Vector3 _worldPosition;
        private Camera _camera;
        private UIDocument _uiDocument;
        private PanelSettings _panelSettings;
        private Label _label;
        private float _elapsed;

        public void Initialize(Vector3 worldPosition, int points, Camera camera)
        {
            _worldPosition = worldPosition;
            _camera = camera;
            BuildUi(points);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            if (_elapsed >= LifetimeSeconds || _camera == null)
            {
                Destroy(gameObject);
                return;
            }

            UpdatePosition();
            _label.style.opacity = 1f - _elapsed / LifetimeSeconds;
        }

        private void OnDestroy()
        {
            if (_panelSettings != null) Destroy(_panelSettings);
        }

        private void BuildUi(int points)
        {
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            // HUD(StageDirector/GameSessionの5)より上、レティクル(10)より下に置く。
            _panelSettings.sortingOrder = 8;

            _uiDocument = gameObject.AddComponent<UIDocument>();
            _uiDocument.panelSettings = _panelSettings;

            _label = new Label($"+{points}");
            _label.style.position = Position.Absolute;
            _label.style.color = new Color(1f, 0.85f, 0.2f);
            _label.style.fontSize = 30;
            _label.style.unityFontStyleAndWeight = FontStyle.Bold;
            RuntimeLabelStyle.ApplyDefaultFont(_label);
            _uiDocument.rootVisualElement.Add(_label);

            UpdatePosition();
        }

        private void UpdatePosition()
        {
            var screenPoint = _camera.WorldToScreenPoint(_worldPosition);
            var progress = _elapsed / LifetimeSeconds;
            _label.style.left = screenPoint.x;
            _label.style.top = Screen.height - screenPoint.y - progress * RiseScreenPixels;
        }
    }
}
