using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;

namespace Rust
{
    //[MessageContract]
    public class Results
    {
        //[MessageBodyMember(Order = 1)]
        public bool Success { get; set; }

        //[MessageBodyMember(Order = 2)]
        public string Message { get; set; }
    }

    public class CheatpunchResults
    {
        //[MessageBodyMember(Order = 1)]
        public bool Success { get; set; }

        public bool Enabled { get; set; }

        //[MessageBodyMember(Order = 2)]
        public string Message { get; set; }
    }

    public class ConfigResults
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
}
