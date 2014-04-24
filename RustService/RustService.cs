using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using System.Xml.Linq;
using SourceRcon;

namespace Rust
{
    public class RustService : IRustService
    {
        public Config Cfg;
        public string Identifier;

        public Dictionary<string, object> DeployRustServer(string identifier, int slots)
        {
            Identifier = identifier;
            string password = GeneratePassword();
            InitializeConfigurationFile();

            InstallRustServer();

            var service = new FireDaemon(Cfg, Identifier, slots);
            service.GenerateXml();

            var ftpUser = new FileZilla(Cfg, Identifier, password);
            ftpUser.GenerateXml();

            var dic = new Dictionary<string, object>
            {
                {"Port", DeploymentResults.Port},
                {"Password", password},
                {"ExceptionThrown", DeploymentResults.ExceptionThrown},
                {"Exceptions", DeploymentResults.Exceptions}
            };

            return dic;
        }

        public Results UninstallRustServer(string ident)
        {
            InitializeConfigurationFile();

            if (!Directory.Exists(Path.Combine(Cfg.InstallPath, ident)))
                return new Results {Success = false, Message = "That user was not found on this machine."};

            StopServer(ident);
            Directory.Delete(Path.Combine(Cfg.InstallPath, ident), true);

            XDocument xDoc = XDocument.Load(Path.Combine(Cfg.FileZillaPath, "FileZilla Server.xml"));

            foreach (XElement item in xDoc.Descendants("User").Where(item => item.Attribute("Name").Value == ident))
                item.Attribute("Name").Remove();

            xDoc.Save(Path.Combine(Cfg.FileZillaPath, "FileZilla Server.xml"));
            Process.Start(Cfg.FileZillaPath + "\\FileZilla Server.exe", "/reload-config");

            return new Results
            {
                Success = true,
                Message = "The specified user was successfully removed from this machine."
            };
        }

        public Results RunSteamUpdate(string ident)
        {
            InitializeConfigurationFile();

            if (!File.Exists(String.Format(@"{0}\scripts\{1}.txt", Cfg.InstallPath, ident)))
                CreateSteamScript(ident);

            Results stop = StopServer(ident);

            if (!stop.Success)
                return new Results {Success = false, Message = stop.Message};

            var steam = new Process();

            steam.StartInfo.FileName = Path.Combine(Cfg.SteamPath, "steamcmd.exe");
            steam.StartInfo.UseShellExecute = false;

            steam.StartInfo.Arguments = String.Format(@"+runscript {0}\scripts\{1}.txt",
                Cfg.AppPath, ident);

            steam.Start();

            steam.WaitForExit(180000);

            StartServer(ident);

            return new Results {Success = true, Message = "Your server has been updated successfully."};
        }

        public Results InstallMagma(string ident)
        {
            InitializeConfigurationFile();
            if (!Directory.Exists(Path.Combine(Cfg.InstallPath, ident, "rust_server_Data")))
                return new Results {Success = false, Message = "The specified user was not found on this machine."};

            if (!Directory.Exists(Path.Combine(Cfg.AppPath, "magma\\rust_server_Data")))
                return new Results
                {
                    Success = false,
                    Message = "This machine is not setup to support Magma installations."
                };

            try
            {
                StartServer(ident);

                CopyDirectory(Path.Combine(Cfg.AppPath, "magma\\rust_server_Data"),
                    Path.Combine(Cfg.InstallPath, ident, "rust_server_Data"));

                CopyDirectory(Path.Combine(Cfg.AppPath, "magma\\save"),
                    Path.Combine(Cfg.InstallPath, ident, "save"));

                return new Results {Success = true, Message = "Magma was installed successfully."};
            }
            catch (Exception e)
            {
                return new Results {Success = false, Message = "Exception: " + e};
            }
        }

        public MagmaResults MagmaStatus(string ident)
        {
            InitializeConfigurationFile();
            const string libFolder = "rust_server_data\\Managed";

            if (!File.Exists(Path.Combine(Cfg.InstallPath, ident, libFolder, "Jint.dll")))
                return new MagmaResults
                {
                    Success = true,
                    Installed = false,
                    Message = "Magma is not installed on your server."
                };
            if (!File.Exists(Path.Combine(Cfg.InstallPath, ident, libFolder, "Magma.dll")))
                return new MagmaResults
                {
                    Success = true,
                    Installed = false,
                    Message = "Magma is not installed on your server."
                };
            if (!File.Exists(Path.Combine(Cfg.InstallPath, ident, libFolder, "Mono.Cecil.dll")))
                return new MagmaResults
                {
                    Success = true,
                    Installed = false,
                    Message = "Magma is not installed on your server."
                };

            return Directory.Exists(Path.Combine(Cfg.InstallPath, ident, "save\\Magma"))
                ? new MagmaResults {Success = true, Installed = true, Message = "Magma is installed on your server."}
                : new MagmaResults
                {
                    Success = true,
                    Installed = false,
                    Message = "Magma is not installed on your server."
                };
        }

