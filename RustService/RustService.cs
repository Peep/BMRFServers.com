using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.ServiceProcess;
using System.Xml.Linq;

namespace Rust 
{
    public class RustService : IRustService 
    {
        public Config cfg;
        public string Identifier;

        public void InitializeConfigurationFile() 
        {
            string configDir = "C:\\servertools\\RustDeployService";

            if (!Directory.Exists(configDir))
                Directory.CreateDirectory(configDir);

            if (cfg == null)
                cfg = new Config(configDir + "\\Settings.cfg");
        }

        private void WriteToEventLog(string message, EventLogEntryType type)
        {
            string cs = "RustDeployService";
            var logType = type;

            if (!EventLog.SourceExists(cs))
                EventLog.CreateEventSource(cs, "Application");    

            EventLog.WriteEntry(cs, message, logType);
        }

        public Dictionary<string, object> DeployRustServer(string identifier, int slots) 
        {
            Identifier = identifier;
            string password = GeneratePassword();
            InitializeConfigurationFile();

            InstallRustServer();

            FireDaemon service = new FireDaemon(cfg, Identifier, slots);
            service.GenerateXml();

            FileZilla ftpUser = new FileZilla(cfg, Identifier, slots, password);
            ftpUser.GenerateXml();

            var dic = new Dictionary<string, object>();

            dic.Add("Port", DeploymentResults.Port);
            dic.Add("Password", password);
            dic.Add("ExceptionThrown", DeploymentResults.ExceptionThrown);
            dic.Add("Exceptions", DeploymentResults.Exceptions);

            return dic;
        }

