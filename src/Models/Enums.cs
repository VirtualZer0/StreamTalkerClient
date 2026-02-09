namespace StreamTalkerClient.Models;

public enum MessageState
{
    Queued,
    Synthesizing,
    WaitingForCache,  // Waiting for identical message to finish synthesis
    Ready,
    Playing,
    Done
}

public enum IndicatorState
{
    Unloaded,
    Loading,
    WarmingUp,
    Ready,
    Active,
    Error
}
