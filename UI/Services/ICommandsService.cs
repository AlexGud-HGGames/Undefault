namespace UI.Services;

public interface ICommandsService
{
    void StartGsiHost();
    void StopGsiHost();
    void ConnectSpotify();
    void SwitchProfile(string profileName);
}
