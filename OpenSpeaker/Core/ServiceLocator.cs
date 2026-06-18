namespace OpenSpeaker.Core;
public static class ServiceLocator
{
    public static AppBootstrapper? Instance { get; private set; }

    public static void Initialize(AppBootstrapper bootstrapper)
    {
        Instance = bootstrapper;
    }
}
