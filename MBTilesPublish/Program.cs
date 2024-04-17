using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using BruTile;
using BruTile.MbTiles;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SQLite;

namespace MBTilesPublish
{
    class MbTilesItem
    {
        public MbTilesItem(string url)
        {
            this.name = Path.GetFileNameWithoutExtension(url);
            this.db = new MbTilesTileSource(new SQLiteConnectionString(url));
        }

        public string name { get; }
        public MbTilesTileSource db { get; }
    }

    class Program
    {
        private static List<MbTilesItem> conns = new List<MbTilesItem>();
        private static Boolean isLog;

        private static string banner = @"
  ________________                          
 /_  __/ ___/ ___/___  ______   _____  _____
  / /  \__ \\__ \/ _ \/ ___/ | / / _ \/ ___/
 / /  ___/ /__/ /  __/ /   | |/ /  __/ /    
/_/  /____/____/\___/_/    |___/\___/_/     
                                            
";

        static void Main(string[] args)
        {
            var rootCommand = new RootCommand
            {
                new Argument<string>("url","The MBTiles Path."),
                new Option<string>(new string[]{"--ports","-p"}, getDefaultValue: () => "8080", "Set Server Ports."),
                new Option<bool>(new string[]{"--log","-l"},"Log to console."),
                new Option<bool>(new string[]{"--folder","-f"}, "Indicates that Url is a folder."),
            };

            rootCommand.Handler = CommandHandler.Create<string, string, bool, bool>((string url, string ports, bool log, bool folder) =>
             {
                 isLog = log;

                 var backgroundColor = Console.BackgroundColor;
                 var foregroundColor = Console.ForegroundColor;

                 var builder = WebApplication.CreateBuilder();
                 builder.Logging.SetMinimumLevel(LogLevel.Warning);

                 var app = builder.Build();
                 app.Use((context, next) =>
                 {
                     context.Response.ContentType = "application/text";
                     context.Response.Headers["Access-Control-Allow-Origin"] = "*";
                     context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST";
                     context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
                     return next(context);
                 });
                 app.UseRouting();

                 var ps = ports.Split(",");
                 foreach (var port in ps)
                 {
                     app.Urls.Add($"http://0.0.0.0:{port}");
                 }

                 Console.ForegroundColor = ConsoleColor.Yellow;
                 Console.WriteLine(banner);

                 // DB
                 if (folder)
                 {
                     if (!Directory.Exists(url))
                     {
                         throw new DirectoryNotFoundException(url + " is not Found!");
                     }
                     DirectoryInfo info = new DirectoryInfo(url);
                     var files = info.GetFiles("*.mbtiles");
                     foreach (var fileInfo in files)
                     {
                         conns.Add(new MbTilesItem(fileInfo.FullName));
                         Console.WriteLine($"{Path.GetFileNameWithoutExtension(fileInfo.FullName)} loaded!");
                     }
                 }
                 else
                 {
                     if (!File.Exists(url))
                     {
                         throw new FileNotFoundException(url + " is not Found!");
                     }
                     conns.Add(new MbTilesItem(url));
                     Console.WriteLine($"{Path.GetFileNameWithoutExtension(url)} loaded!");
                 }


                 //GetDataSourceInfo();

                 Console.WriteLine(Environment.NewLine + "Server on:: " + string.Join(",", ports) + Environment.NewLine);

                 Console.ForegroundColor = ConsoleColor.White;
                 foreach (var con in conns)
                 {
                     foreach (var port in ps)
                     {
                         Console.WriteLine($"You can get the meta like http://127.0.0.1:{port}/{Path.GetFileNameWithoutExtension(con.name)}/meta");
                         Console.WriteLine($"You can get the tile like http://127.0.0.1:{port}/{Path.GetFileNameWithoutExtension(con.name)}/" + "{z}/{x}/{y}");
                     }
                 }
                 Console.ForegroundColor = foregroundColor;

                 app.MapGet("/{mbtiles_name}/{z:int}/{x:int}/{y:int}", GetTile);
                 app.MapGet("/{mbtiles_name}/meta", GetMeta);

                 app.Run();
             });

            rootCommand.InvokeAsync(args);
        }

        private static IResult GetMeta(string mbtiles_name)
        {
            var mb = conns.Find(item => item.name == mbtiles_name);
            if (mb != null)
            {
                return Results.Json(new
                {
                    code = 200,
                    data = new
                    {
                        Description = mb.db.Description,
                        Version = mb.db.Version,
                        Attribution = mb.db.Attribution.Text,
                        Type = mb.db.Type,
                        Format = mb.db.Schema.Format,
                        SRS = mb.db.Schema.Srs,
                        YAxis = mb.db.Schema.YAxis,
                        Size = $"{mb.db.Schema.GetTileWidth(0)}*{mb.db.Schema.GetTileHeight(0)}"
                    }
                });
            }

            LogMessage($"{mbtiles_name} is not found");
            return Results.NotFound();
        }

        private static IResult GetTile(string mbtiles_name, int z, int x, int y, HttpContext context)
        {
            var mb = conns.Find(item => item.name == mbtiles_name);
            if (mb != null)
            {
                var row = (int)Math.Pow(2, z) - 1 - y;
                var data = mb.db.GetTile(new TileInfo() { Index = new TileIndex(x, row, z) });
                if (data != null)
                {
                    return Results.Bytes(data, mimeDictionary[mb.db.Schema.Format.ToLower()]);
                }
                LogMessage($"[{x},{y},{z}] No Found This Tile.");
                return Results.NotFound();
            }
            LogMessage($"{mbtiles_name} is not found");
            return Results.NotFound();
        }

        /// <summary>
        /// 输出日志
        /// </summary>
        /// <param name="message"></param>
        private static void LogMessage(string message)
        {
            if (!isLog) return;

            var time = DateTime.Now;
            var foregroundColor = Console.ForegroundColor;
            var backgroundColor = Console.BackgroundColor;

            Console.BackgroundColor = ConsoleColor.Magenta;
            Console.Write($"[{time.ToShortDateString()} {time.ToLongTimeString()}]");
            Console.BackgroundColor = backgroundColor;
            Console.WriteLine(message);
        }

        private static Dictionary<string, string> mimeDictionary = new Dictionary<string, string>()
        {
            {"png","image/png" },
            {"jpeg","image/jpeg" },
            {"jpg","image/jpg" },
            {"pbf","application/x-protobuf"}
        };
    }
}
