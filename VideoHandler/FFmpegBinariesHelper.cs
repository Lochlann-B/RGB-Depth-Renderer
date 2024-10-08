﻿using System.Runtime.InteropServices;
using DynamicallyLoadedBindings = FFmpeg.AutoGen.Bindings.DynamicallyLoaded.DynamicallyLoadedBindings;

namespace VideoHandler;

public class FFmpegBinariesHelper
{
    // Code from Ruslan B: https://github.com/Ruslan-B/FFmpeg.AutoGen/tree/master, obtained under the GNU lesser general public license.
    public static void RegisterFFmpegBinaries()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var current = Environment.CurrentDirectory;
            var probe = Path.Combine("FFmpeg", "bin", Environment.Is64BitProcess ? "x64" : "x86");

            while (current != null)
            {
                var ffmpegBinaryPath = Path.Combine(current, probe);

                if (Directory.Exists(ffmpegBinaryPath))
                {
                    Console.WriteLine($"FFmpeg binaries found in: {ffmpegBinaryPath}");
                    DynamicallyLoadedBindings.LibrariesPath = ffmpegBinaryPath;
                    return;
                }

                current = Directory.GetParent(current)?.FullName;
            }
        }
        else
            throw new NotSupportedException();
    }
}