        public Results UninstallMagma(string ident)
        {
            InitializeConfigurationFile();
            const string libFolder = "rust_server_data\\Managed";

            try
            {
                StopServer(ident);

                File.Copy(Path.Combine(Cfg.AppPath, "premagma", libFolder, "Assembly-CSharp.dll"),
                    Path.Combine(Cfg.InstallPath, ident, libFolder, "Assembly-CSharp.dll"), true);
                File.Delete(Path.Combine(Cfg.InstallPath, ident, libFolder, "Jint.dll"));
                File.Delete(Path.Combine(Cfg.InstallPath, ident, libFolder, "Magma.dll"));
                File.Delete(Path.Combine(Cfg.InstallPath, ident, libFolder, "Mono.Cecil.dll"));

                return new Results {Success = true, Message = "Magma was successfully uninstalled from your server"};
            }
            catch (Exception e)
            {
                return new Results {Success = false, Message = "Exception: " + e};
            }
        }

        public Results ChangeFtpPass(string ident, string newPass)
        {
            InitializeConfigurationFile();

            XDocument xDoc = XDocument.Load(Path.Combine(Cfg.FileZillaPath, "FileZilla Server.xml"));

            XElement password = (from x in xDoc.Descendants("User")
                where x.Attribute("Name").Value == ident
                select x.Element("Option")).FirstOrDefault();

            if (password == null)
                return new Results
                {
                    Success = false,
                    Message = "Couldn't find your FTP account, please contact support."
                };

            password.SetValue(Crypto.GetMd5(newPass));

            xDoc.Save(Path.Combine(Cfg.FileZillaPath, "FileZilla Server.xml"));
            Process.Start(Path.Combine(Cfg.FileZillaPath, "FileZilla Server.exe"), "/reload-config");

            return new Results {Success = true, Message = "Your FTP password was changed successfully."};
        }

        public CheatpunchResults ToggleCheatpunch(string ident, int action)
        {
            InitializeConfigurationFile();
            string serviceName = String.Format("Rust - {0}", ident);
            string xmlPath = Path.Combine(Cfg.AppPath, String.Format("export\\{0}_export.xml", ident));

            if (action < 0 || action > 2)
                return new CheatpunchResults
                {
                    Success = false,
                    Message = "Error toggling cheatpunch in a way that should never happen, please contact support."
                };

            try
            {
                if (!Directory.Exists(Path.Combine(Cfg.AppPath, "export")))
                    Directory.CreateDirectory(Path.Combine(Cfg.AppPath, "export"));

                if (File.Exists(xmlPath))
                    File.Delete(xmlPath);

                using (var fd = new Process())
                {
                    fd.StartInfo.FileName = Path.Combine(Cfg.FDPath, "FireDaemon.exe");
                    fd.StartInfo.Arguments = String.Format(@"--export ""{0}"" {1}",
                        serviceName, xmlPath);

                    fd.Start();
                    fd.WaitForExit();
                }

                // Skip the first two lines in the exported file
                // This is done to remove the comment header so it will save properly.
                string[] lines = File.ReadAllLines(xmlPath);
                File.WriteAllLines(xmlPath, lines.Skip(2).ToArray());

                if (!File.Exists(xmlPath))
                    return new CheatpunchResults
                    {
                        Success = false,
                        Message = "Error toggling Cheatpunch, please contact support."
                    };

                XDocument xDoc = XDocument.Load(xmlPath);

                XElement parameters = (from x in xDoc.Descendants("Program")
                    let xElement = x.Element("Name")
                    where xElement != null && xElement.Value == serviceName
                    select x.Element("Parameters")).FirstOrDefault();

                if (parameters != null && (parameters.Value.Contains("-cheatpunch") && action == 2))
                    return new CheatpunchResults {Success = true, Enabled = true, Message = "Cheatpunch is enabled"};

                if (parameters != null && (!parameters.Value.Contains("-cheatpunch") && action == 2))
                    return new CheatpunchResults {Success = true, Enabled = false, Message = "Cheatpunch is disabled"};

                if (parameters != null && (parameters.Value.Contains("-cheatpunch") && action == 1))
                    return new CheatpunchResults
                    {
                        Success = true,
                        Enabled = true,
                        Message = "Cheatpunch is already enabled"
                    };

                if (parameters != null && (!parameters.Value.Contains("-cheatpunch") && action == 1))
                {
                    parameters.SetValue(parameters.Value + " -cheatpunch");
                    xDoc.Save(xmlPath);

                    Process.Start(Path.Combine(Cfg.FDPath, "FireDaemon.exe"),
                        String.Format(@"--install {0} edit", xmlPath));

                    return new CheatpunchResults
                    {
                        Success = true,
                        Enabled = true,
                        Message = "Cheatpunch has been enabled."
                    };
                }
                if (parameters == null || (!parameters.Value.Contains("-cheatpunch") || action != 0))
                    return new CheatpunchResults
                    {
                        Success = false,
                        Message = "Error toggling Cheatpunch, please contact support."
                    };
                parameters.SetValue(parameters.Value.Replace("-cheatpunch", ""));
                xDoc.Save(xmlPath);

                Process.Start(Path.Combine(Cfg.FDPath, "FireDaemon.exe"),
                    String.Format(@"--install {0} edit", xmlPath));

                return new CheatpunchResults
                {
                    Success = true,
                    Enabled = false,
                    Message = "Cheatpunch has been disabled."
                };
            }
            catch (Exception e)
            {
                Logger.Log(e.ToString());
                return new CheatpunchResults {Success = false, Message = e.ToString()};
            }
        }

