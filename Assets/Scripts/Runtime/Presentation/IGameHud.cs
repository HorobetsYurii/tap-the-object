namespace TapTheObject.Presentation
{
    /// <summary>The on-screen status, score and notices as the round loop sees them.</summary>
    public interface IGameHud
    {
        void SetStatus(GameStatus status);

        void SetScore(int score);

        void ShowMessage(GameMessage message);

        /// <summary>Drops the current notice, which belongs to the round that raised it.</summary>
        void ClearMessage();
    }
}
