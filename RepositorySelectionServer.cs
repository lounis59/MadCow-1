﻿// Copyright (C) 2011 Iker Ruiz Arnauda (Wesko)
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
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.IO;
using Nini.Config;
using System.Diagnostics;
using System.Threading;

namespace MadCow
{
    public partial class RepositorySelectionServer : Form
    {
        public RepositorySelectionServer()
        {
            InitializeComponent();
            AddAvailableRepositories();
            LaunchServerButton.Enabled = false;
        }

        public void AddAvailableRepositories() //Adds available repos to the list.
        {
            string[] FoldersArray = Directory.GetDirectories(Program.programPath + @"\" + @"Repositories\");
            foreach (string name in FoldersArray)
            {
                DirectoryInfo info = new DirectoryInfo(name);
                this.checkedListBox1.Items.Add(info.Name);
            }
        }

        private void checkedListBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            LaunchServerButton.Enabled = true;
            int selected = checkedListBox1.SelectedIndex;
            if (selected != -1)
            {   //We set the Mooege INI path according to user repo selection for further use when writing new config values.               
                Compile.mooegeINI =  Program.programPath + @"\" + @"Repositories\" + checkedListBox1.Items[selected].ToString() + @"\src\Mooege\bin\Debug\config.ini";
                Compile.currentMooegeExePath = Program.programPath + @"\" + @"Repositories\" + checkedListBox1.Items[selected].ToString() + @"\src\Mooege\bin\Debug\Mooege.exe";
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (ProcessFinder.FindProcess("Mooege") == true)
            {
                var answer = MessageBox.Show("Mooege is already Running. Do you want to restart Mooege?", "Attention",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer == DialogResult.Yes)
                {
                    ProcessFinder.KillProcess("Mooege");
                    Compile.ModifyMooegeINI();
                    Process proc0 = new Process();
                    proc0.StartInfo = new ProcessStartInfo(Compile.currentMooegeExePath);
                    proc0.Start();
                    this.Close();
                }
                else
                {
                    //Do Nothing
                }
            }
            else
            {
                ProcessFinder.KillProcess("Mooege");
                Compile.ModifyMooegeINI();
                Process proc0 = new Process();
                proc0.StartInfo = new ProcessStartInfo(Compile.currentMooegeExePath);
                proc0.Start();
                Thread.Sleep(2000);
                if (ErrorFinder.SearchLogs("Fatal") == true)
                {
                    Console.WriteLine("Closing Mooege due Fatal Exception");
                    ProcessFinder.KillProcess("Mooege");
                }
                this.Close();
            }
        }

        private void checkedListBox1_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            if (checkedListBox1.CheckedItems.Count == 1)
            {
                Boolean isCheckedItemBeingUnchecked = (e.CurrentValue == CheckState.Checked);
                if (isCheckedItemBeingUnchecked)
                {
                    e.NewValue = CheckState.Checked;
                }
                else
                {
                    Int32 checkedItemIndex = checkedListBox1.CheckedIndices[0];
                    checkedListBox1.ItemCheck -= checkedListBox1_ItemCheck;
                    checkedListBox1.SetItemChecked(checkedItemIndex, false);
                    checkedListBox1.ItemCheck += checkedListBox1_ItemCheck;
                }

                return;
            }
        }
    }
}