        public ConfigResults GetConfigValue(string ident, string value = "all")
        {
            InitializeConfigurationFile();
            string configPath = Path.Combine(Cfg.InstallPath, ident, "save\\myserverdata\\cfg\\server.cfg");

            if (!File.Exists(configPath))
                return new ConfigResults
                {
                    Success = false,
                    Message = "Your configuration file was not found, does it exist?"
                };

            string[] configFile = File.ReadAllLines(configPath);
            var dic = new Dictionary<string, string>();

            foreach (var splitValues in configFile.Select(line => line.Split(new[] {' '}, 2)))
            {
                splitValues[1] = splitValues[1].Trim('"');
                if (value == "all")
                    dic.Add(splitValues[0], splitValues[1]);
                else if (splitValues[0] == value)
                    dic.Add(splitValues[0], splitValues[1]);
            }
            return dic.Count == 0
                ? new ConfigResults
                {
                    Success = false,
                    Message = "The specified config value was not present in the file. Add it first."
                }
                : new ConfigResults {Success = true, ConfigValues = dic};
        }

        public Results SetConfigValue(string ident, string key, string value)
        {
            try
            {
                InitializeConfigurationFile();
                string configPath = Path.Combine(Cfg.InstallPath, ident, "save\\myserverdata\\cfg\\server.cfg");

                if (!File.Exists(configPath))
                    return new Results
                    {
                        Success = false,
                        Message = "Config file not found. Either the file is missing or the user doesn't exist."
                    };

                bool useDoubleQuotes = !String.Equals(value, "true", StringComparison.CurrentCultureIgnoreCase)
                                       || String.Equals(value, "false", StringComparison.CurrentCultureIgnoreCase);

                long parsedResult;
                bool canConvert = long.TryParse(value, out parsedResult);
                if (canConvert)
                    useDoubleQuotes = false;

                string[] configFile = File.ReadAllLines(configPath);
                string[] splitValues = null;

                for (int i = 0; i < configFile.Length; i++)
                {
                    splitValues = configFile[i].Split(new[] {' '}, 2); // Separate the key and value using a space.
                    if (splitValues[0] == key) // If the specified key exists, update it's value, write, and return.
                    {
                        splitValues[1] = value;
                        if (useDoubleQuotes)
                            configFile[i] = String.Format(@"{0} ""{1}""", splitValues[0], splitValues[1]);
                        else
                            configFile[i] = String.Format(@"{0} {1}", splitValues[0], splitValues[1]);
                        File.WriteAllLines(configPath, configFile);
                        return new Results {Success = true, Message = "The existing config value was updated."};
                    }
                }

                using (var writer = new StreamWriter(configPath, true))
                    // If we haven't returned yet, assume the value doesn't exist and write it as a new line.
                    writer.WriteLine(@"{0} ""{1}""", key, value);

                if (splitValues != null)
                    return new Results
                    {
                        Success = true,
                        Message = String.Format("{0} has been set to {1}", splitValues[0], splitValues[1])
                    };
            }
            catch (Exception e)
            {
                WriteToEventLog(e.ToString(), EventLogEntryType.Error);
                return new Results
                {
                    Success = false,
                    Message = "An error occurred while trying to write to your config file, please contact support."
                };
            }
            return new Results
            {
                Success = false,
                Message = "An error occurred while attempting to write to your config file, please contact support."
            };
        }

