namespace UI.Models;

public record UiSettings(
    string SelectedGame,
    string ActiveProfile,
    bool AutoStart,
    bool MinimizeToTray
);
