using System;
using TMPro;
using UnityEngine;

namespace TapTheObject.Presentation
{
    /// <summary>
    /// Renders the status, the score and transient notices, and owns the wording so the round loop only
    /// deals with <see cref="GameStatus"/> and <see cref="GameMessage"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameHudView : MonoBehaviour, IGameHud
    {
        [Header("Labels")]
        [SerializeField] private TMP_Text _statusLabel;
        [SerializeField] private TMP_Text _scoreLabel;
        [SerializeField] private TMP_Text _messageLabel;

        [Header("Behaviour")]
        [SerializeField, Min(0.1f)] private float _messageDuration = 1.6f;
        [SerializeField] private Color _missColor = new Color(1f, 0.4f, 0.35f);
        [SerializeField] private Color _warningColor = new Color(1f, 0.78f, 0.3f);

        private float _messageTimeLeft;

        private void Awake()
        {
            HideMessage();
            SetScore(0);
        }

        public void SetStatus(GameStatus status)
        {
            _statusLabel.text = status switch
            {
                GameStatus.Loading => "Loading image...",
                GameStatus.Ready => "Tap the object!",
                GameStatus.Failed => "Content unavailable",
                _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
            };
        }

        public void SetScore(int score)
        {
            _scoreLabel.text = $"Score: {score}";
        }

        public void ShowMessage(GameMessage message)
        {
            switch (message)
            {
                case GameMessage.Miss:
                    Show("Missed, tap the object itself.", _missColor, _messageDuration);
                    break;
                case GameMessage.ImageLoadFailed:
                    Show("Image download failed, showing fallback.", _warningColor, _messageDuration);
                    break;
                case GameMessage.ContentUnavailable:
                    Show("Could not load the game content.", _missColor, float.PositiveInfinity);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(message), message, null);
            }
        }

        public void ClearMessage()
        {
            HideMessage();
        }

        private void Update()
        {
            if (_messageTimeLeft <= 0f)
            {
                return;
            }

            _messageTimeLeft -= Time.deltaTime;
            if (_messageTimeLeft <= 0f)
            {
                HideMessage();
            }
        }

        private void Show(string text, Color color, float duration)
        {
            _messageLabel.text = text;
            _messageLabel.color = color;
            _messageLabel.enabled = true;
            _messageTimeLeft = duration;
        }

        private void HideMessage()
        {
            _messageLabel.enabled = false;
            _messageTimeLeft = 0f;
        }
    }
}
