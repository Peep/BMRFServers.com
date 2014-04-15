using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Text;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;
using System.ServiceProcess;

namespace Rust 
{
    public class FireDaemon 
    {
        Config cfg;
        string ident;
        int slots;
        int port;

        public FireDaemon(Config config, string identifier, int slots) 
        {
            this.cfg = config;
            this.ident = identifier;
            this.slots = slots;
        }

        public void WriteXml(XDocument xml, string filename) 
        {
            try 
            {
                if (!Directory.Exists(String.Format(@"{0}\xml", cfg.AppPath)))
                {
                    Directory.CreateDirectory(String.Format(@"{0}\xml", cfg.AppPath));
                }

                xml.Save(String.Format(@"{0}\xml\{1}.xml", cfg.AppPath, filename));
                InstallService();

            } 
            catch (Exception e) 
            {
                Console.WriteLine(e.ToString());
                DeploymentResults.ExceptionThrown = true;
                DeploymentResults.Exceptions.Add(e);
            }
        }

        // I really don't like this method of deciding whether a port is valid,
        // should probably come up with something different in the future.
        public int GetValidPort() 
        {
            try 
            {
                bool validPort = false;
                int port = 28015;

                if (!File.Exists(String.Format(@"{0}\ports.cfg", cfg.AppPath)))
                {
                    File.Create(String.Format(@"{0}\ports.cfg", cfg.AppPath));
                }

                List<string> parseFile = File.ReadLines(String.Format(@"{0}\ports.cfg", cfg.AppPath)).ToList();
                var ports = parseFile.Select(int.Parse).ToList();

                while (!validPort) 
                {
                    if (ports.Contains(port))
                    {
                        port += 5;
                    }
                    else
                    {
                        validPort = true;
                    }
                }

                this.port = port;

            } 
            catch (Exception e) 
            {
                DeploymentResults.ExceptionThrown = true;
                DeploymentResults.Exceptions.Add(e);
            }

            return this.port;
        }

        public void InstallService() 
        {
            try 
            {
                Logger.Log("Installing FireDaemon Service");
                Console.WriteLine("Installing FireDaemon Service");

                Process fd = new Process();

                fd.StartInfo.FileName = String.Format(@"{0}\FireDaemon.exe", cfg.FDPath);
                fd.StartInfo.UseShellExecute = false;
                fd.StartInfo.Arguments = String.Format(@"--import-all {0}\xml\*.xml", cfg.AppPath);

                fd.StartInfo.RedirectStandardOutput = true;
                fd.StartInfo.RedirectStandardError = true;

                fd.Start();

                string output = fd.StandardOutput.ReadToEnd();
                string error = fd.StandardError.ReadToEnd();

                Logger.Log(error);
                Logger.Log(output);

                fd.WaitForExit();
            } 
            catch (Exception e) 
            {
                Logger.Log(e.ToString());
            }

            ServiceController ctl = ServiceController.GetServices()
                .FirstOrDefault(s => s.ServiceName == String.Format("Rust - {0}", ident));

            if (ctl == null) 
            {
                Logger.Log("FireDaemon Service installation failed!");
            } 

            else 
            {
                Logger.Log(String.Format("{0}'s Rust Server is {1}", ident, ctl.Status));
                using (StreamWriter writer = new StreamWriter(String.Format(@"{0}\ports.cfg", cfg.AppPath), true))
                    writer.WriteLine(port);

                DeploymentResults.Port = port;
            }
        }

