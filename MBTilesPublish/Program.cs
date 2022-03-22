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
using SQLite;

namespace MBTilesPublish
{
    class Program
    {
        private static IWebHost host;
        private static MbTilesTileSource conn;
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
                new Option<int>(new string[]{"--port","-p"},getDefaultValue: () => 8080,"Set Server Port."),
                new Option<bool>(new string[]{"--log","-l"},"Log to console."),
            };

            rootCommand.Handler = CommandHandler.Create<string, int, bool>((string url, int port, bool log) =>
             {
                 isLog = log;

                 var backgroundColor = Console.BackgroundColor;
                 var foregroundColor = Console.ForegroundColor;

                 host = new WebHostBuilder().UseKestrel((options) =>
                 {
                     options.Listen(IPAddress.Any, port, listenOptions => { });
                 }).Configure(app =>
                 {
                     app.Run(ProcessAsync);
                 }).Build();

                 // DB
                 if (!File.Exists(url))
                 {
                     throw new FileNotFoundException(url + " is not Found!");
                 }
                 conn = new MbTilesTileSource(new SQLiteConnectionString(url));

                 Console.ForegroundColor = ConsoleColor.Yellow;
                 Console.WriteLine(banner);
                 GetDataSourceInfo();

                 Console.WriteLine("Server on:: " + port + "\r\nThe Map Server is like: http://127.0.0.1:" + port + "/GetTile?x={x}&y={y}&z={z}\r\n");
                 Console.ForegroundColor = foregroundColor;

                 host.Run();
             });

            rootCommand.InvokeAsync(args);
        }

        private static async void GetDataSourceInfo()
        {
            Console.WriteLine();
            Console.WriteLine("Description:\t{0}", conn.Description);
            Console.WriteLine("Version:\t{0}", conn.Version);
            Console.WriteLine("Attribution:\t{0}", conn.Attribution.Text);
            Console.WriteLine("Type:\t{0}", conn.Type);
            Console.WriteLine("Format:\t{0}", conn.Schema.Format);
            Console.WriteLine("SRS:\t{0}", conn.Schema.Srs);
            Console.WriteLine("YAxis:\t{0}", conn.Schema.YAxis);
            Console.WriteLine("Size:\t{0}*{1}", conn.Schema.GetTileWidth(0), conn.Schema.GetTileHeight(0));
            Console.WriteLine();
        }

        private static async Task ProcessAsync(HttpContext context)
        {
            context.Response.ContentType = "application/text";
            context.Response.Headers["Access-Control-Allow-Origin"] = "*";
            context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST";
            context.Response.Headers["Access-Control_Allow-Headers"] = "Content-Type";

            if ("/GetTile".Equals(context.Request.Path.Value))
            {
                var z = Int32.Parse(context.Request.Query["z"]);
                var x = Int32.Parse(context.Request.Query["x"]);
                var y = Int32.Parse(context.Request.Query["y"]);

                var data = conn.GetTile(new TileInfo() { Index = new TileIndex(x, y, z) });
                if (data == null)
                {
                    context.Response.StatusCode = 404;
                    await context.Response.WriteAsync("No Found This Tile.");
                    LogMessage(context, $"[{x},{y},{z}] No Found This Tile.");
                    return;
                }

                context.Response.ContentType = mimeDictionary[conn.Schema.Format.ToLower()];
                await context.Response.Body.WriteAsync(data, 0, data.Length);
            }
            //else if ("/GetMap".Equals(context.Request.Path.Value))
            //{

            //}
            else
            {
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync("The path is not reuqired.");
                LogMessage(context, $"[{context.Request.Path.Value} is not require]");
                return;
            }
        }

        /// <summary>
        /// 输出日志
        /// </summary>
        /// <param name="message"></param>
        private static void LogMessage(HttpContext content, string message)
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
        };
    }
}
