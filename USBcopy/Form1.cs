using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using System.Threading;
using System.Management.Instrumentation;
using System.Management;

using UsbEject;
using UsbEject.Library;


namespace USBcopy
{

  
    public partial class Form1 : Form
    {
        private bool _loading = false;
        public int sizeDiv = 1024*1024;
        public long sizeNow = 0;
        public int formatNow = 0;
        public List<Task> fTasks = new List<Task>();
        public Form1()
        {
            InitializeComponent();
            loadDrives();
            
        }

        #region Buttons

        // Relaod Button
        private void button1_Click(object sender, EventArgs e)
        {
            loadDrives();
        }
        // Copy Button
        private void button2_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog diag = new FolderBrowserDialog();
            if (diag.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                if (ask("Really copy " + diag.SelectedPath + " to selected Device(s)?\n", "Copy data?"))
                    doWork(0, diag.SelectedPath);
        }
        // Format Button
        private void button3_Click(object sender, EventArgs e)
        {
            if (ask("Really format selected Device?\nYou will lost ALL data!", "Format?"))
                doWork(1);
        }
        // SelAll Button
        private void button4_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < checkedListBox1.Items.Count; i++)
                checkedListBox1.SetItemChecked(i, true);
        }
        // SelNone Button
        private void button5_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < checkedListBox1.Items.Count; i++)
                checkedListBox1.SetItemChecked(i, false);
        }
        // F & C Button
        private void button6_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog diag = new FolderBrowserDialog();
            if (diag.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                if (ask("Really format selected device(s)? ALL data will be lost.\nAfter that " + diag.SelectedPath + " will be copied to the selected Device(s)?", "Format device(s) and copy data?"))
                    doWork(2, diag.SelectedPath);
        }
        // Eject Button
        private void button7_Click(object sender, EventArgs e)
        {
            toggle(false);
            ejectDevices();
            toggle(true);
        }

        #endregion

        private void doWork(int mode, string path = null)
        {
            if (path == null && (mode == 0 || mode == 2)) return;
            toggle(false);
            DateTime timeStamp1 = (DateTime.Now);
            List<Task> tasks = new List<Task>();
            long size = dirSize(path);
            sizeDiv = 1024 * (((size / 1024) > 100663296) ? 2 : 1);
            foreach (int i in checkedListBox1.CheckedIndices)
                switch (mode)
                {
                    case 0:
                        tasks.Add(Task.Factory.StartNew(() => DirectoryCopy(path, i)));
                        toolStripProgressBar1.Maximum = toolStripProgressBar1.Maximum + (int)(size / sizeDiv);
                        break;
                    case 1:
                    case 2:
                        tasks.Add(Task.Factory.StartNew(() => {
                            string driveLetter = checkedListBox1.Items[i].ToString().Split('\t')[0].Substring(0, 2).Trim();
                            updateList(i, "Format...");
                            if (driveLetter.Length != 2 || driveLetter[1] != ':' || !char.IsLetter(driveLetter[0]))
                                return;
                            ManagementObjectSearcher searcher = new ManagementObjectSearcher(@"select * from Win32_Volume WHERE DriveLetter = '" + driveLetter + "'");
                            foreach (ManagementObject vi in searcher.Get())
                                vi.InvokeMethod("Format", new object[] { "FAT32", checkBox2.Checked, 8192, "", false });
                            toolStripProgressBar2.Value = ++formatNow;
                            updateList(i, "Format Done!");
                        }));
                        toolStripProgressBar2.Maximum = toolStripProgressBar2.Maximum + 1;
                        break;
                    default: return;
                }
            if (mode == 2)
            {
                Task.WaitAll(tasks.ToArray());
                tasks.Clear();
                Task.WaitAll(fTasks.ToArray());
                foreach (int i in checkedListBox1.CheckedIndices)
                {
                    tasks.Add(Task.Factory.StartNew(() => DirectoryCopy(path, i)));
                    toolStripProgressBar1.Maximum = toolStripProgressBar1.Maximum + (int)(size / sizeDiv);
                }
            }
            Task.WaitAll(tasks.ToArray());
            label1.Visible = false;
            int sek = (int)((DateTime.Now - timeStamp1).TotalSeconds);
            MessageBox.Show("It took " + (sek / 60).ToString() + ":" + (sek % 60).ToString() + " minutes to format and/or copy.", "Finished", MessageBoxButtons.OK, MessageBoxIcon.Information);
            if (checkBox4.Checked)
                ejectDevices();
            else
                loadDrives();
            toggle(true);
        }

        private void loadDrives()
        {
            checkedListBox1.Items.Clear();
            var drives = DriveInfo.GetDrives().Where(drive => drive.IsReady && drive.DriveType == DriveType.Removable);
            foreach (DriveInfo info in drives)
                checkedListBox1.Items.Add(info.RootDirectory + "\t" + Math.Round((double)info.TotalFreeSpace / 1024 / 1024 / 1024, 2) + "GB/" + Math.Round((double)info.TotalSize / 1024 / 1024 / 1024, 2) + "GB\t" + info.DriveFormat);
        }

        private void DirectoryCopy(string sourceDirName, int i, string destDirName = null)
        {
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);
            DirectoryInfo[] dirs = dir.GetDirectories();
            if (destDirName == null)
            {
                destDirName = checkedListBox1.Items[i].ToString().Split('\t')[0].Trim();
                updateList(i, "Copy...");
            }
            if (!dir.Exists)
                throw new DirectoryNotFoundException("Source directory does not exist or could not be found: " + sourceDirName);
            if (!Directory.Exists(destDirName))
                Directory.CreateDirectory(destDirName);
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
                try
                {
                    file.CopyTo(Path.Combine(destDirName, file.Name), checkBox3.Checked);
                    Task.Factory.StartNew(() =>
                    {
                        sizeNow += file.Length;
                        toolStripProgressBar1.Value = (int)(sizeNow / sizeDiv);
                    }, CancellationToken.None, TaskCreationOptions.None, PriorityScheduler.Highest);
                }
                catch (FileLoadException ex)
                {
                    Task.Factory.StartNew(() =>
                    {
                        sizeNow += file.Length;
                        toolStripProgressBar1.Value = (int)(sizeNow / sizeDiv);
                    }, CancellationToken.None, TaskCreationOptions.None, PriorityScheduler.Highest);
                }
            if (checkBox1.Checked)
                foreach (DirectoryInfo subdir in dirs)
                     DirectoryCopy(subdir.FullName, i, Path.Combine(destDirName, subdir.Name));
            if (destDirName.Length < 4)
                updateList(i, "Copy Done!");
        }

        private void ejectDevices()
        {
            VolumeDeviceClass volumeDeviceClass = new VolumeDeviceClass();
            foreach (Volume device in volumeDeviceClass.Devices)
            {
                if (!device.IsUsb || (device.LogicalDrive == null) || (device.LogicalDrive.Length == 0))
                    continue;
                foreach (var i in checkedListBox1.CheckedItems)
                    if (i.ToString().Contains(device.LogicalDrive))
                        device.Eject(true);
            }
        }

        private void toggle(bool val)
        {
            button1.Enabled = val;
            button2.Enabled = val;
            button3.Enabled = val;
            button4.Enabled = val;
            button5.Enabled = val;
            button6.Enabled = val;
            button7.Enabled = val;
            checkedListBox1.Enabled = val;
            checkBox1.Enabled = val;
            checkBox2.Enabled = val;
            checkBox3.Enabled = val;
            checkBox4.Enabled = val;
            label1.Visible = !val;
            _loading = !val;
            sizeNow = 0;
            formatNow = 0;
            toolStripProgressBar1.Maximum = 0;
            toolStripProgressBar1.Value = 0;
            toolStripProgressBar2.Maximum = 0;
            toolStripProgressBar2.Value = 0;
            fTasks.Clear();
        }

        private bool ask(string text, string title)
        {
            return MessageBox.Show(text, title, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.OK ? true : false;
        }

        private void updateList(int i, string s)
        {
           Task.Factory.StartNew(() =>
            {
                checkedListBox1.Items[i] = checkedListBox1.Items[i].ToString().Split('\t')[0] + "\t" + s;
            }, CancellationToken.None, TaskCreationOptions.None, PriorityScheduler.Highest);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0219)  //WM_DEVICECHANGE
                if (!_loading)
                    loadDrives();
            base.WndProc(ref m);
        }

        private long dirSize(string dirName)
        {
            if (dirName==null) return 0;
            long size = 0;
            DirectoryInfo dir = new DirectoryInfo(dirName);
            if (!dir.Exists)
                throw new DirectoryNotFoundException("Source directory does not exist or could not be found: " + dirName);
            DirectoryInfo[] dirs = dir.GetDirectories();
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
                size += file.Length;
            if (checkBox1.Checked)
                foreach (DirectoryInfo subdir in dirs)
                    size += dirSize(subdir.FullName);
            return size;
        }

     

        private void checkedListBox1_DrawItem(object sender, DrawItemEventArgs e)
        {
            try
            {
                Size checkSize = CheckBoxRenderer.GetGlyphSize(e.Graphics, System.Windows.Forms.VisualStyles.CheckBoxState.MixedNormal);
                int dx = (e.Bounds.Height - checkSize.Width) / 2;
                e.DrawBackground();
                if (checkedListBox1.Items[e.Index].ToString().Contains("Done"))
                {
                    Graphics g = e.Graphics;
                    g.FillRectangle(new SolidBrush(Color.Green), e.Bounds.X + 50 + checkSize.Width, e.Bounds.Y + 1, 100 - checkSize.Width, e.Bounds.Height - 2);
                }
                else if (checkedListBox1.Items[e.Index].ToString().Contains("Format"))
                {
                    Graphics g = e.Graphics;
                    g.FillRectangle(new SolidBrush(Color.Red), e.Bounds.X + 50 + checkSize.Width, e.Bounds.Y + 1, 100 - checkSize.Width, e.Bounds.Height - 2);
                }
                else if (checkedListBox1.Items[e.Index].ToString().Contains("Copy"))
                {
                    Graphics g = e.Graphics;
                    g.FillRectangle(new SolidBrush(Color.Yellow), e.Bounds.X + 50 + checkSize.Width, e.Bounds.Y + 1, 100 - checkSize.Width, e.Bounds.Height - 2);
                }
                else
                {
                    Graphics g = e.Graphics;
                    g.FillRectangle(new SolidBrush(BackColor), e.Bounds);
                }

                bool isChecked = checkedListBox1.GetItemChecked(e.Index);//For some reason e.State doesn't work so we have to do this instead.
                CheckBoxRenderer.DrawCheckBox(e.Graphics, new Point(dx, e.Bounds.Top + dx), isChecked ? System.Windows.Forms.VisualStyles.CheckBoxState.CheckedNormal : System.Windows.Forms.VisualStyles.CheckBoxState.UncheckedNormal);
                using (StringFormat sf = new StringFormat { LineAlignment = StringAlignment.Center })
                {
                    using (Brush brush = new SolidBrush(ForeColor))
                    {
                        float[] tabs = { 50, 130 };
                        sf.SetTabStops(0, tabs);
                        e.Graphics.DrawString(checkedListBox1.Items[e.Index].ToString(), Font, brush, new Rectangle(e.Bounds.Height, e.Bounds.Top, e.Bounds.Width - e.Bounds.Height, e.Bounds.Height), sf);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }


  

}

