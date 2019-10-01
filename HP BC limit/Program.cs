using HP_BC_limit.Properties;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Windows.Forms;

namespace HP_BC_limit
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new HPBCap());
        }
        public class HPBCap : ApplicationContext
        {
            private readonly NotifyIcon trayIcon;
            private readonly Process p;
            private readonly MenuItem[] menuItems;

            public HPBCap()
            {

                string[] locs = {@"C:\Program Files (x86)\HP\BIOS Configuration Utility\BiosConfigUtility64.exe", @"C:\Program Files\HP\BIOS Configuration Utility\BiosConfigUtility64.exe",
                @"C:\Program Files (x86)\HP\BIOS Configuration Utility\BiosConfigUtility.exe", @"C:\Program Files\HP\BIOS Configuration Utility\BiosConfigUtility.exe"};
                string biosConfigUtility = "";
                foreach (string loc in locs)
                {
                    if (File.Exists(loc))
                    {
                        biosConfigUtility = loc;
                        break;
                    }
                }
                if (biosConfigUtility == "")
                {
                    MessageBox.Show("Please install BIOS Configuration Utility first:\n https://ftp.hp.com/pub/caps-softpaq/cmit/HP_BCU.html", "BIOS Configuration Utility not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    //Process.Start("https://ftp.hp.com/pub/caps-softpaq/cmit/HP_BCU.html");
                    Environment.Exit(1);
                }
                if (!IsAdministrator())
                {
                    MessageBox.Show("Make sure you are running as Administrator", "This application needs to be running as administrator", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Environment.Exit(2);
                }


                // Start the child process.
                p = new Process();
                // Redirect the output stream of the child process.
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.FileName = biosConfigUtility;
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.Arguments = "/get";
                p.Start();
                // Do not wait for the child process to exit before
                // reading to the end of its redirected stream.
                // p.WaitForExit();
                // Read the output stream first and then wait.
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                List<string> options = GetOptions(output);
                if (options.Count == 0)
                {
                    MessageBox.Show("Battery Care Function not found! Your bios/computer is not compatible", "Battery Care Function not found", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Environment.Exit(3);
                }
                menuItems = new MenuItem[options.Count + 1];

                for (int i = 0; i < options.Count; i++)
                {
                    string option = options[i];
                    menuItems[i] = new MenuItem(option.TrimStart('*'), ChangeOption)
                    {
                        Checked = option.StartsWith('*')
                    };
                }
                menuItems[options.Count] = new MenuItem("Exit", Exit);

                // Initialize Tray Icon
                trayIcon = new NotifyIcon()
                {
                    Icon = Resources.AppIcon,
                    ContextMenu = new ContextMenu(menuItems),
                    Visible = true
                };

            }

            public static bool IsAdministrator()
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }

            public static List<string> GetOptions(string output)
            {
                List<string> options = new List<string>();
                bool flag = false;
                using (StringReader reader = new StringReader(output))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (flag && line.StartsWith("\t"))
                        {
                            options.Add(line.Trim());
                        }
                        else if (line == "Battery Care Function")
                        {
                            flag = true;
                        }
                        else
                        {
                            flag = false;
                        }
                    }
                }
                return options;
            }

            private void Exit(object sender, EventArgs e)
            {
                // Hide tray icon, otherwise it will remain shown until user mouses over it
                trayIcon.Visible = false;

                Environment.Exit(0);
            }

            private void ChangeOption(object sender, EventArgs e)
            {
                MenuItem item = (MenuItem)sender;
                string val = item.Text;
                if (!item.Checked)
                {
                    p.StartInfo.Arguments = "/setvalue:\"Battery Care Function\",\"" + val + " \"";
                    p.Start();
                    foreach (MenuItem mi in menuItems)
                    {
                        mi.Checked = (mi.Text == val);
                    }
                    p.WaitForExit();
                    UpdateSelection();

                }
            }

            private void UpdateSelection()  
            {
                p.StartInfo.Arguments = "/get";
                p.Start();
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();
                List<string> options = GetOptions(output);
                string selected = "";
                foreach (string option in options)
                {
                    if (option.StartsWith('*'))
                    {
                        selected = option.TrimStart('*');
                        break;
                    }
                }
                foreach (MenuItem mi in menuItems)
                {
                    mi.Checked = mi.Text == selected;
                }
            }
        }

    }
}
