using System;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.ServiceModel.Web;
using Rust;

namespace ConsoleHost
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var baseAddress = new Uri("http://bmrfservers.com:8080/Rustful");
            WebServiceHost serviceHost;
            //WSHttpBinding binding = new WSHttpBinding();
            //BasicHttpBinding binding = new BasicHttpBinding();
            var binding = new WebHttpBinding();

            binding.Security.Mode = WebHttpSecurityMode.None;
            serviceHost = new WebServiceHost(typeof (RustService), baseAddress);

            //serviceHost.Credentials.UserNameAuthentication.UserNamePasswordValidationMode = UserNamePasswordValidationMode.Custom;
            //serviceHost.Credentials.UserNameAuthentication.CustomUserNamePasswordValidator = new CredentialValidator();
            //binding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Basic;

            //ServiceMetadataBehavior smb = new ServiceMetadataBehavior();
            var whb = new WebHttpBehavior();
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

            //whb.HttpGetEnabled = true;
            //whb.HttpsGetEnabled = true;
            //serviceHost.Description.Behaviors.Add(whb);

            serviceHost.Open();

            Console.WriteLine("The service is ready at {0}", baseAddress);
            Console.WriteLine("Press <Enter> to stop the service.");
            Console.ReadLine();

            // Close the ServiceHost.
            serviceHost.Close();
        }
    }
}