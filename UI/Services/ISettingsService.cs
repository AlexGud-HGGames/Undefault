using UI.Models;

namespace UI.Services;

public interface ISettingsService
{
    UiSettings Load();
    void Save(UiSettings settings);
}
