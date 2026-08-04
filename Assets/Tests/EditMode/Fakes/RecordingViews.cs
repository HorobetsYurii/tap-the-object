using System.Collections.Generic;
using TapTheObject.Presentation;
using UnityEngine;

namespace TapTheObject.Tests.Fakes
{
    /// <summary>Records what the round loop asked the object to show.</summary>
    internal sealed class RecordingTargetView : ITargetObjectView
    {
        public Texture2D Texture { get; private set; }

        public int TextureChangeCount { get; private set; }

        public int ErrorFeedbackCount { get; private set; }

        public bool IsLoading { get; private set; }

        public void SetTexture(Texture2D texture)
        {
            Texture = texture;
            TextureChangeCount++;
        }

        public void SetLoading(bool isLoading)
        {
            IsLoading = isLoading;
        }

        public void PlayErrorFeedback()
        {
            ErrorFeedbackCount++;
        }
    }

    /// <summary>Records what the round loop reported to the player.</summary>
    internal sealed class RecordingGameHud : IGameHud
    {
        private readonly List<GameMessage> _messages = new List<GameMessage>();

        public GameStatus Status { get; private set; }

        public int Score { get; private set; }

        public IReadOnlyList<GameMessage> Messages => _messages;

        /// <summary>The notice currently on screen, if any.</summary>
        public GameMessage? VisibleMessage { get; private set; }

        public void SetStatus(GameStatus status)
        {
            Status = status;
        }

        public void SetScore(int score)
        {
            Score = score;
        }

        public void ShowMessage(GameMessage message)
        {
            _messages.Add(message);
            VisibleMessage = message;
        }

        public void ClearMessage()
        {
            VisibleMessage = null;
        }
    }
}
