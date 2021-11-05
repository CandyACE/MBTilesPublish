using System;
using BruTile;
using BruTile.MbTiles;
using SQLite;

namespace MBTilesLoader
{
    class Program
    {
        static void Main(string[] args)
        {
            var mbtiles =
                new MbTilesTileSource(
                    new SQLiteConnectionString(@"D:\Temp\download\影像下载_2107081531\影像下载_2107081531.mbtiles"));
            Console.WriteLine(mbtiles);
            Console.WriteLine(mbtiles.Description);
            var tileinfo = new TileInfo();
            tileinfo.Index = new TileIndex()
            Console.WriteLine(mbtiles.GetTile());
            Console.ReadKey();
        }
    }
}
