#region
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.UI;
using System.Web.UI.WebControls;
using System.IO;
using System.Net;
using System.Configuration;
using System.Text;
using System.Collections.Specialized;
using MySql.Data.MySqlClient;
using System.Text.RegularExpressions;
using System.Net.Mail;
using IPN.HttpReference;
#endregion

namespace IPN 
{
    public partial class IPNHandler : System.Web.UI.Page 
    {
        protected void Page_Load(object sender, EventArgs e) 
        {
            string postUrl = "https://www.paypal.com/cgi-bin/webscr"; // Live URL
            //string postUrl = "https://www.sandbox.paypal.com/cgi-bin/webscr"; // Debug URL

            var req = (HttpWebRequest)WebRequest.Create(postUrl);
            req.Method = "POST";
            req.ContentType = "application/x-www-form-urlencoded";
            byte[] param = Request.BinaryRead(HttpContext.Current.Request.ContentLength);

            //TestClient();

            string request = Encoding.ASCII.GetString(param);

            request += "&cmd=_notify-validate";
            req.ContentLength = request.Length;

            using (var streamOut = new StreamWriter(req.GetRequestStream(), System.Text.Encoding.ASCII))
            {
                streamOut.Write(request);
            }

            var streamIn = new StreamReader(req.GetResponse().GetResponseStream());
            string response = streamIn.ReadToEnd();
            streamIn.Close();

            NameValueCollection paymentArgs = HttpUtility.ParseQueryString(request);

            Log("\n\n-- NEW TRANSACTION --");
            foreach (string arg in paymentArgs)
            {
                Log(String.Format("{0} = {1}", arg, paymentArgs[arg]), false);
            }

            if (response == "VERIFIED") 
            {
                //check that txn_id has not been previously processed
                //check that payment_amount/payment_currency are correct
                if (paymentArgs["txn_type"] == "subscr_payment") 
                {
                    if (paymentArgs["receiver_email"] == "admin@bmrf.me" && paymentArgs["payment_status"] == "Confirmed")
                        ProcessPayment(paymentArgs);
                    else if (paymentArgs["txn_type"] == "subscr_signup") 
                    {
                        ProcessPayment(paymentArgs);
                        // install Rust server
                    }
                } 
                else 
                {
                    Log(String.Format("Payment receiver_email and payment_status were unexpected, payment string: {0}", request));
                    SendEmail("IPN Handler: Payment Error",
                        String.Format("Payment receiver_email and payment_status were unexpected, payment string:\n\n{0}", paymentArgs.ToString()));
                }

                Log("Payment is VERIFIED");
                ProcessPayment(paymentArgs);

            } 
            else if (response == "INVALID")
                Log("Payment is INVALID");
            else 
            {
                //log response/ipn data for manual investigation
            }
        }

        //void TestClient() {
        //    Dictionary<string, object> dic = new Dictionary<string, object>();
        //    var client = new RustServiceClient();
        //    client.ClientCredentials.UserName.UserName = "test";
        //    client.ClientCredentials.UserName.Password = "test123";
        //    dic = client.DeployRustServer("GayNerd", 128);
        //    Log(dic.ToString());
        //}

        public void ProcessPayment(NameValueCollection paymentArgs) 
        {
            try 
            {
                bool isSub = IPNData.ValidTypes.Contains(paymentArgs["txn_type"]);
                if (isSub) 
                {
                    InsertSub(paymentArgs);
                    HandleSubscriber(paymentArgs);
                }
            } 
            catch (Exception e) 
            {
                Log(e.ToString());
                SendEmail("IPN Handler Exception", String.Format("Exception in IPN Listener:\n\n{0}", e.ToString()));
            }
        }

        void InsertSub(NameValueCollection paymentArgs) 
        {
            try 
            {
                using (var con = new MySqlConnection(IPNData.ConnectionString)) 
                {
                    con.Open();

                    var cmd = new MySqlCommand(IPNData.SubscriptionsInsertQuery, con);
                    string[] parameters = IPNData.SubscriptionsInsertParameters;

                    foreach (string parameter in parameters)
                    {
                        cmd.Parameters.Add(new MySqlParameter(String.Format("@{0}", parameter), paymentArgs[parameter]));
                    }

                    cmd.Parameters.Add(new MySqlParameter("@timestamp", DateTime.Now));
                    cmd.ExecuteReader();
                }
            } 
            catch (Exception e) 
            {
                Log(e.ToString());
                SendEmail("IPN Handler Exception", String.Format("Exception in IPN Listener:\n\n{0}", e.ToString()));
            }
        }

