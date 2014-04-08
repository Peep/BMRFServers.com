using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;

namespace Rust 
{
    [MessageContract]
    public class DeploymentResults 
    {
        [MessageBodyMember(Order = 1)]
        public static string Identifier { get; set; }

        [MessageBodyMember(Order = 2)]
        public static int Port { get; set; }

        [MessageBodyMember(Order = 3)]
        public static bool SteamSuccess { get; set; }

        [MessageBodyMember(Order = 4)]
        public static bool FireDaemonSuccess { get; set; }

        [MessageBodyMember(Order = 5)]
        public static bool FtpSuccess {get; set; }

        [MessageBodyMember(Order = 6)]
        public static bool ExceptionThrown { get; set; }

        [MessageBodyMember(Order = 7)]
        public static List<Exception> Exceptions { get; set; }
    }
}
