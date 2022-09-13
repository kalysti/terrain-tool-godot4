using System.Xml.Schema;
using System.IO.Compression;
using System.IO;
using System.ComponentModel;
using System.Collections.Generic;
using Godot;
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Godot.Collections;
using File = Godot.File;

namespace TerrainEditor
{
    [Tool]
    public partial class TerrainMapBox3D : Terrain3D
    {
        [ExportCategory("Terrain Mapbox")]
        [ExportGroup("Mapbox Data")]
        [Export]
        public string mapBoxAccessToken = "pk.eyJ1IjoiaGlnaGNsaWNrZXJzIiwiYSI6ImNrZHdveTAxZjQxOXoyenJvcjlldmpoejEifQ.0LKYqSO1cCQoVCWObvVB5w";
        [Export]
        public string mapBoxCachePath = "user://mapCache";

        protected void initCacheFolder()
        {
            var dir = new Godot.Directory();
            if (!dir.DirExists(mapBoxCachePath))
            {
                dir.Open("user://");
                dir.MakeDir("mapCache");
            }
        }
        private Image loadImageFromBox(string filePath)
        {
            var image = new Image();
            image.Load(filePath);

            return image;
        }

        public Error loadHeightmapFromBox(ref Image image, int x = 12558, int y = 6127, int zoomLevel = 14)
        {
            initCacheFolder();

            var url = "https://api.mapbox.com/v4/mapbox.terrain-rgb/" + zoomLevel + "/" + x + "/" + y + ".pngraw?access_token=" + mapBoxAccessToken;

            var filename = zoomLevel + "_" + x + "_" + y + ".png";
            var filePath = mapBoxCachePath + "/" + filename;

            if (File.FileExists(filePath))
            {
                image = loadImageFromBox(filePath);
                return Error.Ok;
            }
            else
            {
                using (var client = new WebClient())
                {
                    ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072;
                    ServicePointManager.ServerCertificateValidationCallback += (send, certificate, chain, sslPolicyErrors) => { return true; };
                    client.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.BypassCache);
                    client.Headers.Add("Cache-Control", "no-cache");

                    GD.Print("Download: " + url);
                    byte[] data = client.DownloadData(url);
                    GD.Print("Store in: " + filePath);

                    var file = new Godot.File();
                    var result = file.Open(filePath, Godot.File.ModeFlags.Write);
                    if (result == Error.Ok)
                    {
                        file.StoreBuffer(data);
                    }
                    else
                    {
                        GD.PrintErr("Cant write file: " + result);
                        return Error.FileNotFound;
                    }

                    image = loadImageFromBox(filePath);

                    file.Close();
                    GD.PrintErr("Ready storing image succesfull");

                    return Error.Ok;
                }
            }
        }

        public void testGrid()
        {
            this.CreatePatchGrid(1, 4, 64);

            loadTile(new Vector2i(0, 0), 62360, 48541);
            loadTile(new Vector2i(0, 1), 62360, 48542);
            loadTile(new Vector2i(0, 2), 62360, 48543);
            loadTile(new Vector2i(0, 3), 62360, 48544);

            this.Draw();

        }

        public void loadTile(Vector2i patch, int x = 62360, int y = 48541, int zoomLevel = 17)
        {
            var image = new Image();
            var error = loadHeightmapFromBox(ref image, x, y, zoomLevel);

            if (error == Error.Ok)
            {
                loadHeightmapFromImage(patch, image, HeightmapAlgo.RGB8_Full);
            }
        }
    }
}