        public void GenerateXml() {
            int port = GetValidPort();

            XDocument service = new XDocument(
                new XElement("Service",
                    new XElement("Program",
                        new XElement("Name", String.Format("Rust - {0}", ident)),
                        new XElement("DisplayName", String.Format("Rust - {0}", ident)),
                        new XElement("Description", String.Format("Port - {0}", port)),
                        new XElement("WorkingDir", String.Format(@"{0}\{1}", cfg.InstallPath, ident)),
                        new XElement("Executable", String.Format(@"{0}\{1}\rust_server.exe", cfg.InstallPath, ident)),
                        new XElement("Parameters", String.Format(@"-batchmode -maxplayers ""{0}"" -port {1} -datadir ""save/myserverdata/"" -cfg ""{2}\server.cfg""", slots, port, cfg.CfgDir)),
                        new XElement("Delay", "3000"),
                        new XElement("StartUpMode", "1"),
                        new XElement("ForceReplace", "true")
                    ),
                    new XElement("Options",
                        new XElement("AffinityMask", "0"),
                        new XElement("Priority", "0"),
                        new XElement("AppendLogs", "true"),
                        new XElement("EventLogging", "true"),
                        new XElement("InteractWithDesktop", "false"),
                        new XElement("PreLaunchDelay", "0"),
                        new XElement("ConsoleApp", "true"),
                        new XElement("CtrlC", "1"),
                        new XElement("UponExit", "1"),
                        new XElement("FlapCount", "0"),
                        new XElement("UponFail", "0"),
                        new XElement("FailCount", "0"),
                        new XElement("ShutdownDelay", "5000"),
                        new XElement("PreShutdown", "0"),
                        new XElement("PreShutdownDelay", "180000"),
                        new XElement("ShowWindow", "0"),
                        new XElement("JobType", "0"),
                        new XElement("IgnoreFlags", "3")
                    ),
                //new XElement("Logon", new XElement("AccountName", @".\rust")),
                    new XElement("SMF", new XElement("SMFEnabled", "true"), new XElement("SMFFrequency", "5000")),
                    new XElement("Scheduling",
                        new XElement("StartTime", "00:00:00"),
                        new XElement("EndTime", "00:00:00"),
                        new XElement("RunDays", "127"),
                        new XElement("MonthFrom", "0"),
                        new XElement("MonthTo", "0"),
                        new XElement("RestartFreq", "0"),
                        new XElement("RestartEvery", "60"),
                        new XElement("RestartDelay", "0"),
                        new XElement("RestartTime", "00:00:00")
                    ),
                    new XElement("DlgResponder",
                        new XElement("Enabled", "false"),
                        new XElement("CloseAll", "false"),
                        new XElement("CheckFrequency", "5000"),
                        new XElement("IgnoreUnknowns", "true"),
                        new XElement("Responses")
                    ),
                    new XElement("Recovery",
                        new XElement("FirstFailure", "0"),
                        new XElement("SecondFailure", "0"),
                        new XElement("Subsequent", "0"),
                        new XElement("ResetFailCountAfter", "0"),
                        new XElement("RestartServiceDelay", "0"),
                        new XElement("RestartComputerDelay", "0"),
                        new XElement("Program"),
                        new XElement("CommandLineParams"),
                        new XElement("AppendFailCount", "false"),
                        new XElement("EnableActionsForStopWithErrors", "false"),
                        new XElement("SendMsg", "false"),
                        new XElement("RebootMsg")
                    )
                )
            );

            //XDocument updater = new XDocument(
            //    new XElement("Service",
            //        new XElement("Program",
            //            new XElement("Name", String.Format("Rust - {0} Update", ident)),
            //            new XElement("DisplayName", String.Format("Rust - {0} Update", ident)),
            //            new XElement("Description", "Updates server. Turn off server before running. Let this run for 60 seconds."),
            //            new XElement("WorkingDir", cfg.SteamPath),
            //            new XElement("Executable", String.Format(@"{0}\steamcmd.exe", cfg.SteamPath)),
            //            new XElement("Parameters", String.Format(@"+runscript {0}\scripts\{1}.txt", cfg.AppPath, ident)),
            //            new XElement("Delay", "3000"),
            //            new XElement("StartUpMode", "0"),
            //            new XElement("ForceReplace", "true")
            //        ),
            //        new XElement("Options",
            //            new XElement("AffinityMask", "0"),
            //            new XElement("Priority", "0"),
            //            new XElement("AppendLogs", "true"),
            //            new XElement("EventLogging", "true"),
            //            new XElement("InteractWithDesktop", "true"),
            //            new XElement("PreLaunchDelay", "0"),
            //            new XElement("ConsoleApp", "false"),
            //            new XElement("CtrlC", "0"),
            //            new XElement("UponExit", "0"),
            //            new XElement("UponFlap", "0"),
            //            new XElement("FlapCount", "0"),
            //            new XElement("UponFail", "0"),
            //            new XElement("FailCount", "0"),
            //            new XElement("ShutdownDelay", "5000"),
            //            new XElement("PreShutdown", "0"),
            //            new XElement("PreShutdownDelay", "180000"),
            //            new XElement("ShowWindow", "0"),
            //            new XElement("JobType", "0"),
            //            new XElement("IgnoreFlags", "3")
            //        ),
            //        new XElement("SMF", new XElement("SMFEnabled", "true"), new XElement("SMFFrequency", "5000")),
            //        new XElement("Scheduling",
            //            new XElement("StartTime", "00:00:00"),
            //            new XElement("EndTime", "00:00:00"),
            //            new XElement("RunDays", "127"),
            //            new XElement("MonthFrom", "0"),
            //            new XElement("MonthTo", "0"),
            //            new XElement("RestartFreq", "0"),
            //            new XElement("RestartEvery", "60"),
            //            new XElement("RestartDelay", "0"),
            //            new XElement("RestartTime", "00:00:00")
            //        ),
            //        new XElement("PreService",
            //            new XElement("PreWorkingDir", String.Format(@"{0}\scripts", cfg.AppPath)),
            //            new XElement("PreExecutable", String.Format(@"{0}\scripts\stopservice.bat", cfg.AppPath)),
            //            new XElement("PreEventOrder", "1"),
            //            new XElement("PreDeatched", "1"),
            //            new XElement("PreParameters", String.Format(@"""Rust - {0}""", ident)),
            //            new XElement("PreDelay", "3000")
            //        ),
            //        new XElement("PostService",
            //            new XElement("PostWorkingDir", String.Format(@"{0}\scripts", cfg.AppPath)),
            //            new XElement("PostExecutable", String.Format(@"{0}\scripts\stopservice.bat", cfg.AppPath)),
            //            new XElement("PostEventOrder", "2"),
            //            new XElement("PostDeatched", "1"),
            //            new XElement("PostParameters", String.Format(@"""Rust - {0} Update""", ident)),
            //            new XElement("PostDelay", "3000")
            //        ),
            //        new XElement("DlgResponder",
            //            new XElement("Enabled", "false"),
            //            new XElement("CloseAll", "false"),
            //            new XElement("CheckFrequency", "5000"),
            //            new XElement("IgnoreUnknowns", "true"),
            //            new XElement("Responses")
            //        ),
            //        new XElement("Recovery",
            //            new XElement("FirstFailure", "0"),
            //            new XElement("SecondFailure", "0"),
            //            new XElement("Subsequent", "0"),
            //            new XElement("ResetFailCountAfter", "0"),
            //            new XElement("RestartServiceDelay", "0"),
            //            new XElement("RestartComputerDelay", "0"),
            //            new XElement("Program"),
            //            new XElement("CommandLineParams"),
            //            new XElement("AppendFailCount", "false"),
            //            new XElement("EnableActionsForStopWithErrors", "false"),
            //            new XElement("SendMsg", "false"),
            //            new XElement("RebootMsg")
            //        )
            //    )
            //);

            WriteXml(service, ident);
        }
    }
}
