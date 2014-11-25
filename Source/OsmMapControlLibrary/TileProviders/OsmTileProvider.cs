using System;
using System.Globalization;

namespace OsmMapControlLibrary.TileProviders
{
    public class OsmTileProvider : ITileProvider
    {
        public Uri GetTileUri(int zoom, int x, int y)
        {
            return new Uri(FormatUrl(zoom, x, y));
        }

        private string FormatUrl(int zoom, int x, int y)
        {
            return String.Format("http://tile.openstreetmap.org/{0}/{1}/{2}.png",
                zoom.ToString(CultureInfo.InvariantCulture),
                x.ToString(CultureInfo.InvariantCulture),
                y.ToString(CultureInfo.InvariantCulture));
        }

        //public async Task<byte[]> LoadTileAsync(int zoom, int x, int y)
        //{
        //    var url = FormatUrl(zoom, x, y);

        //    var client = new HttpClient();
        //    var response = await client.GetByteArrayAsync(url);

        //    return response;
        //}
    }
}