        public Results RunRconCommand(string ident, string cmd)
        {
            return new Results { Success = true, Message = "This feature is currently disabled" };
            //try
            //{
            //    var rcon = new RconClient(ident);
            //    var result = rcon.ExecuteCommand(cmd);
            //    result.Dequeue();
            //    return new RconResults{ Success = true, Message = "This feature is currently disabled" };
            //}
            //catch (Exception e)
            //{
            //    return e.Message;
            //}
        }

        public ServiceResults ServerStatus(string ident)
        {
            ServiceController ctl = ServiceController.GetServices().FirstOrDefault
                (s => s.ServiceName == String.Format("Rust - {0}", ident));

            if (ctl == null)
            {
                WriteToEventLog(
                    String.Format(
                        "An incorrect call was made to StartServer(): Identifier '{0}' does not exist on this machine.",
                        ident), EventLogEntryType.FailureAudit);
                return new ServiceResults
                {
                    Success = false,
                    Message = "Unable to find a server associated with your account, please contact support."
                };
            }
            string status = ctl.Status.ToString();
            return new ServiceResults {Success = true, Status = status, Message = "Your Rust server is " + status + "."};
        }

        public Results StartServer(string ident)
        {
            try
            {
                ServiceController ctl = ServiceController.GetServices().FirstOrDefault
                    (s => s.ServiceName == String.Format("Rust - {0}", ident));

                if (ctl == null)
                {
                    WriteToEventLog(
                        String.Format(
                            "An incorrect call was made to StartServer(): Identifier '{0}' does not exist on this machine.",
                            ident), EventLogEntryType.FailureAudit);
                    return new Results
                    {
                        Success = false,
                        Message = "Unable to find a server associated with your account, please contact support."
                    };
                }

                if (ctl.Status == ServiceControllerStatus.Running)
                    return new Results {Success = true, Message = "Your server is already running."};

                ctl.Start();
                ctl.WaitForStatus(ServiceControllerStatus.Running, new TimeSpan(0, 0, 5));

                if (ctl.Status == ServiceControllerStatus.Running)
                {
                    WriteToEventLog(String.Format("Server assigned to '{0}' has been started successfully.",
                        ident), EventLogEntryType.SuccessAudit);
                    return new Results
                    {Success = true, Message = "Your server has been started."};
                }
                return new Results
                {
                    Success = false,
                    Message = "Timed out while attempting to start your server, please contact support."
                };
            }
            catch (Exception e)
            {
                WriteToEventLog(e.ToString(), EventLogEntryType.Error);
                return new Results
                {
                    Success = false,
                    Message = "An error occured while attempting to start your server, please contact support."
                };
            }
        }

        public Results StopServer(string ident)
        {
            bool wasSaved = false;
            try
            {
                ServiceController ctl = ServiceController.GetServices().FirstOrDefault
                    (s => s.ServiceName == String.Format("Rust - {0}", ident));

                if (ctl == null)
                {
                    WriteToEventLog(
                        String.Format(
                            "An incorrect call was made to StopServer(): Identifier '{0}' does not exist on this machine.",
                            ident), EventLogEntryType.FailureAudit);
                    return new Results
                    {
                        Success = false,
                        Message = "Unable to find a server associated with your account, please contact support."
                    };
                }

                //if (ctl.Status == ServiceControllerStatus.Running)
                //{
                //    string result = RunRconCommand(ident, "save.all");
                //    if (result.StartsWith("Saving"))
                //        wasSaved = true;
                //}

                if (ctl.Status == ServiceControllerStatus.Stopped)
                    return new Results {Success = true, Message = "Your server is already stopped."};

                ctl.Stop();
                ctl.WaitForStatus(ServiceControllerStatus.Stopped, new TimeSpan(0, 0, 5));

                if (ctl.Status != ServiceControllerStatus.Stopped)
                    return new Results
                    {
                        Success = false,
                        Message = "Timed out while attempting to stop your server, please contact support."
                    };

                WriteToEventLog(String.Format("Server assigned to '{0}' has been stopped successfully.",
                    ident), EventLogEntryType.SuccessAudit);
                return wasSaved
                    ? new Results
                    {
                        Success = true,
                        Message = "Your server data has been saved, and the server is now stopped."
                    }
                    : new Results {Success = true, Message = "Your server has been stopped."};
            }
            catch (Exception e)
            {
                WriteToEventLog(e.ToString(), EventLogEntryType.Error);
                return new Results
                {
                    Success = false,
                    Message = "An error occured while attempting to stop your Rust server, please contact support."
                };
            }
        }

