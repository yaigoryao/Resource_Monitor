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
using System.Windows.Threading;
using SystemPerformance;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.VisualElements;
using LiveChartsCore.SkiaSharpView.WPF;
using SkiaSharp;
using System.ComponentModel;
using System.Collections.ObjectModel;
using LiveChartsCore.Defaults;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Security.Cryptography;
using System.Security.Permissions;

namespace ResourceMonitor
{
    public enum MonitoringParam { CPU = 0, RAM, };

    public class MonitoringUnit
    {
        public string Name { get; set; }
        public int MaxPoints { get; set; } = 20;

        public readonly ObservableCollection<ObservableValue> Values;
        public MonitoringUnit(string name)
        {
            Values = new ObservableCollection<ObservableValue> { };
            Name = name;
        }

        private object Sync = new object();
        public void AddValue(double value)
        {
            lock (Sync)
            {
                Values.Add(new ObservableValue(value));
                if (Values.Count > MaxPoints)
                {
                    Values.RemoveAt(0);
                }
            }
        }
        public void Clear() { lock (Sync) { Values.Clear(); } }
    }
    public class ModelView : ObservableObject
    {
        public Model Model = null;

        public object Sync { get; } = new object();
        public ModelView()
        {
            Series = new ObservableCollection<ISeries>
            {
                new LineSeries<ObservableValue>
                {
                    Values = { },
                    GeometryFill = null,
                    GeometryStroke = null,
                    LineSmoothness = 0,
                    Name = string.Empty,
                }
            };
        }
        public ModelView(Model model) : this()
        {

            Model = model;
            Model.Start();
        }

        public void UpdateParam(MonitoringParam param)
        {
            lock (Sync)
            {
                Model.Stop();
                XAxes[0].Name = Series[0].Name = Model?.Units[param].Name;
                Series[0].Values = Model?.Units[param].Values;
                Model.Start();
            }
        }

        public ObservableCollection<ISeries> Series { get; set; }


        public Axis[] YAxes { get; set; } = new Axis[]
        {
            new Axis
            {
                CrosshairLabelsBackground = SKColors.DarkOrange.AsLvcColor(),
                CrosshairLabelsPaint = new SolidColorPaint(SKColors.DarkRed, 1),
                CrosshairPaint = new SolidColorPaint(SKColors.DarkOrange, 1),
                MaxLimit = 1,
                MinLimit = 0,
                Name  = "Загрузка, %",
                Labeler = value => value.ToString("P"),
            }
        };

        public Axis[] XAxes { get; set; } = new Axis[]
        {
            new Axis
            {
                Labels = new string[] { }
            }
        };
    }

    public class Model
    {
        private const int TIMER_MS_DELAY = 500;
        private object Sync { get; } = new object();

        private DispatcherTimer Timer = null;

        private PerformanceTracker Tracker = null;

        public Dictionary<MonitoringParam, MonitoringUnit> Units { get; } = null;
        public Model()
        {
            Tracker = new PerformanceTracker(10, false);
            Timer = new DispatcherTimer();
            Timer.Tick += new EventHandler(Timer_Tick);
            Timer.Interval = TimeSpan.FromMilliseconds(TIMER_MS_DELAY);
            Units = new Dictionary<MonitoringParam, MonitoringUnit> { { MonitoringParam.CPU, new MonitoringUnit("CPU") },
                                                                      { MonitoringParam.RAM, new MonitoringUnit("RAM") }};
        }

        public void Start()
        {
            if (!Timer.IsEnabled) Timer.Start();
        }

        public void Stop()
        {
            if (Timer.IsEnabled) Timer.Stop();
        }
        public void ClearChart()
        {
            lock (Sync)
            {
                Stop();
                foreach (var unit in Units) unit.Value.Clear();
                Start();
            }
        }
        private void Timer_Tick(object sender, EventArgs e)
        {
            foreach (var unit in Units)
            {
                unit.Value.AddValue(Convert.ToDouble(GetCurrentMonitoringParamValue(unit.Key) / 100));
            }
        }
        public double GetCurrentMonitoringParamValue(MonitoringParam param)
        {
            switch (param)
            {
                case MonitoringParam.CPU: return Tracker.Current_CPU_Usage;
                case MonitoringParam.RAM: return Tracker.Percent_RAM_Used;
                default: return 0;
            }
        }
    }
    public partial class MainWindow : Window
    {
        private bool IsPreviousKeyControl = false;
        private static ModelView ModelView = null;
        private static Model Model = null;
        public MainWindow()
        {
            Model = new Model();
            InitializeComponent();
            ModelView = new ModelView(Model);
            ModelView.UpdateParam(MonitoringParam.CPU);
            DataContext = ModelView;
        }
        private void CPUButton_Click(object sender, RoutedEventArgs e)
        {
            ModelView.UpdateParam(MonitoringParam.CPU);
        }
        private void RAMButton_Click(object sender, RoutedEventArgs e)
        {
            ModelView.UpdateParam(MonitoringParam.RAM);
        }
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            Model.ClearChart();
        }
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            Model.Stop();
        }
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                Model.Stop();
                IsPreviousKeyControl = true;
            }
        }
        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (IsPreviousKeyControl)
            {
                Model.Start();
                IsPreviousKeyControl = false;
            }
        }

    }
}