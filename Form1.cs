// Copyright (C) 2011 Iker Ruiz Arnauda (Wesko)
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Net;
using Nini.Config;
using System.Text.RegularExpressions;

namespace MadCow
{

    public partial class Form1 : Form
    {
        //This variable its used to stablish the default profile loaded into the server control tab, if the user selects another profile this variable changes.
        public static String CurrentProfile = Program.programPath + @"\ServerProfiles\Default.mdc"; //We set this as the default profile loaded.
        //We update this variable with the current supported D3 client after parsing the required version.
        public static String MooegeSupportedVersion;
        //Timing for autoupdate
        private int Tick;
        //Parsing Console into a textbox
        TextWriter _writer = null;
        //TO access controls from outside classes
        public static Form1 GlobalAccess;
        //For tray icon
        private ContextMenu m_menu;

        public Form1()
        {
            InitializeComponent();
            GlobalAccess = this;
            AutoUpdateValue.Enabled = false;
            EnableAutoUpdateBox.Enabled = false;
            PlayDiabloButton.Enabled = false;
            //DeleteHelper.HideFile();
        }

        ///////////////////////////////////////////////////////////
        //Form Load
        ///////////////////////////////////////////////////////////
        private void Form1_Load(object sender, EventArgs e)
        {
            this.VersionLabel.Text = Application.ProductVersion;
            _writer = new TextBoxStreamWriter(txtConsole);
            Console.SetOut(_writer);
            Console.WriteLine("Welcome to MadCow!");
            ToolTip toolTip1 = new ToolTip();
            ApplySettings();
            // Set up the delays for the ToolTip.
            toolTip1.AutoPopDelay = 1800;
            toolTip1.InitialDelay = 100;
            toolTip1.ReshowDelay = 100;
            // Force the ToolTip text to be displayed whether or not the form is active.
            toolTip1.ShowAlways = true;
            // Set up the ToolTip text for the Buttons.
            toolTip1.SetToolTip(this.UpdateMooegeButton, "Update mooege from GitHub to latest version");
            toolTip1.SetToolTip(this.CopyMPQButton, "Copy MPQ's if you have D3 installed");
            toolTip1.SetToolTip(this.FindDiabloButton, "Find Diablo.exe so MadCow can work properly");
            toolTip1.SetToolTip(this.ValidateRepoButton, "Validate the repository so MadCow can download it");
            toolTip1.SetToolTip(this.EnableAutoUpdateBox, "Enable updates to a repository every 'X' minutes");
            toolTip1.SetToolTip(this.RemoteServerButton, "Connects to public server you have entered in");
            toolTip1.SetToolTip(this.ResetRepoFolder, "Resets Repository folder in case of errors");
            toolTip1.SetToolTip(this.DownloadMPQSButton, "Downloads ALL MPQs needed to run Mooege");
            toolTip1.SetToolTip(this.RestoreDefaults, "Resets Server Control settings");
            toolTip1.SetToolTip(this.PlayDiabloButton, "Time to play Diablo 3 through Mooege!");
            InitializeFindPath(); //Search if a Diablo client path already exist.
            RepoCheck(); //Checks for duplicities.
            RepoList(); //Loads Repos from RepoList.txt
            Changelog(); //Loads Changelog comobox values.
            LoadLastUsedProfile(); //We try to Load the last used profile by the user.
            Helper.Helpers();//Loads the correct nameplate for shortcut/balloon/LastRepo enabled/disabled
            TestMPQ.getfileList(); //Load MPQ list from Blizz server. Todo: This might slow down a bit MadCow loading, maybe we could place it somewhere else?.
            Helper.KillUpdater();
		}

        private void ApplySettings()
        {
            if (!File.Exists(Program.programPath + "\\Tools\\" + "madcow.ini")) return;
            var source = new IniConfigSource(Program.programPath + @"\Tools\madcow.ini");
            var src = source.Configs["ShortCut"].Get("Shortcut");
            if (src.Contains("1"))
            {
                ShortCut.Create();
            }

            MPQDestTextBox.Text = source.Configs["DiabloPath"].Get("MPQDest", Path.Combine(Program.programPath, "MPQ"));

            SettingsCheckedListBox.SetItemChecked(0, Convert.ToBoolean(source.Configs["Mooege"].Get("FileLogging", "true")));
            SettingsCheckedListBox.SetItemChecked(1, Convert.ToBoolean(source.Configs["Mooege"].Get("PacketLogging", "false")));
        }

        ///////////////////////////////////////////////////////////
        //Validate Repository
        ///////////////////////////////////////////////////////////
        private void Validate_Repository_Click(object sender, EventArgs e)
        {
            backgroundWorker5.RunWorkerAsync();
        }

        private void backgroundWorker5_DoWork(object sender, DoWorkEventArgs e)
        {
            comboBox1.Invoke (new Action(() =>{ParseRevision.revisionUrl = this.comboBox1.Text;}));
            try
            {
                WebClient client = new WebClient();
                client.DownloadStringCompleted += new DownloadStringCompletedEventHandler(backgroundWorker5_RunWorkerCompleted);
                try
                {
                    Uri uri = new Uri(ParseRevision.revisionUrl + "/commits/master.atom");
                    client.DownloadStringAsync(uri);
                }
                catch (UriFormatException)
                {
                    ActiveForm.Invoke(new Action(() =>
                    {
                        ParseRevision.commitFile = "Incorrect repository entry";
                        pictureBox2.Hide();
                        comboBox1.Text = ParseRevision.commitFile;
                        pictureBox1.Show();
                        AutoUpdateValue.Enabled = false; //If validation fails we set Update and Autoupdate
                        EnableAutoUpdateBox.Enabled = false;      //functions disabled!.
                        UpdateMooegeButton.Enabled = false;
                        Console.WriteLine("Please try a different Repository.");
                    }));
                }

            }
            catch (WebException ex)
            {
                if (ex.Status == WebExceptionStatus.ProtocolError)
                {
                    if (((HttpWebResponse)ex.Response).StatusCode == HttpStatusCode.NotFound)
                    {
                        ParseRevision.commitFile = "Incorrect repository entry";
                    }
                }
                else if (ex.Status == WebExceptionStatus.ConnectFailure)
                {
                    ParseRevision.commitFile = "ConnectionFailure";
                }
                else
                    ParseRevision.commitFile = "Incorrect repository entry";
            }
        }

        private void backgroundWorker5_RunWorkerCompleted(object sender, DownloadStringCompletedEventArgs e)
        {

            if (e.Error != null)
            {
                ParseRevision.commitFile = "Incorrect repository entry";
            }

            else if (e.Result.ToString() != null && e.Error == null)
            {
                ParseRevision.commitFile = e.Result.ToString();
                Int32 pos2 = ParseRevision.commitFile.IndexOf("Commit/");
                String revision = ParseRevision.commitFile.Substring(pos2 + 7, 7);
                ParseRevision.lastRevision = ParseRevision.commitFile.Substring(pos2 + 7, 7);
            }

            try
                {
                    if (ParseRevision.commitFile == "ConnectionFailure")
                    {
                        ActiveForm.Invoke(new Action(() => 
                        { 
                            pictureBox2.Hide();
                            comboBox1.Text = ParseRevision.commitFile;
                            pictureBox1.Show();
                            AutoUpdateValue.Enabled = false; //If validation fails we set Update and Autoupdate
                            EnableAutoUpdateBox.Enabled = false;      //functions disabled!.
                            UpdateMooegeButton.Enabled = false;
                            Console.WriteLine("Internet Problems.");
                        }));
                    }

                    else if (ParseRevision.commitFile == "Incorrect repository entry")
                    {
                        ActiveForm.Invoke(new Action(() => 
                        { 
                        pictureBox2.Hide();
                        comboBox1.Text = ParseRevision.commitFile;
                        pictureBox1.Show();
                        AutoUpdateValue.Enabled = false; //If validation fails we set Update and Autoupdate
                        EnableAutoUpdateBox.Enabled = false;      //functions disabled!.
                        UpdateMooegeButton.Enabled = false;
                        Console.WriteLine("Please try a different Repository.");
                        }));
                    }
                    else
                    {
                        ActiveForm.Invoke(new Action(() => 
                        {
                        pictureBox1.Hide();
                        pictureBox2.Show();
                        comboBox1.ForeColor = Color.Green;
                        comboBox1.Text = ParseRevision.revisionUrl;
                        ParseRevision.getDeveloperName();
                        ParseRevision.getBranchName();
                        UpdateMooegeButton.Enabled = true;
                        AutoUpdateValue.Enabled = true;
                        EnableAutoUpdateBox.Enabled = true;
                        Console.WriteLine("Repository Validated!");
                        RepoListAdd();
                        RepoListUpdate();
                        ChangelogListUpdate();
                        FindBranch.findBrach(comboBox1.Text);
                        label10.Visible = false;
                        BranchComboBox.Visible = true;
                        label24.Visible = true;
                        }));
                    }
                }
                catch (Exception Ex)
                {
                    Console.WriteLine(Ex);
                    ActiveForm.Invoke(new Action(() => 
                    { 
                    pictureBox2.Hide();
                    comboBox1.Text = ParseRevision.commitFile;
                    pictureBox1.Show();
                    }));
                }
        }


        /////////////////////////////
        //UPDATE MOOEGE: This will compare ur current revision, if outdated proceed to download calling ->backgroundWorker1.RunWorkerAsync()->backgroundWorker1_RunWorkerCompleted.
        /////////////////////////////
        private void Update_Mooege_Click(object sender, EventArgs e)
        {
            UpdateMooege();
        }

