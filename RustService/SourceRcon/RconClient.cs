using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.ServiceProcess;
using System.Threading;
using Rust;

namespace SourceRcon
{
    public class RconClient
    {
        private readonly IPAddress _localhost = IPAddress.Parse("127.0.0.1");
        private readonly string _password;
        public Config Cfg;
        public string Identifier;
        private int _port;

        public RconClient(string ident)
        {
            const string configDir = "C:\\servertools\\RustDeployService";

            if (!Directory.Exists(configDir))
                Directory.CreateDirectory(configDir);

            if (Cfg == null)
                Cfg = new Config(configDir + "\\Settings.cfg");


            Identifier = ident;
            _port = GetServerPort();
            _password = GetRconPassword();
        }

        public Queue<string> ExecuteCommand(string cmd)
        {
            try
            {
                var consoleOutput = new Queue<string>();
                var rcon = new SourceRcon();
                rcon.ServerOutput += consoleOutput.Enqueue;

                if (rcon.Connect(new IPEndPoint(_localhost, _port), _password))
                {
                    if (!rcon.Connected)
                    {
                        Thread.Sleep(50);
                        if (!rcon.Connected)
                            Thread.Sleep(100);
                    }

                    if (rcon.Connected)
                    {
                        rcon.ServerCommand(cmd);
                        Thread.Sleep(100);
                        return consoleOutput;
                    }
                    throw new System.TimeoutException("The rcon connection timed out.");
                    consoleOutput.Enqueue("The Rcon connection timed out.");
                    return consoleOutput;
                }
                consoleOutput.Enqueue("Command did not execute successfully.");
                return consoleOutput;
            }
            catch (Exception e)
            {
                Logger.Log(e.ToString());
                throw;
            }
        }

        private int GetServerPort()
        {
            string serviceDesc = null;

            ServiceController ctl = ServiceController.GetServices().FirstOrDefault
                (s => s.ServiceName == String.Format("Rust - {0}", Identifier));
            if (ctl != null)
            {
                string objPath = string.Format("Win32_Service.Name='{0}'", ctl.ServiceName);
                using (var service = new ManagementObject(new ManagementPath(objPath)))
                    serviceDesc = service["Description"].ToString();
            }

            if (serviceDesc == null)
                throw new Exception(
                    String.Format("Unable to parse port from {0}'s service description. Check the service description",
                        Identifier));
            _port = int.Parse(serviceDesc.Substring(serviceDesc.IndexOf("-", StringComparison.Ordinal) + 1));
            _port++;
            return _port;
        }

        private string GetRconPassword()
        {
            string configPath = Path.Combine(Cfg.InstallPath, Identifier, "save\\myserverdata\\cfg\\server.cfg");

            if (!File.Exists(configPath))
                throw new Exception("The configuration file does not exist");

            string[] configFile = File.ReadAllLines(configPath);
            string rconPass = null;

            foreach (var splitValues in configFile.Select(line => line.Split(new[] {' '}, 2)))
            {
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