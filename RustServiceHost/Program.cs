using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using Rust;
using System.ServiceModel.Description;
using System.Configuration.Install;

namespace RustServiceHost 
{
    static class Program 
    {
        static void Main(string[] args) 
        {
            if (args.Length == 0) 
            {
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[] { new RustServiceHost() };
                ServiceBase.Run(ServicesToRun);
            } 
            else if (args.Length == 1) 
            {
                switch (args[0]) 
                {
                    case "-install":
                        InstallService();
                        StartService();
                        break;
                    case "-uninstall":
                        StopService();
                        UninstallService();
                        break;
                    default:
                        throw new NotImplementedException();
                }
            }
        }

        private static bool IsInstalled() 
        {
            using (ServiceController controller = new ServiceController("Rust Service Host")) 
                {
                try 
                {
                    ServiceControllerStatus status = controller.Status;
                } 
                catch 
                {
                    return false;
                }
                return true;
            }
        }

        private static bool IsRunning() 
        {
            using (ServiceController controller = new ServiceController("Rust Service Host")) 
            {
                if (!IsInstalled())
                {
                    return false;
                }
                return (controller.Status == ServiceControllerStatus.Running);
            }
        }

        private static AssemblyInstaller GetInstaller() 
        {
            AssemblyInstaller installer = new AssemblyInstaller(typeof(RustServiceHost).Assembly, null);
            installer.UseNewContext = true;
            return installer;
        }

        private static void InstallService() 
        {
            if (IsInstalled())
            {
                return;
            }

            try 
            {
                using (AssemblyInstaller installer = GetInstaller()) 
                {
                    IDictionary state = new Hashtable();
                    try 
                    {
                        installer.Install(state);
                        installer.Commit(state);
                    } 
                    catch 
                    {
                        try 
                        {
                            installer.Rollback(state);
                        }
                        catch 
                        {
                            throw; 
                        }
                    }
                }
            } 
            catch 
            {
                throw;
            }
        }

        private static void UninstallService() 
        {
            if (!IsInstalled())
            {
                return;
            }
            try 
            {
                using (AssemblyInstaller installer = GetInstaller()) 
                {
                    IDictionary state = new Hashtable();
                    try 
                    {
                        installer.Uninstall(state);
                    } 
                    catch 
                    {
                        throw;
                    }
                }
            } 
            catch 
            {
                throw;
            }
        }

        private static void StartService() 
        {
            if (!IsInstalled())
            {
                return;
            }

            using (ServiceController controller = new ServiceController("Rust Service Host")) 
            {
                try 
                {
                    if (controller.Status != ServiceControllerStatus.Running) 
                    {
                        controller.Start();
                        controller.WaitForStatus(ServiceControllerStatus.Running,
                            TimeSpan.FromSeconds(10));
                    }
                } 
                catch 
                {
                    throw;
                }
            }
        }

        private static void StopService() 
        {
            if (!IsInstalled())
            {
                return;
            }

            using (ServiceController controller = new ServiceController("Rust Service Host")) 
            {
                try 
                {
                    if (controller.Status != ServiceControllerStatus.Stopped) 
                    {
                        controller.Stop();
                        controller.WaitForStatus(ServiceControllerStatus.Stopped,
                             TimeSpan.FromSeconds(10));
                    }
                } 
                catch 
                {
                    throw;
                }
            }
        }
    }
}
