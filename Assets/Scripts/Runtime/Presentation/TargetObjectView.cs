using UnityEngine;

namespace TapTheObject.Presentation
{
    /// <summary>
    /// Shows the round image on the spawned object and flashes it red on an incorrect selection.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public sealed class TargetObjectView : MonoBehaviour, ITargetObjectView
    {
        private static readonly int MainTextureId = Shader.PropertyToID("_MainTex");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private Color _errorColor = new Color(1f, 0.25f, 0.2f);
        [SerializeField, Min(0.05f)] private float _errorFlashDuration = 0.4f;
        [SerializeField] private Color _loadingTint = new Color(0.45f, 0.45f, 0.5f);

        private Renderer _renderer;
        private MaterialPropertyBlock _propertyBlock;
        private Texture2D _texture;
        private float _flashTimeLeft;
        private bool _isLoading;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();

            // A property block, rather than Renderer.material, which would clone the shared material.
            _propertyBlock = new MaterialPropertyBlock();

            // Hidden until the first texture arrives.
            _renderer.enabled = false;
        }

        public void SetTexture(Texture2D texture)
        {
            _texture = texture;
            _renderer.enabled = texture != null;
            ApplyProperties();
        }

        public void SetLoading(bool isLoading)
        {
            _isLoading = isLoading;
            ApplyProperties();
        }

        public void PlayErrorFeedback()
        {
            _flashTimeLeft = _errorFlashDuration;
            ApplyProperties();
        }

        private void Update()
        {
            if (_flashTimeLeft <= 0f)
            {
                return;
            }

            _flashTimeLeft = Mathf.Max(0f, _flashTimeLeft - Time.deltaTime);
            ApplyProperties();
        }

        private void ApplyProperties()
        {
            _renderer.GetPropertyBlock(_propertyBlock);

            if (_texture != null)
            {
                _propertyBlock.SetTexture(MainTextureId, _texture);
            }

            var tint = _isLoading ? _loadingTint : Color.white;
            _propertyBlock.SetColor(ColorId, Color.Lerp(tint, _errorColor, _flashTimeLeft / _errorFlashDuration));
            _renderer.SetPropertyBlock(_propertyBlock);
        }
    }
}
