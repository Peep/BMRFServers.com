﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using System.ServiceProcess;

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
            try 
            {
                string configDir = "C:\\servertools\\RustDeployService";

                if (!Directory.Exists(configDir))
                {
                    Directory.CreateDirectory(configDir);
                }
                cfg = new Config(configDir + "\\Settings.cfg");
            } 
            catch (Exception e) 
            {
                DeploymentResults.ExceptionThrown = true;
                DeploymentResults.Exceptions.Add(e);
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
                if (!Directory.Exists(String.Format(@"{0}\{1}", cfg.InstallPath, Identifier)))
                {
                    Directory.CreateDirectory(String.Format(@"{0}\{1}", cfg.InstallPath, Identifier));
                }

                var chars = "abcdefghijklmnopqrstuvwxyz0123456789";
                var random = new Random();

                var rconPass = new string(
                Enumerable.Repeat(chars, 8)
                .Select(s => s[random.Next(s.Length)])
                .ToArray());

                using (StreamWriter cfgWriter = new StreamWriter(String.Format(@"{0}\{1}\server.cfg", cfg.InstallPath, Identifier))) 
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

        public Results RunSteamUpdate(string ident)
        {
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
                cfg.AppPath, Identifier);

            steam.Start();

            steam.WaitForExit();

            StartServer(ident);

            return new Results
            {
                Success = true,
                Message = "Your Rust server has been updated successfully."
            };
        }

        public Results InstallMagma(string ident)
        {
            if (!Directory.Exists(String.Format(@"{0}\{1}\rust_server_Data", cfg.InstallPath, ident)))
            {
                return new Results
                {
                    Success = false,
                    Message = "The specified user was not found on this machine."
                };
            }

            if (!Directory.Exists(String.Format(@"{0}\magma\rust_server_Data")))
            {
                return new Results
                {
                    Success = false,
                    Message = "This machine is not setup to support Magma installations."
                };
            }

            try
            {
                MoveDirectory(String.Format(@"{0}\magma\rust_server_Data", cfg.AppPath),
                              String.Format(@"{0}\{1}\rust_server_Data", cfg.InstallPath, ident));
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
                Message = "One or more core Magma files were missing on {0}'s server."
            };
        }

        public Results UninstallMagma(string ident)
        {
            try
            {
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

        public static void MoveDirectory(string source, string target)
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
                    File.Move(file, targetFile);
                }

                foreach (var folder in Directory.GetDirectories(folders.Source))
                {
                    stack.Push(new Folders(folder, Path.Combine(folders.Target, Path.GetFileName(folder))));
                }
            }
            Directory.Delete(source, true);
        }
    }
}
