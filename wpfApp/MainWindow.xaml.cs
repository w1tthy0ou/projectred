using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Threading;
using System.IO.MemoryMappedFiles;
using System.IO;
using System.Diagnostics;
using Microsoft.Win32;
using OxyPlot;


namespace wpfApp
{


    public class VMDeamonProcess : INotifyPropertyChanged
    {

        const string PROC_FILE = @"C:\Users\koki\source\repos\kokivanov\IMFsendHelpPls\deamon\bin\Debug\net6.0\deamon.exe"; // Bind to textbox like selecting file


        private Process redMachine;

        private MemoryMappedFile _foundGate; // MMF for amount of words found
        private MemoryMappedViewAccessor _foundAccessor; // 

        private MemoryMappedFile _progressGate; // Percent of data that have been computed
        private MemoryMappedViewAccessor _progressAccessor;

        private MemoryMappedFile _spentTimeGate; // Percent of data that have been computed
        private MemoryMappedViewAccessor _spentTimeAccessor;

        private Mutex _mutex;

        public VMredMachineProcess(int pn, Int64 sp, Int64 ep)
        {   
            _processName = pn;

            _foundGate = MemoryMappedFile.CreateNew(pn + "FoundMMF", sizeof(Int64));
            _foundAccessor = _foundGate.CreateViewAccessor();
            _progressGate = MemoryMappedFile.CreateNew(pn + "ProgressMMF", sizeof(int));
            _progressAccessor = _progressGate.CreateViewAccessor();
            _spentTimeGate = MemoryMappedFile.CreateNew(pn + "SepntTimeMMF", sizeof(int));
            _spentTimeAccessor = _spentTimeGate.CreateViewAccessor();
            _mutex = new Mutex(true, pn + "Mutex");
            _mutex.ReleaseMutex();

            GC.KeepAlive(_mutex);

            redMachine = new Process();

            StartPoint = sp;
            EndPoint = ep;

            redMachine.StartInfo.FileName = PROC_FILE;
            redMachine.StartInfo.Arguments = $"{pn} {sp} {ep}";
            redMachine.Start();
        }

        ~VMredMachineProcess() {
            _foundAccessor.Dispose();
            _foundGate.Dispose();
            _mutex.ReleaseMutex();
            _mutex?.Dispose();
            redMachine?.Kill();
            redMachine?.Dispose();
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private Int64 _startPoint;
        public Int64 StartPoint 
        {
            get { return _startPoint; }
            set { _startPoint = value; OnPropertyChanged("StartPoint"); }
        }

        private Int64 _endPoint;
        public Int64 EndPoint
        {
            get { return _endPoint; }
            set { _endPoint = value; OnPropertyChanged("StartPoint"); }
        }


        private int _progress;
        public int Progress
        {
            get
            {
                return _progress;
            }

            set 
            {
                this._progress = value;
                OnPropertyChanged("Progress");
            }
        }

        public VMredMachineProcess Update()
        {
            byte[] bytes1 = new byte[sizeof(int)];
            byte[] bytes2 = new byte[sizeof(Int64)];
            byte[] bytes3 = new byte[sizeof(Int64)];
            _mutex.WaitOne();
            _progressAccessor.ReadArray(0, bytes1, 0, bytes1.Count());
            _foundAccessor.ReadArray(0, bytes2, 0, bytes2.Length);
            _foundAccessor.ReadArray(0, bytes3, 0, bytes3.Length);
            _mutex.ReleaseMutex();
            int text1 = BitConverter.ToInt32(bytes1, 0);
            Int64 text2 = BitConverter.ToInt64(bytes2, 0);
            Int64 text3 = BitConverter.ToInt64(bytes3, 0);
            this.Progress = text1;
            this.Found = text2;
            this.SpentTime = text3;
            
            IsRunning = !redMachine.HasExited;
            return this;
        }

        private Int64 _spentTime;
        public Int64 SpentTime 
        { 
            get => _spentTime;
            set
            {
                _spentTime = value;
                OnPropertyChanged("SpentTime");
            }
        }

        private int _processName;
        public int ProcessName 
        {
            get { return _processName; }
        }

        private Int64 _found;
        public Int64 Found
        {
            get { return _found; }
            set 
            { 
                _found = value;
                OnPropertyChanged("Found");
            }
        }

        private bool _isRunning = true;
        public bool IsRunning
        {
            get { return _isRunning; }
            set
            {
                this._isRunning = value;
                OnPropertyChanged("isRunning");
            }
        }

        public void OnPropertyChanged([CallerMemberName]string prop = "")
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }


    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    /// 
    

