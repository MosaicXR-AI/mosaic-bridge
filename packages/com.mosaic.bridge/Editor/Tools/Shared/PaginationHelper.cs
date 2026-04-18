using System;
using System.Collections.Generic;
using System.Text;

namespace Mosaic.Bridge.Tools.Shared
{
    public static class PaginationHelper
    {
        public const int DefaultPageSize = 50;
        public const int MaxPageSize = 200;

        public static int DecodeOffset(string pageToken)
        {
            if (string.IsNullOrEmpty(pageToken)) return 0;
            try
            {
                var bytes = Convert.FromBase64String(pageToken);
                var str = Encoding.UTF8.GetString(bytes);
                return int.TryParse(str, out var offset) ? Math.Max(0, offset) : 0;
            }
            catch { return 0; }
        }

        public static string EncodeOffset(int offset)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(offset.ToString()));
        }

        public static int ClampPageSize(int requested)
        {
            if (requested <= 0) return DefaultPageSize;
            return Math.Min(requested, MaxPageSize);
        }

        public static (List<T> page, string nextPageToken) Paginate<T>(
            List<T> allResults, int offset, int pageSize)
        {
            var clamped = ClampPageSize(pageSize);
            var page = new List<T>();
            for (int i = offset; i < Math.Min(offset + clamped, allResults.Count); i++)
                page.Add(allResults[i]);

            string nextToken = null;
            if (offset + clamped < allResults.Count)
                nextToken = EncodeOffset(offset + clamped);

            return (page, nextToken);
        }
    }
}
