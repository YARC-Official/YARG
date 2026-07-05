param([int]$ProcessId, [string]$DumpPath)
$code = @'
using System;
using System.IO;
using System.Runtime.InteropServices;
public static class DumpWriter {
  [Flags] public enum MINIDUMP_TYPE : uint {
    MiniDumpWithFullMemory = 0x2, MiniDumpWithHandleData = 0x4, MiniDumpWithUnloadedModules = 0x20,
    MiniDumpWithFullMemoryInfo = 0x800, MiniDumpWithThreadInfo = 0x1000
  }
  [DllImport("Dbghelp.dll", SetLastError=true)] static extern bool MiniDumpWriteDump(IntPtr hProcess, int processId, IntPtr hFile, MINIDUMP_TYPE dumpType, IntPtr expParam, IntPtr userStreamParam, IntPtr callbackParam);
  public static void Write(int pid, string path) {
    var p = System.Diagnostics.Process.GetProcessById(pid);
    using (var fs = new FileStream(path, FileMode.Create, FileAccess.ReadWrite, FileShare.None)) {
      var type = MINIDUMP_TYPE.MiniDumpWithFullMemory | MINIDUMP_TYPE.MiniDumpWithHandleData | MINIDUMP_TYPE.MiniDumpWithThreadInfo | MINIDUMP_TYPE.MiniDumpWithFullMemoryInfo | MINIDUMP_TYPE.MiniDumpWithUnloadedModules;
      if (!MiniDumpWriteDump(p.Handle, pid, fs.SafeFileHandle.DangerousGetHandle(), type, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }
  }
}
'@
Add-Type $code
[DumpWriter]::Write($ProcessId, $DumpPath)
Get-Item $DumpPath | Select-Object FullName,Length