    // !TODO: Bind search button availability to IsPathValid parameter
    // !TODO: Remove debbuging label


    public class VMMain : INotifyPropertyChanged {
        public ObservableCollection<VMDeamonProcess> VMDeamons { get; set; }

        public ObservableCollection<long> TimeSpend { get; set; } // Time spend with each deamon to find word
        public ObservableCollection<Tuple<long, int>> FoundForPeriod { get; set; } // Difference between words found in each time section
        
        public Dictionary<long, int> DiffFound { get; set; }

        private bool _isPathValid = false; //!TODO Link to Start button enabled
        public bool IsPathValid { 
            get { return _isPathValid; } 
            set { _isPathValid = value; OnPropertyChanged("IsPathValid"); }
        }

        public void UpdateOverall() { // Updates overall progress
            _overallProgress = 0;
            for (int i = 0; i <= VMredMachines.Count(); i++)
            {
                VMredMachines[i].Update();
                _overallProgress += VMredMachines[i].Progress / VMredMachines.Count();
            }
            OverallProgress = _overallProgress;
        }

        private int _overallProgress = 0;
        public int OverallProgress
        { 
            get { return _overallProgress; }
            set { _overallProgress = value; OnPropertyChanged("OverallProgress"); }
        }

        public VMMain() 
        {
            
            FilePathTextBox = "";
            FilePathTextBoxIsUnlocked = false;
        }

        private string _searchedWord;
        public string SearchedWord 
        {
            get { return _searchedWord; }
            set { _searchedWord = value; OnPropertyChanged("SearchedWord"); }
        }

        private string _dbginfo;
        public string DBGInfo 
        {
            get { return _dbginfo; }
            set { _dbginfo = value; OnPropertyChanged("DBGInfo"); }
        }

        private string _filePathTextBox;
        public string FilePathTextBox
        {
            get => _filePathTextBox;
            set { _filePathTextBox = value; OnPropertyChanged("FilePathTextBox"); }
        }

        private bool _filePathTextBoxIsUnlocked;
        public bool FilePathTextBoxIsUnlocked
        {
            get => _filePathTextBoxIsUnlocked;
            set { _filePathTextBoxIsUnlocked = value; OnPropertyChanged("FilePathTextBoxIsUnlocked"); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public void OnPropertyChanged([CallerMemberName] string prop = "")
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }

    }

    public partial class MainWindow : Window
    {
        const int AMOUNT_OF_ROUTINES = 4; // Bind to textbox like selecting file

        private MemoryMappedFile _filePathMMF;
        private MemoryMappedViewAccessor _filePathMMFaccessor;

        private MemoryMappedFile _serchedwordMMF;
        private MemoryMappedViewAccessor _serchedwordMMFaccessor;

        public VMMain MainVM { get; set; }

        private async Task UpdateThread()
        {
            while (!isDone()) 
            { 
                await Task.Delay(500);
            }

            WhenDone();
        }

        ObservableCollection<DataPoint> values;

        private void WhenDone()
        {
            var v = (from c in MainVM.DiffFound
                    orderby c.Key
                    select new DataPoint(c.Key, c.Value));
            foreach (var i in v)
                values.Add(i);
        }

        public MainWindow()
        {
            MainVM = new VMMain();
            values = new ObservableCollection<DataPoint>();
            MainVM.VMDeamons = new ObservableCollection<VMDeamonProcess>();
            MainVM.TimeSpend = new ObservableCollection<long>();
            MainVM.FoundForPeriod = new ObservableCollection<Tuple<long, int>>();
            MainVM.DiffFound = new Dictionary<long, int>();

            DataContext = MainVM;

            InitializeComponent();

        }

        ~MainWindow() {
            _filePathMMF.Dispose();
            _filePathMMFaccessor.Dispose();
            _serchedwordMMF.Dispose();
            _serchedwordMMFaccessor.Dispose();
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            StartSearch();
        }

        private bool isDone() { // Checks is all proccesses finished work
            bool d = true;
            MainVM.OverallProgress = 0;
            for (int i = 0; i < MainVM.VMredMachines.Count(); i++) 
            {
                var oldProgress = MainVM.VMredMachines[i].Progress;
                MainVM.VMredMachines[i].Update();
                MainVM.TimeSpend[i] = MainVM.VMredMachines[i].SpentTime;
                MainVM.FoundForPeriod.Add(new Tuple<long, int>(MainVM.VMredMachines[i].SpentTime, MainVM.VMredMachines[i].Progress - oldProgress));
                MainVM.DiffFound[MainVM.VMredMachines[i].SpentTime] = MainVM.VMredMachines[i].Progress - oldProgress;
                MainVM.OverallProgress += MainVM.VMredMachines[i].Progress / MainVM.VMredMachines.Count();
                d &= !MainVM.VMredMachines[i].IsRunning;
            }
            return d;
        }