        public Results RestartServer(string ident)
        {
            string stopMessage = null;

            try
            {
                ServiceController ctl = ServiceController.GetServices().FirstOrDefault
                    (s => s.ServiceName == String.Format("Rust - {0}", ident));

                if (ctl != null && ctl.Status == ServiceControllerStatus.Running)
                {
                    Results stop = StopServer(ident);
                    stopMessage = stop.Message;
                    ctl.WaitForStatus(ServiceControllerStatus.Stopped, new TimeSpan(0, 0, 5));

                    if (!stop.Success)
                        return new Results {Success = false, Message = stop.Message};
                }

                Results start = StartServer(ident);
                if (ctl != null) ctl.WaitForStatus(ServiceControllerStatus.Running, new TimeSpan(0, 0, 5));

                if (!start.Success)
                    return new Results {Success = false, Message = start.Message};

                return stopMessage != null && stopMessage.Contains("saved")
                    ? new Results
                    {
                        Success = true,
                        Message = "Your server data has been saved, and the server restarted successfully."
                    }
                    : new Results {Success = true, Message = "The server restarted successfully."};
            }
            catch (Exception e)
            {
                WriteToEventLog(e.ToString(), EventLogEntryType.Error);
                return new Results
                {
                    Success = false,
                    Message = "An error occured while attempting to restart your Rust server, please contact support."
                };
            }
        }

        public ResourceResults GetResourceValues()
        {
            InitializeConfigurationFile();
            string networkCard = Cfg.Nic;

            var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            var memCounter = new PerformanceCounter("Memory", "Available MBytes");

            var bandwidthCounter = new PerformanceCounter("Network Interface", "Current Bandwidth", networkCard);
            var dataSentCounter = new PerformanceCounter("Network Interface", "Bytes Sent/sec", networkCard);
            var dataReceivedCounter = new PerformanceCounter("Network Interface", "Bytes Received/sec", networkCard);

            const int totalRam = 32768;

            cpuCounter.NextValue();
            memCounter.NextValue();
            dataSentCounter.NextValue();
            dataReceivedCounter.NextValue();
            bandwidthCounter.NextValue();

            Thread.Sleep(1000); // make sure Windows has updated the counters

            int cpuUsage = (int) cpuCounter.NextValue()/2;
                // dividing this by 2 seems to pull the accurate result, hyperthreading?
            float availableMem = memCounter.NextValue();

            float memUsage = (1 - (availableMem/totalRam))*100;

            float bandwidth = bandwidthCounter.NextValue();
            float send = dataSentCounter.NextValue();
            float receive = dataReceivedCounter.NextValue();

            float utilization = (8*(send + receive))/bandwidth*100;

            var netUsage = (int) utilization;

            return new ResourceResults {CpuUsage = cpuUsage, RamUsage = (int) memUsage, NetUsage = netUsage};
        }

        public void InitializeConfigurationFile()
        {
            const string configDir = "C:\\servertools\\RustDeployService";

            if (!Directory.Exists(configDir))
                Directory.CreateDirectory(configDir);

            if (Cfg == null)
                Cfg = new Config(configDir + "\\Settings.cfg");
        }

        private static void WriteToEventLog(string message, EventLogEntryType type)
        {
            const string cs = "RustDeployService";
            EventLogEntryType logType = type;

            if (!EventLog.SourceExists(cs))
                EventLog.CreateEventSource(cs, "Application");

            EventLog.WriteEntry(cs, message, logType);
        }

        public void CreateSteamScript(string ident)
        {
            try
            {
                using (var scriptWriter = new StreamWriter(Path.Combine(Cfg.AppPath, "scripts", ident + ".txt"), true))
                {
                    scriptWriter.WriteLine("@ShutdownOnFailedCommand 1");
                    scriptWriter.WriteLine("@NoPromptForPassword 1");
                    scriptWriter.WriteLine("login anonymous");
                    scriptWriter.WriteLine(@"force_install_dir {0}\{1}", Cfg.InstallPath, ident);
                    scriptWriter.WriteLine(
                        "app_update 258550 -beta stable -betapassword 05094c962cf2f502bfdfdebf800dd5d3 validate");
                    scriptWriter.WriteLine("quit");
                }
            }
            catch (Exception e)
            {
                WriteToEventLog(e.ToString(), EventLogEntryType.Error);
            }
        }

