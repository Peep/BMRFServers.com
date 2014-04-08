using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;

namespace Rust
{
    [MessageContract]
    class Results
    {
        [MessageBodyMember(Order = 1)]
        public bool Success { get; set; }

        [MessageBodyMember(Order = 2)]
        public string Message { get; set; }
    }
}
