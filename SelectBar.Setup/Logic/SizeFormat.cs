using System;
using System.Collections.Generic;
using System.Globalization;

namespace SelectBar.Setup.Logic;

public static class SizeFormat
{
    private const long MiB = 1048576;
    private const long GiB = 1073741824;

    /// <summary>« 12 Mo », « 1,4 Go » (virgule en français, point en anglais), « absent » si le dossier n'existe pas.</summary>
    public static string Format(long bytes, bool exists, string lang)
    {
        var fr = lang != "en";
        if (!exists) return fr ? "absent" : "not present";
        if (bytes < 0) bytes = 0;
        if (bytes >= GiB)
        {
            var tenths = bytes * 10 / GiB;
            var sep = fr ? "," : ".";
            return (tenths / 10).ToString(CultureInfo.InvariantCulture) + sep + (tenths % 10).ToString(CultureInfo.InvariantCulture) + (fr ? " Go" : " GB");
        }
        if (bytes > 0 && bytes < MiB) return fr ? "< 1 Mo" : "< 1 MB";
        return (bytes / MiB).ToString(CultureInfo.InvariantCulture) + (fr ? " Mo" : " MB");
    }

    /// <summary>Valeur EstimatedSize du registre : en Ko, arrondie au-dessus, bornée à Int32.</summary>
    public static int EstimatedSizeKb(long bytes)
    {
        if (bytes <= 0) return 0;
        var kb = bytes / 1024 + (bytes % 1024 > 0 ? 1 : 0);
        return kb > int.MaxValue ? int.MaxValue : (int)kb;
    }

    public static long Total(IEnumerable<long> sizes)
    {
        long total = 0;
        foreach (var s in sizes) if (s > 0) total += s;
        return total;
    }
}
