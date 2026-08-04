namespace TapTheObject.Presentation
{
    /// <summary>What the player is expected to do right now.</summary>
    public enum GameStatus
    {
        Loading,
        Ready,
        Failed
    }

    /// <summary>A transient notice shown next to the status label.</summary>
    public enum GameMessage
    {
        Miss,
        ImageLoadFailed,
        ContentUnavailable
    }
}
