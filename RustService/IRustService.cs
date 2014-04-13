using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;

namespace Rust 
{
    [ServiceContract]
    public interface IRustService 
    {
        [OperationContract]
        string GetData(int value);

        [OperationContract]
        CompositeType GetDataUsingDataContract(CompositeType composite);

        [OperationContract]
        Dictionary<string, object> DeployRustServer(string identifier, int slots);

        [OperationContract]
        Results RunSteamUpdate(string ident);

        [OperationContract]
        Results InstallMagma(string ident);

        [OperationContract]
        Results IsMagmaInstalled(string ident);

        [OperationContract]
        Results UninstallMagma(string ident);

        [OperationContract]
        Results ChangeConfigValue(string ident, string key, string value);

        [OperationContract]
        Results ChangeFtpPass(string ident, string newPass);

        [OperationContract]
        Results ToggleCheatpunch(string ident, bool enabled);

        [OperationContract]
        Results StartServer(string ident);

        [OperationContract]
        Results StopServer(string ident);

        [OperationContract]
        Results RestartServer(string ident);

        [OperationContract]
        Results UninstallRustServer(string ident);
    }

    // Use a data contract as illustrated in the sample below to add composite types to service operations.
    // You can add XSD files into the project. After building the project, you can directly use the data types defined there, with the namespace "WCFService.ContractType".
    [DataContract]
    public class CompositeType 
    {
        bool boolValue = true;
        string stringValue = "Hello ";

        [DataMember]
        public bool BoolValue 
        {
            get { return boolValue; }
            set { boolValue = value; }
        }

        [DataMember]
        public string StringValue 
        {
            get { return stringValue; }
            set { stringValue = value; }
        }
    }
}
