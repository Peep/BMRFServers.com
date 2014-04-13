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

        public string GetData(int value) 
        {
            return string.Format("You entered: {0}", value);
        }

        public CompositeType GetDataUsingDataContract(CompositeType composite) 
        {
            if (composite == null) 
            {
                throw new ArgumentNullException("composite");
            }
            if (composite.BoolValue) 
            {
                composite.StringValue += "Suffix";
            }
            return composite;
        }

        public void InitializeConfigurationFile() 
        {
            string configDir = "C:\\servertools\\RustDeployService";

            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }
            if (cfg == null)
            {
                cfg = new Config(configDir + "\\Settings.cfg");
            }
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
                using (StreamWriter scriptWriter = new StreamWriter(String.Format(@"{0}\scripts\{1}.txt", cfg.AppPath, ident), true)) 
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
            try 
            {
                if (!Directory.Exists(String.Format(@"{0}\{1}\save\myserverdata\cfg", cfg.InstallPath, Identifier)))
                {
                    Directory.CreateDirectory(String.Format(@"{0}\{1}\save\myserverdata\cfg", cfg.InstallPath, Identifier));
                }

                var chars = "abcdefghijklmnopqrstuvwxyz0123456789";
                var random = new Random();

                var rconPass = new string(
                Enumerable.Repeat(chars, 8)
                .Select(s => s[random.Next(s.Length)])
                .ToArray());

                using (StreamWriter cfgWriter = new StreamWriter(String.Format(@"{0}\{1}\save\myserverdata\cfg\server.cfg", cfg.InstallPath, Identifier))) 
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

                if (!Directory.Exists(String.Format(@"{0}\scripts", cfg.AppPath)))
                {
                    Directory.CreateDirectory(String.Format(@"{0}\scripts", cfg.AppPath));
                }

                if (!File.Exists(String.Format(@"{0}\scripts\{1}.txt", cfg.AppPath, Identifier)))
                {
                    string ident = Identifier;
                    CreateSteamScript(ident);
                }

                Process steam = new Process();

                steam.StartInfo.FileName = String.Format(@"{0}\steamcmd.exe", cfg.SteamPath);
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

                if (File.Exists(String.Format(@"{0}\{1}\rust_server.exe", installPath, Identifier)))
                {
                    if (File.Exists(String.Format(@"{0}\{1}\rust_server_data\mainData", installPath, Identifier)))
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
                    Directory.Delete(String.Format(@"{0}\{1}", installPath, Identifier));
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

                XDocument xDoc = XDocument.Load(String.Format(@"{0}\FileZilla Server.xml", cfg.FileZillaPath));

                foreach (var item in xDoc.Descendants("User"))
                {
                    if (item.Attribute("Name").Value == ident)
                    {
                        item.Attribute("Name").Remove();
                    }
                }
              
                xDoc.Save(String.Format(@"{0}\FileZilla Server.xml", cfg.FileZillaPath));
                Process.Start(cfg.FileZillaPath + "\\FileZilla Server.exe", "/reload-config");

                return new Results
                {
                    Success = true,
                    Message = "The specified user was successfully removed from this machine."
                };
            }
            else
            {
                return new Results
                {
                    Success = false,
                    Message = "That user was not found on this machine."
                };
            }
        }

        public Results RunSteamUpdate(string ident)
        {
            InitializeConfigurationFile();

            if (!File.Exists(String.Format(@"{0}\scripts\{1}.txt", cfg.InstallPath, ident)))
            {
                CreateSteamScript(ident);
            }

            var stop = StopServer(ident);
            if (!stop.Success)
            {
                return new Results
                {
                    Success = false,
                    Message = stop.Message
                };
            }

            Process steam = new Process();

            steam.StartInfo.FileName = String.Format(@"{0}\steamcmd.exe", cfg.SteamPath);
            steam.StartInfo.UseShellExecute = false;

            steam.StartInfo.Arguments = String.Format(@"+runscript {0}\scripts\{1}.txt",
                cfg.AppPath, ident);

            steam.Start();

            steam.WaitForExit(180000);

            StartServer(ident);

            return new Results
            {
                Success = true,
                Message = "Your Rust server has been updated successfully."
            };
        }

        public Results InstallMagma(string ident)
        {
            InitializeConfigurationFile();

            if (!Directory.Exists(String.Format(@"{0}\{1}\rust_server_Data", cfg.InstallPath, ident)))
            {
                return new Results
                {
                    Success = false,
                    Message = "The specified user was not found on this machine."
                };
            }

            if (!Directory.Exists(String.Format(@"{0}\magma\rust_server_Data", cfg.AppPath)))
            {
                return new Results
                {
                    Success = false,
                    Message = "This machine is not setup to support Magma installations."
                };
            }

            try
            {
                StartServer(ident);

                CopyDirectory(String.Format(@"{0}\magma\rust_server_Data", cfg.AppPath),
                              String.Format(@"{0}\{1}\rust_server_Data", cfg.InstallPath, ident));

                CopyDirectory(String.Format(@"{0}\magma\save", cfg.AppPath),
                              String.Format(@"{0}\{1}\save", cfg.InstallPath, ident));

                return new Results
                {
                    Success = true,
                    Message = "The operation completed successfully."
                };
            }
            catch (Exception e)
            {
                return new Results 
                {
                    Success = false,
                    Message = "Exception: " + e.ToString()
                };
            }
        }

        public Results IsMagmaInstalled(string ident)
        {
            InitializeConfigurationFile();

            if (File.Exists(String.Format(@"{0}\{1}\rust_server_Data\Managed\Jint.dll", cfg.InstallPath, ident)))
                if (File.Exists(String.Format(@"{0}\{1}\rust_server_Data\Managed\Magma.dll", cfg.InstallPath, ident)))
                    if (File.Exists(String.Format(@"{0}\{1}\rust_server_Data\Managed\Mono.Cecil.dll", cfg.InstallPath, ident)))
                        if (Directory.Exists(String.Format(@"{0}\{1}\save\Magma", cfg.InstallPath, ident)))
                        {
                            return new Results
                            {
                                Success = true,
                                Message = String.Format("All core Magma files were found on {0}'s server.", ident)
                            };
                        }
            return new Results
            {
                Success = false,
                Message = String.Format("One or more core Magma files were missing on {0}'s server.", ident)
            };
        }

        public Results UninstallMagma(string ident)
        {
            InitializeConfigurationFile();

            try
            {
                StopServer(ident);

                File.Copy(String.Format(@"{0}\premagma\rust_server_Data\managed\Assembly-CSharp.dll", cfg.AppPath),
                          String.Format(@"{0}\{1}\rust_server_Data\Managed\Assembly-CSharp.dll", cfg.InstallPath, ident), true);

                File.Delete(String.Format(@"{0}\{1}\rust_server_data\Managed\Jint.dll", cfg.InstallPath, ident));
                File.Delete(String.Format(@"{0}\{1}\rust_server_data\Managed\Magma.dll", cfg.InstallPath, ident));
                File.Delete(String.Format(@"{0}\{1}\rust_server_data\Managed\Mono.Cecil.dll", cfg.InstallPath, ident));

                return new Results
                {
                    Success = true,
                    Message = String.Format("Magma was successfully uninstalled from {0}'s server", ident)
                };
            }
            catch (Exception e)
            {
                return new Results
                {
                    Success = false,
                    Message = "Exception: " + e.ToString()
                };
            }
        }

        public Results ChangeConfigValue(string ident, string key, string value)
        {
            return new Results
            {

            };
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

        public Results ToggleCheatpunch(string ident, bool enabled)
        {
            InitializeConfigurationFile();
            string serviceName = String.Format("Rust - {0}", ident);
            string xmlPath = Path.Combine(cfg.AppPath, "export\\export.xml");

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
                        serviceName, Path.Combine(cfg.AppPath, "export\\export.xml"));

                    fd.Start();
                    fd.WaitForExit();
                }

                var lines = File.ReadAllLines(xmlPath);
                File.WriteAllLines(xmlPath, lines.Skip(2).ToArray());

                if (File.Exists(xmlPath))
                {
                    var xDoc = XDocument.Load(xmlPath);

                    var parameters = (from x in xDoc.Descendants("Program")
                                      where x.Element("Name").Value == serviceName
                                      select x.Element("Parameters")).FirstOrDefault();

                    if (parameters.Value.Contains("-cheatpunch") && enabled)
                        return new Results { Success = true, Message = "Cheatpunch is already enabled" };

                    else if (!parameters.Value.Contains("-cheatpunch") && enabled)
                    {
                        parameters.SetValue(parameters.Value + " -cheatpunch");

                        xDoc.Save(xmlPath);

                        Process.Start(Path.Combine(cfg.FDPath, "FireDaemon.exe"),
                            String.Format(@"--install {0} edit", xmlPath));

                        return new Results { Success = true, Message = "Cheatpunch has been enabled." };
                    }
                    else if (parameters.Value.Contains("-cheatpunch") && !enabled)
                    {
                        parameters.SetValue(parameters.Value.Replace("-cheatpunch", ""));

                        xDoc.Save(xmlPath);

                        Process.Start(Path.Combine(cfg.FDPath, "FireDaemon.exe"),
                            String.Format(@"--install {0} edit", xmlPath));

                        return new Results { Success = true, Message = "Cheatpunch has been disabled." };
                    }
                }
                else
                {
                    return new Results { Success = false, Message = "It never works the first time." };
                }
                return new Results { Success = false, Message = "Reached the end without returning." };
            }
            catch (Exception e)
            {
                Logger.Log(e.ToString());
                return new Results { Success = false, Message = e.ToString() };
            }
            finally
            {
                File.Delete(xmlPath);
            }
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
                    return new Results
                    {
                        Success = false,
                        Message = "Unable to find a server associated with your account, please contact support."
                    };
                }

                if (ctl.Status == ServiceControllerStatus.Running)
                {
                    return new Results
                    {
                        Success = true,
                        Message = "Your Rust server is already running."
                    };
                }

                ctl.Start();
                ctl.WaitForStatus(ServiceControllerStatus.Running, new TimeSpan(0, 0, 5));

                if (ctl.Status == ServiceControllerStatus.Running)
                {
                    WriteToEventLog(String.Format("Server assigned to '{0}' has been started successfully.",
                                        ident), EventLogEntryType.SuccessAudit);
                    return new Results
                    {
                        Success = true,
                        Message = "Your Rust server has been started."
                    };
                }
                else
                {
                    return new Results
                    {
                        Success = false,
                        Message = "Timed out while attempting to start your Rust server, please contact support."
                    };
                }
            }
            catch (Exception e)
            {
                WriteToEventLog(e.ToString(), EventLogEntryType.Error);
                return new Results
                {
                    Success = false,
                    Message = "An error occured while attempting to start your Rust server, please contact support."
                };
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
                    return new Results
                    {
                        Success = false,
                        Message = "Unable to find a server associated with your account, please contact support."
                    };
                }

                if (ctl.Status == ServiceControllerStatus.Stopped)
                {
                    return new Results
                    {
                        Success = true,
                        Message = "Your Rust server is already stopped."
                    };
                }

                ctl.Stop();
                ctl.WaitForStatus(ServiceControllerStatus.Stopped, new TimeSpan(0, 0, 5));

                if (ctl.Status == ServiceControllerStatus.Stopped)
                {
                    WriteToEventLog(String.Format("Server assigned to '{0}' has been stopped successfully.",
                                       ident), EventLogEntryType.SuccessAudit);
                    return new Results
                    {
                        Success = true,
                        Message = "Your Rust server has been stopped."
                    };
                }
                else
                {
                    return new Results
                    {
                        Success = false,
                        Message = "Timed out while attempting to stop your Rust server, please contact support."
                    };
                }
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
            try
            {
                ServiceController ctl = ServiceController.GetServices().FirstOrDefault
                           (s => s.ServiceName == String.Format("Rust - {0}", ident));

                if (ctl.Status == ServiceControllerStatus.Running)
                {
                    var stop = StopServer(ident);
                    ctl.WaitForStatus(ServiceControllerStatus.Stopped, new TimeSpan(0, 0, 5));

                    if (!stop.Success)
                    {
                        return new Results
                        {
                            Success = false,
                            Message = stop.Message
                        };
                    }
                }

                var start = StartServer(ident);
                ctl.WaitForStatus(ServiceControllerStatus.Running, new TimeSpan(0, 0, 5));

                if (!start.Success)
                {
                    return new Results
                    {
                        Success = false,
                        Message = start.Message
                    };
                }
                return new Results
                {
                    Success = true,
                    Message = "Your Rust server has been restarted successfully."
                };
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
            //Directory.Delete(source, true);
        }
    }
}
