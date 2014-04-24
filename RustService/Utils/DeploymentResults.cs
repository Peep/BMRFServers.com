using System;
using System.Collections.Generic;

namespace Rust
{
    public class DeploymentResults
    {
        public static string Identifier { get; set; }

        public static int Port { get; set; }

        public static bool SteamSuccess { get; set; }

        public static bool FireDaemonSuccess { get; set; }

        public static bool FtpSuccess { get; set; }

        public static bool ExceptionThrown { get; set; }

        public static List<Exception> Exceptions { get; set; }
    }
}