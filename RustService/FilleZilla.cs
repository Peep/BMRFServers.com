using System;
using System.Diagnostics;
using System.Xml.Linq;

namespace Rust
{
    public class FileZilla
    {
        private readonly Config _cfg;
        private readonly string _ident;
        private readonly string _pass;

        public FileZilla(Config config, string identifier, string pass)
        {
            _cfg = config;
            _ident = identifier;
            _pass = pass;
        }

        public void GenerateXml()
        {
            try
            {
                bool userExists = false;

                XDocument xDoc = XDocument.Load(String.Format(@"{0}\FileZilla Server.xml", _cfg.FileZillaPath));

                foreach (XElement item in xDoc.Descendants("User"))
                {
                    if (item.Attribute("Name").Value == _ident)
                    {
                        Logger.Log("User already exists in FileZilla, so it won't be created.");
                        userExists = true;
                    }
                }

                if (!userExists)
                {
                    Console.WriteLine("Creating FTP user");

                    var user =
                        new XElement("User",
                            new XAttribute("Name", String.Format("{0}", _ident)
                                ),
                            new XElement("Option",
                                new XAttribute("Name", "Pass"),
                                new XText(Crypto.GetMd5(_pass))
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
                                    new XAttribute("Dir", String.Format(@"{0}\{1}\logs", _cfg.InstallPath, _ident)),
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
                                    new XAttribute("Dir", String.Format(@"{0}\{1}\save", _cfg.InstallPath, _ident)),
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
                                    new XAttribute("Dir",
                                        String.Format(@"{0}\{1}\rust_server_Data", _cfg.InstallPath, _ident)),
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

                    if (xDoc.Root != null)
                    {
                        XElement xElement = xDoc.Root.Element("Users");
                        if (xElement != null) xElement.Add(user);
                    }
                    xDoc.Save(String.Format(@"{0}\FileZilla Server.xml", _cfg.FileZillaPath));
                    Logger.Log("FTP user created!");

                    Process.Start(_cfg.FileZillaPath + "\\FileZilla Server.exe", "/reload-config");
                    Logger.Log("FileZilla config reloaded!");
                }
            }
            catch (Exception e)
            {
                DeploymentResults.ExceptionThrown = true;
                DeploymentResults.Exceptions.Add(e);
            }
        }
    }
}