using TrayApp.Shared.Models;

namespace TrayApp.Shared.Interfaces;

public interface IConfigurationService
{
    AppConfiguration Load();
    void Save(AppConfiguration configuration);
}
