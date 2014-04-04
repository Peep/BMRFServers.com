using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Rust {
    public class RustService : IRustService {
        public Config cfg;
        public string Identifier;

        public string GetData(int value) {
            return string.Format("You entered: {0}", value);
        }

        public CompositeType GetDataUsingDataContract(CompositeType composite) {
            if (composite == null) {
                throw new ArgumentNullException("composite");
            }
            if (composite.BoolValue) {
                composite.StringValue += "Suffix";
            }
            return composite;
        }

        public void InitializeConfigurationFile() {
            string configDir = "C:\\servertools\\RustDeployService";
            if (!Directory.Exists(configDir))
                Directory.CreateDirectory(configDir);
            cfg = new Config(configDir + "\\Settings.cfg");
        }

        public DeploymentResults DeployRustServer(string identifier, int slots) {
            Identifier = identifier;
            string password = GeneratePassword();
            InitializeConfigurationFile();

            InstallRustServer();

            FireDaemon service = new FireDaemon(cfg, Identifier, slots);
            service.GenerateXml();

            FileZilla ftpUser = new FileZilla(cfg, Identifier, slots, password);
            ftpUser.GenerateXml();

            var results = new DeploymentResults() {
                Identifier = identifier, Port = 28015
            };
            return results;
        }

        public void CreateSteamScript() {
            using (StreamWriter scriptWriter = new StreamWriter(String.Format(@"{0}\scripts\{1}.txt", cfg.AppPath, Identifier), true)) {
                scriptWriter.WriteLine("@ShutdownOnFailedCommand 1");
                scriptWriter.WriteLine("@NoPromptForPassword 1");
                scriptWriter.WriteLine("login anonymous");
                scriptWriter.WriteLine(String.Format(@"force_install_dir {0}\{1}", cfg.InstallPath, Identifier));
                scriptWriter.WriteLine("app_update 258550 -beta stable -betapassword 05094c962cf2f502bfdfdebf800dd5d3 validate");
                scriptWriter.WriteLine("quit");
            }
        }

        public void CreateServerConfig() {
            if (!Directory.Exists(String.Format(@"{0}\{1}", cfg.InstallPath, Identifier)))
                Directory.CreateDirectory(String.Format(@"{0}\{1}", cfg.InstallPath, Identifier));

            var chars = "abcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            var rconPass = new string(
            Enumerable.Repeat(chars, 8)
            .Select(s => s[random.Next(s.Length)])
            .ToArray());
            using (StreamWriter cfgWriter = new StreamWriter(String.Format(@"{0}\{1}\server.cfg", cfg.InstallPath, Identifier))) {
                cfgWriter.WriteLine(String.Format(@"server.hostname ""{0}'s BMRFServers.com Rust Server""", Identifier));
                cfgWriter.WriteLine(String.Format(@"rcon.password ""{0}""", rconPass));
                cfgWriter.WriteLine(@"airdrop.min_players ""15""");
                cfgWriter.WriteLine(@"server.clienttimeout ""3""");
            }
        }

        public async void InstallRustServer() {
            string steamPath = cfg.SteamPath;
            string installPath = cfg.InstallPath;
            Logger.Log(String.Format("Starting new Customer installation for {0}", Identifier));
            try
            {
                if (!Directory.Exists(String.Format(@"{0}\scripts", cfg.AppPath)))
                    Directory.CreateDirectory(String.Format(@"{0}\scripts", cfg.AppPath));

                if (!File.Exists(String.Format(@"{0}\scripts\{1}.txt", cfg.AppPath, Identifier)))
                    CreateSteamScript();

                Task task = RunSteamAsync(cfg, Identifier);
                await task;

                //Process steam = new Process();
                //steam.StartInfo.FileName = String.Format(@"{0}\steamcmd.exe", cfg.SteamPath);
                //steam.StartInfo.UseShellExecute = false;
                //steam.StartInfo.Arguments = String.Format(@"+runscript {0}\scripts\{1}.txt", 
                //    cfg.AppPath, Identifier);
                //steam.StartInfo.RedirectStandardOutput = true;
                //steam.StartInfo.RedirectStandardError = true;
                //steam.Start();
                //Logger.Log(String.Format(@"Installing Rust to {0}\{1} -- please wait...", installPath, Identifier));

                //string output = steam.StandardOutput.ReadToEnd();
                //string error = steam.StandardError.ReadToEnd();
                //Logger.Log(error);
                //Logger.Log(output);
                //steam.WaitForExit();

                if (File.Exists(String.Format(@"{0}\{1}\rust_server.exe", installPath, Identifier)))
                {
                    if (File.Exists(String.Format(@"{0}\{1}\rust_server_data\mainData", installPath, Identifier)))
                    {
                        CreateServerConfig();
                        Logger.Log("Rust seems to have installed successfully.");
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
                Console.WriteLine(e.Message);
            }
        }

        static Task RunSteamAsync(Config cfg, string ident) {
            // there is no non-generic TaskCompletionSource
            var tcs = new TaskCompletionSource<bool>();


            Process steam = new Process();
            steam.StartInfo.FileName = String.Format(@"{0}\steamcmd.exe", cfg.SteamPath);
            steam.StartInfo.UseShellExecute = false;
            steam.StartInfo.Arguments = String.Format(@"+runscript {0}\scripts\{1}.txt",
                cfg.AppPath, ident);
            steam.StartInfo.RedirectStandardOutput = true;
            steam.StartInfo.RedirectStandardError = true;
            //steam.Start();

            //steam.WaitForExit();

            steam.Exited += (sender, args) => {
                tcs.SetResult(true);
                steam.Dispose();
            };

            steam.Start();
            string output = steam.StandardOutput.ReadToEnd();
            string error = steam.StandardError.ReadToEnd();
            Logger.Log(error);
            Logger.Log(output);
            //steam.WaitForExit();
            return tcs.Task;
        }

        static string GeneratePassword() {
            string pass = RandomPassword.Generate(11, 12);
            return pass;
        }
    }
}
