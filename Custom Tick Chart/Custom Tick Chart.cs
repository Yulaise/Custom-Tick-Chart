using cAlgo.API;
using System;
using System.Globalization;
using System.Linq;

namespace cAlgo
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class CustomTickChart : Indicator
    {
        #region Fields

        private const string Name = "Custom Tick Chart";

        private const string TimeFrameNamePrefix = "Tick";

        private string _chartObjectNamesSuffix;

        private CustomOhlcBar _lastBar, _previousBar;

        private Color _bullishBarBodyColor, _bullishBarWickColor, _bearishBarBodyColor, _bearishBarWickColor;

        private bool _isChartTypeValid;

        private Bars _bars;

        private int _timeFrameSizeRatio, _barNumber, _lastBarIndex = -1;

        #endregion Fields

        #region Parameters

        [Parameter("Size(Ticks)", DefaultValue = 10, Group = "General")]
        public int SizeInTicks { get; set; }

        [Parameter("Bullish Bar Color", DefaultValue = "Lime", Group = "Body")]
        public string BullishBarBodyColor { get; set; }

        [Parameter("Bearish Bar Color", DefaultValue = "Red", Group = "Body")]
        public string BearishBarBodyColor { get; set; }

        [Parameter("Opacity", DefaultValue = 100, MinValue = 0, MaxValue = 255, Group = "Body")]
        public int BodyOpacity { get; set; }

        [Parameter("Thickness", DefaultValue = 1, Group = "Body")]
        public int BodyThickness { get; set; }

        [Parameter("Line Style", DefaultValue = LineStyle.Solid, Group = "Body")]
        public LineStyle BodyLineStyle { get; set; }

        [Parameter("Fill", DefaultValue = true, Group = "Body")]
        public bool FillBody { get; set; }

        [Parameter("Show", DefaultValue = true, Group = "Wicks")]
        public bool ShowWicks { get; set; }

        [Parameter("Bullish Bar Color", DefaultValue = "Lime", Group = "Wicks")]
        public string BullishBarWickColor { get; set; }

        [Parameter("Bearish Bar Color", DefaultValue = "Red", Group = "Wicks")]
        public string BearishBarWickColor { get; set; }

        [Parameter("Opacity", DefaultValue = 100, MinValue = 0, MaxValue = 255, Group = "Wicks")]
        public int WicksOpacity { get; set; }

        [Parameter("Thickness", DefaultValue = 2, Group = "Wicks")]
        public int WicksThickness { get; set; }

        [Parameter("Line Style", DefaultValue = LineStyle.Solid, Group = "Wicks")]
        public LineStyle WicksLineStyle { get; set; }

        [Parameter("Open", DefaultValue = true, Group = "OHLC Outputs")]
        public bool IsOpenOutputEnabled { get; set; }

        [Parameter("High", DefaultValue = true, Group = "OHLC Outputs")]
        public bool IsHighOutputEnabled { get; set; }

        [Parameter("Low", DefaultValue = true, Group = "OHLC Outputs")]
        public bool IsLowOutputEnabled { get; set; }

        [Parameter("Close", DefaultValue = true, Group = "OHLC Outputs")]
        public bool IsCloseOutputEnabled { get; set; }

        #endregion Parameters

        #region Other properties

        public ChartArea Area
        {
            get
            {
                return IndicatorArea ?? (ChartArea)Chart;
            }
        }

        #endregion Other properties

        #region Outputs

        [Output("Open", LineColor = "Transparent", PlotType = PlotType.Line)]
        public IndicatorDataSeries Open { get; set; }

        [Output("High", LineColor = "Transparent", PlotType = PlotType.Line)]
        public IndicatorDataSeries High { get; set; }

        [Output("Low", LineColor = "Transparent", PlotType = PlotType.Line)]
        public IndicatorDataSeries Low { get; set; }

        [Output("Close", LineColor = "Transparent", PlotType = PlotType.Line)]
        public IndicatorDataSeries Close { get; set; }

        #endregion Outputs

        #region Overridden methods

        protected override void Initialize()
        {
            _chartObjectNamesSuffix = string.Format("{0}_{1}", Name, DateTime.Now.Ticks);

            var timeFrameName = Chart.TimeFrame.ToString();

            timeFrameName = timeFrameName.Equals("tick", StringComparison.OrdinalIgnoreCase) ? "Tick1" : timeFrameName;

            if (timeFrameName.StartsWith("Tick", StringComparison.Ordinal) == false)
            {
                var name = string.Format("Error_{0}", _chartObjectNamesSuffix);

                var error = string.Format("{0} Error: Current chart is not a Tick chart, please switch to a Tick chart", Name);

                Area.DrawStaticText(name, error, VerticalAlignment.Center, HorizontalAlignment.Center, Color.Red);

                return;
            }
            else if (Convert.ToInt32(timeFrameName.Substring(TimeFrameNamePrefix.Length), CultureInfo.InvariantCulture) > SizeInTicks)
            {
                var name = string.Format("Error_{0}", _chartObjectNamesSuffix);

                var error = string.Format("{0} Error: Your current chart size must be smaller than your set size", Name);

                Area.DrawStaticText(name, error, VerticalAlignment.Center, HorizontalAlignment.Center, Color.Red);

                return;
            }
            else if (timeFrameName.Equals(string.Format("Tick{0}", SizeInTicks), StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            else if (SizeInTicks % Convert.ToInt32(timeFrameName.Substring(TimeFrameNamePrefix.Length), CultureInfo.InvariantCulture) == 0)
            {
                _bars = Bars;
            }
            else
            {
                var timeFrame = GetTimeFrame(SizeInTicks, "tick");

                _bars = GetTimeFrameBars(timeFrame);
            }

            _timeFrameSizeRatio = SizeInTicks / GetTimeFrameSize(_bars.TimeFrame.ToString(), TimeFrameNamePrefix);

            _isChartTypeValid = true;

            _bullishBarBodyColor = GetColor(BullishBarBodyColor, BodyOpacity);
            _bearishBarBodyColor = GetColor(BearishBarBodyColor, BodyOpacity);

            _bullishBarWickColor = GetColor(BullishBarWickColor, WicksOpacity);
            _bearishBarWickColor = GetColor(BearishBarWickColor, WicksOpacity);
        }

        public override void Calculate(int index)
        {
            if (_isChartTypeValid == false) return;

            var otherBarsIndex = _bars.OpenTimes.GetIndexByTime(Bars.OpenTimes[index]);

            if (_lastBarIndex == otherBarsIndex)
            {
                return;
            }

            _lastBarIndex = index;

            var time = _bars.OpenTimes[otherBarsIndex];

            if (_lastBar == null || _barNumber == _timeFrameSizeRatio)
            {
                ChangeLastBar(time, otherBarsIndex);

                _barNumber = 1;
            }
            else
            {
                _barNumber += 1;
            }

            for (int barIndex = _lastBar.Index; barIndex <= otherBarsIndex; barIndex++)
            {
                UpdateLastBar(time, barIndex);

                FillOutputs(index, _lastBar);
            }

            DrawBar(_lastBar);
        }

        #endregion Overridden methods

        #region Other methods

        private void FillOutputs(int index, CustomOhlcBar lastBar)
        {
            if (IsOpenOutputEnabled)
            {
                Open[index] = decimal.ToDouble(lastBar.Open);
            }

            if (IsHighOutputEnabled)
            {
                High[index] = decimal.ToDouble(lastBar.High);
            }

            if (IsLowOutputEnabled)
            {
                Low[index] = decimal.ToDouble(lastBar.Low);
            }

            if (IsCloseOutputEnabled)
            {
                Close[index] = decimal.ToDouble(lastBar.Close);
            }
        }

        private Color GetColor(string colorString, int alpha = 255)
        {
            var color = colorString[0] == '#' ? Color.FromHex(colorString) : Color.FromName(colorString);

            return Color.FromArgb(alpha, color);
        }

        private void DrawBar(CustomOhlcBar lastBar)
        {
            string objectName = string.Format("{0}.{1}", lastBar.StartTime.Ticks, _chartObjectNamesSuffix);

            var barBodyColor = lastBar.Open > lastBar.Close ? _bearishBarBodyColor : _bullishBarBodyColor;

            var open = decimal.ToDouble(lastBar.Open);
            var close = decimal.ToDouble(lastBar.Close);

            var bodyRectangle = Area.DrawRectangle(objectName, lastBar.StartTime, open, lastBar.EndTime, close, barBodyColor, BodyThickness, BodyLineStyle);

            bodyRectangle.IsFilled = FillBody;

            if (ShowWicks)
            {
                string upperWickObjectName = string.Format("{0}.UpperWick", objectName);
                string lowerWickObjectName = string.Format("{0}.LowerWick", objectName);

                var barHalfTimeInMinutes = (lastBar.EndTime - _lastBar.StartTime).TotalMinutes / 2;
                var barCenterTime = lastBar.StartTime.AddMinutes(barHalfTimeInMinutes);

                if (lastBar.Open > lastBar.Close)
                {
                    Area.DrawTrendLine(upperWickObjectName, barCenterTime, open, barCenterTime, decimal.ToDouble(lastBar.High),
                        _bearishBarWickColor, WicksThickness, WicksLineStyle);
                    Area.DrawTrendLine(lowerWickObjectName, barCenterTime, close, barCenterTime, decimal.ToDouble(lastBar.Low),
                        _bearishBarWickColor, WicksThickness, WicksLineStyle);
                }
                else
                {
                    Area.DrawTrendLine(upperWickObjectName, barCenterTime, close, barCenterTime, decimal.ToDouble(lastBar.High),
                        _bullishBarWickColor, WicksThickness, WicksLineStyle);
                    Area.DrawTrendLine(lowerWickObjectName, barCenterTime, open, barCenterTime, decimal.ToDouble(lastBar.Low),
                        _bullishBarWickColor, WicksThickness, WicksLineStyle);
                }
            }
        }

        private void ChangeLastBar(DateTime time, int index)
        {
            if (_lastBar != null)
            {
                DrawBar(_lastBar);
            }

            _previousBar = _lastBar;

            _lastBar = new CustomOhlcBar
            {
                StartTime = time,
                Index = index,
                Open = _previousBar == null ? (decimal)_bars.OpenPrices[index] : _previousBar.Close,
            };

            _lastBar.Close = _lastBar.Open;
            _lastBar.High = _lastBar.Open;
            _lastBar.Low = _lastBar.Open;
        }

        private void UpdateLastBar(DateTime time, int index)
        {
            _lastBar.Close = (decimal)_bars.ClosePrices[index];
            _lastBar.High = Math.Max((decimal)_bars.HighPrices[index], _lastBar.High);
            _lastBar.Low = Math.Min((decimal)_bars.LowPrices[index], _lastBar.Low);
            _lastBar.EndTime = time;
            _lastBar.Index = index;
        }

        private TimeFrame GetTimeFrame(int sizeInPips, string type)
        {
            var timeFrames = (from timeFrame in TimeFrame.GetType().GetFields()
                              where timeFrame.Name.StartsWith(type, StringComparison.OrdinalIgnoreCase)
                              let timeFrameName = timeFrame.Name.Equals("tick", StringComparison.OrdinalIgnoreCase) ? "Tick1" : timeFrame.Name
                              let timeFrameSize = Convert.ToInt32(timeFrameName.Substring(type.Length))
                              where timeFrameSize <= sizeInPips && sizeInPips % timeFrameSize == 0
                              orderby timeFrameSize descending
                              select new Tuple<TimeFrame, int>(timeFrame.GetValue(null) as TimeFrame, timeFrameSize)).ToArray();

            var bestFitTimeFrame = timeFrames.FirstOrDefault(timeFrame => timeFrame.Item2 <= sizeInPips && sizeInPips % timeFrame.Item2 == 0);

            if (bestFitTimeFrame != null) return bestFitTimeFrame.Item1;

            var smallestTimeFrame = timeFrames.LastOrDefault();

            if (smallestTimeFrame != null) return smallestTimeFrame.Item1;

            throw new InvalidOperationException(string.Format("Couldn't find a proper time frame for your provided size ({0} Ticks) and type ({1}).", sizeInPips, type));
        }

        private int GetTimeFrameSize(string timeFrameName, string type)
        {
            return timeFrameName.Equals("tick", StringComparison.OrdinalIgnoreCase) ? 1 : Convert.ToInt32(timeFrameName.Substring(type.Length));
        }

        private Bars GetTimeFrameBars(TimeFrame timeFrame)
        {
            var bars = MarketData.GetBars(timeFrame);

            if (bars.Count == 0)
            {
                throw new InvalidOperationException(string.Format("Couldn't load the {0} time frame bars", timeFrame));
            }

            var numberOfLoadedBars = bars.Count;

            while (numberOfLoadedBars > 0 && bars[0].OpenTime > Bars[0].OpenTime)
            {
                numberOfLoadedBars = bars.LoadMoreHistory();
            }

            return bars;
        }

        #endregion Other methods
    }

    public class CustomOhlcBar
    {
        public DateTime StartTime { get; set; }

        public DateTime EndTime { get; set; }

        public int Index { get; set; }

        public decimal Open { get; set; }

        public decimal High { get; set; }

        public decimal Low { get; set; }

        public decimal Close { get; set; }
    }
}