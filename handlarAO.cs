#region Using declarations
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Indicator;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Strategy;
#endregion

// This namespace holds all strategies and is required. Do not change it.
namespace NinjaTrader.Strategy
{
    /// <summary>
    /// Enter the description of your strategy here
    /// </summary>
    [Description("Enter the description of your strategy here")]
    public class AO : Strategy
    {
        #region Variables
			private string version = "0.0.4";
			private int take = 1; // Default setting for Take
			private int stop = 10; // Default setting for Stop
        #endregion

        /// <summary>
        /// This method is used to configure the strategy and is called once before any strategy method is called.
        /// </summary>
        protected override void Initialize()
        {
            SetProfitTarget("", CalculationMode.Ticks, take);
			SetStopLoss("", CalculationMode.Ticks, stop, false);
			
			CalculateOnBarClose = true;
        }

		//=========================================================================================================     
        #region priceToInt
        /// <summary>
        /// Преобразуем цену double в int для сравнения, т.к. возникают баги с числами с плавающей точкой
        /// </summary>
		private int priceToInt(double p){
            return Convert.ToInt32(p/TickSize);
        } 
		#endregion priceToInt
        //=========================================================================================================			
		
  		//=========================================================================================================     
        #region getFilterValueForAO
        private double getFilterValueForAO(){
            
			// 5
			if (
				(Instrument.ToString().StartsWith("CL"))
				//|| (Instrument.ToString().StartsWith("HO"))
			) {
                return 5;
            }
			
			// 30
            if (
				(Instrument.ToString().StartsWith("ES"))
				|| (Instrument.ToString().StartsWith("GC"))
				//|| (Instrument.ToString().StartsWith("PL"))
				//|| (Instrument.ToString().StartsWith("6C"))
			) {
                return 30;
            }
            
			
			// 0.5
            if (
				(Instrument.ToString().StartsWith("NG"))
			) {
                return 0.5;
            }
			return 0;
        } 
		#endregion getFilterValueForAO
        //=========================================================================================================			
		
        /// <summary>
        /// Called on each bar update event (incoming tick)
        /// </summary>
        protected override void OnBarUpdate()
        {
			// переход с зеленого в красный в положительной зоне - ШОРТ пример http://prntscr.com/hrqg68
			if (
				(bwAO().AOPos[1] > 0) 										// зеленый бар индикатора выше нуля на прошлой свече
				&& (bwAO().AONeg[0] > 0) 									// красный бар индикатора выше нуля на текущей свче
				&& (priceToInt(Math.Abs(bwAO().AOPos[1])) < priceToInt(getFilterValueForAO()))
				&& (priceToInt(Math.Abs(bwAO().AONeg[0])) < priceToInt(getFilterValueForAO()))							
				
			){
				Print(Time[0]+" bwAO().AOPos[1]="+bwAO().AOPos[1]+" bwAO().AONeg[0]="+bwAO().AONeg[0]);
				EnterShort(DefaultQuantity, "");
			}
			
			// переход с красного в зеленый в отрицательной зоне - ЛОНГ пример http://prntscr.com/hrqggo
			if (
				(bwAO().AONeg[1] < 0)										// зеленый бар индикатора выше нуля на прошлой свече
				&& (bwAO().AOPos[0] < 0)									// красный бар индикатора выше нуля на текущей свче		
				&& (priceToInt(Math.Abs(bwAO().AONeg[1])) < priceToInt(getFilterValueForAO()))
				&& (priceToInt(Math.Abs(bwAO().AOPos[0])) < priceToInt(getFilterValueForAO()))
			) {
				Print(Time[0]+" bwAO().AONeg[1]="+bwAO().AONeg[1]+" bwAO().AOPos[0]="+bwAO().AOPos[0]);
				EnterLong(DefaultQuantity, "");
			}
        }

        #region Properties
        [Description("")]
        [GridCategory("Parameters")]
        public int Take
        {
            get { return take; }
            set { take = Math.Max(1, value); }
        }

        [Description("")]
        [GridCategory("Parameters")]
        public int Stop
        {
            get { return stop; }
            set { stop = Math.Max(1, value); }
        }
        #endregion
    }
}
