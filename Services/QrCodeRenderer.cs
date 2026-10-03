using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml.Media.Imaging;
using QRCoder;

namespace QuickPlaces.Services;

/// <summary>Draws a QR code into a bitmap: black modules on white, so it scans in dark mode too.</summary>
internal static class QrCodeRenderer
{
    private const int PixelsPerModule = 8;

    public static WriteableBitmap Render(string text)
    {
        using QRCodeGenerator generator = new();
        using QRCodeData data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.M);
        var matrix = data.ModuleMatrix; // includes the quiet zone
        int modules = matrix.Count;
        int size = modules * PixelsPerModule;

        byte[] pixels = new byte[size * size * 4];
        Array.Fill(pixels, (byte)255);
        for (int my = 0; my < modules; my++)
        {
            for (int mx = 0; mx < modules; mx++)
            {
                if (!matrix[my][mx])
                {
                    continue;
                }

                for (int y = my * PixelsPerModule; y < (my + 1) * PixelsPerModule; y++)
                {
                    int row = y * size * 4;
                    for (int x = mx * PixelsPerModule; x < (mx + 1) * PixelsPerModule; x++)
                    {
                        int i = row + x * 4;
                        pixels[i] = pixels[i + 1] = pixels[i + 2] = 0; // B, G, R; alpha stays 255
                    }
                }
            }
        }

        WriteableBitmap bitmap = new(size, size);
        pixels.CopyTo(0, bitmap.PixelBuffer, 0, pixels.Length);
        bitmap.Invalidate();
        return bitmap;
    }
}
