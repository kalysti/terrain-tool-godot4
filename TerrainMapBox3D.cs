using System.Net;
using System.Net.Cache;
using Godot;

namespace TerrainEditor;

[Tool]
public partial class TerrainMapBox3D : Terrain3D
{
    [ExportCategory("Terrain Mapbox")]
    [ExportGroup("Mapbox Data")]
    [Export]
    public string MapBoxAccessToken { get; set; } = "pk.eyJ1IjoiaGlnaGNsaWNrZXJzIiwiYSI6ImNrZHdveTAxZjQxOXoyenJvcjlldmpoejEifQ.0LKYqSO1cCQoVCWObvVB5w";

    [Export]
    public string MapBoxCachePath { get; set; } = "user://mapCache";

    protected void InitCacheFolder()
    {
        DirAccess? dir = DirAccess.Open("user://");
        if (!dir.DirExists("mapCache"))
            dir.MakeDir("mapCache");
    }

    private Image LoadImageFromBox(string filePath)
    {
        var image = new Image();
        image.Load(filePath);

        return image;
    }

    public Error LoadHeightmapFromBox(ref Image image, int x = 12558, int y = 6127, int zoomLevel = 14)
    {
        InitCacheFolder();

        string? url = "https://api.mapbox.com/v4/mapbox.terrain-rgb/" + zoomLevel + "/" + x + "/" + y + ".pngraw?access_token=" + MapBoxAccessToken;

        string? filename = zoomLevel + "_" + x + "_" + y + ".png";
        string? filePath = MapBoxCachePath + "/" + filename;

        if (FileAccess.FileExists(filePath))
        {
            image = LoadImageFromBox(filePath);
            return Error.Ok;
        }
        else
        {
            using (var client = new WebClient())
            {
                ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                ServicePointManager.ServerCertificateValidationCallback += (send, certificate, chain, sslPolicyErrors) => true;
                client.CachePolicy = new RequestCachePolicy(RequestCacheLevel.BypassCache);
                client.Headers.Add("Cache-Control", "no-cache");

                GD.Print("Download: " + url);
                byte[] data = client.DownloadData(url);
                GD.Print("Store in: " + filePath);

                FileAccess? result = FileAccess.Open(filePath, FileAccess.ModeFlags.Write);
                if (result.IsOpen())
                {
                    result.StoreBuffer(data);
                }
                else
                {
                    GD.PrintErr("Cant write file: " + result);
                    return Error.FileNotFound;
                }

                image = LoadImageFromBox(filePath);

                result.Flush();
                GD.PrintErr("Ready storing image succesfull");

                return Error.Ok;
            }
        }
    }

    public void TestGrid()
    {
        CreatePatchGrid(1, 4, 64);

        LoadTile(new Vector2i(0, 0), 62360, 48541);
        LoadTile(new Vector2i(0, 1), 62360, 48542);
        LoadTile(new Vector2i(0, 2), 62360, 48543);
        LoadTile(new Vector2i(0, 3), 62360, 48544);

        Draw();
    }

    public void LoadTile(Vector2i patch, int x = 62360, int y = 48541, int zoomLevel = 17)
    {
        var image = new Image();
        Error error = LoadHeightmapFromBox(ref image, x, y, zoomLevel);

        if (error == Error.Ok)
        {
            LoadHeightmapFromImage(patch, image, HeightmapAlgo.RGB8_FULL);
        }
    }
}