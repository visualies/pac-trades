#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

//This namespace holds Indicators in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Indicators
{
	public class ValidHighLow : Indicator
	{
		private enum Direction
		{
			Long,
			Short
		}
		
		private List<int> swingHighs { get; set; }
		private List<int> bullishHighs { get; set; }
		private List<int> swingLows { get; set; }
		private List<int> bearishLows { get; set; }
		private List<int> validHighs { get; set; }
		private List<int> validLows { get; set; }

		private double highestHigh = 0.0;
		private double lowestLow = 0.0;
		
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"Enter the description for your new custom Indicator here.";
				Name										= "ValidHighLow";
				Calculate									= Calculate.OnBarClose;
				IsOverlay									= false;
				DisplayInDataBox							= true;
				DrawOnPricePanel							= true;
				DrawHorizontalGridLines						= true;
				DrawVerticalGridLines						= true;
				PaintPriceMarkers							= true;
				ScaleJustification							= NinjaTrader.Gui.Chart.ScaleJustification.Right;
				//Disable this property if your indicator requires custom values that cumulate with each new market data event. 
				//See Help Guide for additional information.
				IsSuspendedWhileInactive					= true;
				ValidHighColor = Brushes.Green;
				ValidLowColor = Brushes.Red;
				MarkSwingPeaks = false;
				PrintCandleIndices = false;
				ShowSwings = true;
				ShowFractals = true;
			}
			else if (State == State.Configure)
			{
				swingHighs = new List<int>();
				swingLows = new List<int>();
				bullishHighs = new List<int>();
				bearishLows = new List<int>();
				validHighs = new List<int>();
				validLows = new List<int>();
				
			}
			else if (State == State.DataLoaded)
			{
			}
		}

		protected override void OnBarUpdate()
		{
			if (CurrentBar == 1) Initialize(CurrentBar);
			
			// Check if we have at least one previous candle
			if (CurrentBar < 3) return;
			
			MarkBarIndex();

			ValidateHighs(CurrentBar);
			ValidateLows(CurrentBar);
			
			UpdateHighs(CurrentBar);
			UpdateLows(CurrentBar);
		}
		
		private void Initialize(int barIndex)
		{
			//validHighs.Add(barIndex);
			validLows.Add(barIndex);
			Draw.TriangleUp(this, "NewBearishLow" + barIndex, true,CurrentBar - barIndex, Low.GetValueAt(barIndex) - 0.3, Brushes.Orange);
		}

		private void MarkBarIndex()
		{
			if (!PrintCandleIndices) return;
			if (CurrentBar % 5 == 0)
			{
				Draw.Text(this, "bar" + CurrentBar, $"{CurrentBar}", 0, High[0] + 2, Brushes.DimGray);
			}

			Print("--------------------");
			Print($"Bar {CurrentBar} out of {Bars.Count}");
		}
		
		private void UpdateHighs(int barIndex)
		{
			Print("Updating highs");
			var currentHigh = High.GetValueAt(barIndex);
			var currentHighClose = Close.GetValueAt(barIndex);

			if (!IsBullishCandle(barIndex)) return;
			Print($"Candle @{barIndex} is a bullish candle.");

			//Here we check for the high to be closed above but then register the wick high as new highest high
			if (currentHighClose <= highestHigh) return;
			Print($"Candle @{barIndex} put in a new high of {currentHigh} above previous highs of {highestHigh}.");
			
			highestHigh = currentHigh;
			
			bullishHighs.Add(barIndex);
		}
		
		private void UpdateLows(int barIndex)
		{
			Print("Updating lows");
			var currentLow = Low.GetValueAt(barIndex);
			var currentLowClose = Close.GetValueAt(barIndex);
			
			if (!IsBearishCandle(barIndex)) return;
			Print($"Candle @{barIndex} is a bearish candle.");
			
			//Same as above but for bearish candles
			if (currentLowClose >= lowestLow) return;
			Print($"Candle @{barIndex} put in a new low of {currentLow} below previous lows of {lowestLow}.");
			
			lowestLow = currentLow;
			
			bearishLows.Add(barIndex);
		}

		private void ValidateHighs(int barIndex)
		{
			Print("Validating highs");
			if (!IsSearchingHigh()) return;

			var lastBullish = bullishHighs.LastOrDefault();
			var lastBullishLow = Low.GetValueAt(lastBullish);

			Print($"Did we close below the low @{lastBullishLow} of the bullish candle that put in the high @{lastBullish}?");
			if (!(Close.GetValueAt(barIndex) < lastBullishLow))
			{
				Print("No. continue.");
				return;
			}
			
			//Update the current low since we might have skipped some with late validation
			var lastBearish = LowestBarFromRange(lastBullish, barIndex);
			bearishLows.Add(lastBearish);
			
			validHighs.Add(lastBullish);
			lowestLow = Low.GetValueAt(lastBearish);
			
			Print($"Yes, validated high @{lastBullish}");
			Print($"Resetting lowest low to {lowestLow}");

			var high = HighestBarFromRange(validLows.LastOrDefault(), barIndex);
			var validIndex = MarkSwingPeaks ? high : lastBullish;

			swingHighs.Add(validIndex);
			
			if (ShowFractals) Draw.TriangleDown(this, "valid-high-" + barIndex, true, CurrentBar - validIndex, High.GetValueAt(validIndex) + 0.3, ValidHighColor);
			if (ShowSwings) Draw.Line(this, "swing-high-" + barIndex, CurrentBar - validIndex, High.GetValueAt(validIndex), CurrentBar - swingLows.LastOrDefault(), Low.GetValueAt(swingLows.LastOrDefault()), Brushes.DimGray);
		}
		
		private void ValidateLows(int barIndex)
		{
			Print("Validating lows");
			if (!IsSearchingLow()) return;

			var lastBearish = bearishLows.LastOrDefault();
			var lastBearishHigh = High.GetValueAt(lastBearish);
            
			Print($"Did we close above the high @{lastBearishHigh} of the bearish candle that put in the low @{lastBearish}?");
			if (!(Close.GetValueAt(barIndex) > lastBearishHigh))
			{
				Print("No. continue.");
				return;
			}
			
			//Update the current high since we might have skipped some with late validation
			var lastBullish = HighestBarFromRange(lastBearish, barIndex);
			bullishHighs.Add(lastBullish);
			
			validLows.Add(lastBearish);
			highestHigh = High.GetValueAt(lastBullish);
			
			Print($"Yes, validated low @{lastBearish}");
			Print($"Resetting highest high to {highestHigh}");
				
			var low = LowestBarFromRange(validHighs.LastOrDefault(), barIndex);
			var validIndex = MarkSwingPeaks ? low : lastBearish;
			
			swingLows.Add(validIndex);
			
			if (ShowFractals) Draw.TriangleUp(this, "valid-low-" + barIndex, true, CurrentBar - validIndex, Low.GetValueAt(validIndex) - 0.3, ValidLowColor);
			if (ShowSwings) Draw.Line(this, "swing-low-" + barIndex, CurrentBar - validIndex, Low.GetValueAt(validIndex), CurrentBar - swingHighs.LastOrDefault(), High.GetValueAt(swingHighs.LastOrDefault()), Brushes.DimGray);
		}

		private int LowestBarFromRange(int startIndex, int endIndex)
		{
			var lowprice = double.MaxValue;
			var lowIndex = 0;

			for (var i = startIndex; i <= endIndex; i++)
			{
				if (!(Low.GetValueAt(i) < lowprice)) continue;
				lowprice = Low.GetValueAt(i);
				lowIndex = i;
			}

			return lowIndex;
		}
		
		private int HighestBarFromRange(int startIndex, int endIndex)
		{
			var highprice = double.MinValue;
			var highIndex = 0;

			for (var i = startIndex; i <= endIndex; i++)
			{
				if (!(High.GetValueAt(i) > highprice)) continue;
				highprice = High.GetValueAt(i);
				highIndex = i;
			}

			return highIndex;
		}
		
		
		private bool IsSearchingHigh()
		{
			Print($"Are we searching for valid highs?");
			if (validLows.Count == 0 || validHighs.Count == 0)
			{
				Print("Yes valid lows or highs are 0.");
				return true;
			}
			if (validLows.LastOrDefault() > validHighs.LastOrDefault())
			{
				Print("Yes the last one was a valid low @" + validLows.LastOrDefault());
				return true;
			}

			Print("No. The last one was a valid high @" + validHighs.LastOrDefault());
			return false;
		}

		private bool IsSearchingLow()
		{
			Print($"Are we searching for valid lows?");
			if (validLows.Count == 0 || validHighs.Count == 0)
			{
				Print("Yes valid lows or highs are 0.");
				return true;
			}

			if (validHighs.LastOrDefault() > validLows.LastOrDefault())
			{
				Print("Yes the last one was a valid high @" + validHighs.LastOrDefault());
				return true;
			}

			Print("No. The last one was a valid low @" + validLows.LastOrDefault());
			return false;
		}

		private bool IsBullishCandle(int barIndex)
		{
			return GetDirection(barIndex) == Direction.Long;
		}
		
		private bool IsBearishCandle(int barIndex)
		{
			return GetDirection(barIndex) == Direction.Short;
		}
		
		private Direction GetDirection(int barIndex)
		{
			return Close.GetValueAt(barIndex) > Open.GetValueAt(barIndex) ? Direction.Long : Direction.Short;
		}
		
		[XmlIgnore()]
		[Display(Name = "Mark Swing High/Low", GroupName = "Options", Order = 1)]
		public bool MarkSwingPeaks
		{ get; set; }
		
		[XmlIgnore()]
		[Display(Name = "Print Candle Indices", GroupName = "Debug Options", Order = 20)]
		public bool PrintCandleIndices
		{ get; set; }
		
		[XmlIgnore()]
		[Display(Name = "Valid Highs", GroupName = "Display", Order = 1)]
		public Brush ValidHighColor
		{ get; set; }
		
		[XmlIgnore()]
		[Display(Name = "Valid Lows", GroupName = "Display", Order = 2)]
		public Brush ValidLowColor
		{ get; set; }
		
		[XmlIgnore()]
		[Display(Name = "Show Swings", GroupName = "Display", Order = 3)]
		public bool ShowSwings
		{ get; set; }
		
		[XmlIgnore()]
		[Display(Name = "Show Fractals", GroupName = "Display", Order = 4)]
		public bool ShowFractals
		{ get; set; }
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private ValidHighLow[] cacheValidHighLow;
		public ValidHighLow ValidHighLow()
		{
			return ValidHighLow(Input);
		}

		public ValidHighLow ValidHighLow(ISeries<double> input)
		{
			if (cacheValidHighLow != null)
				for (int idx = 0; idx < cacheValidHighLow.Length; idx++)
					if (cacheValidHighLow[idx] != null &&  cacheValidHighLow[idx].EqualsInput(input))
						return cacheValidHighLow[idx];
			return CacheIndicator<ValidHighLow>(new ValidHighLow(), input, ref cacheValidHighLow);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.ValidHighLow ValidHighLow()
		{
			return indicator.ValidHighLow(Input);
		}

		public Indicators.ValidHighLow ValidHighLow(ISeries<double> input )
		{
			return indicator.ValidHighLow(input);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.ValidHighLow ValidHighLow()
		{
			return indicator.ValidHighLow(Input);
		}

		public Indicators.ValidHighLow ValidHighLow(ISeries<double> input )
		{
			return indicator.ValidHighLow(input);
		}
	}
}

#endregion
