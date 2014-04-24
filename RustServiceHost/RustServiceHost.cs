using System;
using System.IO;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Security;
using System.ServiceModel.Web;
using System.ServiceProcess;
using Rust;

namespace RustServiceHost
{
    public partial class RustServiceHost : ServiceBase
    {
        private readonly Uri baseAddress = new Uri("https://bmrfservers.com:8000/Rustful");
        private readonly WebHttpBinding binding = new WebHttpBinding();
        private WebServiceHost serviceHost;

        public RustServiceHost()
        {
            InitializeComponent();
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                binding.Security.Mode = WebHttpSecurityMode.Transport;
                serviceHost = new WebServiceHost(typeof (RustService), baseAddress);

                serviceHost.Credentials.UserNameAuthentication.UserNamePasswordValidationMode =
                    UserNamePasswordValidationMode.Custom;
                serviceHost.Credentials.UserNameAuthentication.CustomUserNamePasswordValidator =
                    new CredentialValidator();
                binding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Basic;

                serviceHost.AddServiceEndpoint(typeof (IRustService), binding, "");

                var smb = new ServiceMetadataBehavior();
                var debug = serviceHost.Description.Behaviors.Find<ServiceDebugBehavior>();

                // if not found - add behavior with setting turned on 
                if (debug == null)
                {
                    serviceHost.Description.Behaviors.Add(
                        new ServiceDebugBehavior {IncludeExceptionDetailInFaults = true});
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