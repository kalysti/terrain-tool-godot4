using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Godot;
using HttpClient = System.Net.Http.HttpClient;

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

    protected static void InitCacheFolder()
    {
        try
        {
            DirAccess? dir = DirAccess.Open("user://");
            if (!dir.DirExists("mapCache"))
                dir.MakeDir("mapCache");
        }
        catch (Exception e)
        {
            GD.PrintErr(e);
        }
    }

    public async Task<Error> LoadHeightmapFromBox(int x = 12558, int y = 6127, int zoomLevel = 14)
    {
        try
        {
            InitCacheFolder();

            string url = $"https://api.mapbox.com/v4/mapbox.terrain-rgb/{zoomLevel}/{x}/{y}.pngraw?access_token={MapBoxAccessToken}";

            string filePath = $"{MapBoxCachePath}/{zoomLevel}_{x}_{y}.png";

            if (FileAccess.FileExists(filePath))
            {
                return Error.Ok;
            }


            using (var client = new HttpClient()) //TODO: check if Godot HttpClient is better.
            {
                // ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                // ServicePointManager.ServerCertificateValidationCallback += (send, certificate, chain, sslPolicyErrors) => true;
                // client.CachePolicy = new RequestCachePolicy(RequestCacheLevel.BypassCache); //why do we request 
                // client.Headers.Add("Cache-Control", "no-cache");

                GD.Print($"Download: {url}");
                HttpResponseMessage responseMessage = await client.GetAsync(url);

                if (responseMessage.StatusCode == HttpStatusCode.OK)
                {
                    GD.Print($"Store in: {filePath}");

                    using (FileAccess? result = FileAccess.Open(filePath, FileAccess.ModeFlags.Write))
                    {
                        if (result.IsOpen())
                        {
                            result.StoreBuffer(await responseMessage.Content.ReadAsByteArrayAsync());
                        }
                        else
                        {
                            GD.PrintErr($"Cant write file: {result}");
                            return Error.FileNotFound;
                        }

                        result.Flush();
                    }

                    GD.PrintErr("Done storing image successfully");

                    return Error.Ok;
                }


                GD.PrintErr($"Request failed: {responseMessage}");
                return Error.Failed;
            }
        }
        catch (Exception e)
        {
            GD.PrintErr(e);
        }

        return Error.Bug;
    }

    public void TestGrid()
    {
        try
        {
            CreatePatchGrid(1, 4, 64);

            LoadTile(new Vector2I(0, 0), 62360, 48541);
            LoadTile(new Vector2I(0, 1), 62360, 48542);
            LoadTile(new Vector2I(0, 2), 62360, 48543);
            LoadTile(new Vector2I(0, 3), 62360, 48544);

            Draw();
        }
        catch (Exception e)
        {
            GD.PrintErr(e);
        }
    }

    public void LoadTile(Vector2I patch, int x = 62360, int y = 48541, int zoomLevel = 17)
    {
        try
        {
            Task.Run(async () =>
            {
                string filePath = $"{MapBoxCachePath}/{zoomLevel}_{x}_{y}.png";
                Error error = await LoadHeightmapFromBox(x, y, zoomLevel);
                if (error == Error.Ok)
                {
                    Image? image = Image.LoadFromFile(filePath);
                    LoadHeightmapFromImage(patch, image, HeightmapAlgo.RGB8_FULL);
                }
            });
        }
        catch (Exception e)
        {
            GD.PrintErr(e);
        }
    }
}