        private void UpdateMooege()
        {
            //We set or "reset" progressbar value to cero.
            generalProgressBar.Value = 0;
            generalProgressBar.Update();
            if (Directory.Exists(Program.programPath + @"\Repositories\" + ParseRevision.developerName + "-" + ParseRevision.branchName + "-" + ParseRevision.lastRevision))
            {
                if (EnableAutoUpdateBox.Checked == true) //Using AutoUpdate:
                {
                    Console.WriteLine("You have latest [" + ParseRevision.developerName + "] revision: " + ParseRevision.lastRevision);
                    if (File.Exists(Program.programPath + "\\Tools\\" + "madcow.ini"))
                    {
                        IConfigSource source = new IniConfigSource(Program.programPath + @"\Tools\madcow.ini");
                        String Src = source.Configs["Balloons"].Get("ShowBalloons");

                        if (Src.Contains("1"))
                        {
                            notifyIcon1.ShowBalloonTip(1000, "MadCow", "You have latest [" + ParseRevision.developerName + "] revision: " + ParseRevision.lastRevision, ToolTipIcon.Info);
                        }
                    }
                    Tick = (int)this.AutoUpdateValue.Value;
                    label1.Text = "Update in " + Tick + " minutes.";
                }
                else //With out AutoUpdate:
                {
                    Console.WriteLine("You have latest [" + ParseRevision.developerName + "] revision: " + ParseRevision.lastRevision);
                    if (File.Exists(Program.programPath + "\\Tools\\" + "madcow.ini"))
                    {
                        IConfigSource source = new IniConfigSource(Program.programPath + @"\Tools\madcow.ini");
                        String Src = source.Configs["Balloons"].Get("ShowBalloons");

                        if (Src.Contains("1"))
                        {
                            notifyIcon1.ShowBalloonTip(1000, "MadCow", "You have latest [" + ParseRevision.developerName + "] revision: " + ParseRevision.lastRevision, ToolTipIcon.Info);
                        }
                    }
                }
            }

            else if (Directory.Exists(Program.programPath + @"/MPQ")) //Checks for MPQ Folder
            {
                if (EnableAutoUpdateBox.Checked == true) //Using AutoUpdate:
                {
                    timer1.Stop();
                    Console.WriteLine("Found default MadCow MPQ folder");
                    DeleteHelper.DeleteOldRepoVersion(ParseRevision.developerName); //We delete old repo version.
                    UpdateMooegeButton.Enabled = false;
                    Console.WriteLine("Downloading...");
                    if (File.Exists(Program.programPath + "\\Tools\\" + "madcow.ini"))
                    {
                        IConfigSource source = new IniConfigSource(Program.programPath + @"\Tools\madcow.ini");
                        String Src = source.Configs["Balloons"].Get("ShowBalloons");

                        if (Src.Contains("1"))
                        {
                            Form1.GlobalAccess.notifyIcon1.ShowBalloonTip(1000, "MadCow", "Downloading...", ToolTipIcon.Info);
                        }
                    }
                    backgroundWorker1.RunWorkerAsync();
                }
                else //With out AutoUpdate:
                {
                    Console.WriteLine("Found default MadCow MPQ folder");
                    DeleteHelper.DeleteOldRepoVersion(ParseRevision.developerName); //We delete old repo version.
                    UpdateMooegeButton.Enabled = false;
                    Console.WriteLine("Downloading...");
                    if (File.Exists(Program.programPath + "\\Tools\\" + "madcow.ini"))
                    {
                        IConfigSource source = new IniConfigSource(Program.programPath + @"\Tools\madcow.ini");
                        String Src = source.Configs["Balloons"].Get("ShowBalloons");

                        if (Src.Contains("1"))
                        {
                            Form1.GlobalAccess.notifyIcon1.ShowBalloonTip(1000, "MadCow", "Downloading...", ToolTipIcon.Info);
                        }
                    }
                    backgroundWorker1.RunWorkerAsync();
                }
            }

            else
            {
                if (EnableAutoUpdateBox.Checked == true) //Using AutoUpdate:
                {
                    timer1.Stop();
                    DeleteHelper.DeleteOldRepoVersion(ParseRevision.developerName); //We delete old repo version.
                    Console.WriteLine("Downloading...");
                    if (File.Exists(Program.programPath + "\\Tools\\" + "madcow.ini"))
                    {
                        IConfigSource source = new IniConfigSource(Program.programPath + @"\Tools\madcow.ini");
                        String Src = source.Configs["Balloons"].Get("ShowBalloons");

                        if (Src.Contains("1"))
                        {
                            Form1.GlobalAccess.notifyIcon1.ShowBalloonTip(1000, "MadCow", "Downloading...", ToolTipIcon.Info);
                        }
                    }
                    Directory.CreateDirectory(Program.programPath + "/MPQ");
                    UpdateMooegeButton.Enabled = false;
                    backgroundWorker1.RunWorkerAsync();
                }
                else //With out AutoUpdate:
                {
                    DeleteHelper.DeleteOldRepoVersion(ParseRevision.developerName); //We delete old repo version.
                    Console.WriteLine("Downloading...");
                    if (File.Exists(Program.programPath + "\\Tools\\" + "madcow.ini"))
                    {
                        IConfigSource source = new IniConfigSource(Program.programPath + @"\Tools\madcow.ini");
                        String Src = source.Configs["Balloons"].Get("ShowBalloons");

                        if (Src.Contains("1"))
                        {
                            Form1.GlobalAccess.notifyIcon1.ShowBalloonTip(1000, "MadCow", "Downloading...", ToolTipIcon.Info);
                        }
                    }
                    Directory.CreateDirectory(Program.programPath + "/MPQ");
                    UpdateMooegeButton.Enabled = false;
                    backgroundWorker1.RunWorkerAsync();
                }
            }
        }

        ///////////////////////////////////////////////////////////
        //Play Diablo Button
        ///////////////////////////////////////////////////////////
        private void PlayDiablo_Click(object sender, EventArgs e)
        {
            IConfigSource source = new IniConfigSource(Program.programPath + @"\Tools\madcow.ini");
            String LastRepo = source.Configs["LastPlay"].Get("Enabled");

            if (LastRepo.Contains("1"))
            {
                label25.Visible = true;
            }
            else
            {
                label25.Visible = false;
            }

            if (ErrorFinder.hasMpqs() == true) //We check for MPQ files count before allowing the user to proceed to play.
            {
                System.Threading.Thread t = new System.Threading.Thread(new System.Threading.ThreadStart(ThreadProc));
                t.Start();
            }
            else if (Diablo3UserPathSelection != null && ErrorFinder.hasMpqs() == false)
            {
                var ErrorAnswer = MessageBox.Show("You haven't copied MPQ files." + "\nWould you like MadCow to fix this for you?", "Fatal Error!",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Stop);

                if (ErrorAnswer == DialogResult.Yes)
                {
                    MPQprocedure.StartCopyProcedure();
                    PlayDiabloButton.Enabled = false;
                    CopyMPQButton.Enabled = false;
                }
            }
            else
                Console.WriteLine("[FATAL] You are missing MPQ files!" + "\nPlease use CopyMpq button or copy Diablo3 MPQ's folder content into MadCow MPQ folder.");
        }

        public void ThreadProc()
        {
            IConfigSource source = new IniConfigSource(Program.programPath + @"\Tools\madcow.ini");
            String LastRepo = source.Configs["LastPlay"].Get("Enabled");

            if (RepositorySelectionPlay.LastPlayed() == true && LastRepo.Contains("1"))
            {
                Diablo.Play();
            }

            else
            {
                Application.Run(new RepositorySelectionPlay());
            }
            //We add ErrorFinder call here, in order to know if Mooege had issues loading.
            if (File.Exists(Program.programPath + @"\logs\mooege.log"))
            {
                if (ErrorFinder.SearchLogs("Fatal") == true)
                {
                    //We delete de Log file HERE. Nowhere else!.
                    DeleteHelper.Delete(0);
                    if (ErrorFinder.errorFileName.Contains("d3-update-base-")) //This will handle corrupted mpqs and missing mpq files.
                    {
                        var ErrorAnswer = MessageBox.Show(@"Missing or Corrupted file [" + ErrorFinder.errorFileName + @"]" + "\nWould you like MadCow to fix this for you?", "Found corrupted file!",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Stop);

                        if (ErrorAnswer == DialogResult.Yes)
                        {
                            //We move the user to the Help tab so he can see the progress of the download.
                            tabControl1.Invoke(new Action(() =>
                            {
                                this.tabControl1.SelectTab("tabPage4");
                            }
                            ));
                            //We execute the procedure to start downloading the corrupted file @ FixMpq();
                            FixMpq();
                        }
                    }
                    if (ErrorFinder.errorFileName.Contains("CoreData"))
                    {
                        var ErrorAnswer = MessageBox.Show(@"Corrupted file [" + ErrorFinder.errorFileName + @".mpq]" + "\nWould you like MadCow to fix this for you?", "Fatal Error!",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Stop);

                        if (ErrorAnswer == DialogResult.Yes)
                        {
                            tabControl1.Invoke(new Action(() =>
                            {
                                this.tabControl1.SelectTab("tabPage4");
                            }
                            ));
                            FixMpq();
                        }
                    }
                    if (ErrorFinder.errorFileName.Contains("ClientData"))
                    {
                        var ErrorAnswer = MessageBox.Show(@"Corrupted file [" + ErrorFinder.errorFileName + @".mpq]" + "\nWould you like MadCow to fix this for you?", "Fatal Error!",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Stop);

                        if (ErrorAnswer == DialogResult.Yes)
                        {
                            tabControl1.Invoke(new Action(() =>
                            {
                                this.tabControl1.SelectTab("tabPage4");
                            }
                            ));
                            FixMpq();
                        }
                    }
                    if (ErrorFinder.errorFileName.Contains("MajorFailure"))
                    {
                        var ErrorAnswer = MessageBox.Show(@"Seems some major files are corrupted." + "\nWould you like MadCow to fix this for you?", "Found Corrupted Files!",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Stop);

                        if (ErrorAnswer == DialogResult.Yes)
                        {
                            tabControl1.Invoke(new Action(() =>
                            {
                                this.tabControl1.SelectTab("tabPage4");
                            }
                            ));
                            backgroundWorker3.RunWorkerAsync();
                        }
                    }
                    else
                    {
                        Console.WriteLine("Unknown Exception");
                        Console.WriteLine(ErrorFinder.errorFileName);
                    }
                }
                else
                {
                    //nothing
                }
            }
            else
            {
                //Nothing!
                //If the user closes Repo selection and we already went through fixing the MPQ, then Mooege.log will not exist and
                //Madcow would crash when trying to read mooege.log.
            }
        }

        ///////////////////////////////////////////////////////////
        //Copy MPQ files from D3 to MadCow MPQ Folder.
        ///////////////////////////////////////////////////////////

        private void CopyMPQs_Click(object sender, EventArgs e)
        {
            MPQprocedure.StartCopyProcedure();
        }


        ///////////////////////////////////////////////////////////
        //Remote Server Settings
        ///////////////////////////////////////////////////////////
        private void RemoteServer_Click(object sender, EventArgs e)
        {
            //Remote Server
            //Opens Diablo with extension to Remote Server
            Process proc1 = new Process();
            proc1.StartInfo = new ProcessStartInfo(Diablo3UserPathSelection.Text);
            String HostIP = textBox2.Text;
            String Port = textBox3.Text;
            String ServerHost = HostIP + @":" + Port;
            proc1.StartInfo.Arguments = @" -launch -auroraaddress " + ServerHost;
            MessageBox.Show(proc1.StartInfo.Arguments);
            proc1.Start();
            Console.WriteLine("Starting Diablo...");
        }


        ///////////////////////////////////////////////////////////
        //Server Control Settings
        ///////////////////////////////////////////////////////////
        private void RestoreDefault_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            //Restore Default Server Control Settings
            BnetServerIp.Text = "0.0.0.0";
            BnetServerPort.Text = "1345";
            GameServerIp.Text = "0.0.0.0";
            GameServerPort.Text = "1999";
            PublicServerIp.Text = "0.0.0.0";
            NATcheckBox.Checked = false;
            MOTD.Text = "Welcome to mooege development server!";
        }

        private void LaunchServer_Click(object sender, EventArgs e)
        {
            System.Threading.Thread t = new System.Threading.Thread(new System.Threading.ThreadStart(ThreadProc2));
            t.Start();
        }

        public void ThreadProc2()
        {
            Application.Run(new RepositorySelectionServer());
            if (File.Exists(Program.programPath + @"\logs\mooege.log"))
            {
                if (ErrorFinder.SearchLogs("Fatal") == true)
                {
                    //We delete de Log file HERE. Nowhere else!.
                    DeleteHelper.Delete(0);
                    if (ErrorFinder.errorFileName.Contains("d3-update-base-"))
                    {
                        var ErrorAnswer = MessageBox.Show(@"Missing or Corrupted file [" + ErrorFinder.errorFileName + @"]" + "\nWould you like MadCow to fix this for you?", "Found corrupted file!",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Stop);

                        if (ErrorAnswer == DialogResult.Yes)
                        {
                            //We move the user to the Help tab so he can see the progress of the download.
                            tabControl1.Invoke(new Action(() =>
                            {
                                this.tabControl1.SelectTab("tabPage4");
                            }
                            ));
                            //We execute the procedure to start downloading the corrupted file @ FixMpq();
                            FixMpq();
                        }
                    }
                    if (ErrorFinder.errorFileName.Contains("CoreData"))
                    {
                        var ErrorAnswer = MessageBox.Show(@"Corrupted file [" + ErrorFinder.errorFileName + @".mpq]" + "\nWould you like MadCow to fix this for you?", "Fatal Error!",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Stop);

                        if (ErrorAnswer == DialogResult.Yes)
                        {
                            tabControl1.Invoke(new Action(() =>
                            {
                                this.tabControl1.SelectTab("tabPage4");
                            }
                            ));
                            FixMpq();
                        }
                    }
                    if (ErrorFinder.errorFileName.Contains("ClientData"))
                    {
                        var ErrorAnswer = MessageBox.Show(@"Corrupted file [" + ErrorFinder.errorFileName + @".mpq]" + "\nWould you like MadCow to fix this for you?", "Fatal Error!",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Stop);

                        if (ErrorAnswer == DialogResult.Yes)
                        {
                            tabControl1.Invoke(new Action(() =>
                            {
                                this.tabControl1.SelectTab("tabPage4");
                            }
                            ));
                            FixMpq();
                        }
                    }
                    if (ErrorFinder.errorFileName.Contains("MajorFailure"))
                    {
                        var ErrorAnswer = MessageBox.Show(@"Seems some major files are corrupted." + "\nWould you like MadCow to fix this for you?", "Found Corrupted Files!",
                            MessageBoxButtons.YesNo, MessageBoxIcon.Stop);

                        if (ErrorAnswer == DialogResult.Yes)
                        {
                            tabControl1.Invoke(new Action(() =>
                            {
                                this.tabControl1.SelectTab("tabPage4");
                            }
                            ));
                            backgroundWorker3.RunWorkerAsync();
                        }
                    }
                    else
                    {
                        Console.WriteLine("Unknown Exception");
                        Console.WriteLine(ErrorFinder.errorFileName);
                    }
                }
                else
                {
                    //nothing
                }
            }
            else
            {
                //Nothing!
                //If the user closes Repo selection and we already went through fixing the MPQ, then Mooege.log will not exist and
                //Madcow would crash when trying to read mooege.log.
            }
        }

        ///////////////////////////////////////////////////////////
        //Timer stuff for AutoUpdate
        ///////////////////////////////////////////////////////////

        private void AutoUpdate_CheckedChanged(object sender, EventArgs e)
        {
            Tick = (int)this.AutoUpdateValue.Value;

            if (EnableAutoUpdateBox.Checked == true)
            {
                label1.Text = "Update in " + Tick + " minutes.";
                timer1.Start();
                BranchComboBox.Enabled = false;
                AutoUpdateValue.Enabled = false;
                comboBox1.Enabled = false;
                ValidateRepoButton.Enabled = false;
                UpdateMooegeButton.Visible = false;
                label1.Visible = true;
            }

            else if (EnableAutoUpdateBox.Checked == false)
            {
                timer1.Stop();
                label1.Text = " ";
                BranchComboBox.Enabled = true;
                AutoUpdateValue.Enabled = true;
                comboBox1.Enabled = true;
                ValidateRepoButton.Enabled = true;
                UpdateMooegeButton.Visible = true;
                label1.Visible = false;
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
           Tick--;
           if (Tick == 0)
           {
                UpdateMooege(); //Runs Update //This was changed in the previous commit.
                Tick = (int)this.AutoUpdateValue.Value;
           }
           else
               label1.Text = "Update in " + Tick + " minutes.";
        }

        ///////////////////////////////////////////////////////////
        //Diablo Path Stuff
        ///////////////////////////////////////////////////////////

        private void InitializeFindPath()
        {
            if (File.Exists(Program.programPath + "\\Tools\\" + "madcow.ini"))
            {
                try
                {
                    IConfigSource source = new IniConfigSource(Program.programPath + @"\Tools\madcow.ini");
                    String Src = source.Configs["DiabloPath"].Get("D3Path");

                    if (Src.Contains("MODIFY")) //If a d3 path exist, then we wont find "MODIFY" and program will proceed.
                    {
                        Diablo3UserPathSelection.Text = "Please Select your Diablo III path.";
                        CopyMPQButton.Enabled = false;
                        PlayDiabloButton.Enabled = false;
                        textBox2.Enabled = false;
                        textBox3.Enabled = false;
                        RemoteServerButton.Enabled = false;
                    }
                    else
                    {
                        Diablo3UserPathSelection.Text = Src;
                        CopyMPQButton.Enabled = true;
                        PlayDiabloButton.Enabled = true;
                        textBox2.Enabled = true;
                        textBox3.Enabled = true;
                        RemoteServerButton.Enabled = true;
                        //Freezes at the start longer, but at least you get verified when you load!
                        backgroundWorker2.RunWorkerAsync(); //Compares Versions of D3 with Mooege
                    }
                }
                catch (Exception Ex)
                {
                    Console.WriteLine("Something failed while trying to verify D3 Version or Writting INI");
                    Console.WriteLine(Ex);
                }
            }
        }

        //Find Diablo Path Dialog
        private void FindDiablo_Click(object sender, EventArgs e)
        {
            String madCowIni = "madcow.ini"; //Our INI setting file.
            //Opens path to find Diablo3
            OpenFileDialog FindD3Exe = new OpenFileDialog();
            FindD3Exe.Title = "MadCow By Wesko";
            FindD3Exe.InitialDirectory = @"C:\Program Files (x86)\Diablo III Beta\";
            FindD3Exe.Filter = "Diablo III|Diablo III.exe";
            DialogResult response = new DialogResult();
            response = FindD3Exe.ShowDialog();
            if (response == DialogResult.OK) // If user was able to locate Diablo III.exe
            {
                // Get the directory name.
                String dirName = System.IO.Path.GetDirectoryName(FindD3Exe.FileName);
                // Output Name
                Diablo3UserPathSelection.Text = FindD3Exe.FileName;
                //Bottom three are Enabled on Remote Server
                textBox2.Enabled = true;
                textBox3.Enabled = true;
                RemoteServerButton.Enabled = true;

                if (File.Exists(Program.programPath + "\\Tools\\" + madCowIni))
                {
                    //First we modify the Mooege INI storage path.
                    IConfigSource source = new IniConfigSource(Program.programPath + "\\Tools\\" + madCowIni);
                    IConfig config = source.Configs["DiabloPath"];
                    config.Set("D3Path", Diablo3UserPathSelection.Text);
                    IConfig config1 = source.Configs["DiabloPath"];
                    config1.Set("MPQpath", new FileInfo(Diablo3UserPathSelection.Text).DirectoryName + "\\Data_D3\\PC\\MPQs");
                    source.Save();
                    Console.WriteLine("Correctly applied D3 client path to madcow.ini");
                    Console.WriteLine("Verifying Diablo..");
                }

                backgroundWorker2.RunWorkerAsync(); //Compares versions of D3 with Mooege
            }//If the user opens the dialog to select a d3 path and he closes the dialog but already had a d3 path, then no warning will be triggered. (BUG FIXED-wesko)
            else if (response == DialogResult.Cancel && this.Diablo3UserPathSelection.TextLength == 35)//If user didn't select a Diablo III.exe, we show a warning and ofc, we dont save any path.
            {
                MessageBox.Show("You didn't select a Diablo III client", "Warning",
                MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        /////////////////////////////////
        //DOWNLOAD SOURCE FROM REPOSITORY
        /////////////////////////////////
        public static String selectedBranch;
        private void backgroundWorker1_DoWork(object sender, DoWorkEventArgs e)
        {
            //We get the selected branch first.
            BranchComboBox.Invoke(new Action(() => { selectedBranch = BranchComboBox.SelectedItem.ToString(); }));
            Uri url = new Uri(ParseRevision.revisionUrl + "/zipball/" + selectedBranch);
            System.Net.HttpWebRequest request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);
            System.Net.HttpWebResponse response = (System.Net.HttpWebResponse)request.GetResponse();
            response.Close();
            // Gets bytes.
            Int64 iSize = response.ContentLength;

            // Keeping track of downloaded bytes.
            Int64 iRunningByteTotal = 0;

            // Open Webclient.
            using (System.Net.WebClient client = new System.Net.WebClient())
            {
                // Open the file at the remote path.
                using (System.IO.Stream streamRemote = client.OpenRead(new Uri(ParseRevision.revisionUrl + "/zipball/" + selectedBranch)))
                {
                    // We write those files into the file system.
                    using (Stream streamLocal = new FileStream(Program.programPath + "/Repositories/Mooege.zip", FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        // Loop the stream and get the file into the byte buffer
                        int iByteSize = 0;
                        byte[] byteBuffer = new byte[iSize];
                        while ((iByteSize = streamRemote.Read(byteBuffer, 0, byteBuffer.Length)) > 0)
                        {
                            // Write the bytes to the file system at the file path specified
                            streamLocal.Write(byteBuffer, 0, iByteSize);
                            iRunningByteTotal += iByteSize;

                            // Calculate the progress out of a base "100"
                            double dIndex = (double)(iRunningByteTotal);
                            double dTotal = (double)byteBuffer.Length;
                            double dProgressPercentage = (dIndex / dTotal);
                            int iProgressPercentage = (int)(dProgressPercentage * 100);

                            // Update the progress bar
                            backgroundWorker1.ReportProgress(iProgressPercentage);
                        }

                        // Clean up the file stream
                        streamLocal.Close();
                    }

                    // Close the connection to the remote server
                    streamRemote.Close();
                }
            }

        }

        //UPDATE PROGRESS BAR
        private void backgroundWorker1_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            progressBar2.Value = e.ProgressPercentage;
        }

        //PROCEED WITH THE PROCESS ONCE THE DOWNLOAD ITS COMPLETE
        private void backgroundWorker1_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            //We reset progressbar value after finishing.
            Console.WriteLine("Download Complete!");
            if (File.Exists(Program.programPath + "\\Tools\\" + "madcow.ini"))
            {
                IConfigSource source = new IniConfigSource(Program.programPath + @"\Tools\madcow.ini");
                String Src = source.Configs["Balloons"].Get("ShowBalloons");

                if (Src.Contains("1"))
                {
                    Form1.GlobalAccess.notifyIcon1.ShowBalloonTip(1000, "MadCow", "Download Complete!", ToolTipIcon.Info);
                }
            }
            progressBar2.Value = 0;
            progressBar2.Update();
            generalProgressBar.PerformStep();
            MadCowProcedure.RunWholeProcedure();
            UpdateMooegeButton.Enabled = true;
            if (EnableAutoUpdateBox.Checked == true)
            {
                Tick = (int)this.AutoUpdateValue.Value;
                timer1.Start();
                label1.Text = "Update in " + Tick + " minutes.";
            }
        }

        ///////////////////////////////////////////////////////////
        //ResetRepoFolder
        ///////////////////////////////////////////////////////////

        private void ResetRepoFolder_Click(object sender, EventArgs e)
        {
            DeleteHelper.Delete(1);
        }

        ///////////////////////////////////////////////////////////
        //Verify Diablo 3 Version compared to Mooege supported one.
        ///////////////////////////////////////////////////////////

        //We open a WebClient in the backgroundworker so it doesnt freeze MadCow UI.
        private void backgroundWorker2_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                WebClient client = new WebClient();
                client.DownloadStringCompleted += new DownloadStringCompletedEventHandler(Checkversions);
                Uri uri = new Uri("https://raw.github.com/mooege/mooege/master/src/Mooege/Common/Versions/VersionInfo.cs");
                client.DownloadStringAsync(uri);
            }
            catch
            {
                Console.WriteLine("Check yor internet connection");
            }
        }

        //After Asyn download complete, we proceed to parse the required version by Mooege in VersionInfo.cs.
        private void Checkversions(Object sender, DownloadStringCompletedEventArgs e)
        {
            try
            {
                if (File.Exists(Diablo3UserPathSelection.Text))
                {
                    String parseVersion = e.Result;
                    FileVersionInfo d3Version = FileVersionInfo.GetVersionInfo(Diablo3UserPathSelection.Text);
                    Int32 ParsePointer = parseVersion.IndexOf("RequiredPatchVersion = ");
                    String MooegeVersion = parseVersion.Substring(ParsePointer + 23, 4); //Gets required version by Mooege
                    MooegeSupportedVersion = MooegeVersion; //Public String to display over D3 path validation.
                    int CurrentD3VersionSupported = Convert.ToInt32(MooegeVersion);
                    int LocalD3Version = d3Version.FilePrivatePart;

                    //DISABLED MATCHING CLIENT VERSIONS FOR NOW.
                    if (LocalD3Version == CurrentD3VersionSupported || LocalD3Version != CurrentD3VersionSupported)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("Found the correct Mooege supported version of Diablo III [" + CurrentD3VersionSupported + "]");
                        Console.ForegroundColor = ConsoleColor.White;
                        PlayDiabloButton.Invoke(new Action(() =>
                        {
                            PlayDiabloButton.Enabled = true;
                        }
                        ));
                        CopyMPQButton.Invoke(new Action(() =>
                        {
                            CopyMPQButton.Enabled = true;
                        }
                        ));
                    }
                    //If the versions missmatch:
                    /*else if (LocalD3Version != CurrentD3VersionSupported)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("Wrong client version FOUND!");
                        Console.ForegroundColor = ConsoleColor.White;
                        MessageBox.Show("You need Diablo III Client version [" + MooegeSupportedVersion + "] in order to play over Mooege.\nPlease Update.", "Warning",
                        MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                        PlayDiabloButton.Invoke(new Action(() =>
                        {
                            PlayDiabloButton.Enabled = false;
                        }
                        ));
                        CopyMPQButton.Invoke(new Action(() =>
                        {
                            CopyMPQButton.Enabled = false;
                        }
                        ));
                    }*/
                }
                else //If User removed or changed D3 exe location that was already saved in madcow path, we set madcow.ini paths to default again.
                {
                    IConfigSource madcowIni = new IniConfigSource(Program.programPath + @"\Tools\madcow.ini");
                    madcowIni.Configs["DiabloPath"].Set("D3Path", "MODIFY");
                    madcowIni.Configs["DiabloPath"].Set("MPQpath", "");
                    madcowIni.ReplaceKeyValues();
                    madcowIni.Save();
                    MessageBox.Show("Could not find Diablo III.exe, please select the proper path again.", "Warning",
                    MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    Diablo3UserPathSelection.Invoke(new Action(() => { this.Diablo3UserPathSelection.Text = "Please Select your Diablo III path."; }));
                    PlayDiabloButton.Invoke(new Action(() => { PlayDiabloButton.Enabled = false; }));
                    CopyMPQButton.Invoke(new Action(() => { CopyMPQButton.Enabled = false; }));
                }
            }
            catch
            {
                Console.WriteLine("[FATAL] Internet connection failed or GitHub site is down!");
            }
        }

        ///////////////////////////////////////////////////////////
        //Save Profile (We write a .mdc (Madcow :P) file into "ServerProfiles" Folder. //We validate first the IP & Port Entries.
        ///////////////////////////////////////////////////////////
        private void SaveProfile_Click(object sender, EventArgs e)
        {
            //Match Pattern
            string pattern = @"(([1-9]?[0-9]|1[0-9]{2}|2[0-4][0-9]|255[0-5])\.){3}([1-9]?[0-9]|1[0-9]{2}|2[0-4][0-9]|25[0-5])";
            Regex check = new Regex(pattern);
            string[] ipFields = { this.BnetServerIp.Text, this.GameServerIp.Text, this.PublicServerIp.Text };
            int i = 0; //Foreach IP counter.
            int j = 0; //Foreach PORT counter.
            Boolean invalidIP = false;

            //Error handling if textbox null -> Giving feedback in the textbox field.
            for (int x = 0; x < ipFields.Length; x++)
            {
                if (string.IsNullOrEmpty(ipFields[x]))
                {
                    IpErrorHandler(x);
                    invalidIP = true;
                }
            }

            //VALIDATION FOR IP
            if (invalidIP == false)
            {
                foreach (string value in ipFields)
                {
                    for (int Lp = 0; Lp < 999; Lp++)
                    {
                        string IPAddress = String.Format("{0}.{0}.{0}.{0}", Lp);

                        if (Regex.Match(ipFields[i], pattern).Success)
                        {
                            IpCorrectHandler(i);
                        }
                        else
                        {
                            IpErrorHandler(i);
                            invalidIP = true;
                            break;
                        }
                    }
                    i++;
                }
            }

            //VALIDATION FOR PORT

            string[] portFields = { this.BnetServerPort.Text, this.GameServerPort.Text };
            int Number;
            bool isNumber;

            for (int x = 0; x < portFields.Length; x++)
            {
                if (string.IsNullOrEmpty(portFields[x]))
                {
                    PortErrorHandler(x);
                    invalidIP = true;
                }
            }

            foreach (string value in portFields)
            {
                isNumber = Int32.TryParse(portFields[j], out Number);

                if (!isNumber || portFields[j].Length > 4 || portFields[j].Length < 4)
                {
                    PortErrorHandler(j);
                    invalidIP = true;
                }
                else
                {
                    PortCorrectHandler(j);
                }
                j++;
            }

            if (invalidIP == false)
            {
                //If invalidIP == false which means all fields are valid, we hide any error cross that might be around.
                this.ErrorBnetServerIp.Visible = false;
                this.ErrorBnetServerPort.Visible = false;
                this.ErrorGameServerIp.Visible = false;
                this.ErrorGameServerPort.Visible = false;
                this.ErrorGameServerPort.Visible = false;
                this.ErrorPublicServerIp.Visible = false;

                //If invalidIP == false which means all fields are valid, we show all green ticks!
                this.TickBnetServerIP.Visible = true;
                this.TickBnetServerPort.Visible = true;
                this.TickGameServerIp.Visible = true;
                this.TickGameServerPort.Visible = true;
                this.TickGameServerPort.Visible = true;
                this.TickPublicServerIp.Visible = true;

                //We proceed to ask the user where to save the file.
                SaveFileDialog saveProfile = new SaveFileDialog();
                saveProfile.Title = "Save Server Profile";
                saveProfile.DefaultExt = ".mdc";
                saveProfile.Filter = "MadCow Profile|*.mdc";
                saveProfile.InitialDirectory = Program.programPath + @"\ServerProfiles";
                saveProfile.ShowDialog();

                if (saveProfile.FileName == "")
                {
                    Console.WriteLine("You didn't specify a profile name");
                }

                else
                {
                    CurrentProfile = saveProfile.FileName; //We set the global string value, we will grab this value from RepositorySelectionServer.
                    TextWriter tw = new StreamWriter(saveProfile.FileName);
                    //tw.WriteLine("Bnet Server Ip");
                    tw.WriteLine(this.BnetServerIp.Text);
                    //tw.WriteLine("Game Server Ip");
                    tw.WriteLine(this.GameServerIp.Text);
                    //tw.WriteLine("Public Server Ip");
                    tw.WriteLine(this.PublicServerIp.Text);
                    //tw.WriteLine("Bnet Server Port");
                    tw.WriteLine(this.BnetServerPort.Text);
                    //tw.WriteLine("Game Server Port");
                    tw.WriteLine(this.GameServerPort.Text);
                    //tw.WriteLine("MOTD");
                    tw.WriteLine(this.MOTD.Text);
                    //tw.WriteLine("NAT");
                    tw.WriteLine(this.NATcheckBox.Checked);
                    tw.Close();
                    Console.Write("Saved profile " + saveProfile.FileName + " succesfully");
                    //Proceed to save the profile over our INI file.
                    IConfigSource source = new IniConfigSource(Program.programPath + @"\Tools\madcow.ini");
                    source.Configs["Profiles"].Set("Profile", CurrentProfile);
                    source.Save();
                }
            }
        }

        private void IpErrorHandler(int i)
        {
            switch (i)
            {
                case 0: //BnetServerIp
                    this.BnetServerIp.Text = "Invalid IP";
                    this.ErrorBnetServerIp.Visible = true;
                    this.TickBnetServerIP.Visible = false;
                    break;
                case 1: //GameServerIp
                    this.GameServerIp.Text = "Invalid IP";
                    this.ErrorGameServerIp.Visible = true;
                    this.TickGameServerIp.Visible = false;
                    break;
                case 2: //PublicServerIp
                    this.PublicServerIp.Text = "Invalid IP";
                    this.ErrorPublicServerIp.Visible = true;
                    this.TickPublicServerIp.Visible = false;
                    break;
            }
        }

        private void IpCorrectHandler(int i)
        {
            switch (i)
            {
                case 0: //BnetServerIp
                    this.ErrorBnetServerIp.Visible = false;
                    this.TickBnetServerIP.Visible = true;
                    break;
                case 1: //GameServerIp
                    this.ErrorGameServerIp.Visible = false;
                    this.TickGameServerIp.Visible = true;
                    break;
                case 2: //PublicServerIp
                    this.ErrorPublicServerIp.Visible = false;
                    this.TickPublicServerIp.Visible = true;
                    break;
            }
        }

        private void PortErrorHandler(int i)
        {
            switch (i)
            {
                case 0: //BnetServerPort
                    this.BnetServerPort.Text = "Invalid Port";
                    this.ErrorBnetServerPort.Visible = true;
                    this.TickBnetServerPort.Visible = false;
                    break;
                case 1: //GameServerPort
                    this.GameServerPort.Text = "Invalid Port";
                    this.ErrorGameServerPort.Visible = true;
                    this.TickGameServerPort.Visible = false;
                    break;
            }
        }

        private void PortCorrectHandler(int i)
        {
            switch (i)
            {
                case 0: //BnetServerPort
                    this.ErrorBnetServerPort.Visible = false;
                    this.TickBnetServerPort.Visible = true;
                    break;
                case 1: //GameServerPort
                    this.ErrorGameServerPort.Visible = false;
                    this.TickGameServerPort.Visible = true;
                    break;
            }
        }

        ///////////////////////////////////////////////////////////
        //Load Profile - We load from a .mdc file, update CurrenProfile variable and parse the values into the boxes.
        ///////////////////////////////////////////////////////////
        private void LoadProfile_Click(object sender, EventArgs e)
        {
            OpenFileDialog OpenProfile = new OpenFileDialog();
            OpenProfile.Title = "Save Server Profile";
            OpenProfile.Filter = "MadCow Profile|*.mdc";
            OpenProfile.InitialDirectory = Program.programPath + @"\ServerProfiles";
            OpenProfile.ShowDialog();
            if (OpenProfile.FileName == "")
            {
                Console.WriteLine("You didn't select a profile name");
            }

            else
            {
                CurrentProfile = OpenProfile.FileName; //We set the global string value, we will grab this value from RepositorySelectionServer.
                TextReader tr = new StreamReader(OpenProfile.FileName);
                this.BnetServerIp.Text = tr.ReadLine();
                this.GameServerIp.Text = tr.ReadLine();
                this.PublicServerIp.Text = tr.ReadLine();
                this.BnetServerPort.Text = tr.ReadLine();
                this.GameServerPort.Text = tr.ReadLine();
                this.MOTD.Text = tr.ReadLine();
                if (tr.ReadLine().Contains("True"))
                {
                    this.NATcheckBox.Checked = true;
                }
                else
                {
                    this.NATcheckBox.Checked = false;
                }
                tr.Close();
                //Loading a profile means it has the correct values for every box, so first we disable every red cross that might be out there.
                this.ErrorBnetServerIp.Visible = false;
                this.ErrorBnetServerPort.Visible = false;
                this.ErrorGameServerIp.Visible = false;
                this.ErrorGameServerPort.Visible = false;
                this.ErrorGameServerPort.Visible = false;
                this.ErrorPublicServerIp.Visible = false;
                //Loading a profile means it has the correct values for every box, so we change everything to green ticked.
                this.TickBnetServerIP.Visible = true;
                this.TickBnetServerPort.Visible = true;
                this.TickGameServerIp.Visible = true;
                this.TickGameServerPort.Visible = true;
                this.TickGameServerPort.Visible = true;
                this.TickPublicServerIp.Visible = true;
                Console.Write("Loaded Profile " + OpenProfile.FileName + " succesfully");
                //Proceed to save the profile over our INI file.
                IConfigSource source = new IniConfigSource(Program.programPath + @"\Tools\madcow.ini");
                source.Configs["Profiles"].Set("Profile", CurrentProfile);
                source.Save();
            }
        }

        public void LoadLastUsedProfile()
        {
            try
            {
                IConfigSource source = new IniConfigSource(Program.programPath + @"\Tools\madcow.ini");
                String currentProfile = source.Configs["Profiles"].Get("Profile");

                if (currentProfile.Length > 0)
                {
                    TextReader tr = new StreamReader(currentProfile);
                    this.BnetServerIp.Text = tr.ReadLine();
                    this.GameServerIp.Text = tr.ReadLine();
                    this.PublicServerIp.Text = tr.ReadLine();
                    this.BnetServerPort.Text = tr.ReadLine();
                    this.GameServerPort.Text = tr.ReadLine();
                    this.MOTD.Text = tr.ReadLine();
                    if (tr.ReadLine().Contains("True"))
                    {
                        this.NATcheckBox.Checked = true;
                    }
                    else
                    {
                        this.NATcheckBox.Checked = false;
                    }
                    tr.Close();

                    //Loading a profile means it has the correct values for every box, so first we disable every red cross that might be out there.
                    this.ErrorBnetServerIp.Visible = false;
                    this.ErrorBnetServerPort.Visible = false;
                    this.ErrorGameServerIp.Visible = false;
                    this.ErrorGameServerPort.Visible = false;
                    this.ErrorGameServerPort.Visible = false;
                    this.ErrorPublicServerIp.Visible = false;
                    //Loading a profile means it has the correct values for every box, so we change everything to green ticked.
                    this.TickBnetServerIP.Visible = true;
                    this.TickBnetServerPort.Visible = true;
                    this.TickGameServerIp.Visible = true;
                    this.TickGameServerPort.Visible = true;
                    this.TickGameServerPort.Visible = true;
                    this.TickPublicServerIp.Visible = true;
                    Console.WriteLine("Loaded last used Server profile succesfully");
                }
            }
            catch
            {
                Console.WriteLine("[Error] While loading server profile.");
            }
        }

        ////////////////////////////////////////////////////////////////////////
        //URL TEXT FIELD COLOR MANAGEMENT
        //This has the function on turning letters red if Error, Black if normal.
        ////////////////////////////////////////////////////////////////////////

        private void comboBox1_TextChanged(object sender, EventArgs e)
        {
            string currentText = ParseRevision.commitFile;
            BranchComboBox.Visible = false;
            label10.Visible = true;
            label24.Visible = false;
            UpdateMooegeButton.Enabled = false;
            AutoUpdateValue.Enabled = false; //If user is typing a new URL Update and Autoupdate
            EnableAutoUpdateBox.Enabled = false;      //Functions gets disabled
            try
            {
                if (currentText == "Incorrect repository entry")
                {
                    this.comboBox1.ForeColor = Color.Red;
                }
                else
                {
                    comboBox1.ForeColor = Color.Black;
                    label4.BackColor = System.Drawing.Color.Transparent;
                    pictureBox1.Hide();//Error Image (Cross)
                    pictureBox2.Hide();//Correct Image (Tick)
                }
            }
            catch
            {
                comboBox1.ForeColor = SystemColors.ControlText;
            }
        }

        //Color handler for BnetServerIp
        private void BnetServerIp_TextChanged(object sender, EventArgs e)
        {
            string currentText = this.BnetServerIp.Text;
            try
            {
                if (currentText == "Invalid IP")
                {
                    Console.WriteLine("Check for input errors!");
                    this.BnetServerIp.ForeColor = Color.Red;
                }
                else
                {
                    this.BnetServerIp.ForeColor = Color.Black;
                }
            }
            catch
            {
                this.BnetServerIp.ForeColor = SystemColors.ControlText;
            }
        }
        //Color handler for GameServerIp
        private void GameServerIp_TextChanged(object sender, EventArgs e)
        {
            string currentText = this.GameServerIp.Text;
            try
            {
                if (currentText == "Invalid IP")
                {
                    Console.WriteLine("Check for input errors!");
                    this.GameServerIp.ForeColor = Color.Red;
                }
                else
                {
                    this.GameServerIp.ForeColor = Color.Black;
                }
            }
            catch
            {
                this.GameServerIp.ForeColor = SystemColors.ControlText;
            }
        }
        //Color handler for PublicServerIp
        private void PublicServerIp_TextChanged(object sender, EventArgs e)
        {
            string currentText = this.PublicServerIp.Text;
            try
            {
                if (currentText == "Invalid IP")
                {
                    Console.WriteLine("Check for input errors!");
                    this.PublicServerIp.ForeColor = Color.Red;
                }
                else
                {
                    this.PublicServerIp.ForeColor = Color.Black;
                }
            }
            catch
            {
                this.PublicServerIp.ForeColor = SystemColors.ControlText;
            }
        }
        //Color handler for BnetServerPort
        private void BnetServerPort_TextChanged(object sender, EventArgs e)
        {
            string currentText = this.BnetServerPort.Text;
            try
            {
                if (currentText == "Invalid Port")
                {
                    Console.WriteLine("Check for input errors!");
                    this.BnetServerPort.ForeColor = Color.Red;
                }
                else
                {
                    this.BnetServerPort.ForeColor = Color.Black;
                }
            }
            catch
            {
                this.BnetServerPort.ForeColor = SystemColors.ControlText;
            }
        }
        //Color handler for GameServerPort
        private void GameServerPort_TextChanged(object sender, EventArgs e)
        {
            string currentText = this.GameServerPort.Text;
            try
            {
                if (currentText == "Invalid Port")
                {
                    Console.WriteLine("Check for input errors!");
                    this.GameServerPort.ForeColor = Color.Red;
                }
                else
                {
                    this.GameServerPort.ForeColor = Color.Black;
                }
            }
            catch
            {
                this.GameServerPort.ForeColor = SystemColors.ControlText;
            }
        }

        ////////////////////////////////////////////////////////////////////////
        //Download MPQs selected by the user.
        ////////////////////////////////////////////////////////////////////////
        private void DownloadMPQSButton_Click(object sender, EventArgs e)
        {
            System.Threading.Thread t = new System.Threading.Thread(new System.Threading.ThreadStart(MPQThread));
            t.Start();
        }

        private void MPQThread()
        {
            Application.Run(new MPQDownloader());
        }

        private void DownloadMPQS(object sender, DoWorkEventArgs e)
        {
            IConfigSource source = new IniConfigSource(Program.programPath + @"\Tools\madcow.ini");
            int i = 0; //We use this variable to select save path destination.            
            //Will use this to determinate the correct save path.
            String[] mpqDestination = {
                                          Path.Combine(source.Configs["DiabloPath"].Get("MPQDest"), "base"),
                                          source.Configs["DiabloPath"].Get("MPQDest")
                                      };
            Stopwatch speedTimer = new Stopwatch();
            foreach (string value in MPQDownloader.mpqSelection)
            {
                Uri url = new Uri(value);
                System.Net.HttpWebRequest request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);
                System.Net.HttpWebResponse response = (System.Net.HttpWebResponse)request.GetResponse();

                //Parsing the file name.
                var fullName = url.LocalPath.TrimStart('/');
                var name = Path.GetFileNameWithoutExtension(fullName);
                var ext = Path.GetExtension(fullName);
                //End Parsing.              
                response.Close();
                //Setting save path
                if (name == "CoreData" || name == "ClientData") i = 1; //Path \MPQ\
                else i = 0; //Path \MPQ\base\

                Int64 iSize = response.ContentLength;
                Int64 iRunningByteTotal = 0;

                using (System.Net.WebClient client = new System.Net.WebClient())
                {
                    using (System.IO.Stream streamRemote = client.OpenRead(new Uri(value)))
                    {
                        using (Stream streamLocal = new FileStream(Program.programPath + mpqDestination[i] + name + ext, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            //We start the timer to measure speed - This still needs testing not sure if speed its accuarate. - wesko
                            speedTimer.Start();
                            DownloadingFileName.Invoke(new Action(() =>
                            {
                                this.DownloadingFileName.Text = "Downloading File: " + name + ext;
                            }
                            ));

                            int iByteSize = 0;
                            byte[] byteBuffer = new byte[iSize];
                            while ((iByteSize = streamRemote.Read(byteBuffer, 0, byteBuffer.Length)) > 0)
                            {
                                streamLocal.Write(byteBuffer, 0, iByteSize);
                                iRunningByteTotal += iByteSize;

                                double dIndex = (double)(iRunningByteTotal);
                                double dTotal = (double)byteBuffer.Length;
                                double dProgressPercentage = (dIndex / dTotal);
                                int iProgressPercentage = (int)(dProgressPercentage * 100);

                                //We calculate the download speed.
                                TimeSpan ts = speedTimer.Elapsed;
                                double bytesReceivedSpeed = (iRunningByteTotal / 1024) / ts.TotalSeconds;
                                DownloadFileSpeed.Invoke(new Action(() =>
                                {
                                    this.DownloadFileSpeed.Text = "Downloading Speed: " + Convert.ToInt32(bytesReceivedSpeed) + "Kbps";
                                }
                                ));
                                backgroundWorker3.ReportProgress(iProgressPercentage);
                            }
                            streamLocal.Close();
                        }
                        streamRemote.Close();
                    }
                }
            } speedTimer.Stop();
        }

        private void downloader_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            DownloadingFileName.Visible = true;
            DownloadFileSpeed.Visible = true;
            DownloadMPQSprogressBar.Value = e.ProgressPercentage;
        }

