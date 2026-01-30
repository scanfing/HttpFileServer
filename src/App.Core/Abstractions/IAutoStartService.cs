namespace Core.Abstractions;

public interface IAutoStartService
{
    bool IsEnabled();
    void Enable();
    void Disable();
}