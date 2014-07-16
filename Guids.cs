// Guids.cs
// MUST match guids.h
using System;

namespace YifanLu.DumpMemory
{
    static class GuidList
    {
        public const string guidDumpMemoryPkgString = "853d08ac-7acc-4887-8e2f-faf040db2c11";
        public const string guidDumpMemoryCmdSetString = "1ba39c72-3216-46ee-8e47-2073a8516d89";
        public const string guidToolWindowPersistanceString = "4adad90e-1c12-4bb8-9c8e-8a0a56fe49ca";

        public static readonly Guid guidDumpMemoryCmdSet = new Guid(guidDumpMemoryCmdSetString);
    };
}