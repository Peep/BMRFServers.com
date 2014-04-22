using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EasyConfigLib.Storage;

namespace Rust 
{
    public class Config : EasyConfig 
    {
        [Field("SteamCMD Path", "AppSettings")]
        public string SteamPath = @"D:\Development\SteamCMD";
        [Field("Application Directory")]
        public string AppPath = @"D:\Development\RustDeployService";
        [Field("FireDaemon Path")]
        public string FDPath = @"C:\Program Files\FireDaemon";
        [Field("Fusion Path")]
        public string FusionPath = @"C:\Program Files\FireDaemon Fusion";
        [Field("Filezilla Path")]
        public string FileZillaPath = @"D:\Development\FileZilla";
        [Field("Network Interface Controller")]
        public string Nic = "Intel[R] 82579LM Gigabit Network Connection";
        [Field("Rust Path", "Customer Settings")]
        public string InstallPath = @"D:\Test";
        [Field("Config Location")]
        public string CfgDir = @"save\myserverdata\cfg";

        public Config(string filename)
            : base(filename) {

        }
    }
}
