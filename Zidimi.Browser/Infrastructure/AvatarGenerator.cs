using System.Collections.Concurrent;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Zidimi.Browser.Infrastructure;

/// <summary>
/// Presents Chromium's native profile avatar when Chromium has created <c>avatar.ico</c>.
/// Zidimi never creates or rewrites that file. If the current Chromium profile has no native avatar
/// image, a deterministic vector is generated only in memory as a presentation fallback.
/// </summary>
public static class AvatarGenerator
{
    private const double CircleRadius = 12.0;

    private static readonly string[] Paths = {
        "M12,2A2,2 0 0,1 14,4C14,4.74 13.6,5.39 13,5.73V7H14A7,7 0 0,1 21,14H22A1,1 0 0,1 23,15V18A1,1 0 0,1 22,19H21V20A2,2 0 0,1 19,22H5A2,2 0 0,1 3,20V19H2A1,1 0 0,1 1,18V15A1,1 0 0,1 2,14H3A7,7 0 0,1 10,7H11V5.73C10.4,5.39 10,4.74 10,4A2,2 0 0,1 12,2M7.5,13A2.5,2.5 0 0,0 5,15.5A2.5,2.5 0 0,0 7.5,18A2.5,2.5 0 0,0 10,15.5A2.5,2.5 0 0,0 7.5,13M16.5,13A2.5,2.5 0 0,0 14,15.5A2.5,2.5 0 0,0 16.5,18A2.5,2.5 0 0,0 19,15.5A2.5,2.5 0 0,0 16.5,13Z",
        "M12,2A9,9 0 0,1 21,11V21L18,19L15,21L12,19L9,21L6,19L3,21V11A9,9 0 0,1 12,2M9,8A2,2 0 0,0 7,10A2,2 0 0,0 9,12A2,2 0 0,0 11,10A2,2 0 0,0 9,8M15,8A2,2 0 0,0 13,10A2,2 0 0,0 15,12A2,2 0 0,0 17,10A2,2 0 0,0 15,8Z",
        "M12,8L10.67,8.09C9.81,7.07 7.4,4.5 5,4.5C5,7 5.86,9.58 6.53,10.64C5.58,11.83 5,13.34 5,15C5,18.87 8.13,22 12,22C15.87,22 19,18.87 19,15C19,13.34 18.42,11.83 17.47,10.64C18.14,9.58 19,7 19,4.5C16.6,4.5 14.19,7.07 13.33,8.09L12,8M9,14A1.5,1.5 0 0,1 10.5,15.5A1.5,1.5 0 0,1 9,17A1.5,1.5 0 0,1 7.5,15.5A1.5,1.5 0 0,1 9,14M15,14A1.5,1.5 0 0,1 16.5,15.5A1.5,1.5 0 0,1 15,17A1.5,1.5 0 0,1 13.5,15.5A1.5,1.5 0 0,1 15,14Z",
        "M11,19V17H13V19M14,14V16H10V14M9,13H15L15,11H9M5,11C4.45,11 4,10.55 4,10C4,9.45 4.45,9 5,9C5.55,9 6,9.45 6,10C6,10.55 5.55,11 5,11M19,11C18.45,11 18,10.55 18,10C18,9.45 18.45,9 19,9C19.55,9 20,9.45 20,10C20,10.55 19.55,11 19,11M21.5,9.5C21.5,11.23 20.35,12.7 18.8,13.25L17,14V17A1,1 0 0,1 16,18H15V21H9V18H8A1,1 0 0,1 7,17V14L5.2,13.25C3.65,12.7 2.5,11.23 2.5,9.5C2.5,7.57 4.07,6 6,6H6.18L8.6,4.19A3,3 0 0,1 10.4,3.5H13.6A3,3 0 0,1 15.4,4.19L17.82,6H18C19.93,6 21.5,7.57 21.5,9.5Z",
        "M12,2C8,2 4,5 4,9C4,11 5.4,12.5 5.4,12.5C5.4,12.5 5,14.5 5.8,16.2C6.1,16.8 6.4,17.2 6.8,17.6L5.5,21L8,20.8C9,21.5 10.4,22 12,22C13.6,22 15,21.5 16,20.8L18.5,21L17.2,17.6C17.6,17.2 17.9,16.8 18.2,16.2C19,14.5 18.6,12.5 18.6,12.5C18.6,12.5 20,11 20,9C20,5 16,2 12,2M10,8.5A2,2 0 0,1 12,10.5A2,2 0 0,1 10,12.5A2,2 0 0,1 8,10.5A2,2 0 0,1 10,8.5M14,8.5A2,2 0 0,1 16,10.5A2,2 0 0,1 14,12.5A2,2 0 0,1 12,10.5A2,2 0 0,1 14,8.5Z",
        "M12,2A10,10 0 0,1 22,12A10,10 0 0,1 12,22A10,10 0 0,1 2,12A10,10 0 0,1 12,2M12,10C9.64,10 7.5,11.3 6.36,13.25C7,14.7 8.35,15.82 10,16.34V15.5A1.5,1.5 0 0,1 11.5,14H12.5A1.5,1.5 0 0,1 14,15.5V16.34C15.65,15.82 17,14.7 17.64,13.25C16.5,11.3 14.36,10 12,10M9,11.5A1.5,1.5 0 0,0 7.5,13A1.5,1.5 0 0,0 9,14.5A1.5,1.5 0 0,0 10.5,13A1.5,1.5 0 0,0 9,11.5M15,11.5A1.5,1.5 0 0,0 13.5,13A1.5,1.5 0 0,0 15,14.5A1.5,1.5 0 0,0 16.5,13A1.5,1.5 0 0,0 15,11.5Z"
    };

