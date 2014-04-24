using System.Collections.Generic;

namespace Rust
{
    public struct Results
    {
        public bool Success { get; set; }

        public string Message { get; set; }
    }

    public struct CheatpunchResults
    {
        public bool Success { get; set; }

        public bool Enabled { get; set; }

        public string Message { get; set; }
    }

    public struct ConfigResults
    {
        public bool Success { get; set; }

        public Dictionary<string, string> ConfigValues { get; set; }

        public string Message { get; set; }
    }

    public struct ServiceResults
    {
        public bool Success { get; set; }

        public string Status { get; set; }

        public string Message { get; set; }
    }

    public struct MagmaResults
    {
        public bool Success { get; set; }

        public string Message { get; set; }

        public bool Installed { get; set; }
    }

    public struct ResourceResults
    {
        public int CpuUsage { get; set; }
        public int RamUsage { get; set; }
        public int NetUsage { get; set; }
    }
}