        void HandleSubscriber(NameValueCollection paymentArgs) 
        {
            string status = "N/A";
            try 
            {
                using (var con = new MySqlConnection(IPNData.ConnectionString)) 
                {
                    con.Open();
                    var cmd = new MySqlCommand(IPNData.SubscribersSelectQuery, con);
                    cmd.Parameters.Add(new MySqlParameter("@subscr_id", paymentArgs["subscr_id"]));

                    using (MySqlDataReader dr = cmd.ExecuteReader()) 
                    {
                        if (paymentArgs["txn_type"] == "subscr_payment")
                            status = "Active";
                        if (paymentArgs["txn_type"] == "subscr_eot")
                            status = "Inactive";

                        if (!dr.HasRows) 
                        {
                            // User is a subscriber but not in the subscribers table, add them
                            cmd = new MySqlCommand(IPNData.SubscribersInsertQuery, con);
                            string[] parameters = IPNData.SubscribersInsertParameters;

                            foreach (string parameter in parameters)
                            {
                                cmd.Parameters.Add(new MySqlParameter(String.Format("@{0}", parameter), paymentArgs[parameter]));
                            }

                            dr.Close();
                            cmd.ExecuteReader();
                        }
                    }
                }
                using (var con = new MySqlConnection(IPNData.ConnectionString)) 
                {
                    con.Open();
                    var cmd = new MySqlCommand(IPNData.SubscribersUpdateQuery, con);

                    cmd.Parameters.Add(new MySqlParameter("@status", status));
                    cmd.Parameters.Add(new MySqlParameter("@subscr_id", paymentArgs["subscr_id"]));

                    cmd.ExecuteReader();
                }
            } 
            catch (Exception e) 
            {
                Log(e.ToString());
                SendEmail("IPN Handler Exception", String.Format("Exception in IPN Listener:\n\n{0}", e.ToString()));
            }
        }

        void Log(string content, bool datePrefix = true) 
        {
            string logFile = String.Format("C:\\sites\\rogovo.zombies.nu\\debug.log");

            if (datePrefix)
            {
                File.AppendAllText(logFile, DateTime.Now + ": " + content + Environment.NewLine);
            }
            else if (!datePrefix)
            {
                File.AppendAllText(logFile, content + Environment.NewLine);
            }
        }

        public void SendEmail(string subject, string message) 
        {
            string to = "support@bmrfservers.com";
            string from = "system@bmrfservers.com";
            var msg = new MailMessage(from, to);

            msg.Subject = subject;
            msg.Body = message;
            SmtpClient client;

            client = new SmtpClient 
            {
                Port = 25,
                Host = "mail.bmrf.me",
                EnableSsl = false,
                Timeout = 10000,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new System.Net.NetworkCredential("system@bmrfservers.com", "poopfeast420")
            };

            try 
            {
                client.Send(msg);
            } 
            catch (Exception ex) 
            {
                Log(String.Format("Exception caught in SendExceptionMail(): {0}", ex.ToString()));
            }
        }
    }

    public struct IPNData 
    {
        public static string ConnectionString = "Server=localhost;Port=3306;Database=paypal;Uid=ipn;Pwd=ipn";
        public static string[] ValidTypes = { "subscr_signup", "subscr_cancel", "subscr_modify", "subscr_failed", "subscr_payment", "subscr_eot" };
        public static string SubscriptionsInsertQuery = "INSERT INTO subscriptions VALUES (@transaction_subject, @payment_date, @txn_type, @subscr_id, @last_name, @residence_country, @item_name, @payment_gross, @mc_currency, @business, @protection_eligibility, @verify_sign, @payer_status, @payer_email, @txn_id, @receiver_email, @first_name, @payer_id, @receiver_id, @payment_status, @payment_fee, @mc_fee, @mc_gross, @charset, @notify_version, @ipn_track_id)";
        public static string[] SubscriptionsInsertParameters = { "transaction_subject", "payment_date", "txn_type", "subscr_id", "last_name", "residence_country", "item_name", "payment_gross", "mc_currency", "business", "protection_eligibility", "verify_sign", "payer_status", "payer_email", "txn_id", "receiver_email", "first_name", "payer_id", "receiver_id", "payment_status", "payment_fee", "mc_fee", "mc_gross", "charset", "notify_version", "ipn_track_id" };
        public static string SubscribersSelectQuery = "SELECT * FROM subscribers WHERE subscr_id = @subscr_id";
        public static string SubscribersInsertQuery = "INSERT INTO subscribers (payer_email, first_name, last_name, item_name, subscr_id) VALUES (@payer_email, @first_name, @last_name, @item_name, @subscr_id)";
        public static string[] SubscribersInsertParameters = { "payer_email", "first_name", "last_name", "item_name", "subscr_id" };
        public static string SubscribersUpdateQuery = "UPDATE subscribers SET status = @status WHERE subscr_id = @subscr_id";
    }
}