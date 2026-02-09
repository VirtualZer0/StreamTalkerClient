namespace StreamTalkerClient.Models;

/// <summary>
/// Steps in the first-launch wizard flow.
/// </summary>
public enum WizardStep
{
    Welcome,                    // Step 1: Welcome message
    ExistingServerChoice,       // Step 2: Local vs Remote choice
    LocalCheck,                 // Step 2.1: Auto-check localhost
    RemoteServerInput,          // Step 2.2: Remote URL input
    WslInstall,                 // Step 3.0: WSL installation (Windows only)
    DockerInstall,              // Step 3.1: Docker installation guide
    ResourceConfig,             // Step 4.1: RAM/CPU sliders
    ServerLaunch,               // Step 4.2: Docker launch + health check
    Completed                   // Final success state
}
