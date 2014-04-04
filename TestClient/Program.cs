using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WCFTestClient {
    class Program {
        static void Main(string[] args) {
            var client = new RustServiceReference.RustServiceHost();
            client.ClientCredentials.UserName.UserName = "fuckyouwindows";
            client.ClientCredentials.UserName.Password = "1test";
            var test = client.Subtract(5, 2);
            Console.WriteLine("Received {0}", test);
            Console.ReadLine();
        }
    }
}
