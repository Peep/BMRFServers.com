using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.ServiceModel;
using Rust;
using System.ServiceModel.Description;
using System.ServiceModel.Security;
using System.ServiceModel.Web;
using System.Timers;
using System.IO;

namespace RustServiceHost 
{
    public partial class RustServiceHost : ServiceBase 
    {
        Uri baseAddress = new Uri("https://bmrfservers.com:8000/Rustful");
        WebServiceHost serviceHost;
        WebHttpBinding binding = new WebHttpBinding();

        public RustServiceHost() 
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args) 
        {
            try 
            {
                binding.Security.Mode = WebHttpSecurityMode.Transport;
                serviceHost = new WebServiceHost(typeof(RustService), baseAddress);

                serviceHost.Credentials.UserNameAuthentication.UserNamePasswordValidationMode = UserNamePasswordValidationMode.Custom;
                serviceHost.Credentials.UserNameAuthentication.CustomUserNamePasswordValidator = new CredentialValidator();
                binding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Basic;

                serviceHost.AddServiceEndpoint(typeof(IRustService), binding, "");

                ServiceMetadataBehavior smb = new ServiceMetadataBehavior();
                ServiceDebugBehavior debug = serviceHost.Description.Behaviors.Find<ServiceDebugBehavior>();

                // if not found - add behavior with setting turned on 
                if (debug == null) 
                {
                    serviceHost.Description.Behaviors.Add(
                         new ServiceDebugBehavior() { IncludeExceptionDetailInFaults = true });
                } 
                else 
                {
                    // make sure setting is turned ON
                    if (!debug.IncludeExceptionDetailInFaults) 
                    {
                        debug.IncludeExceptionDetailInFaults = true;
                    }
                }

                debug.HttpHelpPageEnabled = false;

                smb.HttpGetEnabled = true;
                smb.HttpsGetEnabled = true;
                serviceHost.Description.Behaviors.Add(smb);

                serviceHost.Open();

            } 
            catch (Exception e) 
            {
                using (var writer = new StreamWriter("C:\\ServiceErrorLog.log")) 
                {
                    writer.WriteLine(e.ToString());
                }
            }
        }

        protected override void OnStop() 
        {
            serviceHost.Close();
        }
    }
}