        public void CreateServerConfig()
        {
            const string configPath = "save\\myserverdata\\cfg";
            try
            {
                if (!Directory.Exists(Path.Combine(Cfg.InstallPath, Identifier, configPath)))
                    Directory.CreateDirectory(Path.Combine(Cfg.InstallPath, Identifier, configPath));

                const string chars = "abcdefghijklmnopqrstuvwxyz0123456789";
                var random = new Random();

                var rconPass = new string(
                    Enumerable.Repeat(chars, 8)
                        .Select(s => s[random.Next(s.Length)])
                        .ToArray());

                using (
                    var cfgWriter = new StreamWriter(Path.Combine(Cfg.InstallPath, Identifier, configPath, "server.cfg"))
                    )
                {
                    cfgWriter.WriteLine(@"server.hostname ""{0}'s BMRFServers.com Rust Server""", Identifier);
                    cfgWriter.WriteLine(@"rcon.password ""{0}""", rconPass);
                    cfgWriter.WriteLine(@"airdrop.min_players ""15""");
                    cfgWriter.WriteLine(@"server.clienttimeout ""3""");
                }
            }
            catch (Exception e)
            {
                DeploymentResults.ExceptionThrown = true;
                DeploymentResults.Exceptions.Add(e);
            }
        }

        public bool InstallRustServer()
        {
            bool success = false;
            try
            {
                string installPath = Cfg.InstallPath;

                Logger.Log(String.Format("Starting new Customer installation for {0}", Identifier));

                if (!Directory.Exists(Path.Combine(Cfg.AppPath, "scripts")))
                    Directory.CreateDirectory(Path.Combine(Cfg.AppPath, "scripts"));

                if (!File.Exists(String.Format(@"{0}\scripts\{1}.txt", Cfg.AppPath, Identifier)))
                {
                    string ident = Identifier;
                    CreateSteamScript(ident);
                }

                var steam = new Process
                {
                    StartInfo =
                    {
                        FileName = Path.Combine(Cfg.SteamPath, "steamcmd.exe"),
                        UseShellExecute = false,
                        Arguments = String.Format(@"+runscript {0}\scripts\{1}.txt",
                            Cfg.AppPath, Identifier),
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                steam.Start();

                Logger.Log(String.Format(@"Installing Rust to {0}\{1} -- please wait...", installPath, Identifier));

                string output = steam.StandardOutput.ReadToEnd();
                string error = steam.StandardError.ReadToEnd();

                Logger.Log(error);
                Logger.Log(output);

                steam.WaitForExit();

                if (File.Exists(Path.Combine(installPath, Identifier, "rust_server.exe")))
                {
                    if (File.Exists(Path.Combine(installPath, Identifier, "rust_server_data\\maindata")))
                    {
                        CreateServerConfig();
                        Logger.Log("Rust seems to have installed successfully.");
                        success = true;
                    }
                }
                else
                {
                    Console.WriteLine("Couldn't find critical game files, aborting installation.");
                    Logger.Log("Couldn't find critical game files, aborting installation.");
                    Directory.Delete(Path.Combine(installPath, Identifier));
                }
            }
            catch (Exception e)
            {
                DeploymentResults.ExceptionThrown = true;
                DeploymentResults.Exceptions.Add(e);
            }
            return success;
        }

        private static string GeneratePassword()
        {
            string pass = RandomPassword.Generate(11, 12);
            return pass;
        }

        public static void CopyDirectory(string source, string target)
        {
            var stack = new Stack<Folders>();
            stack.Push(new Folders(source, target));

            while (stack.Count > 0)
            {
                Folders folders = stack.Pop();
                Directory.CreateDirectory(folders.Target);
                foreach (string file in Directory.GetFiles(folders.Source, "*.*"))
                {
                    string targetFile = Path.Combine(folders.Target, Path.GetFileName(file));
                    if (File.Exists(targetFile)) File.Delete(targetFile);
                    File.Copy(file, targetFile);
                }

                foreach (string folder in Directory.GetDirectories(folders.Source))
                {
                    stack.Push(new Folders(folder, Path.Combine(folders.Target, Path.GetFileName(folder))));
                }
            }
        }
    }
}