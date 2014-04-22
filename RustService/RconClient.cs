using System;
using System.Net;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Management;
using System.ServiceProcess;
using EasyConfigLib;
using Rust;

namespace SourceRcon
{
    public class RconClient
    {
        public Config cfg;
        private IPAddress localhost = IPAddress.Parse("127.0.0.1");
        private int port;
        private string password;
        public string Identifier;

        public RconClient(string ident)
        {
            string configDir = "C:\\servertools\\RustDeployService";

            if (!Directory.Exists(configDir))
                Directory.CreateDirectory(configDir);

            if (cfg == null)
                cfg = new Config(configDir + "\\Settings.cfg");


            this.Identifier = ident;
            this.port = GetServerPort();
            this.password = GetRconPassword();
        }

        public Queue<string> ExecuteCommand(string cmd)
        {
            var consoleOutput = new Queue<string>();
            SourceRcon rcon = new SourceRcon();
            rcon.ServerOutput += new StringOutput(consoleOutput.Enqueue);

            if (rcon.Connect(new IPEndPoint(this.localhost, this.port), this.password))
            {
                while (!rcon.Connected)
                    Thread.Sleep(10);

                rcon.ServerCommand(cmd);
                Thread.Sleep(100);
                return consoleOutput;
            }
            throw new Exception("Command did not execute successfully.");
        }

        private int GetServerPort()
        {
            string serviceDesc;

            ServiceController ctl = ServiceController.GetServices().FirstOrDefault
                        (s => s.ServiceName == String.Format("Rust - {0}", Identifier));
            string objPath = string.Format("Win32_Service.Name='{0}'", ctl.ServiceName);
            using (ManagementObject service = new ManagementObject(new ManagementPath(objPath)))
                serviceDesc = service["Description"].ToString();

            if (serviceDesc != null)
            {
                port = int.Parse(serviceDesc.Substring(serviceDesc.IndexOf("-") + 1));
                port++;
                return port;
            }
            throw new Exception(String.Format("Unable to parse port from {0}'s service description. Check the service description", Identifier));
        }

        private string GetRconPassword()
        {
            string configPath = Path.Combine(cfg.InstallPath, Identifier, "save\\myserverdata\\cfg\\server.cfg");

            if (!File.Exists(configPath))
                throw new Exception("The configuration file does not exist");

            var configFile = File.ReadAllLines(configPath);
            string rconPass = null;

            foreach (var line in configFile)
            {
                var splitValues = line.Split(new char[] { ' ' }, 2);
                splitValues[1] = splitValues[1].Trim('"');
                if (splitValues[0] == "rcon.password")
                    rconPass = splitValues[1];
            }

            if (rconPass == null)
                throw new Exception("The Rcon password was not found in the configuration file.");

            return rconPass;
        }
    }
}
