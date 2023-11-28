using System.IO;
using System;
using System.Text.Json.Nodes;

namespace craftsim;

public class Config
{
    public JsonObject TransitionDB = new();

    public void LoadFromFile(FileInfo file)
    {
        try
        {
            var contents = File.ReadAllText(file.FullName);
            var json = JsonNode.Parse(contents) as JsonObject;
            //var version = (int?)json?["Version"] ?? 0;
            var payload = json?["Payload"] as JsonObject;
            if (payload != null)
            {
                TransitionDB = payload["TransitionDB"] as JsonObject ?? new();
            }
        }
        catch (Exception e)
        {
            Service.Log.Error($"Failed to load config from {file.FullName}: {e}");
        }
    }

    public void SaveToFile(FileInfo file)
    {
        try
        {
            JsonObject payload = new();
            payload["TransitionDB"] = TransitionDB;
            JsonObject jContents = new()
            {
                { "Payload", payload }
            };
            File.WriteAllText(file.FullName, jContents.ToString());
        }
        catch (Exception e)
        {
            Service.Log.Error($"Failed to save config to {file.FullName}: {e}");
        }
    }
}
