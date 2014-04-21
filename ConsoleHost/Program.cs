using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ServiceModel;
using Rust;
using System.ServiceModel.Description;
using System.ServiceModel.Security;

namespace ConsoleHost
{
    class Program
    {

        static void Main(string[] args)
        {
            Uri baseAddress = new Uri("http://bmrfservers.com:8080/TestService");
            ServiceHost serviceHost;
            //WSHttpBinding binding = new WSHttpBinding();
            BasicHttpBinding binding = new BasicHttpBinding();

            binding.Security.Mode = BasicHttpSecurityMode.None;
            serviceHost = new ServiceHost(typeof(RustService), baseAddress);

            //serviceHost.Credentials.UserNameAuthentication.UserNamePasswordValidationMode = UserNamePasswordValidationMode.Custom;
            //serviceHost.Credentials.UserNameAuthentication.CustomUserNamePasswordValidator = new CredentialValidator();
            //binding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Basic;

            serviceHost.AddServiceEndpoint(typeof(IRustService), binding, "RustServiceHost");

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

            smb.HttpGetEnabled = true;
            smb.HttpsGetEnabled = true;
            serviceHost.Description.Behaviors.Add(smb);

            serviceHost.Open();

            Console.WriteLine("The service is ready at {0}", baseAddress);
            Console.WriteLine("Press <Enter> to stop the service.");
            Console.ReadLine();

            // Close the ServiceHost.
            serviceHost.Close();
        }
    }
}
