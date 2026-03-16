using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace OpenSoundcoreWindows;

internal static class OpenScq30Native
{
    static OpenScq30Native()
    {
        NativeLibrary.SetDllImportResolver(
            Assembly.GetExecutingAssembly(),
            static (libraryName, assembly, searchPath) =>
            {
                if (!string.Equals(libraryName, "headphone_ffi", StringComparison.Ordinal))
                {
                    return IntPtr.Zero;
                }

                var baseDirectory = AppContext.BaseDirectory;
                var candidates = new[]
                {
                    Path.Combine(baseDirectory, "headphone_ffi.dll"),
                    Path.Combine(baseDirectory, "..", "headphone_ffi.dll"),
                };

                foreach (var candidate in candidates)
                {
                    var fullPath = Path.GetFullPath(candidate);
                    if (File.Exists(fullPath) && NativeLibrary.TryLoad(fullPath, out var handle))
                    {
                        return handle;
                    }
                }

                return IntPtr.Zero;
            });
    }

    [DllImport("headphone_ffi", CallingConvention = CallingConvention.Cdecl)]
    private static extern uint openscq30_supported_model_count();

    [DllImport("headphone_ffi", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr openscq30_status_message();

    [DllImport("headphone_ffi", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr openscq30_device_model_name(uint index);

    [DllImport("headphone_ffi", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr openscq30_connect(
        [MarshalAs(UnmanagedType.LPStr)] string macAddress,
        [MarshalAs(UnmanagedType.LPStr)] string name);

    [DllImport("headphone_ffi", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr openscq30_demo_connect();

    [DllImport("headphone_ffi", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr openscq30_get_ambient_sound_mode();

    [DllImport("headphone_ffi", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr openscq30_get_device_snapshot();

    [DllImport("headphone_ffi", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr openscq30_set_ambient_sound_mode(
        [MarshalAs(UnmanagedType.LPStr)] string mode);

    [DllImport("headphone_ffi", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr openscq30_set_noise_canceling_mode(
        [MarshalAs(UnmanagedType.LPStr)] string mode);

    [DllImport("headphone_ffi", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr openscq30_set_transparency_mode(
        [MarshalAs(UnmanagedType.LPStr)] string mode);

    [DllImport("headphone_ffi", CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr openscq30_disconnect();

    [DllImport("headphone_ffi", CallingConvention = CallingConvention.Cdecl)]
    private static extern void openscq30_string_free(IntPtr ptr);

    public static uint SupportedModelCount() => openscq30_supported_model_count();

    public static string StatusMessage() => ReadAndFree(openscq30_status_message());

    public static string DeviceModelName(uint index) => ReadAndFree(openscq30_device_model_name(index));

    public static string Connect(string macAddress, string name) => ReadAndFree(openscq30_connect(macAddress, name));

    public static string ConnectDemoDevice() => ReadAndFree(openscq30_demo_connect());

    public static string GetAmbientSoundMode() => ReadAndFree(openscq30_get_ambient_sound_mode());

    public static string GetDeviceSnapshot() => ReadAndFree(openscq30_get_device_snapshot());

    public static string SetAmbientSoundMode(string mode) => ReadAndFree(openscq30_set_ambient_sound_mode(mode));

    public static string SetNoiseCancelingMode(string mode) => ReadAndFree(openscq30_set_noise_canceling_mode(mode));

    public static string SetTransparencyMode(string mode) => ReadAndFree(openscq30_set_transparency_mode(mode));

    public static string Disconnect() => ReadAndFree(openscq30_disconnect());

    private static string ReadAndFree(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero)
        {
            return string.Empty;
        }

        try
        {
            return Marshal.PtrToStringAnsi(ptr) ?? string.Empty;
        }
        finally
        {
            openscq30_string_free(ptr);
        }
    }
}