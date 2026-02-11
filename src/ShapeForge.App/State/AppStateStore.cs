using System.Text.Json;

namespace ShapeForge.App.State;

public sealed class AppStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _path;

    public AppStateStore()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var folder = Path.Combine(appData, "ShapeForge");
        Directory.CreateDirectory(folder);
        _path = Path.Combine(folder, "app-state.json");
    }

    public AppUiState Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new AppUiState();
            }

            var content = File.ReadAllText(_path);
            return JsonSerializer.Deserialize<AppUiState>(content, JsonOptions) ?? new AppUiState();
        }
        catch
        {
            return new AppUiState();
        }
    }

    public void Save(AppUiState state)
    {
        var content = JsonSerializer.Serialize(state, JsonOptions);
        File.WriteAllText(_path, content);
    }
}