        private void downloader_DownloadedComplete(object sender, RunWorkerCompletedEventArgs e)
        {
            DownloadingFileName.Visible = false;
            DownloadFileSpeed.Visible = false;
            MPQDownloader.mpqSelection.Clear();//Reset the Array values after downloading.
            Console.WriteLine("Download complete.");
        }

        ////////////////////////////////////////////////////////////////////////
        //Download Mpq files that could not be parsed by Mooege.
        ////////////////////////////////////////////////////////////////////////
        public void FixMpq()
        {
            DeleteHelper.Delete(2); //Delete corrupted file.
            backgroundWorker4.RunWorkerAsync();
        }

        private void DownloadSpecificMPQS(object sender, DoWorkEventArgs e)
        {
            var downloadBaseFileUrl = "http://ak.worldofwarcraft.com.edgesuite.net/d3-pod/20FB5BE9/NA/7162.direct/Data_D3/PC/MPQs/base/" + ErrorFinder.errorFileName + @".MPQ";
            var downloadFileUrl = "http://ak.worldofwarcraft.com.edgesuite.net/d3-pod/20FB5BE9/NA/7162.direct/Data_D3/PC/MPQs/" + ErrorFinder.errorFileName + @".mpq";

            IConfigSource source = new IniConfigSource(Program.programPath + @"\Tools\madcow.ini");
            String downloadDestination = source.Configs["DiabloPath"].Get("MPQpath");
            Stopwatch speedTimer = new Stopwatch();

            if (ErrorFinder.errorFileName.Contains("CoreData") || ErrorFinder.errorFileName.Contains("ClientData"))
            {
                Uri url = new Uri(downloadFileUrl);
                System.Net.HttpWebRequest request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);
                System.Net.HttpWebResponse response = (System.Net.HttpWebResponse)request.GetResponse();
                //Parsing the file name.
                var fullName = url.LocalPath.TrimStart('/');
                var name = Path.GetFileNameWithoutExtension(fullName);
                var ext = Path.GetExtension(fullName);
                //End Parsing.
                response.Close();
                Int64 iSize = response.ContentLength;
                Int64 iRunningByteTotal = 0;

                using (System.Net.WebClient client = new System.Net.WebClient())
                {
                    using (System.IO.Stream streamRemote = client.OpenRead(new Uri(downloadFileUrl)))
                    {
                        using (Stream streamLocal = new FileStream(downloadDestination + @"\" + name + ext, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            Console.WriteLine("Starting download...");
                            speedTimer.Start();
                            DownloadingFileName.Invoke(new Action(() =>
                            {
                                this.DownloadingFileName.Text = "Downloading File: " + name + ext;
                            }
                            ));

                            int iByteSize = 0;
                            byte[] byteBuffer = new byte[iSize];
                            while ((iByteSize = streamRemote.Read(byteBuffer, 0, byteBuffer.Length)) > 0)
                            {
                                streamLocal.Write(byteBuffer, 0, iByteSize);
                                iRunningByteTotal += iByteSize;

                                double dIndex = (double)(iRunningByteTotal);
                                double dTotal = (double)byteBuffer.Length;
                                double dProgressPercentage = (dIndex / dTotal);
                                int iProgressPercentage = (int)(dProgressPercentage * 100);

                                //We calculate the download speed.
                                TimeSpan ts = speedTimer.Elapsed;
                                double bytesReceivedSpeed = (iRunningByteTotal / 1024) / ts.TotalSeconds;
                                DownloadFileSpeed.Invoke(new Action(() =>
                                {
                                    this.DownloadFileSpeed.Text = "Downloading Speed: " + Convert.ToInt32(bytesReceivedSpeed) + "Kbps";
                                }
                                ));
                                backgroundWorker4.ReportProgress(iProgressPercentage);
                            }
                            streamLocal.Close();
                        }
                        streamRemote.Close();
                    }
                }
                speedTimer.Stop();
                File.Copy(downloadDestination + @"\" + ErrorFinder.errorFileName + @".mpq", Program.programPath + @"\MPQ\" + ErrorFinder.errorFileName + @".mpq", true);
            }
            else
            {
                Uri url = new Uri(downloadBaseFileUrl);
                System.Net.HttpWebRequest request = (System.Net.HttpWebRequest)System.Net.WebRequest.Create(url);
                System.Net.HttpWebResponse response = (System.Net.HttpWebResponse)request.GetResponse();
                //Parsing the file name.
                var fullName = url.LocalPath.TrimStart('/');
                var name = Path.GetFileNameWithoutExtension(fullName);
                var ext = Path.GetExtension(fullName);
                //End Parsing.
                response.Close();
                Int64 iSize = response.ContentLength;
                Int64 iRunningByteTotal = 0;

                using (System.Net.WebClient client = new System.Net.WebClient())
                {
                    using (System.IO.Stream streamRemote = client.OpenRead(new Uri(downloadBaseFileUrl)))
                    {
                        using (Stream streamLocal = new FileStream(downloadDestination + @"\base\" + name + ext, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            Console.WriteLine("Starting download...");
                            speedTimer.Start();
                            DownloadingFileName.Invoke(new Action(() =>
                            {
                                this.DownloadingFileName.Text = "Downloading File: " + name + ext;
                            }
                            ));

                            int iByteSize = 0;
                            byte[] byteBuffer = new byte[iSize];
                            while ((iByteSize = streamRemote.Read(byteBuffer, 0, byteBuffer.Length)) > 0)
                            {
                                streamLocal.Write(byteBuffer, 0, iByteSize);
                                iRunningByteTotal += iByteSize;

                                double dIndex = (double)(iRunningByteTotal);
                                double dTotal = (double)byteBuffer.Length;
                                double dProgressPercentage = (dIndex / dTotal);
                                int iProgressPercentage = (int)(dProgressPercentage * 100);

                                //We calculate the download speed.
                                TimeSpan ts = speedTimer.Elapsed;
                                double bytesReceivedSpeed = (iRunningByteTotal / 1024) / ts.TotalSeconds;
                                DownloadFileSpeed.Invoke(new Action(() =>
                                {
                                    this.DownloadFileSpeed.Text = "Downloading Speed: " + Convert.ToInt32(bytesReceivedSpeed) + "Kbps";
                                }
                                ));
                                backgroundWorker4.ReportProgress(iProgressPercentage);
                            }
                            streamLocal.Close();
                        }
                        streamRemote.Close();
                    }
                }
                speedTimer.Stop();
                File.Copy(downloadDestination + @"\base\" + ErrorFinder.errorFileName + @".MPQ", Program.programPath + @"\MPQ\" + @"\base\" + ErrorFinder.errorFileName + @".MPQ", true);
            }
        }

        private void downloader_ProgressChanged2(object sender, ProgressChangedEventArgs e)
        {
            DownloadingFileName.Invoke(new Action(() =>
            {
                DownloadingFileName.Visible = true;
                DownloadFileSpeed.Visible = true;
                DownloadMPQSprogressBar.Value = e.ProgressPercentage;
            }
            ));
        }

        private void downloader_DownloadedComplete2(object sender, RunWorkerCompletedEventArgs e)
        {
            IConfigSource source = new IniConfigSource(Program.programPath + @"\Tools\madcow.ini");
            String downloadSource = source.Configs["DiabloPath"].Get("MPQpath");
            String downloadDestination = Program.programPath + @"\MPQ\base\";
            DownloadingFileName.Invoke(new Action(() =>
            {
                DownloadingFileName.Visible = false;
                DownloadFileSpeed.Visible = false;
            }
            ));
            Console.WriteLine("Download complete.");
            try
            {
                //File.Copy(downloadSource + @"\base\" + ErrorFinder.errorFileName + @".MPQ", downloadDestination + ErrorFinder.errorFileName + @".MPQ", true);
                //Console.WriteLine("Copied new MPQ to MadCow MPQ home Folder.");
                //We give the user an announce of success.
                DialogResult response = DotNetPerls.BetterDialog.ShowDialog("MadCow Worker",
                "MadCow succesfully fixed:",
                ErrorFinder.errorFileName + @".MPQ",
                "", "OK", Properties.Resources.correct);
                if (response == DialogResult.Cancel) //We take this as the OK response.
                {
                    //Since problem must be fixed, we take the user to the Update tab & execute repo selection again
                    //We move the user to the Help tab so he can see the progress of the download.
                    tabControl1.Invoke(new Action(() =>
                    {
                        this.tabControl1.SelectTab("tabPage1");
                    }
                    ));
                    System.Threading.Thread t = new System.Threading.Thread(new System.Threading.ThreadStart(ThreadProc));
                    t.Start();
                }
            }
            catch
            {
                Console.WriteLine("Error while copying new MPQ to MadCow MPQ home Folder");
            }

        }

        ////////////////////////////////////////////////////////////////////////
        //Dynamically Add Repos, but also remove duplicates.
        ////////////////////////////////////////////////////////////////////////
        private Int32 RepoListIndex;

        private void RepoList()
        {
            StreamReader sr = new StreamReader(Program.programPath + @"\Tools\RepoList.txt");
            string line = sr.ReadLine();

            while (line != null)
            {
                comboBox1.Items.Add(line);
                line = sr.ReadLine();
                RepoListIndex++;
            }
            sr.Close();
        }
        private void RepoCheck()
        {
            string[] lines = File.ReadAllLines(Program.programPath + @"\Tools\RepoList.txt");
            File.WriteAllLines(Program.programPath + @"\Tools\RepoList.txt", lines.Distinct().ToArray());
        }

        private void RepoListAdd()
        {
            StreamWriter str;
            str = File.AppendText(Program.programPath + @"\Tools\RepoList.txt");
            str.WriteLine(comboBox1.Text);
            str.Close();
        }

        private void RepoListUpdate()
        {
            RepoCheck();
            comboBox1.Items.Clear();
            StreamReader sr = new StreamReader(Program.programPath + @"\Tools\RepoList.txt");
            string line = sr.ReadLine();

            while (line != null)
            {
                comboBox1.Items.Add(line);
                line = sr.ReadLine();
                RepoListIndex++;
            }
            sr.Close();
        }

        ////////////////////////////////////////////////////////////////////////
        //Changelog.
        ////////////////////////////////////////////////////////////////////////

        //Fill Changelog Repository ComboBox.
        private void Changelog()
        {
            StreamReader sr = new StreamReader(Program.programPath + @"\Tools\RepoList.txt");
            string line = sr.ReadLine();

            while (line != null)
            {
                string s = line.Replace(@"https://github.com/", "");
                string d = s.Replace(@"/mooege", "");
                string e = d.Replace(@"/d3sharp", "");
                comboBox2.Items.Add(e);
                line = sr.ReadLine();
            }
            sr.Close();
        }

        //Update Changelog repository list in real time.
        private void ChangelogListUpdate()
        {
            comboBox2.Items.Clear();
            StreamReader sr = new StreamReader(Program.programPath + @"\Tools\RepoList.txt");
            string line = sr.ReadLine();
            while (line != null)
            {
                string s = line.Replace(@"https://github.com/", "");
                string d = s.Replace(@"/mooege", "");
                string e = d.Replace(@"/d3sharp", "");
                comboBox2.Items.Add(e);
                line = sr.ReadLine();
            }
            sr.Close();
        }

        //Parse commit file and display into the textbox.
        private void DisplayChangelog(object sender, AsyncCompletedEventArgs e)
        {
            textBox1.Invoke(new Action(() => { textBox1.Clear(); }));
            using (FileStream fileStream = new FileStream(Program.programPath + @"\Commits.ATOM", FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                using (TextReader reader = new StreamReader(fileStream))
                {
                    string line;
                    int i = 0; //This is to get rid of the first <title> tag.
                    while ((line = reader.ReadLine()) != null)
                    {
                        //For commits comment.
                        if (System.Text.RegularExpressions.Regex.IsMatch(line, "<title>") && i > 0)
                        {
                            var regex = new Regex("<title>(.*)</title>");
                            var match = regex.Match(line);
                            textBox1.Invoke(new Action(() => { textBox1.AppendText(i + @".-" + match.Groups[1].Value + "\n"); }));
                            i++;
                        }
                        else if (System.Text.RegularExpressions.Regex.IsMatch(line, "<title>") && i == 0)
                        {
                            i++;
                        }

                        //For update date/time.
                        if (System.Text.RegularExpressions.Regex.IsMatch(line, "<updated>") && i > 1)
                        {
                            var regex = new Regex("<updated>(.*)</updated>");
                            var match = regex.Match(line);
                            textBox1.Invoke(new Action(() => { textBox1.AppendText(@"Updated: " + match.Groups[1].Value + "\n"); }));
                        }

                        //For developer that pushed.
                        if (System.Text.RegularExpressions.Regex.IsMatch(line, "<name>") && i > 0)
                        {
                            var regex = new Regex("<name>(.*)</name>");
                            var match = regex.Match(line);
                            textBox1.Invoke(new Action(() =>
                            {
                                textBox1.AppendText(@"Author: " + match.Groups[1].Value + "\n");
                                textBox1.AppendText(Environment.NewLine);
                            }));
                        }
                    }
                }
            }
            //We scroll the content up to show latest commits first.
            textBox1.Invoke(new Action(() =>
            {
                textBox1.SelectionStart = 0;
                textBox1.ScrollToCaret();
            }));
        }

        private void backgroundWorker6_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                WebClient client = new WebClient();
                client.DownloadFileAsync(new Uri(selectedRepo + @"/commits/master.atom"), @"Commits.atom");
                client.DownloadFileCompleted += new AsyncCompletedEventHandler(DisplayChangelog);
            }
            catch
            {
                Console.WriteLine("Check yor internet connection");
            }
        }

        public String selectedRepo = "";
        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            //We first parse the repo name selected by the user.
            selectedRepo = this.comboBox2.Text;

            //We search for that repo URL over RepoList.txt
            StreamReader sr = new StreamReader(Program.programPath + @"\Tools\RepoList.txt");
            string line = sr.ReadLine();

            while (line != null)
            {
                if (System.Text.RegularExpressions.Regex.IsMatch(line, selectedRepo))
                {
                    //Pass the whole URL to selectedRepo string that we will use to create the new Uri.
                    selectedRepo = line;
                }
                line = sr.ReadLine();
            }
            sr.Close();
            //Proceed to download the commit file and parse.
            backgroundWorker6.RunWorkerAsync();
        }

        //BE AWARE: CRAP BELOW.
        private void tabPage1_Click(object sender, EventArgs e) { }
        private void label2_Click(object sender, EventArgs e) { }
        private void label10_Click(object sender, EventArgs e) { }
        private void label9_Click(object sender, EventArgs e) { }
        private void textBox6_TextChanged(object sender, EventArgs e) { }
        private void textBox7_TextChanged(object sender, EventArgs e) { }
        private void label12_Click(object sender, EventArgs e) { }
        private void openFileDialog1_FileOk(object sender, CancelEventArgs e) { }
        private void numericUpDown1_ValueChanged(object sender, EventArgs e) { }
        private void toolTip1_Popup(object sender, PopupEventArgs e) { }
        private void openFileDialog1_FileOk_1(object sender, CancelEventArgs e) { }
        private void textBox2_TextChanged(object sender, EventArgs e) { }
        private void textBox3_TextChanged(object sender, EventArgs e) { }
        private void textBox13_TextChanged(object sender, EventArgs e) { /*Bnet Server IP*/ }
        private void textBox12_TextChanged(object sender, EventArgs e) { /*Bnet Server Port*/ }
        private void textBox11_TextChanged(object sender, EventArgs e) { /*Game Server IP*/ }
        private void textBox10_TextChanged(object sender, EventArgs e) { /*Game Server Port*/ }
        private void textBox9_TextChanged(object sender, EventArgs e) { /*Public Server IP*/ }
        private void checkBox3_CheckedChanged(object sender, EventArgs e) { /*enable or disable NAT*/ }

        ////////////////////////////////////////////////////////////////////////
        //Tray Icon Stuff
        ////////////////////////////////////////////////////////////////////////
        public Int32 notifyCount = 0;

        public void loadTrayMenu()
        {
            m_menu = new ContextMenu();
            m_menu.MenuItems.Add(0, new MenuItem("Check Updates", new System.EventHandler(Tray_CheckUpdates)));
            m_menu.MenuItems.Add(1, new MenuItem("Show", new System.EventHandler(Show_Click)));
            m_menu.MenuItems.Add(2, new MenuItem("Hide", new System.EventHandler(Hide_Click)));
            m_menu.MenuItems.Add(3, new MenuItem("Exit", new System.EventHandler(Exit_Click)));
            notifyIcon1.ContextMenu = m_menu;
        }

        private void Tray_CheckUpdates(object sender, EventArgs e)
        {
            if (UpdateMooegeButton.Enabled == true)
            {
                UpdateMooege();
            }
            else
                if (File.Exists(Program.programPath + "\\Tools\\" + "madcow.ini"))
                {
                    IConfigSource source = new IniConfigSource(Program.programPath + @"\Tools\madcow.ini");
                    String Src = source.Configs["Balloons"].Get("ShowBalloons");

                    if (Src.Contains("1"))
                    {
                        notifyIcon1.ShowBalloonTip(1000, "MadCow", "You must select and validate a repository first!", ToolTipIcon.Info);
                    }
                }
        }
        private void Form1_Resize(object sender, EventArgs e)
        {
            IConfigSource source = new IniConfigSource(Program.programPath + @"\Tools\madcow.ini");
            String Balloons = source.Configs["Balloons"].Get("ShowBalloons");
            String TrayIcon = source.Configs["Tray"].Get("Enabled");

            if (FormWindowState.Minimized == WindowState)
            {
                if (TrayIcon.Contains("1"))
                {
                    Hide();
                    if (File.Exists(Program.programPath + "\\Tools\\" + "madcow.ini"))
                    {
                        if (Balloons.Contains("1"))
                        {
                            if (notifyCount < 1) //This is to avoid displaying this Balloon everytime the user minimize, it will only show first time.
                            {
                                notifyIcon1.ShowBalloonTip(1000, "MadCow", "MadCow will continue running minimized.", ToolTipIcon.Info);
                                notifyCount++;
                            }
                        }
                    }
                }
            }
        }
        private void notifyIcon1_DoubleClick(object sender, EventArgs e)
        {
            Show();
            WindowState = FormWindowState.Normal;
        }
        protected void Exit_Click(Object sender, System.EventArgs e)
        {
            Close();
        }
        protected void Hide_Click(Object sender, System.EventArgs e)
        {
            Hide();
            if (File.Exists(Program.programPath + "\\Tools\\" + "madcow.ini"))
            {
                IConfigSource source = new IniConfigSource(Program.programPath + @"\Tools\madcow.ini");
                String Src = source.Configs["Balloons"].Get("ShowBalloons");

                if (Src.Contains("1"))
                {
                    if (notifyCount < 1) //This is to avoid displaying this Balloon everytime the user minimize, it will only show first time.
                    {
                        notifyIcon1.ShowBalloonTip(1000, "MadCow", "MadCow will continue running minimized.", ToolTipIcon.Info);
                        notifyCount++;
                    }
                }
            }
        }
        protected void Show_Click(Object sender, System.EventArgs e)
        {
            Show();
            WindowState = FormWindowState.Normal;
        }
        ////////////////////////////////////////////////////////////////////////////////////////
        // Branching:
        // BranchComboBox Selected Index Change. We use this to update the revision ID that we need to properly find the folder and compile.
        //////////////////////////////////////////////////////////////////////////////////////////
        private void BranchComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            BranchComboBox.Invoke(new Action(() => { selectedBranch = BranchComboBox.SelectedItem.ToString(); }));
            WebClient client = new WebClient();
            client.DownloadStringCompleted += new DownloadStringCompletedEventHandler(BranchParse);
            Uri uri = new Uri(comboBox1.Text + "/commits/" + selectedBranch + ".atom");
            client.DownloadStringAsync(uri);
        }

