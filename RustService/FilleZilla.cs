using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Diagnostics;

namespace Rust {
    public class FileZilla {
        Config cfg;
        string ident;
        int slots;
        string pass;

        public FileZilla(Config config, string identifier, int slots, string pass) {
            this.cfg = config;
            this.ident = identifier;
            this.slots = slots;
            this.pass = pass;
        }

        public void GenerateXml() {
            try {
                bool userExists = false;

                XDocument xDoc = XDocument.Load(String.Format(@"{0}\FileZilla Server.xml", cfg.FileZillaPath));

                foreach (var item in xDoc.Descendants("User")) {
                    if (item.Attribute("Name").Value == ident) {
                        Logger.Log("User already exists in FileZilla, so it won't be created.");
                        userExists = true;
                    }
                }

                if (!userExists) {
                    Console.WriteLine("Creating FTP user");

                    XElement user =
                        new XElement("User",
                            new XAttribute("Name", String.Format("{0}", ident)
                        ),
                        new XElement("Option",
                            new XAttribute("Name", "Pass"),
                            new XText(GetMd5(pass))
                        ),
                            new XElement("Option",
                            new XAttribute("Name", "Group"),
                            new XText("")
                        ),
                        new XElement("Option",
                            new XAttribute("Name", "Bypass server userlimit"),
                            new XText("0")
                        ),
                        new XElement("Option",
                            new XAttribute("Name", "User Limit"),
                            new XText("")
                        ),
                        new XElement("Option",
                            new XAttribute("Name", "IP Limit"),
                            new XText("0")
                        ),
                        new XElement("Option",
                            new XAttribute("Name", "Enabled"),
                            new XText("1")
                        ),
                        new XElement("Option",
                            new XAttribute("Name", "Comments"),
                            new XText("")
                        ),
                        new XElement("Option",
                            new XAttribute("Name", "ForceSsl"),
                            new XText("0")
                        ),
                        new XElement("Option",
                            new XAttribute("Name", "8plus3"),
                            new XText("0")
                        ),
                        new XElement("IPFilter", "", new XElement("Disallowed"), new XElement("Allowed")),
                        new XElement("Permissions",
                                new XElement("Permission",
                                new XAttribute("Dir", String.Format(@"{0}\{1}\logs", cfg.InstallPath, ident)),
                                new XElement("Option",
                                new XAttribute("Name", "FileRead"),
                                new XText("1")
                                ),
                                new XElement("Option",
                                new XAttribute("Name", "FileWrite"),
                                new XText("1")
                                ),
                                new XElement("Option",
                                new XAttribute("Name", "FileDelete"),
                                new XText("1")
                                ),
                                new XElement("Option",
                                new XAttribute("Name", "FileAppend"),
                                new XText("1")
                                ),
                                new XElement("Option",
                                new XAttribute("Name", "DirCreate"),
                                new XText("1")
                                ),
                                new XElement("Option",
                                new XAttribute("Name", "DirDelete"),
                                new XText("1")
                                ),
                                new XElement("Option",
                                new XAttribute("Name", "DirList"),
                                new XText("1")
                                ),
                                new XElement("Option",
                                new XAttribute("Name", "DirSubdirs"),
                                new XText("1")
                                ),
                                new XElement("Option",
                                new XAttribute("Name", "IsHome"),
                                new XText("1")
                                ),
                                new XElement("Option",
                                new XAttribute("Name", "AutoCreate"),
                                new XText("0")
                                )
                            ),
                            new XElement("Permission",
                                new XAttribute("Dir", String.Format(@"{0}\{1}\save", cfg.InstallPath, ident)),
                                    new XElement("Aliases",
                                        new XElement("Alias", @"/save")),
                                new XElement("Option",
                                new XAttribute("Name", "FileRead"),
                                new XText("1")
                                ),
                                new XElement("Option",
                                new XAttribute("Name", "FileWrite"),
                                new XText("1")
                                ),
                                new XElement("Option",
                                new XAttribute("Name", "FileDelete"),
                                new XText("1")
                                ),
                                new XElement("Option",
                                new XAttribute("Name", "FileAppend"),
                                new XText("1")
                                ),
                                new XElement("Option",
                                new XAttribute("Name", "DirCreate"),
                                new XText("1")
                                ),
                                new XElement("Option",
                                new XAttribute("Name", "DirDelete"),
                                new XText("1")
                                ),
                                new XElement("Option",
                                new XAttribute("Name", "DirList"),
                                new XText("1")
                                ),
                                new XElement("Option",
                                new XAttribute("Name", "DirSubdirs"),
                                new XText("1")
                                ),
                                new XElement("Option",
                                new XAttribute("Name", "IsHome"),
                                new XText("0")
                                ),
                                new XElement("Option",
                                new XAttribute("Name", "AutoCreate"),
                                new XText("0")
                                )
                            ),
                            new XElement("Permission",
                                new XAttribute("Dir", String.Format(@"{0}\{1}\rust_server_Data", cfg.InstallPath, ident)),
                                    new XElement("Aliases",
                                        new XElement("Alias", @"/rust_server_Data")),
                                new XElement("Option",
                                new XAttribute("Name", "FileRead"),
                                new XText("1")
                                ),
                                new XElement("Option",
                                new XAttribute("Name", "FileWrite"),
                                new XText("1")
                                ),
                                new XElement("Option",
                                new XAttribute("Name", "FileDelete"),
                                new XText("1")
                                ),
                                new XElement("Option",
                                new XAttribute("Name", "FileAppend"),
                                new XText("1")
                                ),
                                new XElement("Option",
                                new XAttribute("Name", "DirCreate"),
                                new XText("1")
                                ),
                                new XElement("Option",
                                new XAttribute("Name", "DirDelete"),
                                new XText("1")
                                ),
                                new XElement("Option",
                                new XAttribute("Name", "DirList"),
                                new XText("1")
                                ),
                                new XElement("Option",
                                new XAttribute("Name", "DirSubdirs"),
                                new XText("1")
                                ),
                                new XElement("Option",
                                new XAttribute("Name", "IsHome"),
                                new XText("0")
                                ),
                                new XElement("Option",
                                new XAttribute("Name", "AutoCreate"),
                                new XText("0")
                                )
                            )
                        ),
                        new XElement("SpeedLimits",
                            new XAttribute("D1Type", "0"),
                            new XAttribute("D1Limit", "10"),
                            new XAttribute("ServerD1LimitBypass", "0"),
                            new XAttribute("U1Type", "0"),
                            new XAttribute("U1Limit", "10"),
                            new XAttribute("ServerUlLimitBypass", "0"),
                            new XElement("Download"), new XElement("Upload")
                        )
                    );

                    xDoc.Root.Element("Users").Add(user);
                    xDoc.Save(String.Format(@"{0}\FileZilla Server.xml", cfg.FileZillaPath));
                    Logger.Log("FTP user created!");

                    Process.Start(cfg.FileZillaPath + "\\FileZilla Server.exe", "/reload-config");
                    Logger.Log("FileZilla config reloaded!");
                }
            }
            catch (Exception e) {
                DeploymentResults.ExceptionThrown = true;
                DeploymentResults.Exceptions.Add(e);
            }
        }

        static string GetMd5(string pass) {
            byte[] hash;
            using (MD5 md5 = MD5.Create()) {
                hash = md5.ComputeHash(Encoding.UTF8.GetBytes(pass));
            }
            var md5Pass = ToHex(hash, false);
            return md5Pass;
        }

        public static string ToHex(byte[] bytes, bool upperCase) {
            StringBuilder result = new StringBuilder(bytes.Length * 2);

            for (int i = 0; i < bytes.Length; i++)
                result.Append(bytes[i].ToString(upperCase ? "X2" : "x2"));

            return result.ToString();
        }
    }
}
