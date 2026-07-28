namespace TaynDM;

/// <summary>
/// Simple logging abstraction used by services.
/// AppLogger is the default implementation.
/// </summary>
public interface ILogger
{
    void LogInfo(string message);
    void LogWarning(string message);
    void LogError(string message, Exception? ex = null);
}