        private void BranchParse(object sender, DownloadStringCompletedEventArgs e)
        {
            String result = e.Result.ToString();
            Int32 pos2 = result.IndexOf("Commit/");
            String revision = result.Substring(pos2 + 7, 7);
            ParseRevision.lastRevision = result.Substring(pos2 + 7, 7);
        }

        ////////////////////////////////////////////////////////////////////////////////////////
        // Shortcut Disabler
        ////////////////////////////////////////////////////////////////////////////////////////
        private void button1_Click(object sender, EventArgs e)
        {
            if (File.Exists(Program.programPath + "\\Tools\\" + "madcow.ini"))
            {
                try
                {
                    IConfigSource source = new IniConfigSource(Program.programPath + @"\Tools\madcow.ini");
                    String Src = source.Configs["ShortCut"].Get("Shortcut");

                    if (Src.Contains("1"))
                    {
                        source.Configs["ShortCut"].Set("Shortcut", 0);
                        source.Save();
                        label9.ResetText();
                        label9.Text = "Disabled";
                        label9.ForeColor = Color.DimGray;
                    }
                    else
                    {
                        source.Configs["ShortCut"].Set("Shortcut", 1);
                        source.Save();
                        label9.ResetText();
                        label9.Text = "Enabled";
                        label9.ForeColor = Color.SeaGreen;
                    }
                }
                catch
                {
                    Console.WriteLine("[Error] At ShortCut Disabler.");
                }
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////////
        // BalloonTips Disabler
        ////////////////////////////////////////////////////////////////////////////////////////
        private void button2_Click(object sender, EventArgs e)
        {
            if (File.Exists(Program.programPath + "\\Tools\\" + "madcow.ini"))
            {
                try
                {
                    IConfigSource source = new IniConfigSource(Program.programPath + @"\Tools\madcow.ini");
                    String Src = source.Configs["Balloons"].Get("ShowBalloons");

                    if (Src.Contains("1"))
                    {
                        source.Configs["Balloons"].Set("ShowBalloons", 0);
                        source.Save();
                        label21.ResetText();
                        label21.Text = "Disabled";
                        label21.ForeColor = Color.DimGray;
                        chain.Visible = false;
                    }
                    else
                    {
                        source.Configs["Balloons"].Set("ShowBalloons", 1);
                        source.Save();
                        label21.ResetText();
                        label21.Text = "Enabled";
                        label21.ForeColor = Color.SeaGreen;
                        //Tray Icon (We need Tray Icon Enabled)
                        source.Configs["Tray"].Set("Enabled", 1);
                        source.Save();
                        label27.ResetText();
                        label27.Text = "Enabled";
                        label27.ForeColor = Color.SeaGreen;
                        chain.Visible = true;
                    }
                }
                catch
                {
                    Console.WriteLine("[Error] At ShowBalloons Disabler.");
                }
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////////
        // Remember LastRepository Disabler
        ////////////////////////////////////////////////////////////////////////////////////////
        private void button3_Click(object sender, EventArgs e)
        {
            if (File.Exists(Program.programPath + "\\Tools\\" + "madcow.ini"))
            {
                try
                {
                    IConfigSource source = new IniConfigSource(Program.programPath + @"\Tools\madcow.ini");
                    String Src = source.Configs["LastPlay"].Get("Enabled");

                    if (Src.Contains("1"))
                    {
                        source.Configs["LastPlay"].Set("Enabled", 0);
                        source.Save();
                        label23.ResetText();
                        label23.Text = "Disabled";
                        label23.ForeColor = Color.DimGray;
                        label25.Visible = false;
                        chain.Visible = false;
                    }
                    else
                    {
                        source.Configs["LastPlay"].Set("Enabled", 1);
                        source.Save();
                        label23.ResetText();
                        label23.Text = "Enabled";
                        label23.ForeColor = Color.SeaGreen;
                        label25.Visible = true;
                        chain.Visible = false;
                    }
                }
                catch
                {
                    Console.WriteLine("[Error] At LastRepository Disabler.");
                }
            }
        }

        ////////////////////////////////////////////////////////////////////////////////////////
        // Tray Icon Disabler
        ////////////////////////////////////////////////////////////////////////////////////////
        private void button4_Click(object sender, EventArgs e)
        {
            if (File.Exists(Program.programPath + "\\Tools\\" + "madcow.ini"))
            {
                try
                {
                    IConfigSource source = new IniConfigSource(Program.programPath + @"\Tools\madcow.ini");
                    String Src = source.Configs["Tray"].Get("Enabled");

                    if (Src.Contains("1"))
                    {
                        //Tray Icon Functionality.
                        source.Configs["Tray"].Set("Enabled", 0);
                        source.Save();
                        label27.ResetText();
                        label27.Text = "Disabled";
                        label27.ForeColor = Color.DimGray;
                        //Balloons (We dont want balloons if we dont want tray icon)
                        source.Configs["Balloons"].Set("ShowBalloons", 0);
                        source.Save();
                        label21.ResetText();
                        label21.Text = "Disabled";
                        label21.ForeColor = Color.DimGray;
                        chain.Visible = true;

                    }
                    else
                    {
                        source.Configs["Tray"].Set("Enabled", 1);
                        source.Save();
                        label27.ResetText();
                        label27.Text = "Enabled";
                        label27.ForeColor = Color.SeaGreen;
                        chain.Visible = false;
                    }
                }
                catch
                {
                    Console.WriteLine("[Error] At LastRepository Disabler.");
                }
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            Process firstProc = new Process();
            firstProc.StartInfo.FileName = @"MadCowUpdater\MadCowUpdater.exe";
            firstProc.Start();
        }

        private void BrowseMPQPathButton_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                var source = new IniConfigSource(Program.programPath + @"\Tools\madcow.ini");
                source.Configs["DiabloPath"].Set("MPQDest", folderBrowserDialog1.SelectedPath);
                source.Save();
                MPQDestTextBox.Text = folderBrowserDialog1.SelectedPath;
            }
        }

        private void SettingsCheckedListBox_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            SaveSettings();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveSettings();
        }

        private void SaveSettings()
        {
            if (!File.Exists(Program.programPath + @"\Tools\madcow.ini")) return;
            var source = new IniConfigSource(Program.programPath + @"\Tools\madcow.ini");

            source.Configs["Mooege"].Set("FileLogging", SettingsCheckedListBox.GetItemChecked(0).ToString(CultureInfo.InvariantCulture));
            source.Configs["Mooege"].Set("PacketLogging", SettingsCheckedListBox.GetItemChecked(1).ToString(CultureInfo.InvariantCulture));
            source.Configs["DiabloPath"].Set("MPQDest", MPQDestTextBox.Text);
            source.Save();
        }
    }
}