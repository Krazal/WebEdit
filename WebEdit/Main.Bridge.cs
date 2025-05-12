using Npp.DotNet.Plugin;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static Npp.DotNet.Plugin.Win32;

namespace WebEdit
{
    partial class Main
    {
        [UnmanagedCallersOnly(EntryPoint = "setInfo", CallConvs = new[] { typeof(CallConvCdecl) })]
        internal unsafe static void SetInfo(NppData* notepadPlusData)
        {
            PluginData.NppData = *notepadPlusData;
            Instance.OnSetInfo();
        }

        [UnmanagedCallersOnly(EntryPoint = "beNotified", CallConvs = new[] { typeof(CallConvCdecl) })]
        internal unsafe static void BeNotified(ScNotification* notification)
        {
            Instance.OnBeNotified(*notification);
        }

        [UnmanagedCallersOnly(EntryPoint = "messageProc", CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static NativeBool MessageProc(uint msg, UIntPtr wParam, IntPtr lParam)
        {
            return Instance.OnMessageProc(msg, wParam, lParam);
        }

        [UnmanagedCallersOnly(EntryPoint = "getFuncsArray", CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static IntPtr GetFuncsArray(IntPtr nbF) => IDotNetPlugin.OnGetFuncsArray(nbF);

        [UnmanagedCallersOnly(EntryPoint = "getName", CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static IntPtr GetName() => IDotNetPlugin.OnGetName();

        [UnmanagedCallersOnly(EntryPoint = "isUnicode", CallConvs = new[] { typeof(CallConvCdecl) })]
        internal static NativeBool IsUnicode() => IDotNetPlugin.OnIsUnicode();

        static Main()
        {
            Instance = new Main();
            PluginData.PluginNamePtr = Marshal.StringToHGlobalUni(PluginName);
        }

        private Main(){}
        private static readonly IDotNetPlugin Instance;
    }
}
