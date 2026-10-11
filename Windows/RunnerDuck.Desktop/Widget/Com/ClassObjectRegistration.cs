/*
 ClassObjectRegistration.cs
 RunnerDuck.App

 Created by duks93 on 2026/10/06.
 Copyright 2026 Kyome22 (Takuto Nakamura)
 Modified for RunnerDuck by duks93, 2026.

 Licensed under the Apache License, Version 2.0 (the "License");
 you may not use this file except in compliance with the License.
 You may obtain a copy of the License at

 http://www.apache.org/licenses/LICENSE-2.0

 Unless required by applicable law or agreed to in writing, software
 distributed under the License is distributed on an "AS IS" BASIS,
 WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 See the License for the specific language governing permissions and
 limitations under the License.
 */

// Adapted from the Windows App SDK widget sample (MIT License):
// https://github.com/microsoft/WindowsAppSDK-Samples/tree/main/Samples/Widgets/cs-console-packaged

using System.Runtime.InteropServices;
using Microsoft.Windows.Widgets.Providers;
using WinRT;

namespace RunnerDuck.App.Widget.Com;

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("00000001-0000-0000-C000-000000000046")]
internal interface IClassFactory
{
    [PreserveSig]
    int CreateInstance(IntPtr pUnkOuter, ref Guid riid, out IntPtr ppvObject);

    [PreserveSig]
    int LockServer(bool fLock);
}

internal sealed class WidgetProviderFactory<T> : IClassFactory where T : IWidgetProvider, new()
{
    private const int ClassENoAggregation = unchecked((int)0x80040110);
    private const int ENoInterface = unchecked((int)0x80004002);

    private static readonly Guid IUnknownID = new("00000000-0000-0000-C000-000000000046");

    public int CreateInstance(IntPtr pUnkOuter, ref Guid riid, out IntPtr ppvObject)
    {
        ppvObject = IntPtr.Zero;
        if (pUnkOuter != IntPtr.Zero)
        {
            return ClassENoAggregation;
        }
        if (riid != typeof(T).GUID && riid != IUnknownID)
        {
            return ENoInterface;
        }
        ppvObject = MarshalInspectable<IWidgetProvider>.FromManaged(new T());
        return 0;
    }

    public int LockServer(bool fLock) => 0;
}

internal sealed class ClassObjectRegistration : IDisposable
{
    private const uint LocalServerContext = 0x4;
    private const uint MultipleUseRegistration = 0x1;

    private readonly uint cookie;

    private ClassObjectRegistration(uint cookie)
    {
        this.cookie = cookie;
    }

    public static ClassObjectRegistration Register<T>() where T : IWidgetProvider, new()
    {
        var result = CoRegisterClassObject(
            typeof(T).GUID,
            new WidgetProviderFactory<T>(),
            LocalServerContext,
            MultipleUseRegistration,
            out var cookie
        );
        Marshal.ThrowExceptionForHR(result);
        return new ClassObjectRegistration(cookie);
    }

    public void Dispose() => CoRevokeClassObject(cookie);

    [DllImport("ole32.dll")]
    private static extern int CoRegisterClassObject(
        [MarshalAs(UnmanagedType.LPStruct)] Guid rclsid,
        [MarshalAs(UnmanagedType.IUnknown)] object pUnk,
        uint dwClsContext,
        uint flags,
        out uint lpdwRegister
    );

    [DllImport("ole32.dll")]
    private static extern int CoRevokeClassObject(uint dwRegister);
}
