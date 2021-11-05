using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;

namespace MBTilesPublish
{
    class Program
    {
        private static IWebHost host;
        private static SqliteConnection conn;

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
        };

            rootCommand.Handler = CommandHandler.Create<string, int>((string url, int port) =>
            {
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
                conn = new SqliteConnection("Data Source=" + url);
                conn.Open();

                Console.WriteLine(banner);
                Console.WriteLine("Server on:: " + port + "\r\nThe Map Server is like: http://127.0.0.1:" + port + "/{z}/{x}/{y}.png\r\n");

                host.Run();
            });

            rootCommand.InvokeAsync(args);
        }

        private static async void GetDataSourceInfo()
        {

        }

        private static async Task ProcessAsync(HttpContext context)
        {
            context.Response.ContentType = "image/png";
            context.Response.Headers["Access-Control-Allow-Origin"] = "*";
            context.Response.Headers["Access-Control-Allow-Methods"] = "GET, POST";
            context.Response.Headers["Access-Control_Allow-Headers"] = "Content-Type";

            string[] keys = context.Request.Path.Value?.Split("/");
            if (keys.Length <= 0)
            {
                context.Response.StatusCode = 404;
                context.Response.WriteAsync("");
                return;
            }

            var z = keys[1];
            var x = keys[2];
            var y = keys[3].Split('.')[0];

            var command = conn.CreateCommand();
            command.CommandText = @"Select tile_data from tiles where zoom_level=$z and tile_column=$x and tile_row=$y";
            command.Parameters.AddWithValue("$z", z);
            command.Parameters.AddWithValue("$x", x);
            command.Parameters.AddWithValue("$y", y);

            using (var reader = command.ExecuteReader())
            {
                reader.Read();
                var data = reader.GetStream(0);
                const int bufferSize = 1 << 10;
                var buffer = new byte[bufferSize];
                while (true)
                {
                    var bytesRead = await data.ReadAsync(buffer, 0, bufferSize);
                    if (bytesRead == 0) break;
                    await context.Response.Body.WriteAsync(buffer, 0, bytesRead);
                }

                await context.Response.Body.FlushAsync();
            }
        }
    }
}
