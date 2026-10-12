// Copyright 2026 duks93. Licensed under the Apache License, Version 2.0.
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32;

namespace RunnerDuck.App.Tray;

internal static class TrayLaunchGuard
{
    internal const string RegistrationKey = @"Software\RunnerDuck";
    internal const string RegistrationValue = "InstalledExecutable";

    public static bool RunIfAllowed(
        string? processPath, string? installedPath, bool hasPackageIdentity, string defaultTrayPath, Action run)
    {
        if (!IsAllowed(processPath, installedPath, hasPackageIdentity, defaultTrayPath)) return false;
        run();
        return true;
    }

    internal static bool IsAllowed(
        string? processPath, string? installedPath, bool hasPackageIdentity, string defaultTrayPath)
    {
        if (hasPackageIdentity) return true;
        var executable = Normalize(processPath);
        var expected = Normalize(installedPath) ?? Normalize(defaultTrayPath);
        return executable is not null && expected is not null &&
            string.Equals(executable, expected, StringComparison.OrdinalIgnoreCase);
    }

    public static string? ReadInstalledExecutable(RegistryKey currentUser)
    {
        try
        {
            using var key = currentUser.OpenSubKey(RegistrationKey);
            if (key?.GetValue(RegistrationValue) is not string path ||
                key.GetValueKind(RegistrationValue) != RegistryValueKind.String) return null;
            return Normalize(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or SecurityException)
        {
            return null;
        }
    }

    public static bool HasPackageIdentity()
    {
        uint length = 0;
        return GetCurrentPackageFullName(ref length, IntPtr.Zero) == 122;
    }

    private static string? Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            if (!Path.IsPathFullyQualified(path) || path.IndexOfAny(['"', '<', '>', '|', '?', '*']) >= 0 ||
                path[2..].Contains(':') ||
                !string.Equals(Path.GetFileName(path), "RunnerDuck.exe", StringComparison.OrdinalIgnoreCase)) return null;
            return Path.GetFullPath(path);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or IOException)
        {
            return null;
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int GetCurrentPackageFullName(ref uint packageFullNameLength, IntPtr packageFullName);
}
