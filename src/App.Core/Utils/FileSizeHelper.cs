namespace Core.Utils;

public class FileSizeHelper
{
    private static readonly string[] SizeUnits = ["B", "KB", "MB", "GB", "TB", "PB", "EB"];

    public static string BytesToSize(long byteCount)
    {
        if (byteCount == 0)
        {
            return "0 B";
        } 
        var step = (int)Math.Truncate(Math.Log(byteCount, 1024L));
        if (step > SizeUnits.Length)
            step = SizeUnits.Length - 1;
        var v = byteCount / Math.Pow(1024L, step);
        v = Math.Round(v, 2);
        return $"{v}{SizeUnits[step]}";
    }
}