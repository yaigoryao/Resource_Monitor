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
using System.Diagnostics;
using LiveChartsCore.SkiaSharpView.Painting.Effects;

namespace ResourceMonitor
{
    public enum MonitoringParam { CPU = 0, RAM, Default};

    public class MonitoringUnit
    {
        public string Name { get; set; }
        public int MaxPoints { get; set; } = 30;

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
        private const int TIMER_MS_DELAY = 500;

        public Model Model = null;

        private MonitoringParam CurrentParam = MonitoringParam.Default;
        public object Sync { get; } = new object();

        private DispatcherTimer Timer = null;
        public ModelView()
        {
            Model = new Model();
            Timer = new DispatcherTimer();
            Timer.Tick += new EventHandler(Timer_Tick);
            Timer.Interval = TimeSpan.FromMilliseconds(TIMER_MS_DELAY);
            Series = new ObservableCollection<ISeries>
            {
                new LineSeries<ObservableValue>
                {
                    Values = { },
                    GeometryFill = null,
                    GeometryStroke = null,
                    LineSmoothness = 0,
                    Name = string.Empty,
                },
                new LineSeries<ObservablePoint>
                {
                    Values = new ObservableCollection<ObservablePoint> { },
                    GeometryFill = null,
                    GeometryStroke = null,
                    Fill = null,
                    Stroke = new SolidColorPaint
                    {
                        Color = SKColors.Red,
                        StrokeCap = SKStrokeCap.Round,
                        StrokeThickness = 3,
                        PathEffect = new DashEffect( new float[] { 7.5f, 15 } ),
                    }

                }

            };
        }
        public void UpdateMaxValue()
        {
            Series[1].Values = new ObservableCollection<ObservablePoint> { new ObservablePoint(0, Model.GetMaxUnitValue(CurrentParam)),
                                                                               new ObservablePoint((Model?.Units[CurrentParam]?.Values?.Count) ?? 0, Model.GetMaxUnitValue(CurrentParam)) };

        }
        public void UpdateParam(MonitoringParam Param)
        {
            if (CurrentParam == Param) return;
                
            lock (Sync)
            {
                CurrentParam = Param;
                Stop();
                XAxes[0].Name = Series[0].Name = Model?.Units[CurrentParam].Name;
                Series[0].Values = Model?.Units[CurrentParam].Values;
                UpdateMaxValue();
                Start();
            }
        }

        public void Start()
        {
            if (!Timer.IsEnabled) Timer.Start();
        }

        public void Stop()
        {
            if (Timer.IsEnabled) Timer.Stop();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            Model.AddValues();
            UpdateMaxValue();
        }

        public void ClearChart()
        {
            Model.ClearValues();
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

        private object Sync { get; } = new object();

        private PerformanceTracker Tracker = null;
        public Dictionary<MonitoringParam, MonitoringUnit> Units { get; } = null;
        public Model()
        {
            Tracker = new PerformanceTracker(10, false);
           
            Units = new Dictionary<MonitoringParam, MonitoringUnit> { { MonitoringParam.CPU, new MonitoringUnit("CPU") },
                                                                      { MonitoringParam.RAM, new MonitoringUnit("RAM") }};
        }
        public void ClearValues()
        {
            lock (Sync)
            {
                foreach (var unit in Units) unit.Value.Clear();
            }
        }
        public double GetMaxUnitValue(MonitoringParam Param)
        {
            return Units[Param]?.Values?.Select(value => value.Value).Max() ?? 0;
        }

        public void AddValues()
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
            InitializeComponent();
            ModelView = new ModelView();
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
            ModelView.ClearChart();
        }
        private void Window_Closing(object sender, CancelEventArgs e)
        {
            ModelView.Stop();
        }
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyboardDevice.Modifiers == ModifierKeys.Control)
            {
                ModelView.Stop();
                IsPreviousKeyControl = true;
            }
        }
        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (IsPreviousKeyControl)
            {
                ModelView.Start();
                IsPreviousKeyControl = false;
            }
        }

        private void ControlExpander_Expanded(object sender, RoutedEventArgs e)
        {
            //ModelView.ControlExpanderState = "0.2*";
        }

        private void ControlExpander_Collapsed(object sender, RoutedEventArgs e)
        {
            //ModelView.ControlExpanderState = "0.1*";
        }

    }
}