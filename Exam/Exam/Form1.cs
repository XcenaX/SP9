using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace Exam
{
    public partial class Form1 : Form
    {
        Mutex mutex;
        List<string> rubbishWords = new List<string>();
        List<FileInfo> filesNamesWithRubbishWords = new List<FileInfo>();
        List<int> countChanges = new List<int>();        

        private delegate void Action(int value);
        private Action ChangeUI;

        private delegate void Enabled(bool value);        
             
        private static int proccessCount = 0;

        private bool isButtonPressed = false;

        private string currentLabelText;

        public Form1()
        {
            InitializeComponent();

            currentLabelText = "";

            Thread thread = new Thread(SetProgressBarMax)
            {
                IsBackground = true
            };
            thread.Start();

            ChangeUI = new Action(delegate (int value) {
                progressBar1.Value = value;
            });

            ThreadPool.SetMaxThreads(200, 200);
            ThreadPool.SetMinThreads(10, 10);

            bool isCreated;
            mutex = new Mutex(true, "ServerOnceMutex", out isCreated);

            if (!isCreated)
            {
                MessageBox.Show("Эта программа уже запущена!", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Stop);
                System.Environment.Exit(1);
            }
            
        }

        private void SetProgressBarMax()
        {           
            var drives = DriveInfo.GetDrives();
            DirectoryInfo directoryInfo;            

            foreach (var drive in drives)
            {
                try
                {
                    directoryInfo = new DirectoryInfo(drive.Name);                    
                    ThreadPool.QueueUserWorkItem(StartCount, directoryInfo);                    
                }
                catch (Exception) { }
            }            
        }

        private void StartCount(object state)
        {            
            var info = state as DirectoryInfo;
            WalkDirectoryTree(info, true);            
        }

        void WalkDirectoryTree(DirectoryInfo root, bool justForCount)
        {            
            System.IO.FileInfo[] files = null;
            System.IO.DirectoryInfo[] subDirs = null;
            try
            {
                files = root.GetFiles("*.txt");
                if (justForCount)
                {

                    progressBar1.Invoke(new Action((x) => { progressBar1.Maximum += x; }), files.Length);

                    int maximum = 0;
                    progressBar1.Invoke(new Action((x) => { maximum = progressBar1.Maximum; }), maximum);

                    label2.Invoke(new Action((x) => { label2.Text = "Прогрaмма нашла файлов для ProgressBar : " + x; }), maximum);
                }
                else
                {
                    foreach (var file in files)
                    {                        
                        ThreadPool.QueueUserWorkItem(SearchWords, file);
                    }
                }
            }
            catch (Exception e)
            {
                
            }
            if (files != null)
            {                
                subDirs = root.GetDirectories();
                foreach (DirectoryInfo dirInfo in subDirs)
                {                    
                    if(!justForCount) ThreadPool.QueueUserWorkItem(Start, dirInfo);
                    else ThreadPool.QueueUserWorkItem(StartCount, dirInfo);
                }
                return;
            }            
        }

        private void button3_Click(object sender, EventArgs e)
        {
            button3.Enabled = false;
            button6.Enabled = false;
            proccessCount = 0;

            Thread thread = new Thread(Start)
            {
                IsBackground = true
            };
            thread.Start(null);
        }

        public string Find(string findSymbol, string needSymbol, string[] array)
        {
            int count = 0;
            string result = "";
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] == findSymbol)
                {
                    array[i] = needSymbol;
                    count++;
                    result += array[i];
                }
            }
            countChanges.Add(count);            
            return result;
        }

        private void Start(object state)
        {
            if (state == null)
            {
                var drives = DriveInfo.GetDrives();
                DirectoryInfo directoryInfo;

                foreach (var drive in drives)
                {
                    try
                    {
                        directoryInfo = new DirectoryInfo(drive.Name);
                        foreach (var folder in directoryInfo.GetDirectories())
                        {
                            ThreadPool.QueueUserWorkItem(StartSearch, folder);
                        }
                    }
                    catch (Exception) { }
                }
            }
            else
            {
                ThreadPool.QueueUserWorkItem(StartSearch, state as DirectoryInfo);
            }
        }

        private void StartSearch(object state)
        {
            var directoryInfo = state as DirectoryInfo;            
            WalkDirectoryTree(directoryInfo,false);                                    
        }

        private void SearchWords(object state)
        {
            var file = state as FileInfo;

            string text = "";
            try
            {
                using (StreamReader reader = new StreamReader(file.FullName))
                {
                    text = reader.ReadToEnd();
                }
            }
            catch (Exception) { return; }


            bool isContain = false;

            foreach (var word in rubbishWords)
            {
                if (text.Contains(word))
                {
                    isContain = true;
                    string stars = "";
                    for (int i = 0; i < word.Length; i++) stars += '*';
                    text = Find(word, stars, text.Split(' ', ',', '!', '.', '/'));
                }
            }
            if (isContain)
            {
                try
                {
                    File.Create(textBox4.Text + @"\" + file.Name);
                    filesNamesWithRubbishWords.Add(new FileInfo(textBox4.Text + file.Name));
                    using (var writer = new StreamWriter(textBox4.Text + @"\" + file.Name))
                    {
                        writer.WriteLine(text);
                    }
                }
                catch (Exception) { }
            }
            proccessCount++;
            label2.Invoke(new Action((x) => { label2.Text = "Программа проверила файлов : " + proccessCount; }), proccessCount);
            progressBar1.Invoke(ChangeUI, proccessCount);            
        }

        private void WriteStatistics()
        {            
            using (var writer = new StreamWriter(textBox3.Text))
            {
                writer.WriteLine("Имя\tРазмер\tПуть\tКол-во замен");
                for(int i = 0; i < filesNamesWithRubbishWords.Count; i++)
                {
                    writer.WriteLine(filesNamesWithRubbishWords[i].Name + "\t" + filesNamesWithRubbishWords[i].Length + "\t" + filesNamesWithRubbishWords[i].FullName + "\t" + countChanges[i]);
                }                                                
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                textBox1.Text = dialog.SelectedPath;
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if(textBox1.Text == "")
            {
                MessageBox.Show("Вы ничего не ввели!", "Внимание", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string[] words;

            if (File.Exists(textBox1.Text))
            {
                using(StreamReader stream = new StreamReader(textBox1.Text))
                {
                    words = stream.ReadToEnd().Split(' ');
                }                
            }
            else
            {
                words = textBox1.Text.Split(' ');                
            }
            foreach (var word in words)
            {
                rubbishWords.Add(word);                
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dialog = new FolderBrowserDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                textBox4.Text = dialog.SelectedPath;
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            if (dialog.ShowDialog() == DialogResult.OK)
            {
                textBox3.Text = dialog.FileName;
            }
        }

        private void IsButtonEnabled(object sender, System.Timers.ElapsedEventArgs e)
        {
            if(currentLabelText == label2.Text && !isButtonPressed)
            {                
                button3.Invoke(new Enabled((x) => { button3.Enabled = x; }), true);
                proccessCount = 0;
                isButtonPressed = true;                
            }
            else if(isButtonPressed && currentLabelText == label2.Text)
            {
                button3.Invoke(new Enabled((x) => { button3.Enabled = x; }), true);
                button6.Invoke(new Enabled((x) => { button6.Enabled = x; }), true);
                proccessCount = 0;
            }
            currentLabelText = label2.Text;
        }

        private void button6_Click(object sender, EventArgs e)
        {
            WriteStatistics();
        }

        private void timer2_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {

        }
    }
}