    private static readonly Color[] Colors = {
        Color.FromRgb(239, 68, 68), Color.FromRgb(249, 115, 22),
        Color.FromRgb(234, 179, 8), Color.FromRgb(34, 197, 94),
        Color.FromRgb(59, 130, 246), Color.FromRgb(168, 85, 247),
        Color.FromRgb(236, 72, 153), Color.FromRgb(14, 165, 233)
    };

    private static readonly ConcurrentDictionary<string, ImageSource> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static ImageSource CreateImageSource(string profileId, int size = 128)
    {
        var id = UserDataPaths.NormalizeProfileId(profileId);
        var nativePath = Path.Combine(UserDataPaths.ProfileDir(id), "avatar.ico");
        if (File.Exists(nativePath))
        {
            try
            {
                var stamp = File.GetLastWriteTimeUtc(nativePath).Ticks;
                return Cache.GetOrAdd($"native|{id}|{stamp}", _ => LoadNativeAvatar(nativePath));
            }
            catch (Exception ex)
            {
                AppLogger.Log("ProfileAvatar", ex, $"Reading Chromium avatar for '{id}'.");
            }
        }

        return Cache.GetOrAdd($"fallback|{id}", _ => CreateCore(id));
    }

    private static ImageSource LoadNativeAvatar(string path)
    {
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        var frame = decoder.Frames
            .OrderByDescending(x => x.PixelWidth * x.PixelHeight)
            .FirstOrDefault() ?? throw new InvalidDataException("Chromium avatar.ico contains no image frame.");
        frame.Freeze();
        return frame;
    }

    private static ImageSource CreateCore(string profileId)
    {
        var hash = StableHash(profileId);
        var pathIndex = (int)(hash % (uint)Paths.Length);
        var colorIndex = (int)((hash / (uint)Paths.Length) % (uint)Colors.Length);

        var iconGeometry = Geometry.Parse(Paths[pathIndex]);
        iconGeometry.Freeze();

        var drawingGroup = new DrawingGroup();
        drawingGroup.Children.Add(new GeometryDrawing(
            new SolidColorBrush(Colors[colorIndex]),
            null,
            new EllipseGeometry(new System.Windows.Point(CircleRadius, CircleRadius), CircleRadius, CircleRadius)));
        drawingGroup.Children.Add(new GeometryDrawing(Brushes.White, null, iconGeometry));
        drawingGroup.ClipGeometry = new EllipseGeometry(new System.Windows.Point(CircleRadius, CircleRadius), CircleRadius, CircleRadius);
        drawingGroup.Freeze();

        var image = new DrawingImage(drawingGroup);
        image.Freeze();
        return image;
    }

    private static uint StableHash(string? value)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var ch in value ?? string.Empty)
            {
                hash ^= char.ToUpperInvariant(ch);
                hash *= 16777619;
            }
            return hash;
        }
    }
}