        public void CreateSteamScript(string ident) 
        {
            try 
            {
                using (StreamWriter scriptWriter = new StreamWriter(Path.Combine(cfg.AppPath, "scripts", ident + ".txt"), true)) 
                {
                    scriptWriter.WriteLine("@ShutdownOnFailedCommand 1");
                    scriptWriter.WriteLine("@NoPromptForPassword 1");
                    scriptWriter.WriteLine("login anonymous");
                    scriptWriter.WriteLine(String.Format(@"force_install_dir {0}\{1}", cfg.InstallPath, ident));
                    scriptWriter.WriteLine("app_update 258550 -beta stable -betapassword 05094c962cf2f502bfdfdebf800dd5d3 validate");
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
            string configPath = "save\\myserverdata\\cfg";
            try 
            {
                if (!Directory.Exists(Path.Combine(cfg.InstallPath, Identifier, configPath)))
                    Directory.CreateDirectory(Path.Combine(cfg.InstallPath, Identifier, configPath));

                var chars = "abcdefghijklmnopqrstuvwxyz0123456789";
                var random = new Random();

                var rconPass = new string(
                Enumerable.Repeat(chars, 8)
                .Select(s => s[random.Next(s.Length)])
                .ToArray());

                using (StreamWriter cfgWriter = new StreamWriter(Path.Combine(cfg.InstallPath, Identifier, configPath, "server.cfg"))) 
                {
                    cfgWriter.WriteLine(String.Format(@"server.hostname ""{0}'s BMRFServers.com Rust Server""", Identifier));
                    cfgWriter.WriteLine(String.Format(@"rcon.password ""{0}""", rconPass));
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
                string steamPath = cfg.SteamPath;
                string installPath = cfg.InstallPath;

                Logger.Log(String.Format("Starting new Customer installation for {0}", Identifier));

                if (!Directory.Exists(Path.Combine(cfg.AppPath, "scripts")))
                    Directory.CreateDirectory(Path.Combine(cfg.AppPath, "scripts"));

                if (!File.Exists(String.Format(@"{0}\scripts\{1}.txt", cfg.AppPath, Identifier)))
                {
                    string ident = Identifier;
                    CreateSteamScript(ident);
                }

                Process steam = new Process();

                steam.StartInfo.FileName = Path.Combine(cfg.SteamPath, "steamcmd.exe");
                steam.StartInfo.UseShellExecute = false;

                steam.StartInfo.Arguments = String.Format(@"+runscript {0}\scripts\{1}.txt",
                    cfg.AppPath, Identifier);

                steam.StartInfo.RedirectStandardOutput = true;
                steam.StartInfo.RedirectStandardError = true;

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

        public Results UninstallRustServer(string ident)
        {
            InitializeConfigurationFile();

            if (Directory.Exists(Path.Combine(cfg.InstallPath, ident)))
            {
                StopServer(ident);
                Directory.Delete(Path.Combine(cfg.InstallPath, ident), true);

                XDocument xDoc = XDocument.Load(Path.Combine(cfg.FileZillaPath, "FileZilla Server.xml"));

                foreach (var item in xDoc.Descendants("User"))
                {
                    if (item.Attribute("Name").Value == ident)
                        item.Attribute("Name").Remove();
                }

                xDoc.Save(Path.Combine(cfg.FileZillaPath, "FileZilla Server.xml"));
                Process.Start(cfg.FileZillaPath + "\\FileZilla Server.exe", "/reload-config");

                return new Results { Success = true, Message = "The specified user was successfully removed from this machine." };
            }
            else
            {
                return new Results { Success = false, Message = "That user was not found on this machine." };
            }
        }

        public Results RunSteamUpdate(string ident)
        {
            InitializeConfigurationFile();

            if (!File.Exists(String.Format(@"{0}\scripts\{1}.txt", cfg.InstallPath, ident)))
                CreateSteamScript(ident);

            var stop = StopServer(ident);

            if (!stop.Success)
                return new Results { Success = false, Message = stop.Message };

            Process steam = new Process();

            steam.StartInfo.FileName = Path.Combine(cfg.SteamPath, "steamcmd.exe");
            steam.StartInfo.UseShellExecute = false;

            steam.StartInfo.Arguments = String.Format(@"+runscript {0}\scripts\{1}.txt",
                cfg.AppPath, ident);

            steam.Start();

            steam.WaitForExit(180000);

            StartServer(ident);

            return new Results { Success = true, Message = "Your Rust server has been updated successfully." };
        }

        public Results InstallMagma(string ident)
        {
            InitializeConfigurationFile();
            if (!Directory.Exists(Path.Combine(cfg.InstallPath, ident, "rust_server_Data")))
                return new Results { Success = false, Message = "The specified user was not found on this machine." };

            if (!Directory.Exists(Path.Combine(cfg.AppPath, "magma\\rust_server_Data")))
                return new Results { Success = false, Message = "This machine is not setup to support Magma installations." };

            try
            {
                StartServer(ident);

                CopyDirectory(Path.Combine(cfg.AppPath, "magma\\rust_server_Data"),
                              Path.Combine(cfg.InstallPath, ident, "rust_server_Data"));

                CopyDirectory(Path.Combine(cfg.AppPath, "magma\\save"),
                              Path.Combine(cfg.InstallPath, ident, "save"));

                return new Results { Success = true, Message = "The operation completed successfully." };
            }
            catch (Exception e)
            {
                return new Results { Success = false, Message = "Exception: " + e.ToString() };
            }
        }

        public MagmaResults MagmaStatus(string ident)
        {
            InitializeConfigurationFile();
            string libFolder = "rust_server_data\\Managed";

            if (File.Exists(Path.Combine(cfg.InstallPath, ident, libFolder, "Jint.dll")))
                if (File.Exists(Path.Combine(cfg.InstallPath, ident, libFolder, "Magma.dll")))
                    if (File.Exists(Path.Combine(cfg.InstallPath, ident, libFolder, "Mono.Cecil.dll")))
                        if (Directory.Exists(Path.Combine(cfg.InstallPath, ident, "save\\Magma")))
                        {
                            return new MagmaResults { Success = true, Installed = true, Message = String.Format("All core Magma files were found on {0}'s server.", ident) };
                        }
            return new MagmaResults { Success = true, Installed = false, Message = String.Format("One or more core Magma files were missing on {0}'s server.", ident) };
        }

        public Results UninstallMagma(string ident)
        {
            InitializeConfigurationFile();
            string libFolder = "rust_server_data\\Managed";

            try
            {
                StopServer(ident);

                File.Copy(Path.Combine(cfg.AppPath, "premagma", libFolder, "Assembly-CSharp.dll"),
                          Path.Combine(cfg.InstallPath, ident, libFolder, "Assembly-CSharp.dll"), true);
                File.Delete(Path.Combine(cfg.InstallPath, ident, libFolder, "Jint.dll"));
                File.Delete(Path.Combine(cfg.InstallPath, ident, libFolder, "Magma.dll"));
                File.Delete(Path.Combine(cfg.InstallPath, ident, libFolder, "Mono.Cecil.dll"));

                return new Results { Success = true, Message = String.Format("Magma was successfully uninstalled from {0}'s server", ident) };
            }
            catch (Exception e)
            {
                return new Results { Success = false, Message = "Exception: " + e.ToString() };
            }
        }

        public Results ChangeFtpPass(string ident, string newPass)
        {
            InitializeConfigurationFile();    

            XDocument xDoc = XDocument.Load(Path.Combine(cfg.FileZillaPath, "FileZilla Server.xml"));

            var password = (from x in xDoc.Descendants("User")
                            where x.Attribute("Name").Value == ident
                            select x.Element("Option")).FirstOrDefault();

            if (password != null)
            {
                password.SetValue(Crypto.GetMd5(newPass));

                xDoc.Save(Path.Combine(cfg.FileZillaPath, "FileZilla Server.xml"));
                Process.Start(Path.Combine(cfg.FileZillaPath, "FileZilla Server.exe"), "/reload-config");

                return new Results { Success = true, Message = "The password was changed successfully." };
            }
            else
            {
                return new Results { Success = false, Message = "The specified user was not found on this machine." };
            }
        }

        public CheatpunchResults ToggleCheatpunch(string ident, int action)
        {
            InitializeConfigurationFile();
            string serviceName = String.Format("Rust - {0}", ident);
            string xmlPath = Path.Combine(cfg.AppPath, String.Format("export\\{0}_export.xml", ident));

            if (action < 0 || action > 2)
                return new CheatpunchResults { Success = false, Message = "This method was not called properly, action must be between 0 and 2." };

            try
            {
                if (!Directory.Exists(Path.Combine(cfg.AppPath, "export")))
                    Directory.CreateDirectory(Path.Combine(cfg.AppPath, "export"));

                if (File.Exists(xmlPath))
                    File.Delete(xmlPath);

                using (var fd = new Process())
                {
                    fd.StartInfo.FileName = Path.Combine(cfg.FDPath, "FireDaemon.exe");
                    fd.StartInfo.Arguments = String.Format(@"--export ""{0}"" {1}",
                        serviceName, xmlPath);

                    fd.Start();
                    fd.WaitForExit();
                }

                // Skip the first two lines in the exported file
                // This is done to remove the comment header so it will save properly.
                var lines = File.ReadAllLines(xmlPath);
                File.WriteAllLines(xmlPath, lines.Skip(2).ToArray());

                if (File.Exists(xmlPath))
                {
                    var xDoc = XDocument.Load(xmlPath);

                    var parameters = (from x in xDoc.Descendants("Program")
                                      where x.Element("Name").Value == serviceName
                                      select x.Element("Parameters")).FirstOrDefault();

                    if (parameters.Value.Contains("-cheatpunch") && action == 2)
                        return new CheatpunchResults { Success = true, Enabled = true, Message = "Cheatpunch is enabled" };

                    if (!parameters.Value.Contains("-cheatpunch") && action == 2)
                        return new CheatpunchResults { Success = true, Enabled = false, Message = "Cheatpunch is disabled" };

                    if (parameters.Value.Contains("-cheatpunch") && action == 1)
                        return new CheatpunchResults { Success = true, Enabled = true, Message = "Cheatpunch is already enabled" };

                    else if (!parameters.Value.Contains("-cheatpunch") && action == 1)
                    {
                        parameters.SetValue(parameters.Value + " -cheatpunch");
                        xDoc.Save(xmlPath);

                        Process.Start(Path.Combine(cfg.FDPath, "FireDaemon.exe"),
                            String.Format(@"--install {0} edit", xmlPath));

                        return new CheatpunchResults { Success = true, Enabled = true, Message = "Cheatpunch has been enabled." };
                    }
                    else if (parameters.Value.Contains("-cheatpunch") && action == 0)
                    {
                        parameters.SetValue(parameters.Value.Replace("-cheatpunch", ""));
                        xDoc.Save(xmlPath);

                        var process = Process.Start(Path.Combine(cfg.FDPath, "FireDaemon.exe"),
                            String.Format(@"--install {0} edit", xmlPath));

                        return new CheatpunchResults { Success = true, Enabled = false, Message = "Cheatpunch has been disabled." };
                    }
                }

                return new CheatpunchResults { Success = false, Message = "No clauses were satisfied. Does the user exist?" };
            }
            catch (Exception e)
            {
                Logger.Log(e.ToString());
                return new CheatpunchResults { Success = false, Message = e.ToString() };
            }
            finally
            {
                //File.Delete(xmlPath);
            }
        }

        public ConfigResults GetConfigValue(string ident, string value = "all")
        {
            InitializeConfigurationFile();
            string configPath = Path.Combine(cfg.InstallPath, ident, "save\\myserverdata\\cfg\\server.cfg");
        
            if (!File.Exists(configPath))
                return new ConfigResults { Success = false, Message = "Config file not found. Either the file is missing or the user doesn't exist." };

            var configFile = File.ReadAllLines(configPath);
            var dic = new Dictionary<string, string>();

            foreach (var line in configFile)
            {
                var splitValues = line.Split(new char[] {' '}, 2);
                splitValues[1] = splitValues[1].Trim('"');
                if (value == "all")
                    dic.Add(splitValues[0], splitValues[1]);
                else if (splitValues[0] == value)
                    dic.Add(splitValues[0], splitValues[1]);
            }
            if (dic.Count == 0)
            {
                return new ConfigResults { Success = false, Message = "The specified config value was not present in the file. Add it first." };
            }
            return new ConfigResults { Success = true, ConfigValues = dic };
        }

        public Results SetConfigValue(string ident, string key, string value)
        {
            try
            {
                InitializeConfigurationFile();
                string configPath = Path.Combine(cfg.InstallPath, ident, "save\\myserverdata\\cfg\\server.cfg");

                if (!File.Exists(configPath))
                    return new Results { Success = false, Message = "Config file not found. Either the file is missing or the user doesn't exist." };

                bool useDoubleQuotes = true;
                if (String.Equals(value, "true", StringComparison.CurrentCultureIgnoreCase) || String.Equals(value, "false", StringComparison.CurrentCultureIgnoreCase))
                    useDoubleQuotes = false;

                long parsedResult;
                bool canConvert = long.TryParse(value, out parsedResult);
                if (canConvert)
                    useDoubleQuotes = false;

                var configFile = File.ReadAllLines(configPath);

                for (int i = 0; i < configFile.Length; i++)
                {
                    var splitValues = configFile[i].Split(new char[] { ' ' }, 2); // Separate the key and value using a space.
                    if (splitValues[0] == key) // If the specified key exists, update it's value, write, and return.
                    {
                        splitValues[1] = value;
                        if (useDoubleQuotes)
                            configFile[i] = String.Format(@"{0} ""{1}""", splitValues[0], splitValues[1]);
                        else
                            configFile[i] = String.Format(@"{0} {1}", splitValues[0], splitValues[1]);
                        File.WriteAllLines(configPath, configFile);
                        return new Results { Success = true, Message = "The existing config value was updated." };
                    }
                }

                using (var writer = new StreamWriter(configPath, true)) // If we haven't returned yet, assume the value doesn't exist and write it as a new line.
                    writer.WriteLine(String.Format(@"{0} ""{1}""", key, value));

                return new Results { Success = true, Message = "The specified config value was added to the config file" };
            }
            catch (Exception e)
            {
                WriteToEventLog(e.ToString(), EventLogEntryType.Error);
                return new Results { Success = false, Message = "An error occurred while trying to write to your config file, please contact support." };
            }
        }

        public ServiceResults ServerStatus(string ident)
        {
            ServiceController ctl = ServiceController.GetServices().FirstOrDefault
                (s => s.ServiceName == String.Format("Rust - {0}", ident));

            if (ctl == null)
            {
                WriteToEventLog(String.Format("An incorrect call was made to StartServer(): Identifier '{0}' does not exist on this machine.",
                                    ident), EventLogEntryType.FailureAudit);
                return new ServiceResults { Success = false, Message = "Unable to find a server associated with your account, please contact support." };
            }
            string status = ctl.Status.ToString();
            return new ServiceResults { Success = true, Status = status, Message = "Your Rust server is " + status };
        }

        public Results StartServer(string ident)
        {
            try
            {
                ServiceController ctl = ServiceController.GetServices().FirstOrDefault
                    (s => s.ServiceName == String.Format("Rust - {0}", ident));

                if (ctl == null)
                {
                    WriteToEventLog(String.Format("An incorrect call was made to StartServer(): Identifier '{0}' does not exist on this machine.",
                                        ident), EventLogEntryType.FailureAudit);
                    return new Results { Success = false, Message = "Unable to find a server associated with your account, please contact support." };
                }

                if (ctl.Status == ServiceControllerStatus.Running)
                    return new Results { Success = true, Message = "Your Rust server is already running." };

                ctl.Start();
                ctl.WaitForStatus(ServiceControllerStatus.Running, new TimeSpan(0, 0, 5));

                if (ctl.Status == ServiceControllerStatus.Running)
                {
                    WriteToEventLog(String.Format("Server assigned to '{0}' has been started successfully.",
                                        ident), EventLogEntryType.SuccessAudit);
                    return new Results
                    { Success = true, Message = "Your Rust server has been started." };
                }
                else
                    return new Results { Success = false, Message = "Timed out while attempting to start your Rust server, please contact support." };
            }
            catch (Exception e)
            {
                WriteToEventLog(e.ToString(), EventLogEntryType.Error);
                return new Results { Success = false, Message = "An error occured while attempting to start your Rust server, please contact support." };
            }
        }

        public Results StopServer(string ident)
        {
            try
            {
                ServiceController ctl = ServiceController.GetServices().FirstOrDefault
                        (s => s.ServiceName == String.Format("Rust - {0}", ident));

                if (ctl == null)
                {
                    WriteToEventLog(String.Format("An incorrect call was made to StopServer(): Identifier '{0}' does not exist on this machine.",
                                        ident), EventLogEntryType.FailureAudit);
                    return new Results { Success = false, Message = "Unable to find a server associated with your account, please contact support." };
                }

                if (ctl.Status == ServiceControllerStatus.Stopped)
                    return new Results { Success = true, Message = "Your Rust server is already stopped." };

                ctl.Stop();
                ctl.WaitForStatus(ServiceControllerStatus.Stopped, new TimeSpan(0, 0, 5));

                if (ctl.Status == ServiceControllerStatus.Stopped)
                {
                    WriteToEventLog(String.Format("Server assigned to '{0}' has been stopped successfully.",
                                       ident), EventLogEntryType.SuccessAudit);
                    return new Results { Success = true, Message = "Your Rust server has been stopped." };
                }
                else
                {
                    return new Results {Success = false, Message = "Timed out while attempting to stop your Rust server, please contact support." };
                }
            }
            catch (Exception e)
            {
                WriteToEventLog(e.ToString(), EventLogEntryType.Error);
                return new Results { Success = false, Message = "An error occured while attempting to stop your Rust server, please contact support." };
            }
        }

        public Results RestartServer(string ident)
        {
            try
            {
                ServiceController ctl = ServiceController.GetServices().FirstOrDefault
                           (s => s.ServiceName == String.Format("Rust - {0}", ident));

                if (ctl.Status == ServiceControllerStatus.Running)
                {
                    var stop = StopServer(ident);
                    ctl.WaitForStatus(ServiceControllerStatus.Stopped, new TimeSpan(0, 0, 5));

                    if (!stop.Success)
                        return new Results { Success = false, Message = stop.Message };
                }

                var start = StartServer(ident);
                ctl.WaitForStatus(ServiceControllerStatus.Running, new TimeSpan(0, 0, 5));

                if (!start.Success)
                    return new Results { Success = false, Message = start.Message };
                
                return new Results { Success = true, Message = "Your Rust server has been restarted successfully." };
            }
            catch (Exception e)
            {
                WriteToEventLog(e.ToString(), EventLogEntryType.Error);
                return new Results { Success = false, Message = "An error occured while attempting to restart your Rust server, please contact support." };
            }
        }

        static string GeneratePassword() 
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
                var folders = stack.Pop();
                Directory.CreateDirectory(folders.Target);
                foreach (var file in Directory.GetFiles(folders.Source, "*.*"))
                {
                    string targetFile = Path.Combine(folders.Target, Path.GetFileName(file));
                    if (File.Exists(targetFile)) File.Delete(targetFile);
                    File.Copy(file, targetFile);
                }

                foreach (var folder in Directory.GetDirectories(folders.Source))
                {
                    stack.Push(new Folders(folder, Path.Combine(folders.Target, Path.GetFileName(folder))));
                }
            }
        }
    }
}
