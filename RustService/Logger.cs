using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Rust 
{
    public class Logger 
    {
        public static void Log(string arg) 
        {
            if (!Directory.Exists(@"C:\servertools\RustDeployService"))
            {
                Directory.CreateDirectory(@"C:\servertools\RustDeployService");
            }

            using (StreamWriter writer = new StreamWriter(String.Format(@"C:\servertools\RustDeployService\Debug.log"), true)) 
            {
                writer.WriteLine(String.Format("{0}: {1}", DateTime.Now, arg));
            }
        }
    }
}