        public void ChooseFileButton_Copy_Click(object sender, RoutedEventArgs e)
        {
            MainVM.FilePathTextBoxIsUnlocked = true;
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.InitialDirectory = "c:\\";
            openFileDialog.Filter = "txt files (*.txt)|*.txt";
            openFileDialog.FilterIndex = 2;
            openFileDialog.RestoreDirectory = true;

            if (openFileDialog.ShowDialog() != null)
            {
                if (MainVM.FilePathTextBox != openFileDialog.FileName && (MainVM.FilePathTextBox == "" || MainVM.FilePathTextBox == "c:\\"))
                    MainVM.FilePathTextBox = openFileDialog.FileName;
            }

            validateUserEntry();
        }

        private void validateUserEntry() // Validates file path and unlock Start button
        {
            if (!File.Exists(MainVM.FilePathTextBox))
            {
                MainVM.IsPathValid = false;
                string messageBoxText = "Can't find file in provided path.";
                string caption = "Invalid file path";
                MessageBoxButton button = MessageBoxButton.OK;
                MessageBoxImage icon = MessageBoxImage.Warning;
                MessageBoxResult result;

                result = MessageBox.Show(messageBoxText, caption, button, icon, MessageBoxResult.Yes);
            }
            else 
            {
                MainVM.IsPathValid = true;
            }
            
        }

        private async void StartSearch() { // Called when Start button pressed
            if (!MainVM.IsPathValid || String.IsNullOrWhiteSpace(MainVM.SearchedWord)) // Validates file path
            { 
                MessageBox.Show("Пустое поле не может быть элементом поиска", "Ошибка!", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var buff1 = Encoding.Default.GetBytes(MainVM.FilePathTextBox);
            _filePathMMF = MemoryMappedFile.CreateOrOpen("FilePathMMF", buff1.Length);
            _filePathMMFaccessor = _filePathMMF.CreateViewAccessor();

            var buff2 = Encoding.Default.GetBytes(MainVM.SearchedWord); 
            _serchedwordMMF = MemoryMappedFile.CreateOrOpen("SearchedWordMMF", buff2.Length);
            _serchedwordMMFaccessor = _serchedwordMMF.CreateViewAccessor();

            // GC Zone
            GC.KeepAlive(_filePathMMF);
            GC.KeepAlive(_filePathMMFaccessor);
            GC.KeepAlive(_serchedwordMMF);
            GC.KeepAlive(_serchedwordMMFaccessor);

            // Writes target file path and target word to mmf
            byte[] bytes1 = Encoding.Default.GetBytes(MainVM.FilePathTextBox);
            _filePathMMFaccessor.WriteArray(0, bytes1, 0, bytes1.Count());
            byte[] bytes2 = Encoding.Default.GetBytes(MainVM.SearchedWord);
            _serchedwordMMFaccessor.WriteArray(0, bytes2, 0, bytes2.Count());

            var fc = File.ReadLines(MainVM.FilePathTextBox).Count(); // Get length of target file

            Int64 step = (Int64)Math.Floor((double)(fc / AMOUNT_OF_ROUTINES));
            MainVM.DBGInfo = $"{MainVM.SearchedWord}, {fc}, {AMOUNT_OF_ROUTINES}, {step}";

            // Creates deamons

            if (step == fc)
            {
                MainVM.VMredMachines.Add(new VMDeamonProcess(0, 0, fc));
                await UpdateThread();
                return;
            }

            Int64 sp = 0;
            int Index = 0;
            for (; sp + step < fc;)
            {
                MainVM.TimeSpend.Add(0);
                MainVM.VMredMachines.Add(new VMDeamonProcess(Index, sp, sp += step));
                Index++;
            }

            await UpdateThread();
        }

        public void Terminate() 
        {
            MainVM.VMDeamons.Clear();
            GC.ReRegisterForFinalize(_filePathMMF);
            GC.ReRegisterForFinalize(_filePathMMFaccessor);
            GC.ReRegisterForFinalize(_serchedwordMMF);
            GC.ReRegisterForFinalize(_serchedwordMMFaccessor);
        }

        private void ProgressBarKek_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            foreach (var i in values)
            {
                Trace.WriteLine($"{i.X} - {i.Y}");
            }
        }

        private Line xAxisLine, yAxisLine;
        private double xAxisStart = 0, yAxisStart = 0, interval = 25;
        private Polyline chartPolyline;
   
}





