using System.Text.Json;
using System.IO;
using System.Collections.Generic;

public class SettingsManager
{
    private const string SettingsFilePath = "UserSettings.json";

    public static void SaveSettings(UserSettings settings)
    {
        string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsFilePath, json);
    }

    public static List<string> GetProjectPaths()
    {
        var settings = LoadSettings();
        var projectPaths = new List<string>();

        foreach (var project in settings.Projects)
        {
            projectPaths.Add(project.Path);
        }

        return projectPaths;
    }



    public static UserSettings LoadSettings()
    {
        if (File.Exists(SettingsFilePath))
        {
            string json = File.ReadAllText(SettingsFilePath);
            return JsonSerializer.Deserialize<UserSettings>(json);
        }
        return new UserSettings();
    }


}
