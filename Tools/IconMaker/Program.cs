using System.Drawing;
using System.Drawing.Imaging;

if (args.Length < 2)
{
    Console.WriteLine("Usage: IconMaker <input.png> <output.ico>");
    return;
}

using var source = new Bitmap(args[0]);

int[] sizes = { 16, 24, 32, 48, 64, 128, 256 };

using var output = File.Create(args[1]);
using var writer = new BinaryWriter(output);

// ICO header
writer.Write((ushort)0);
writer.Write((ushort)1);
writer.Write((ushort)sizes.Length);

var images = new List<byte[]>();

foreach (int size in sizes)
{
    using var resized = new Bitmap(size, size);

    using (var g = Graphics.FromImage(resized))
    {
        g.Clear(Color.Transparent);
        g.InterpolationMode =
            System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        g.SmoothingMode =
            System.Drawing.Drawing2D.SmoothingMode.HighQuality;
        g.PixelOffsetMode =
            System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

        g.DrawImage(source, 0, 0, size, size);
    }

    using var ms = new MemoryStream();
    resized.Save(ms, ImageFormat.Png);
    images.Add(ms.ToArray());
}

int offset = 6 + (16 * images.Count);

for (int i = 0; i < images.Count; i++)
{
    int size = sizes[i];
    byte[] data = images[i];

    writer.Write((byte)(size >= 256 ? 0 : size));
    writer.Write((byte)(size >= 256 ? 0 : size));
    writer.Write((byte)0);
    writer.Write((byte)0);
    writer.Write((ushort)1);
    writer.Write((ushort)32);
    writer.Write((uint)data.Length);
    writer.Write((uint)offset);

    offset += data.Length;
}

foreach (byte[] data in images)
{
    writer.Write(data);
}

Console.WriteLine($"Created: {args[1]}");
