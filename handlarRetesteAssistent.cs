#region Using declarations
 using System;
 using System.ComponentModel;
 using System.Diagnostics;
 using System.Drawing;
 using System.Drawing.Drawing2D;
 using System.Xml.Serialization;
 using NinjaTrader.Cbi;
 using NinjaTrader.Data;
 using NinjaTrader.Gui.Chart;
 using System.Collections;
 using System.Collections.Generic;
 using System.Linq;
 using System.Xml;
 using System.Net;
 using System.Net.Cache;
 using System.IO;
 using System.Text;
 using System.Windows.Forms;
#endregion

// This namespace holds all strategies and is required. Do not change it.
// version v.2.2.0.3
namespace NinjaTrader.Strategy{
    /// <summary>
    /// Strategy-helper in my trading.
    /// </summary>
    [Description("Strategy-helper from handlar")]
    public class handlarRetesterAssistent : Strategy{
        #region Variables

        // Настройки ордера
        private int     takeProfit                      = 3;                                       // значение тейк профита
        private int     stopLoss                        = 8;                                       // значение стоп лосса
        private int     lot                             = 2;                                        // объем сделки в лотах
        private bool    killLimitOrder                  = false;                                    // true - лимитник висит только одну свечу, потом нинзя удаляет его, false - лимитник будет висеть, пока не исполнится
        private int     ticksToLimit                    = 3;                                        // на сколько тиков цена должна подойти к уровню, чтобы был выставлен лимитник
        IOrder          entryOrder                      = null;                                     // переменная, в которую записывается активный ордер, NULL если нет ни лимитника ни рыночного ордера
        private bool    breakeven                       = true;                                     // выходить по безубытку, если цена прошагала в минус
        private bool    showSLandTP                     = true;                                     // показывать линии стоп лосса и тейк профита
        IRay            stopRay                         = null; 
        IRay            takeRay                         = null;
        IText           stopText                        = null;
        IText           takeText                        = null;
        double          stopPrice                       = 0;
        double          takePrice                       = 0;
		private bool	alwaysActiveOneLimitOrder		= true;
        
        // Настройки отладочной информации
        private string  debugInformation                = "";
        private bool    debugMode                       = true;                                     // включаем или отключаем режим отладки (вывод в окно Output), чтобы показывать дополнительную информацию в ходе работы стратегии           
        private bool    showBalanceInfo                 = false;                                    // показывать информацию о деньгах в информационной панели справа вверху?
        
        // Общие параметры стратегии
        private bool    isNewSession                    = false; 
        private string  patternName                     = "";
        private bool    removeLimitOrderIfNedotick      = false;                                    // удалять лимитник и уровень, если был недотик, т.е. цена подошла на определенное количество тиков, не забрала в рынок и ушла в сторону сделки на расстояние, равное тейку
        
        // Настройки манименеджмента
        private int     dailyLossCount                  = 0;                                        // счетчик для количества убыточных сделок на день
        private double  totalLoss                       = 0;
        private double  dailyLoss                       = 0;
        private int     dailyProfitCount                = 0;                                        // счетчик для количества прибыльных сделок на день
        private double  totalProfit                     = 0;
        private double  dailyProfit                     = 0;
        private double  accountBalance                  = 0;
        private double  curPnL                          = 0;                                        // прибыль (убыток) в текущей открытой сделке
        private double  curCommission                   = 0;                                        // комиссия на круг в сделке
        private Color   curPnLColor                     = Color.Green;                              // цвет для фона, где будет показываться текущий PnL зеленый елси мы в плюсе, красный если в минусе
        private bool    isLossLimitReached              = false;                                    // достигнут ли лимит убыточных сделок на день
        
        // 1.2 Пользовательские уровни
        private bool    useUserLevels                   = true;                                     // можно ли искать пользовательские уровни?
        private bool    allowTradeUserLevels            = true;                                     // можно ли выставлять лимитники по пользовательским уровням?
        private int     cursorX                         = 0;                                        // бар под курсором
        private double  cursorY                         = 0;                                        // цена под курсором
        private double  priceMin                        = 0;
        private double  priceMax                        = 3000;
        
        // Настройки разрешенного времени для торгов
        private int     startTime                       = 12000;                                    // По умолчанию ставим StartTime = 02:15:00
        private int     stopTime                        = 220000;                                   // По умолчанию ставим StopTime  = 23:00:00
        
        // Настройки для ретеста зеркального уровня
        bool            useMirrorLevels                 = false;                                     // можно ли искать зеркальные уровни?
        bool            allowTradeMirrorLevels          = false;                                     // можно ли выставлять лимитники по зеркальным уровням?
        double          BDLevel                         = 0;                                        // уровень пробоя, он же в дальнейшем уровень ретеста или зеркальный уровень
        int             BDBar                           = 0;
        int             BDFreeSpaceBars                 = 5;                                        // количество баров слева от импульсного бара, на расстонии которого цена еще не была
        int             priceWaitingBars                = 3;                                        // количество баров, в течении которого цена должна быть выше или ниже зеркального уровня после пробоя (должна закрепиться)
        bool            BDComplete                      = false;                                    // определяем состояние, был пробой и мы ждем закрепления цены (true) ИЛИ пробоя нет и можно искать его (false)
        string          BDType                          = "";                                       // тип зеркального уровня, в шорт или в лонг
        string          customTag                       = "";

        // настройки для ретеста недельного VWAP
        private DataSeries wVWAP;
        private IRay lastWVWAP;

        // описание структуры для хранения уровня
        private struct myLevelType{
            public double   price;                                                                  // цена уровня
            public bool     isNaked;                                                                // голый уровень (не отработанный еще, цена не пересекала его) или нет (цена его отработала)
            public IRay     draw;                                                                   // луч для визуализации уровня, для голых уровней
            public IText    label;                                                                  // точка для визуализции места клика для удаления луча
            public DateTime time;                                                                   // время появления уровня
        }       
        
        private Dictionary<int, myLevelType> longLevels = new Dictionary<int, myLevelType>();                 // массив для хранения всех уровней в лонг
        private Dictionary<int, myLevelType> shortLevels = new Dictionary<int, myLevelType>();                // массив для хранения всех уровней в шорт  
        private myLevelType levelShort;
        private myLevelType levelLong; 
        private myLevelType level;
        
        // события мышки
        private MouseEventHandler mouseUpH;
            
        
        #endregion
        
        //=========================================================================================================     
        #region Initialize
        /// <summary>
        /// This method is used to configure the strategy and is called once before any strategy method is called.
        /// </summary>
        protected override void Initialize(){
            // если тейк и стоп заданы, тогда сделки будут открываться как в АТМ, т.е. стоп и тейк будут выставлены автоматом с открытием сделки
            if(TakeProfit > 0) SetProfitTarget("", CalculationMode.Ticks, TakeProfit);
            if(StopLoss > 0) SetStopLoss("", CalculationMode.Ticks, StopLoss, false);
            
            // анализировать будем каждый тик, но где надо, будем только по закрытии бара работать, т.к. нинзя
            // на истории при родном расчете на закрытии бара очень не правильно считает
            CalculateOnBarClose = false;
            BarsRequired = 6;
            
            // MaximumBarsLookBack determines how many values the DataSeries will have access to
            wVWAP = new DataSeries(this, MaximumBarsLookBack.Infinite);
            wVWAP = handlarVWAPIndicator().wVWAP;

            if (UseUserLevels){
                // Теперь займемся перехватом событий от мышки если разрешено добавление пользовательских уровней
                mouseUpH=new MouseEventHandler(this.chart_MouseUp);         
                this.ChartControl.ChartPanel.MouseUp += mouseUpH;
                
            }
        }       
        #endregion Initialize
        //========================================================================================================= 

        //=========================================================================================================     
        #region HELPERS 
        /// <summary>
        /// Собраны различные функции-помощники общего назначения
        /// </summary>
        
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
        #region getBarBody
        /// <summary>
        /// получаем величину тела свечи
        /// </summary>
        private int getBarBody(int number){
            return Convert.ToInt32(Math.Abs(priceToInt(Open[number]) - priceToInt(Close[number])));
        }     
        #endregion getBarBody
        //=========================================================================================================
        
        
        //=========================================================================================================     
        #region ATRToInt
        /// <summary>
        /// Преобразуем значение ATR double в int для сравнения, т.к. возникают баги с числами с плавающей точкой
        /// </summary>
        private int ATRToInt(double p){
            //return Convert.ToInt32(p*10000/TickSize);
            if (
                    Instrument.ToString().StartsWith("6A") 
                    || Instrument.ToString().StartsWith("6C") 
                    || Instrument.ToString().StartsWith("6E")
                    || Instrument.ToString().StartsWith("6S")
                ) {
                return Convert.ToInt32(p*1000000);
            }
            if (Instrument.ToString().StartsWith("6B")) {
                return Convert.ToInt32(p*10000);
            }
            if (Instrument.ToString().StartsWith("6J")) {
                return Convert.ToInt32(p*10000000);
            }
            if (
                    Instrument.ToString().StartsWith("CL")
                    || Instrument.ToString().StartsWith("ES")   
                ) {
                return Convert.ToInt32(p*10000);
            }
            if (
                    Instrument.ToString().StartsWith("GC")  
                ) {
                return Convert.ToInt32(p*100);
            }
            return 0;
        }     
        #endregion ATRToInt
        //=========================================================================================================     
        
        //=========================================================================================================     
        #region drawArea
        /// <summary>
        /// Преобразуем цену double в int для сравнения, т.к. возникают баги с числами с плавающей точкой
        /// </summary>
        private IRectangle drawArea(int startBar, int stopBar, Color color, string tag, int opacity){
            double minPrice = 1000000, maxPrice = 0;
            for (int i = stopBar; i <= startBar; i++){
                if(Low[i] < minPrice){
                    minPrice = Low[i];
                }
                
                if (High[i] > maxPrice){
                    maxPrice = High[i];
                }
            }
            minPrice -= 2*TickSize;
            maxPrice += 2*TickSize;
            return DrawRectangle("area-"+Time[startBar]+"__"+tag, false, startBar, minPrice, stopBar, maxPrice, color, color, opacity);
        }     
        #endregion drawArea
        //=========================================================================================================     
        
        #endregion HELPERS
        //========================================================================================================= 
        
        //=========================================================================================================
        #region LEVELS
        /// <summary>
        /// Собраны функции для работы с уровнями: удаление, добавление
        /// </summary>
        
        //=========================================================================================================     
        #region addShortLevel   
        /// <summary>
        /// Добавляем шортовый уровень в массив и рисуем его на графике
        /// </summary>
        private void addShortLevel(double price, int barNumber){
            int key;
            
            if (shortLevels.Count > 0){
                key = shortLevels.Keys.Max() + 1;
            } else {
                key = shortLevels.Count;
            }

            level.price = price;
            level.isNaked = true;
            level.time = Time[barNumber];
            if (AllowTradeMirrorLevels || AllowTradeUserLevels){
                level.draw = DrawRay("shortRay" + price, barNumber, price, barNumber-1, price, Color.Red);
            } else {
                DrawLine("lineShort" + price, barNumber, price, -10, price, Color.Red); 
            }
            level.label = DrawText("shortPrice" + price, false, price.ToString(), barNumber, price, 2, Color.White, new Font ("Arial", 9, FontStyle.Regular), StringAlignment.Far, Color.White, Color.Red, 8);
            if (!shortLevels.ContainsKey(key)) {
                shortLevels.Add(key, level);
                if (UseDebug) {Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| " + "Шортовый уровень (" + price + ") добавлен!");}
            } else {
                if (UseDebug) {Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| " + "Шортовый уровень уже есть в массиве почему-то...(( " + price + "[" + key + "]");}
            }
        }               
        #endregion addShortLevel
        //=========================================================================================================         
                
        //=========================================================================================================     
        #region addLongLevel   
        /// <summary>
        /// Добавляем лонговый уровень в массив и рисуем его на графике
        /// </summary>
        private void addLongLevel(double price, int barNumber){
            int key;
            
            if (longLevels.Count > 0){
                key = longLevels.Keys.Max() + 1;
            } else {
                key = longLevels.Count;
            }
            level.price = price;
            level.isNaked = true;
            level.time = Time[barNumber];
            if (AllowTradeMirrorLevels || AllowTradeUserLevels){
                level.draw = DrawRay("longRay"+price, barNumber, price, barNumber-1, price, Color.Lime);
            } else {
                DrawLine("lineLong" + price, barNumber, price, -10, price, Color.Lime);
            }
            level.label = DrawText("longPrice"+price, false, price.ToString(), barNumber, price, 2, Color.White, new Font ("Arial", 9, FontStyle.Regular), StringAlignment.Far, Color.White, Color.Lime, 8);
            if (!longLevels.ContainsKey(key)) {
                longLevels.Add(key, level);
                if (UseDebug) {Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| " + "Лонговый уровень (" + price + ") добавлен!");}
            } else {
                if (UseDebug) {Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| " + "Лонговый уровень уже есть в массиве почему-то...(( " + price + "[" + key + "]");}
            }
        }               
        #endregion addLongLevel
        //=========================================================================================================         
        
        //=========================================================================================================     
        #region removeShortLevel        
        private void removeShortLevel(double price, int barNumber){
            foreach(KeyValuePair<int, myLevelType> kvp in shortLevels){ 
                if ((price <= kvp.Value.price + TickSize) && (price >= kvp.Value.price - TickSize)){
                    shortLevels.Remove(kvp.Key);
                    RemoveDrawObject(kvp.Value.draw.Tag);
                    RemoveDrawObject(kvp.Value.label.Tag);
                                
                    // показываем информацию о сделках (количество, прибыль-убытки, уровни)
                    showTradesInformation();
                    
                    // показываем информацию о текущем PnL, если мы в рынке или об установленном лимитнике
                    showCurrentPnLInformation();
                }
            }
        }
        #endregion removeShortLevel      
        //=========================================================================================================         
        
        //=========================================================================================================     
        #region removeLongLevel         
        private void removeLongLevel(double price, int barNumber){
            foreach(KeyValuePair<int, myLevelType> kvp in longLevels){  
                if ((price <= kvp.Value.price + TickSize) && (price >= kvp.Value.price - TickSize)){
                    longLevels.Remove(kvp.Key);
                    RemoveDrawObject(kvp.Value.draw.Tag);
                    RemoveDrawObject(kvp.Value.label.Tag);
                                
                    // показываем информацию о сделках (количество, прибыль-убытки, уровни)
                    showTradesInformation();
                    
                    // показываем информацию о текущем PnL, если мы в рынке или об установленном лимитнике
                    showCurrentPnLInformation();
                }
            }
        }
        #endregion removeLongLevel      
        //========================================================================================================= 
        
        //=========================================================================================================     
        #region removeLevel         
        private void removeLevel(double price, int barNumber){
                                    
            // Проверяем, по цене, на которую кликнули есть уровень в списке лонговых или шортовых уровней
            foreach(KeyValuePair<int, myLevelType> kvp in longLevels){  
                if ((price <= kvp.Value.price+TickSize) && (price >= kvp.Value.price-TickSize)){
                    longLevels.Remove(kvp.Key);
                    RemoveDrawObject(kvp.Value.draw.Tag);
                    RemoveDrawObject(kvp.Value.label.Tag);
                                
                    // показываем информацию о сделках (количество, прибыль-убытки, уровни)
                    showTradesInformation();
                    
                    // показываем информацию о текущем PnL, если мы в рынке или об установленном лимитнике
                    showCurrentPnLInformation();
                    if (UseDebug) {Print(Instrument.FullName.ToString()+" |||" + Time[barNumber] + " ||| " + "Лонговых уровней после удаления: "+longLevels.Count);}
                }
            }
            
            
            foreach(KeyValuePair<int, myLevelType> kvp in shortLevels){ 
                if ((price <= kvp.Value.price+TickSize) && (price >= kvp.Value.price-TickSize)){
                    shortLevels.Remove(kvp.Key);
                    RemoveDrawObject(kvp.Value.draw.Tag);
                    RemoveDrawObject(kvp.Value.label.Tag);
                                
                    // показываем информацию о сделках (количество, прибыль-убытки, уровни)
                    showTradesInformation();
                    
                    // показываем информацию о текущем PnL, если мы в рынке или об установленном лимитнике
                    showCurrentPnLInformation();
                    if (UseDebug) {Print(Instrument.FullName.ToString()+" |||" + Time[barNumber] + " ||| " + "Шортовых уровней после удаления: "+shortLevels.Count);}
                }
            }
        }
        #endregion removeLevel      
        //=========================================================================================================     
        
        //=========================================================================================================     
        #region clearLevels
        /// <summary>
        /// Функция подчищает оба массива от уже неактуальных уровней
        /// </summary>
        private void clearLevels(){
            int key = -1;
            myLevelType lvl;
            lvl.price = 0;
            lvl.draw = null;
            lvl.label = null;
            // Удаляем из массива LONG все значения, которые больше или равны последней цене Bid
            
            if (longLevels.Count > 0){
                
                try{if (longLevels.Count == 1){
                    //Print(Instrument.FullName.ToString()+" |||" + Time[0] + " ||| " + "longLevels.Count = 1");
                    if (priceToInt(longLevels[0].price) >= priceToInt(GetCurrentBid())) {
                        key = 0;
                        lvl = longLevels[0];
                    }
                } else {
                    //Print(Instrument.FullName.ToString()+" |||" + Time[0] + " ||| " + "longLevels.Count = " + longLevels.Count);
                    key = longLevels.FirstOrDefault(x => priceToInt(x.Value.price) >= priceToInt(GetCurrentBid())).Key; // КРУТО!!! фактически тот же поиск ключа по значению но без конструкции foreach{}
                    if (key > 0){
                        lvl = longLevels[key];
                    }
                }} catch(Exception e){Print("longLevels.Count > 0 :: "+e.ToString());}
                
                // если найден уровень для удаления, значит цена будет больше нуля, этим и воспользуемся
                if (priceToInt(lvl.price) > 0){
                    if ((Position.MarketPosition != MarketPosition.Flat) || (entryOrder == null)){
                        if (longLevels.ContainsKey(key)){
                            longLevels.Remove(key);
                            if (UseDebug){
                                Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| " + "Из массива лонговых уровней был удален уровень (" + lvl.price + ")");
                            }
                            changeRayToLine("long", lvl.price, lvl.draw, lvl.label);
                        }
                    }
                }
            }
            
            lvl.price = 0;
            lvl.draw = null;
            lvl.label = null;
            
            // Удаляем из массива SHORT все значения, которые меньше или равны последней цене Ask
            if (shortLevels.Count > 0){
                try{if (shortLevels.Count == 1){
                    //Print(Instrument.FullName.ToString()+" |||" + Time[0] + " ||| " + "shortLevels.Count = 1");
                    if (priceToInt(shortLevels[0].price) <= priceToInt(GetCurrentAsk())) {
                        key = 0;
                        lvl = shortLevels[0];
                    }
                } else {
                    //Print(Instrument.FullName.ToString()+" |||" + Time[0] + " ||| " + "shortLevels.Count = " + shortLevels.Count);
                    key = shortLevels.FirstOrDefault(x => x.Value.price <= GetCurrentAsk()).Key;    // КРУТО!!! фактически тот же поиск ключа по значению но без конструкции foreach{}
                    if (key > 0){
                        lvl = shortLevels[key];
                    }
                }} catch(Exception e){Print("shortLevels.Count > 0 :: "+e.ToString());}
                
                // если найден уровень для удаления, значит цена будет больше нуля, этим и воспользуемся
                if (priceToInt(lvl.price) > 0){
                    if ((Position.MarketPosition != MarketPosition.Flat) || (entryOrder == null)){
                        if (shortLevels.ContainsKey(key)){
                            shortLevels.Remove(key);
                            if (UseDebug){
                                Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| " + "Из массива шортовых уровней был удален уровень (" + lvl.price + ")");
                            }
                            changeRayToLine("short", lvl.price, lvl.draw, lvl.label);
                        }
                    }
                }
            }
            
            /*if (RemoveLimitOrderIfNedotick && (entryOrder != null) && (Position.MarketPosition == MarketPosition.Flat)) {
                // если есть лимитный ордер и активирована функция удаления ордера по недотику
                if (entryOrder.OrderAction == OrderAction.Buy) {
                    // Если это лимитка в лонг
                    if ((priceToInt(GetCurrentBid()) - priceToInt(entryOrder.LimitPrice)) > (TakeProfit * priceToInt(TickSize))) {
                        if (UseDebug){
                            Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| " + "Цена уже отошла от лимита в лонг на расстояние тейка, НЕДОТИК, уровень отработал!!!");
                            CancelOrder(entryOrder);
                        }
                    }
                }
                
                
                if (entryOrder.OrderAction == OrderAction.SellShort) {
                    // Если это лимитка в шорт
                    if ((priceToInt(entryOrder.LimitPrice) - priceToInt(GetCurrentAsk())) > (TakeProfit * priceToInt(TickSize))) {
                        if (UseDebug){
                            Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| " + "Цена уже отошла от лимита в шорт на расстояние тейка, НЕДОТИК, уровень отработал!!!");
                            CancelOrder(entryOrder);
                        }
                    }
                }
            }*/
            // показываем информацию о сделках (количество, прибыль-убытки, уровни)
            showTradesInformation();
            
            // показываем информацию о текущем PnL, если мы в рынке или об установленном лимитнике
            showCurrentPnLInformation();

        }               
        #endregion clearLevels
        //=========================================================================================================         
        
        //=========================================================================================================     
        #region changeRayToLine     
        /// <summary>
        /// 
        /// </summary>
        private void changeRayToLine(string direction, double price, IRay draw, IText label){
            if (direction == "long") {
                // рисуем линию по цене луча
                DrawLine("lineLong" + price, draw.Anchor1BarsAgo, draw.Anchor1Y, 0, draw.Anchor1Y, Color.Lime);
                
                // луч удаляем и элемент из списка тоже удаляем, т.к. уровень уже покрытый, а не голый
                RemoveDrawObject(draw.Tag);
                RemoveDrawObject(label.Tag);

            
            } else {
                // рисуем линию по цене луча
                DrawLine("lineShort" + price, draw.Anchor1BarsAgo, draw.Anchor1Y, 0, draw.Anchor1Y, Color.Red);
                
                // луч удаляем и элемент из списка тоже удаляем, т.к. уровень уже покрытый, а не голый
                RemoveDrawObject(draw.Tag);
                RemoveDrawObject(label.Tag);
            }
            
        }               
        #endregion changeRayToLine
        //=========================================================================================================
        
        #endregion LEVELS
        //========================================================================================================= 
        
        //=========================================================================================================     
        #region USER_LEVELS
        /// <summary>
        /// Собраны функции, которые необходимы для работы с выставляемыми пользователем уровнями
        /// </summary>
        
        //=========================================================================================================     
        #region addNewUserLevel   
        /// <summary>
        /// Функция добавляет в нужный "карман" уровень и рисует луч
        /// </summary>
        private void addNewUserLevel(double price, int barNumber){
            
            // нужно добавить уровень в лонг
            if (priceToInt(price) < priceToInt(GetCurrentAsk())) {
                patternName = "userLevel";
                if (UseDebug) {Print(Instrument.FullName.ToString()+" |||" + Time[barNumber] + " ||| " + "Найден пользовательский уровень лонг по цене "+price);}
                try{addLongLevel(price, barNumber);} catch(Exception e){Print("addLongLevel :: "+e.ToString());}
            }
            
            // нужно добавить уровень в шорт
            if (priceToInt(price) > priceToInt(GetCurrentBid())) {
                patternName = "userLevel";
                if (UseDebug) {Print(Instrument.FullName.ToString()+" |||" + Time[barNumber] + " ||| " + "Найден пользовательский уровень шорт по цене "+price);}
                try{addShortLevel(price, barNumber);} catch(Exception e){Print("addLongLevel :: "+e.ToString());}
            }
            
            // показываем информацию о сделках (количество, прибыль-убытки, уровни)
            showTradesInformation();
            
            // показываем информацию о текущем PnL, если мы в рынке или об установленном лимитнике
            showCurrentPnLInformation();
        }     
        #endregion addNewUserLevel
        //=========================================================================================================     
        
        //=========================================================================================================     
        #region getBarFromX
        private int getBarFromX(int x){
            if (ChartControl == null)
                return 0;
            
            int idxSelectedBar      = 0;
            int idxFirstBar         = ChartControl.FirstBarPainted;
            int idxLastBar          = ChartControl.LastBarPainted;
            int totalNoOfBars       = Bars.Count - 1;
            int firstBarX           = ChartControl.GetXByBarIdx(Bars,idxFirstBar);
            int lastBarX            = ChartControl.GetXByBarIdx(Bars,idxLastBar);
            int pixelRangeOfBars    = lastBarX - firstBarX + 1;
            int selectedBarNoScreen= (int)(Math.Round(((double)(x - firstBarX)) / (ChartControl.BarSpace), 0));
            int SelectedBarNumber   = idxFirstBar + selectedBarNoScreen;
            
            if (x <= firstBarX)
                idxSelectedBar = totalNoOfBars - idxFirstBar;
            else if (x >= lastBarX)
                idxSelectedBar = totalNoOfBars - idxLastBar;
            else
                idxSelectedBar = totalNoOfBars - SelectedBarNumber;
            
            if (false) // Display info in Output window if enabled 
            {
                Print("------------------------------");
                Print("Instrument: " + Instrument.FullName + ",    Chart: " +  BarsPeriod.ToString()); // To identify which chart the data was coming from                                  
                Print("Mouse Coordinate X: " + x.ToString());
                Print("Total Bars On Chart: " + totalNoOfBars.ToString());
                Print("BarWidth: " + ChartControl.BarWidth.ToString());
                Print("BarSpace: " + ChartControl.BarSpace.ToString());
                Print("Firs Bar Idx On Screen: " + idxFirstBar.ToString());
                Print("Last Bar Idx On Screen: " + idxLastBar.ToString());
                Print("No Of Bars On Screen: " + (idxLastBar - idxFirstBar).ToString());
                Print("First (Left) On Screen Bar X Coordinate: " + firstBarX.ToString());
                Print("Last (Right) On Screen Bar X Coordinate: " + lastBarX.ToString());
                Print("Pixel Range Of Visible Bars: " + pixelRangeOfBars.ToString());
                Print("Bar Position On Screen From Left: " + selectedBarNoScreen.ToString());
                Print("Selected Bar Number: " + SelectedBarNumber.ToString());
                Print("Selected Bar Index: " + idxSelectedBar.ToString());
            }
                
            //return idxSelectedBar;
            return SelectedBarNumber;
        }
        #endregion getBarFromX      
        //=========================================================================================================     
        
        //=========================================================================================================     
        #region checkPOCLevels   
        /// <summary>
        /// Проверяем все нарисованные на графике лучи, отбираем те, у которых тег начинается с определенных симвоволов, чтобы найти линию ПОКа от профила Олега
        /// </summary>
        private void checkPOCLevels(){
            //Print("=================================\nкаждый тик");
			int sLevelCount = 0;
			int lLevelCount = 0;
			double nearestShortLevelPrice = 100000;
			double nearestLongLevelPrice = 0;
			string di = "";
			if (_Savos_News_For_Strategy().CanTrade[0] == 1) {
				
				#region find POC levels
				foreach (ChartObject co in ChartControl.ChartObjects){
					//Print(Times[0][0]+" || _Savos_News_For_Strategy().CanTrade[0] = "+_Savos_News_For_Strategy().CanTrade[0].ToString());
					if (
							(
								co.Tag.StartsWith("VpocRangeLineExt")
								|| co.Tag.StartsWith("VPOC_Ray")
							) 
							&& (co is IRay)
						){
						IRay pocLine = (IRay) co;
						//Print("найден луч POC c тегом "+pocLine.Tag.ToString());
						
						#region calc short levels
						// сначала определим, это продолжение линии POC выше или ниже текущей цены
						if (priceToInt(pocLine.Anchor1Y) > priceToInt(Close[0])){
							sLevelCount += 1;
							if (priceToInt(pocLine.Anchor1Y) < priceToInt(nearestShortLevelPrice)) {
								nearestShortLevelPrice = pocLine.Anchor1Y;
							}
							
							
							// если ПОК выше текущей цены, значит уровень в шорт
							int prDelta = priceToInt(pocLine.Anchor1Y) - priceToInt(Close[0]);
							Print (Instrument.FullName.ToString()+" |||" + "Найден лонговый POC с тегом "+pocLine.Tag.ToString()+" по цене "+pocLine.Anchor1Y + " delta price="+prDelta);
							
							if (!alwaysActiveOneLimitOrder) {
								if (prDelta <= ticksToLimit) {
									patternName = "retestPOC";
									
									// Выставляем лимитник в шорт по уровню продолженного POC
									if ((entryOrder != null) && (priceToInt(entryOrder.LimitPrice) != priceToInt(pocLine.Anchor1Y - TickSize))) CancelOrder(entryOrder);
									
									//if (_Savos_News_For_Strategy().CanTrade[0] == 1) {
										Print (Instrument.FullName.ToString()+" |||" + "Выставляем лимитник в шорт по уровню продолженного POC"+pocLine.Anchor1Y);
										openOrder(Lot, "SHORT", pocLine.Anchor1Y - TickSize, patternName, true);
									//}
								}
							}
						}
						#endregion
						
						#region calc long levels
						if (priceToInt(pocLine.Anchor1Y) < priceToInt(Close[0])){
							lLevelCount += 1;
							if (priceToInt(pocLine.Anchor1Y) > priceToInt(nearestLongLevelPrice)) {
								nearestLongLevelPrice = pocLine.Anchor1Y;
							}
							
							// если ПОК ниже текущей цены, значит уровень в лонг
							int prDelta = priceToInt(Close[0]) - priceToInt(pocLine.Anchor1Y);
							//Print (Instrument.FullName.ToString()+" |||" + "Найден лонговый POC с тегом "+pocLine.Tag.ToString()+" по цене "+pocLine.Anchor1Y + " delta price="+prDelta);
							
							if (!alwaysActiveOneLimitOrder) {
								if (prDelta <= ticksToLimit) {
									patternName = "retestPOC";
									// Выставляем лимитник в лонг по уровню продолженного POC
									
									if ((entryOrder != null) && (priceToInt(entryOrder.LimitPrice) != priceToInt(pocLine.Anchor1Y + TickSize))) CancelOrder(entryOrder);
									
									//if (_Savos_News_For_Strategy().CanTrade[0] == 1) {
										Print (Instrument.FullName.ToString()+" |||" + "Выставляем лимитник в лонг по уровню продолженного POC"+pocLine.Anchor1Y);
										openOrder(Lot, "LONG", pocLine.Anchor1Y + TickSize, patternName, true);
									/*} else {
										if (UseDebug) {
											Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| " + " Новости!!! Новые ордера не выставляем!");
										}
									}*/
								}
							}
						}
						#endregion
					}
				}
				#endregion
				
				if (alwaysActiveOneLimitOrder) {
					// Дальше нужно прочекать, сначала для ШОРТОВ, после для ЛОНГОВ
					
					if (entryOrder != null) {
						// Если есть выставленный лимитник
						if (entryOrder.OrderType == OrderType.Limit) {
							// Если есть только ближайший уровень в лонг
							if ((sLevelCount == 0) && (lLevelCount > 0)) {
								
								// Удалим лимитку, если она стоит по невыгодной цене, т.е. в лонг, но ниже ,чем ближайший уровень в лонг
								if (entryOrder.LimitPrice != nearestLongLevelPrice) {
									CancelOrder(entryOrder);
								}
								
								// Удалим лимитку, если это лимитка в шорт
								if (entryOrder.Name == "Sell short") {
									CancelOrder(entryOrder);
								}
								
								// Все подготовительные операции сделаны, время выставить лимитник в лонг
								Print (Instrument.FullName.ToString()+" |||" + "Выставляем лимитник в лонг по ближайшему уровню");
								openOrder(Lot, "LONG", nearestLongLevelPrice, patternName, true);
							}
							
							// Если есть только ближайший уровень в шорт
							if ((sLevelCount > 0) && (lLevelCount == 0)) {
								
								// Удалим лимитку, если она стоит по невыгодной цене, т.е. в шорт, но выше ,чем ближайший уровень в шорт
								if (entryOrder.LimitPrice != nearestShortLevelPrice) {
									CancelOrder(entryOrder);
								}
								
								// Удалим лимитку, если это лимитка в лонг
								if (entryOrder.Name == "Buy") {
									CancelOrder(entryOrder);
								}
								
								// Все подготовительные операции сделаны, время выставить лимитник в шорт
								Print (Instrument.FullName.ToString()+" |||" + "Выставляем лимитник в шорт по ближайшему уровню");
								openOrder(Lot, "SHORT", nearestShortLevelPrice, patternName, true);
							}
							
							// Проверим, если это ситуация, когда есть И уровень в шорт И уровень лонг, тогда делаем так
							if ((sLevelCount > 0) && (lLevelCount > 0)) {
								// Посчитаем дистанцию между ближайшими уровнями
								int distanceBetweenNearestLevels = priceToInt(nearestShortLevelPrice) - priceToInt(nearestLongLevelPrice);
								// Найдем треть этого расстояния, именно эту дельту будем дальше использовать, чтобы проверить нахождение цены в пределах трети расстояния между уровнями
								double deltaPrice = TickSize*(distanceBetweenNearestLevels/3);
								
								// Нужно понять, в какой трети расстояние между уровнями мы находимся сейчас и какой уровень выставлять.
								string pos = "";
								
								if (Close[0] <= (nearestLongLevelPrice + deltaPrice)) {
									// Если мы находимся в пределах трети расстояни между уровнями возле уровня в лонг, то нужно ставить лимитник в лонг
									pos = "long";
								} else if (Close[0] >= (nearestShortLevelPrice - deltaPrice)) {
									// Если мы находимся в пределах трети расстояния между уровнями возле уровня в шорт, то нужно ставить лимитрик в шорт
									pos = "short";						
								} else {
									pos = "flat";
								}
								
								// Если мы не в статусе flat, то проверяем дальше
								if (pos != "flat") {
									
									// Если мы должны рассматривать выставление лимитника в шорт
									// SHORT
									if (pos == "short") {
										
										// Если выставлен лимитник в лонг, то удалим его
										if (entryOrder.Name == "Buy") CancelOrder(entryOrder);
										
										// Если выставлен лимитник в шорт не по той цене, по которой есть ближайший уровень в шорт, то удалим его
										if ((entryOrder.Name == "Sell short") && (entryOrder.LimitPrice != nearestShortLevelPrice)) CancelOrder(entryOrder);
										
										// Все подготовительные операции завершены, время выставить лимитник в шорт
										Print (Instrument.FullName.ToString()+" |||" + "Выставляем лимитник в шорт по ближайшему уровню");
										openOrder(Lot, "SHORT", nearestShortLevelPrice, patternName, true);
									}
									
									// LONG
									if (pos == "long") {
										
										// Если выставлен лимитник в шорт, то удалим его
										if (entryOrder.Name == "Sell short") CancelOrder(entryOrder);	
										
										// Если выставлен лимитник не по той цене, по которой есть ближайший уровень в лонг, то удалим его
										if ((entryOrder.Name == "Buy") && (entryOrder.LimitPrice != nearestLongLevelPrice)) CancelOrder(entryOrder);
									
										// Все подготовительные операции завершены, время выставить лимитник в лонг
										Print (Instrument.FullName.ToString()+" |||" + "Выставляем лимитник в лонг по ближайшему уровню");
										openOrder(Lot, "LONG", nearestLongLevelPrice, patternName, true);
									}
								}
							}
							
							// Проверим, если это ситуация, когда нет НИ уровня в лонг НИ уровня в шорт, тогда просто удалим лимитник
							if ((sLevelCount == 0) && (lLevelCount == 0)) {
								CancelOrder(entryOrder);
							}
						}
					} else {
						// Если выставленного лимитника нет, установим его!
						
						// Проверим, если это ситуация, когда есть И уровень в шорт И уровень лонг, тогда делаем так
						if ((sLevelCount > 0) && (lLevelCount > 0)) {
							int distanceBetweenNearestLevels = priceToInt(nearestShortLevelPrice) - priceToInt(nearestLongLevelPrice);
							double deltaPrice = TickSize*(distanceBetweenNearestLevels/2);
							
							// Eсли мы находимся в пределах половины расстояния между уровнями и ближе к уровню в лонг, то готовимся выставить лимитник в лонг, т.к. мы ближе к уровню в лонг
							if (Close[0] <= (nearestLongLevelPrice + deltaPrice)) {
								// Проверяем на наличие новостей и если все хорошо, то выставляем лимитни в лонг
								//if (_Savos_News_For_Strategy().CanTrade[0] == 1) {
									Print (Instrument.FullName.ToString()+" |||" + "Выставляем лимитник в лонг по ближайшему уровню");
									openOrder(Lot, "LONG", nearestLongLevelPrice, patternName, true);
								/*} else {
									if (UseDebug) {
										Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| " + " Новости!!! Новые ордера не выставляем!");
									}
								}*/
							}
							
							// Eсли мы находимся в пределах половины расстояния между уровнями и ближе к уровню в шорт, то готовимся выставить лимитник в шорт, т.к. мы ближе к уровню в шорт
							if (Close[0] >= (nearestShortLevelPrice - deltaPrice)) {
								// Проверяем на наличие новостей и если все хорошо, то выставляем лимитни в шорт
								//if (_Savos_News_For_Strategy().CanTrade[0] == 1) {
									Print (Instrument.FullName.ToString()+" |||" + "Выставляем лимитник в шорт по ближайшему уровню");
									openOrder(Lot, "SHORT", nearestShortLevelPrice, patternName, true);
								/*} else {
									if (UseDebug) {
										Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| " + " Новости!!! Новые ордера не выставляем!");
									}
								}*/
							}
						}
						
						// Проверим, если это ситуация, когда есть только уровень в лонг, тогда делаем так
						if ((sLevelCount == 0) && (lLevelCount > 0)) {
							// Проверяем на наличие новостей и если все хорошо, то выставляем лимитни в лонг
							//if (_Savos_News_For_Strategy().CanTrade[0] == 1) {
								Print (Instrument.FullName.ToString()+" |||" + "Выставляем лимитник в лонг по ближайшему уровню");
								openOrder(Lot, "LONG", nearestLongLevelPrice, patternName, true);
							/*} else {
								if (UseDebug) {
									Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| " + " Новости!!! Новые ордера не выставляем!");
								}
							}*/
						
						}
						
						// Проверим, если это ситуация, когда есть только уровень в шорт, тогда делаем так
						if ((sLevelCount > 0) && (lLevelCount == 0)) {
							// Проверяем на наличие новостей и если все хорошо, то выставляем лимитни в шорт
							//if (_Savos_News_For_Strategy().CanTrade[0] == 1) {
								Print (Instrument.FullName.ToString()+" |||" + "Выставляем лимитник в шорт по ближайшему уровню");
								openOrder(Lot, "SHORT", nearestShortLevelPrice, patternName, true);
							/*} else {
								if (UseDebug) {
									Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| " + " Новости!!! Новые ордера не выставляем!");
								}
							}*/
						
						}
					}
				}
			
				if (entryOrder != null) {
					di = String.Format("alwaysActiveOneLimitOrder={0}\nentryOrder.Name={1}\norderType={2}\n",alwaysActiveOneLimitOrder,entryOrder.Name,entryOrder.OrderType);
				} else {
					di = String.Format("alwaysActiveOneLimitOrder={0}\n",alwaysActiveOneLimitOrder);
				}
				if (sLevelCount > 0){
					di += String.Format("Ближайший POC SHORT : {0} ({1})\n", nearestShortLevelPrice, sLevelCount);
				} else {
					di += "Ближайший POC SHORT: нет"+"\n";
				}
				
				if (lLevelCount > 0){
					di += String.Format("Ближайший POC LONG: {0} ({1})\n", nearestLongLevelPrice, lLevelCount);  
				} else {
					di += "Ближайший POC LONG: нет"+"\n";
				}
				di +="\n\n\n";
           		DrawTextFixed("dibl", di, TextPosition.BottomLeft, Color.White, new Font ("Arial", 10, FontStyle.Regular), Color.White, Color.DarkBlue, 8);
			} else {
				di = "Новости, не торгуем!!!";
				di +="\n\n";
				DrawTextFixed("dibl", di, TextPosition.BottomLeft, Color.White, new Font ("Arial", 10, FontStyle.Regular), Color.White, Color.Red, 8);			
			}
        }               
        #endregion checkPOCLevels   
        //========================================================================================================= 

        //=========================================================================================================     
        #region checkWeeklyVWAP
        // <summary>
        // Проверяем каждый бар, если WeeklyVWAP нарисовался на другом уровне, чем прежний, то луч удаляем и рисуем новый по цене недельного VWAP
        // </summary>
        
        private void checkWeeklyVWAP(){
            //Print (Instrument.FullName.ToString()+" |||" + "Выставляем лимитник в шорт по уровню продолженного POC"+pocLine.Anchor1Y);
            
            if (priceToInt(wVWAP[0]) != priceToInt(wVWAP[1])) {
                if (lastWVWAP != null) {
                    RemoveDrawObject(lastWVWAP.Tag);
                    Print (Instrument.FullName.ToString()+" |||" + " Удаляем старый луч");
                }
                if (priceToInt(GetCurrentBid()) > priceToInt(wVWAP[0])) {
                    lastWVWAP = DrawRay("weeklyVWAPRay", false, 0, wVWAP[0], -1, wVWAP[0], Color.Green, DashStyle.Dot, 3);
                    Print (Instrument.FullName.ToString()+" |||" + " Рисуем новый луч");
                }

                if (priceToInt(GetCurrentAsk()) < priceToInt(wVWAP[0])) {
                    lastWVWAP = DrawRay("weeklyVWAPRay", false, 0, wVWAP[0], -1, wVWAP[0], Color.Red, DashStyle.Dot, 3);
                    Print (Instrument.FullName.ToString()+" |||" + " Рисуем новый луч");
                }
            } else {
                if (lastWVWAP == null) {
                    if (priceToInt(GetCurrentBid()) > priceToInt(wVWAP[0])) {
                        lastWVWAP = DrawRay("weeklyVWAPRay", false, 0, wVWAP[0], -1, wVWAP[0], Color.Green, DashStyle.Dot, 3);
                        Print (Instrument.FullName.ToString()+" |||" + " Рисуем новый луч");
                    }

                    if (priceToInt(GetCurrentAsk()) < priceToInt(wVWAP[0])) {
                        lastWVWAP = DrawRay("weeklyVWAPRay", false, 0, wVWAP[0], -1, wVWAP[0], Color.Red, DashStyle.Dot, 3);
                        Print (Instrument.FullName.ToString()+" |||" + " Рисуем новый луч");
                    }
                }   
            }
        }               
        #endregion checkWeeklyVWAP   
        //=========================================================================================================         
        
        //=========================================================================================================     
        #region getPriceFromY
        private double getPriceFromY(int y){
            // переберем ВСЕ цены на графике от реально видимой максимальной до реально видимой минимальной
            int curY = 0;
            int prevY = 0;
            double curP = 0;
            double prevP = priceMin;
            
            for(double i = priceMin; i <= priceMax; i+=TickSize) {
                curY = ChartControl.GetYByValue(Bars, i);
                if(curY >= y) {
                    // если по проверяемой цене пока еще Y БОЛЬШЕ  нашего заданного - запоминаем его и продолжаем поиски
                    // мы ведь двигаемся по цене снизу вверх, а по Y-координате это выходит от БОЛЬШЕГО К МЕНЬШЕМУ
                    prevY = curY;
                    prevP = i;
                } else {
                    // если по проверяемой цене Y уже МЕНЬШЕ нашего заданного - прерываем цикл
                    curP = i;
                    break;
                }
            }
            // по идее наша искомая ЦЕНА теперь МЕЖДУ prevP и curP - вернем среднее арифметическое
            //int result = (int)(prevP/this.TickSize + (curP/this.TickSize - prevP/this.TickSize)/2);
            int result = (int)(priceToInt(prevP) + (priceToInt(curP) - priceToInt(prevP))/2);
            return result*TickSize+TickSize;
        }       
        #endregion getPriceFromY
        //=========================================================================================================     
        
        //=========================================================================================================     
        #region InputBox
        public static DialogResult InputBox(string title, string promptText, ref string value){
            Form form = new Form();
            Label label = new Label();
            TextBox textBox = new TextBox();
            Button buttonOk = new Button();
            Button buttonCancel = new Button();
            LinkLabel link = new LinkLabel();

            form.Text = title;
            label.Text = promptText;
            textBox.Text = value;
            link.Text = "handlar.info";
            link.Links[0].LinkData = "http://handlar.info/";
            
            buttonOk.Text = "OK";
            buttonCancel.Text = "Cancel";
            buttonOk.DialogResult = DialogResult.OK;
            buttonCancel.DialogResult = DialogResult.Cancel;

            label.SetBounds(9, 18, 372, 13);
            textBox.SetBounds(9, 36, 372, 20);
            buttonOk.SetBounds(228, 72, 75, 23);
            buttonCancel.SetBounds(309, 72, 72, 23);
            link.SetBounds(9, 76, 75, 23);

            label.AutoSize = true;
            textBox.Anchor = textBox.Anchor | AnchorStyles.Right;
            buttonOk.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            buttonCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;

            form.ClientSize = new Size(396, 107);
            form.Controls.AddRange(new Control[] { label, textBox, buttonOk, buttonCancel, link });
            form.ClientSize = new Size(Math.Max(300, label.Right + 10), form.ClientSize.Height);
            form.FormBorderStyle = FormBorderStyle.FixedDialog;
            form.StartPosition = FormStartPosition.CenterScreen;
            form.MinimizeBox = false;
            form.MaximizeBox = false;
            form.AcceptButton = buttonOk;
            form.CancelButton = buttonCancel;

            DialogResult dialogResult = form.ShowDialog();
            value = textBox.Text;
            return dialogResult;
        }       
        #endregion InputBox
        //========================================================================================================= 
        
        //=========================================================================================================     
        #region Mouse_Events
        #region MouseUP
        private void chart_MouseUp (object sender, MouseEventArgs e){
            try {
                if (e.Button == MouseButtons.Left){
                    // определеяем номер бара, на который пришелся клик
                    cursorX = Bars.Count - 1 - getBarFromX(e.X);
                    // определяем цену, по которой кликнули
                    cursorY = getPriceFromY(e.Y);
                    if(cursorX > Bars.Count - 1) {  // следим, чтобы не зайти ЗА ПРАВЫЙ КРАЙ графика
                        cursorX = Bars.Count - 1;
                    }
                    // Нажата левая кнопка мышки, но без горячих клавиш. 
                    if (((Control.ModifierKeys & Keys.Control) == 0) && ((Control.ModifierKeys & Keys.Shift) == 0) ){
                        // Работаем с имеющимися уровнями или выходим ни с чем, так как кликнули без Shift
                    } 
                    if ((Control.ModifierKeys & Keys.Shift) != 0){
                        // если в настройках разрешен поиск пользовательских уровней, тогда добавляем новый пользовательский уровень, т.к. кликнули с зажатым Shift
                        if (UseUserLevels){
                            string userprice = cursorY.ToString();
                            if (InputBox("Введите значение", "Точная цена для уровня", ref userprice) == DialogResult.OK){
                                double inputPrice = Convert.ToDouble(userprice);
                                addNewUserLevel(inputPrice, cursorX);
                            };
                        }
                    }
                
                    // зажали alt и кликнули, чтобы удалить уровень
                    if ((Control.ModifierKeys & Keys.Alt) != 0){
                        if (UseUserLevels){
                            removeLevel(cursorY, cursorX);
                        }
                    
                    }
                }
            } catch (Exception exc) { /* можно что-то вставить */}
            ChartControl.ChartPanel.Invalidate();
            ChartControl.Refresh();
        }
        #endregion MouseUP
        #endregion Mouse_Events      
        //=========================================================================================================         
        
        #endregion USER_LEVELS
        //========================================================================================================= 
        
        //=========================================================================================================     
        #region MIRROR_LEVEL
        /// <summary>
        /// Собраны функции, которые необходимы для работы зеркальным уровнем
        /// </summary>
        
        //=========================================================================================================     
        #region findBDLevelForLong   
        /// <summary>
        /// Ищем уровень в лонг, который пробила импольсная свеча
        /// </summary>
        private void findBDLevelForLong(){
            int longFractal = findFractalForLong(23);
                        
            if (longFractal == 0) {
                BDLevel = MAX(High, 23)[3];// начиная c 3-го бара
                customTag = "lm" + BDLevel;
            } else {
                BDLevel = Math.Max(High[longFractal], MAX(High, 23)[3]);
                customTag = "fn-" + BDLevel;
            }

        }
        #endregion findBDLevelForLong
        //=========================================================================================================
        
        //=========================================================================================================     
        #region findBDLevelForShort   
        /// <summary>
        /// Ищем уровень в шорт, который пробила импольсная свеча
        /// </summary>
        private void findBDLevelForShort(){
            int shortFractal = findFractalForShort(23);
            
            if (shortFractal == 0) {
                BDLevel = MIN(Low, 23)[3];// начиная со второго бара
                customTag = "sm" + BDLevel;
            } else {
                BDLevel = Math.Min(Low[shortFractal], MIN(Low, 23)[3]);
                customTag = "fn-" + BDLevel;
            }
        }
        #endregion findBDLevelForShort
        //=========================================================================================================
        
        //=========================================================================================================     
        #region checkBDBar   
        /// <summary>
        /// Ищем импульсную свечу пробоя
        /// </summary>
        private void checkBDBar(){
            //Print("swing: " + PriceActionSwingTrend(1, 15, 7, PriceActionSwing.Utility.SwingTypes.Standart)); 
            // сначала проверим, может быть уже есть свершенный импульсный пробой и мы находимся в стадии ожидания закрепления цены, 
            // в таком случае никаких новых пробоев не ищем
            //if (!BDComplete){
                //Print("==============="+Time[1]+" "+Instrument.FullName+"==========================");
                double  p1 = ATRToInt(ATR(14)[2]);
                //Print("p1="+p1);
                double  p2 = ATRToInt(ATR(14)[3]);
                //Print("p2="+p2);
                double  p3 = ATRToInt(ATR(14)[4]);
                //Print("p3="+p3);
                double  p12 = Math.Abs(p1 - p2);
                //Print("p12="+p12);      // 216
                double  p32 = Math.Abs(p3 - p2);
                //Print("p32="+p32);      //70
                
                //if ((p12 > p32) && (p1 > p2) && ((High[1]-Low[1]) > ((High[2]-Low[2])*2.5))){   
                if ((p12 > (3*p32)) && (p1 > p2) & (p12 > 40)){   
                    
                    // short pattern
                    if (priceToInt(Open[2]) >= priceToInt(Close[2]) ){
                        drawArea(3, 1, Color.Red, customTag, 1);
                        findBDLevelForShort();
                        
                        // нашли импульсную свечу, нашли уровень (первый фрактал или минимальный лой), сейчас проверим, импульсная свеча закрылась ниже уровня пробоя?
                        if (priceToInt(Low[2]) < priceToInt(BDLevel)){
                            if (priceToInt(Close[2]) < priceToInt(Low[3])){
                                if (priceToInt(Close[1]) < priceToInt(BDLevel)){
                                    patternName = "mirrorLevel";
                                    //drawArea(3, 1, Color.Red, customTag, 1);
                                    if (UseDebug) {Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| " + "Найден зеркальный уровень шорт по цене " + BDLevel);}
                                    try{addShortLevel(BDLevel, 2+PriceWaitingBars);} catch(Exception e){Print("checkBDBar :: "+e.ToString());}
                                    PlaySound(@"C:\Program Files (x86)\NinjaTrader 7\sounds\levelToShort.wav");
                                    
                                    BDComplete  = true;
                                    BDBar       = 0;
                                    BDType      = "short";
                                    // все хорошо, но нужно подождать закрепления цены, чтобы найденный уровень был истинным
                                 } else {
                                    if (UseDebug) {Print(Instrument.FullName.ToString()+" |||" + Time[2] + " ||| " + "СВЕЧА НЕ ПОДХОДИТ ПО УСЛОВИЯМ. Следующая свеча после импульсной закрылась выше уровня " + BDLevel);}
                                }
                            } else {
                                if (UseDebug) {Print(Instrument.FullName.ToString()+" |||" + Time[2] + " ||| " + "СВЕЧА НЕ ПОДХОДИТ ПО УСЛОВИЯМ. Импульсная свеча закрылась выше лоя предыдущей свечи");}
                            }
                        } else {
                            if (UseDebug) {Print(Instrument.FullName.ToString()+" |||" + Time[2] + " ||| " + "СВЕЧА НЕ ПОДХОДИТ ПО УСЛОВИЯМ. Лой сигнальной свечи выше уровня " + BDLevel);}
                        }

                    }
                    
                    // long pattern
                    if (priceToInt(Open[2]) <= priceToInt(Close[2])){
                        drawArea(3, 1, Color.Green, customTag, 1);
                        findBDLevelForLong();
                        
                        // нашли импульсную свечу, нашли уровень (первый фрактал или максимальный хай), сейчас , импульсная свеча закрылась выше уровня пробоя?
                        if (priceToInt(High[2]) > priceToInt(BDLevel)){
                            if (priceToInt(Close[2]) > priceToInt(High[3])){
                                if(priceToInt(Close[1]) > priceToInt(BDLevel)){
                                    patternName = "mirrorLevel";
                                    //drawArea(3, 1, Color.Green, customTag, 1);
                                    if (UseDebug) {Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| " + "Найден зеркальный уровень лонг по цене " + BDLevel);}
                                    try{addLongLevel(BDLevel, 2+PriceWaitingBars);} catch(Exception e){Print("checkBDBar :: "+e.ToString());}
                                    PlaySound(@"C:\Program Files (x86)\NinjaTrader 7\sounds\levelToLong.wav");
                                    BDComplete  = true;
                                    BDBar       = 0;
                                    BDType      = "long";
                                    // все хорошо, но нужно подождать закрепления цены, чтобы найденный уровень был истинным
                                } else {
                                    if (UseDebug) {Print(Instrument.FullName.ToString()+" |||" + Time[2] + " ||| " + "СВЕЧА НЕ ПОДХОДИТ ПО УСЛОВИЯМ. Следующая свеча после импульсной закрылась ниже уровня " + BDLevel);}
                                }
                            } else {
                                if (UseDebug) {Print(Instrument.FullName.ToString()+" |||" + Time[2] + " ||| " + "СВЕЧА НЕ ПОДХОДИТ ПО УСЛОВИЯМ. Импульсная свеча закрылась ниже хая предыдущей свечи");}
                            }
                        } else {
                            if (UseDebug) {Print(Instrument.FullName.ToString()+" |||" + Time[2] + " ||| " + "СВЕЧА НЕ ПОДХОДИТ ПО УСЛОВИЯМ. Хай сигнальной свечи ниже уровня " + BDLevel);}
                        }
                    }
                }
            /*} else {
                // мы ждем, закрепится цена или нет нужное количество баров
                if (BDBar < PriceWaitingBars) {
                    if (BDType == "long"){
                        if (Low[1] <= BDLevel){
                            BDComplete  = false;
                            BDBar       = 0;
                            BDType      = "";
                        } else {
                            BDBar += 1; 
                        }
                    }
                    
                    if (BDType == "short"){
                        if (High[1] >= BDLevel){
                            BDComplete  = false;
                            BDBar       = 0;
                            BDType      = "";
                        } else {
                            BDBar += 1; 
                        }
                    }
                } else {
                    BDComplete  = false;
                    BDBar       = 0;
                    if (BDType == "long"){
                        try{addLongLevel(BDLevel, 2+PriceWaitingBars);} catch(Exception e){Print("checkBDBar :: "+e.ToString());}
                    }
                    
                    if (BDType == "short"){
                        try{addShortLevel(BDLevel, 2+PriceWaitingBars);} catch(Exception e){Print("checkBDBar :: "+e.ToString());}
                    }
                }
            }*/
        }
             
        #endregion checkBDBar
        //=========================================================================================================
        
        //=========================================================================================================     
        #region checkFreeSpaceForLong
        /// <summary>
        /// Проверяем, есть ли достаточное количество свободного воздуха слева от пробитого лонгового уровня
        /// </summary>
        private bool checkFreeSpaceForLong(int bars){
            int n = 3;
            while (n++ < bars) {
                if (High[n] > BDLevel){
                    return false;
                    break;
                }
            }
            
            return true;
        }              
        #endregion checkFreeSpaceForLong   
        //=========================================================================================================
        
        //=========================================================================================================     
        #region findFractalForLong
        /// <summary>
        /// Проверяем, есть ли на заданном диапазоне фрактал в лонг
        /// </summary>
        private int findFractalForLong(int bars){
            int n = 3;
            while (n < bars) {
                if ((High[n] > High[n-1]) && (High[n] > High[n+1])){
                    return n;
                    break;
                }
                n += 1;
            }
            
            return 0;
        }              
        #endregion findFractalForLong   
        //=========================================================================================================     
                
        //=========================================================================================================     
        #region findFractalForShort
        /// <summary>
        /// Проверяем, есть ли на заданном диапазоне фрактал в шорт
        /// </summary>
        private int findFractalForShort(int bars){
            int n = 3;
            while (n < bars) {
                if ((Low[n] < Low[n-1]) && (Low[n] < Low[n+1])){
                    return n;
                    break;
                }
                n += 1;
            }
            
            return 0;
        }              
        #endregion findFractalForShort   
        //========================================================================================================= 
        
        //=========================================================================================================     
        #region checkFreeSpaceForShort
        /// <summary>
        /// Проверяем, есть ли достаточное количество свободного воздуха слева от пробитого шортового
        /// </summary>
        private bool checkFreeSpaceForShort(int bars){
            int n = 3;
            while (n++ < bars) {
                if (Low[n] < BDLevel){
                    return false;
                    break;
                }
            }
            
            return true;
        }              
        #endregion checkFreeSpaceForShort   
        //=========================================================================================================
        
        
        #endregion MIRROR_LEVEL
        //=========================================================================================================         
        
        //=========================================================================================================     
        #region isTradeTime
        private bool isTradeTime(){
            if ((ToTime(Time[1]) >= StartTime) && (ToTime(Time[1]) < StopTime)){
                DrawTextFixed("rt",     "Время торговать!",       TextPosition.TopRight,      Color.White,    new Font ("Arial", 12, FontStyle.Regular), Color.White,    Color.DarkGreen,    5);
                DrawTextFixed("rt2",    "",     TextPosition.TopRight,      Color.White,    new Font ("Arial", 12, FontStyle.Bold), Color.White,    Color.DarkGreen,    5);
                    
                return true;
            } else{
                DrawTextFixed("rt", "Время пить чай, отдыхать...",     TextPosition.TopRight,      Color.White,          new Font ("Arial", 12, FontStyle.Regular), Color.White,          Color.DarkRed,      5);
                return false;
            }
        }
        #endregion isTradeTime
        //=========================================================================================================
        
        //=========================================================================================================
        #region openOrder
        private void openOrder(int lot, string position, double entryPrice, string orderComment, bool orderDebug){
            if (entryOrder == null){
                if (position == "LONG") {
                    try {
                        if (killLimitOrder){
                            if (UseDebug) {
                                Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| " + "Выставляем BUY LIMIT ("+entryPrice+") killLimitOrder=true");
                            }
                            entryOrder = EnterLongLimit(lot, entryPrice, orderComment);
                        } else {
                            if (UseDebug) {
                                Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| " + "Выставляем BUY LIMIT ("+entryPrice+") killLimitOrder=false");
                            }
                            entryOrder = EnterLongLimit(0, true, lot, entryPrice, orderComment);
                        }
                    } catch (Exception ex) {
                        Print("====================================");
                        Print("При попытке ЛОНГА возникла проблема: " + ex.Message.ToString());
                        Print("entryPrice = " + entryPrice + " | ASK = " + GetCurrentAsk());
                        Print("====================================");
                        
                        /*if (killLimitOrder){
                            if (UseDebug) {
                                //Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| " + "Выставляем BUY STOP ("+longLevels.Aggregate((l, r) => l.Value.price > r.Value.price ? l : r).Value.price+") killLimitOrder=true");
                                Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| " + "Выставляем BUY STOP ("+entryPrice+") killLimitOrder=true");
                            }
                            entryOrder = EnterLongStop(lot, entryPrice, orderComment);
                        } else {
                            if (UseDebug) {
                                //Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| " + "Выставляем BUY STOP ("+longLevels.Aggregate((l, r) => l.Value.price > r.Value.price ? l : r).Value.price+") killLimitOrder=false");
                                Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| " + "Выставляем BUY STOP ("+entryPrice+") killLimitOrder=false");
                            }
                            entryOrder = EnterLongStop(0, true, lot, entryPrice, orderComment);
                        }*/
                    }
                }
                if (position == "SHORT") {
                    try {
                        if (killLimitOrder){
                            //if (UseDebug) {
                                Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| " + "Выставляем SELL LIMIT ("+entryPrice+") killLimitOrder=true");
                            //}
                            entryOrder = EnterShortLimit(lot, entryPrice, orderComment);
                        } else {
                            //if (UseDebug) {
                                Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| " + "Выставляем SELL LIMIT ("+entryPrice+") killLimitOrder=false");
                            //}
                            entryOrder = EnterShortLimit(0, true, lot, entryPrice, orderComment);
                        }
                    } catch (Exception ex) {
                        Print("====================================");
                        Print("При попытке ШОРТА возникла проблема: " + ex.Message.ToString());
                        Print("orderEntry = " + entryPrice + " | ASK = " + GetCurrentAsk() + " | BID = " + GetCurrentBid());
                        Print("====================================");
                        if (killLimitOrder){
                            if (UseDebug) {
                                //Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| " + "Выставляем SELL LIMIT ("+shortLevels.Aggregate((l, r) => l.Value.price < r.Value.price ? l : r).Value.price+") killLimitOrder=true");
                                Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| " + "Выставляем SELL LIMIT ("+entryPrice+") killLimitOrder=true");
                            }
                            entryOrder = EnterShortStop(lot, entryPrice, orderComment);
                        } else {
                            if (UseDebug) {
                                //Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| " + "Выставляем SELL LIMIT ("+shortLevels.Aggregate((l, r) => l.Value.price < r.Value.price ? l : r).Value.price+") killLimitOrder=false");
                                Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| " + "Выставляем SELL LIMIT ("+entryPrice+") killLimitOrder=false");
                            }
                            entryOrder = EnterShortStop(0, true, lot, entryPrice, orderComment);
                        }
                    }
                }
            }
        }       
        #endregion OpenOrder
        //=========================================================================================================
          
        //=========================================================================================================
        #region showCurrentPnLInformation
        private void showCurrentPnLInformation(){       
            if (entryOrder != null){
                if (Position.MarketPosition == MarketPosition.Flat) {
                    if (entryOrder.OrderAction == OrderAction.Buy) {
                        DrawTextFixed("lb", "BUY LIMIT "+entryOrder.LimitPrice.ToString(), TextPosition.BottomLeft, Color.White, new Font ("Arial", 12, FontStyle.Regular), Color.White, Color.DarkGreen, 5);
                    }
                    if (entryOrder.OrderAction == OrderAction.SellShort) {
                        DrawTextFixed("lb", "SELL LIMIT "+entryOrder.LimitPrice.ToString(), TextPosition.BottomLeft, Color.White, new Font ("Arial", 12, FontStyle.Regular), Color.White, Color.DarkRed, 5);
                    }
                } else {
                    curPnL = Math.Round(Position.GetProfitLoss(Close[0], PerformanceUnit.Currency), 2);
                    if (curPnL >= 0) {
                        curPnLColor = Color.DarkGreen;
                    } else {
                        curPnLColor = Color.DarkRed;
                    }
                    if (entryOrder.OrderAction == OrderAction.Buy) {
                        if (showBalanceInfo){
                            DrawTextFixed("lb", "BUY $"+ curPnL, TextPosition.BottomLeft, Color.White, new Font ("Arial", 12, FontStyle.Regular), Color.White, curPnLColor, 5);
                        } else {
                            DrawTextFixed("lb", "BUY", TextPosition.BottomLeft, Color.White, new Font ("Arial", 12, FontStyle.Regular), Color.White, curPnLColor, 5);
                        }
                    }
                    if (entryOrder.OrderAction == OrderAction.SellShort) {
                        if (showBalanceInfo){
                            DrawTextFixed("lb", "SELL $"+ curPnL, TextPosition.BottomLeft, Color.White, new Font ("Arial", 12, FontStyle.Regular), Color.White, curPnLColor, 5);
                        } else {
                            DrawTextFixed("lb", "SELL"+ curPnL, TextPosition.BottomLeft, Color.White, new Font ("Arial", 12, FontStyle.Regular), Color.White, curPnLColor, 5);
                        }
                    }
                }
            } else {
                DrawTextFixed("lb", "", TextPosition.BottomLeft, Color.White, new Font ("Arial", 12, FontStyle.Regular), Color.Transparent, Color.Transparent, 5);
                curPnL = 0;
            }
        }
        #endregion showCurrentPnLInformation        
        //=========================================================================================================
        
        //=========================================================================================================
        #region showTradesInformation
        private void showTradesInformation(){    
        
            if (shortLevels.Count > 0){
                debugInformation += "Ближайший SHORT: " + shortLevels.Aggregate((l, r) => l.Value.price < r.Value.price ? l : r).Value.price + " (" + shortLevels.Count + ")\n";
            } else {
                debugInformation += "Ближайший SHORT: нет"+"\n";
            }
            
            if (longLevels.Count > 0){
                debugInformation += "Ближайший LONG: " + longLevels.Aggregate((l, r) => l.Value.price > r.Value.price ? l : r).Value.price + " (" + longLevels.Count + ")\n";  
            } else {
                debugInformation += "Ближайший LONG: нет"+"\n";
            }
            
            debugInformation += "Сделки за день (+/-): ";
            
            if (dailyProfitCount > 0){
                if (showBalanceInfo){
                    debugInformation += dailyProfitCount+" ($"+ dailyProfit +") / ";
                } else {
                    debugInformation += dailyProfitCount +" / ";
                }
            } else {
                if (showBalanceInfo){
                    debugInformation += "0 / ";
                }
            }

            if (dailyLossCount > 0){
                if (showBalanceInfo){
                    if (showBalanceInfo){
                        debugInformation += dailyLossCount+" ($"+ dailyLoss +")\n";
                    } else {
                        debugInformation += dailyLossCount+"\n";
                    }
                }
            } else {
                debugInformation += "0\n";
            }
            
            accountBalance = totalProfit + totalLoss;
            if (showBalanceInfo){
                debugInformation += "Баланс: $"+ accountBalance +"\n";
            }
            
            
            if (isLossLimitReached){
                debugInformation += "Достигнут лимит убыточных сделок на день!!!"+"\n";
            }
            
            DrawTextFixed("tl", debugInformation, TextPosition.TopLeft, Color.White, new Font ("Arial", 10, FontStyle.Regular), Color.White, Color.DarkGreen, 5);
            debugInformation = "";
            /*if (UseDebug){
                Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| " + "showTradesInformation() end");
            }*/
        }
        #endregion showTradesInformation        
        //========================================================================================================= 
        
        //=========================================================================================================
        #region setLimitOrder
        /// <summary>
        /// Проверяем, можно ли выставить лимитник, если да, то выставляем, если при этом находим установленный ранее лимитник - удаляем его и устанавливаем новый 
        /// </summary>
        private void setLimitOrder(string direction){
            bool condition = false, conditionLong = false, condirionShort = false, conditionReOpen = false;
            double price = 0;
            string limitDiff1 = "", limitDiff2 = "", limitSame1 = "", limitSame2 = "";
            
            // в зависимости от того лонг или шорт проверяем, формируем правильные значения переменных
            
            if (direction == "SHORT"){
                price = shortLevels.Aggregate((l, r) => l.Value.price < r.Value.price ? l : r).Value.price;
                condition = priceToInt(GetCurrentBid()) >= (priceToInt(price) - (TicksToLimit * priceToInt(TickSize))); 
                if (entryOrder != null)
                    conditionReOpen = priceToInt(entryOrder.LimitPrice) > priceToInt(price);
                limitDiff1 = "Buy";
                limitDiff2 = "BuyToCover";
                limitSame1 = "Sell";
                limitSame2 = "SellShort";
            }
            
            if (direction == "LONG"){
                price = longLevels.Aggregate((l, r) => l.Value.price > r.Value.price ? l : r).Value.price;
                condition = priceToInt(GetCurrentAsk()) <= (priceToInt(price) + (TicksToLimit * priceToInt(TickSize)));
                if (entryOrder != null)
                    conditionReOpen = priceToInt(entryOrder.LimitPrice) < priceToInt(price);
                limitDiff1 = "Sell";
                limitDiff2 = "SellShort";
                limitSame1 = "Buy";
                limitSame2 = "BuyToCover";
            }
            // если массив уровней не пустой
            if (condition) {
                if (Position.MarketPosition == MarketPosition.Flat){
                    // если нет открытых позиций, т.е. мы не в рынке
                    if (entryOrder != null){
                        if ((entryOrder.OrderAction.ToString() == limitDiff1) || (entryOrder.OrderAction.ToString() == limitDiff2)){
                            CancelOrder(entryOrder);
                        }
                        if ((entryOrder.OrderAction.ToString() == limitSame1) || (entryOrder.OrderAction.ToString() == limitSame2)){
                            if(conditionReOpen){ 
                                CancelOrder(entryOrder);
                                openOrder(Lot, direction, price, patternName, true);
                                return;
                            }
                        }
                    } else {
                        // если установленного лимитника нет
                        openOrder(Lot, direction, price, patternName, true);
                        return;
                    }
                }
            }
        }
        #endregion setLimitOrder        
        //=========================================================================================================
        
        //=========================================================================================================
        #region OnBarUpdate
        protected override void OnBarUpdate(){
            if (Historical) return;

            if ((BarsInProgress == 0) && (CurrentBars[0] >= BarsRequired)){
                if (_Savos_News_For_Strategy().CanTrade[0] != 1 && entryOrder != null){
                    /*if (UseDebug) {
                        Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| " + " Новости, снимаем ордера, если есть. Новые не выставляем!");
                    }*/
                    CancelOrder(entryOrder);
                }
                
                if (isTradeTime()) {
					if (FirstTickOfBar) {
                        // если началась новая сессия, то на первом тике делаем сброс переменных
                        if (!isNewSession){
                            //if (UseDebug){Print(Instrument.FullName.ToString()+" |||" + Time[1] + " |||isTradeTime and first tick and isNewSession="+isNewSession.ToString());}
                            //if (UseDebug){Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| " + "Новый день"+High[1]+" L1="+Low[1]+": ATR(14)[1]="+ATR(14)[1]+"  ATR(14)[2]="+ATR(14)[2]);}
                            isNewSession = true;
                            // обнуляем количество убыточных сделок за день
                            dailyLossCount = 0;
                            dailyLoss = 0;
                            
                            // обнуляем количество прибыльных сделок за день
                            dailyProfitCount = 0;
                            dailyProfit = 0;
                        }
                        
                        //if (UseDebug){Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| " + "Первый тик нового бара H1="+High[1]+" L1="+Low[1]+": ATR(14)[1]="+ATR(14)[1]+"  ATR(14)[2]="+ATR(14)[2]);}
                        /*if(UseMirrorLevels){
                            checkBDBar();
                        }*/
                        
                        
                    } 
					//if (UseDebug){Print(Instrument.FullName.ToString()+" |||" + Time[1] + " |||isTradeTime and each tick and isNewSession="+isNewSession.ToString());}
                    if (shortLevels.Count > 0){
                        if ((UseMirrorLevels && AllowTradeMirrorLevels) || (UseUserLevels && AllowTradeUserLevels)){
                            setLimitOrder("SHORT");
                        }
                    }
                    
                    if (longLevels.Count > 0){
                        if ((UseMirrorLevels && AllowTradeMirrorLevels) || (UseUserLevels && AllowTradeUserLevels)){
                            setLimitOrder("LONG");
                        }
                    }

                    // проверяем линии поков
                    checkPOCLevels();

                    // мониторим недельный VWAP    
                    //checkWeeklyVWAP();

                    clearLevels();
                    
                } else {
                    // Искать уровни мы будем даже в неторговое время, а вот уже торговать по уровнят только в отведенное для торгов время
                    if (isNewSession) {
                        isNewSession = false;
                        // Если не торговое время, тогда удаляем все лимитники
                        if (UseDebug) {
                            Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| " + "Время не торговое уже");
                        }
                        if (entryOrder != null)
                        
                        CancelOrder(entryOrder);
                        if (UseDebug) {
                            Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| " + "Отменяем ордер лимитный или закрываем рыночный");
                        }
                    }
                    //clearLevels();
                    // тут пишем код, который будет выполняться при поступлении каждого нового тика для основного графика
                    
                    // показываем информацию о сделках (количество, прибыль-убытки, уровни)
                    //showTradesInformation();
                    
                    // показываем информацию о текущем PnL, если мы в рынке или об установленном лимитнике
                    //showCurrentPnLInformation();
                }
            }
        }   
        #endregion OnBarUpdate
        //=========================================================================================================
        
        //=========================================================================================================
        #region OnOrderUpdate
         /// <summary>
        /// Called on each incoming order event
        /// </summary>
        protected override void OnOrderUpdate(IOrder order){
            // Handle entry orders here. The entryOrder object allows us to identify that the order that is calling the OnOrderUpdate() method is the entry order.
            //Print(Instrument.FullName.ToString()+" |||" + Time[1] + order.ToString());
            if (AllowTradeMirrorLevels || AllowTradeUserLevels){
                if (entryOrder != null && entryOrder == order){   
                    // Reset the entryOrder object to null if order was cancelled without any fill
                    if (UseDebug) {
                        Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| " + "OnOrderUpdate вызван | OrderState="+order.OrderState.ToString()+" | Order.Filled="+order.Filled);
                    }
                    
                    if (order.OrderState == OrderState.Cancelled && order.Filled == 0){
                        if (UseDebug) {
                            Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| " + "OnOrderUpdate вызван, ордер отменен, очищаем entryOrder");
                        }
                        if (UseDebug) {
                            Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| " + order.ToString());
                        }
                        entryOrder = null;
                    }
                    
                    if ((order.OrderState == OrderState.Filled) && (order.Filled > 0)){
                        // в этом месте будем заменять луч на линию, так тут будем точно знать, что ордер забрали в рынок   
                        
                        if (UseDebug) {
                            Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| Ордер забрали в рынок, меняем луч на линию и удаляем уровень из кармана");
                        }
                        clearLevels();
                    }
                    
                }
                
                
                if (entryOrder != null) {
                    if ((order.Name == "Profit target") && (order.OrderState == OrderState.Filled)){
                        if (UseDebug) {
                            Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| " + "Ордер закрылся по тейку ");
                        }
                        dailyProfitCount += 1;
                        
                        // после того, как закрылись по тейку, нужно учесть комиссию за закрытие
                        dailyProfit += curPnL - (Lot * curCommission);
                        totalProfit += curPnL - (Lot * curCommission);
                        entryOrder = null;
                        RemoveDrawObject(takeRay.Tag);
                        RemoveDrawObject(stopRay.Tag);
                    }
                    
                    if ((order.Name == "Stop loss") && (order.OrderState == OrderState.Filled)){
                        if (UseDebug) {
                            Print(Instrument.FullName.ToString()+" |||" + Time[1] + " ||| " + "Ордер закрылся по стопу");
                        }
                        dailyLossCount += 1;
                        dailyLoss += curPnL - (Lot * curCommission);
                        totalLoss += curPnL - (Lot * curCommission);
                        entryOrder = null;
                        RemoveDrawObject(takeRay.Tag);
                        RemoveDrawObject(takeText.Tag);
                        RemoveDrawObject(stopRay.Tag);
                        RemoveDrawObject(stopText.Tag);
                    }
                    
                    if ((order.Name == "Stop loss") && (order.OrderState == OrderState.Accepted)){
                        // рисуем линию для стопа
                        stopRay = DrawRay("stopRay", false, 5, order.StopPrice, 0, order.StopPrice, Color.Red, DashStyle.Solid, 3);
                        stopText = DrawText("stopText", false, "SL", 10, ChartControl.GetYByValue(Bars, 0), 0, Color.White, new Font ("Arial", 12, FontStyle.Regular), StringAlignment.Center, Color.Red, Color.Red, 10); 
                    }
                    
                    if ((order.Name == "Profit target") && (order.OrderState == OrderState.Accepted)){
                        // рисуем линию для тейка
                        takeRay = DrawRay("takeRay", false, 5, order.LimitPrice, 0, order.LimitPrice, Color.Green, DashStyle.Solid, 3);
                        takeText = DrawText("takeText", false, "SL", 10, ChartControl.GetYByValue(Bars, 0), 0, Color.White, new Font ("Arial", 12, FontStyle.Regular), StringAlignment.Center, Color.Green, Color.Green, 10); 
                    }
                    
                }
            }
            // показываем информацию о сделках (количество, прибыль-убытки, уровни)
            showTradesInformation();
            
            // показываем информацию о текущем PnL, если мы в рынке или об установленном лимитнике
            showCurrentPnLInformation();
            
        }
        #endregion OnOrderUpdate        
        //=========================================================================================================
        
        //=========================================================================================================
        #region OnExecution
        protected override void OnExecution(IExecution execution){
            if (entryOrder != null && entryOrder == execution.Order){
                curCommission = execution.Commission;
            }   
        }
        #endregion OnExecution        
        //=========================================================================================================
        
        #region Properties    

        [Description("1. Показывать баланс?")]
        [GridCategory("5. Info settings")]
        [Gui.Design.DisplayNameAttribute("2. Показывать баланс?")]
        public bool ShowBalanceInfo
        {
            get { return showBalanceInfo; }
            set { showBalanceInfo = value; }
        } 
            #region OrderSettings        
            [Description("2. Take Profit в пунктах (0 - нет TP)")]
            [GridCategory("1. Order settings")]
            [Gui.Design.DisplayNameAttribute("2. Take Profit в пунктах (0 - нет TP)")]
            public int TakeProfit
            {
                get { return takeProfit; }
                set { takeProfit = Math.Max(0, value); }
            }

            [Description("3. Stop Loss в пунктах (0 - нет SL)")]
            [GridCategory("1. Order settings")]
            [Gui.Design.DisplayNameAttribute("3. Stop Loss в пунктах (0 - нет SL)")]
            public int StopLoss
            {
                get { return stopLoss; }
                set { stopLoss = Math.Max(0, value); }
            }
            
            [Description("1. Объем в пунктах")]
            [GridCategory("1. Order settings")]
            [Gui.Design.DisplayNameAttribute("1. Количество лотов для сделки")]
            public int Lot
            {
                get { return lot; }
                set { lot = Math.Max(0, value); }
            }
            #endregion OrderSettings
                
            #region UserLevelSettings
            [Description("1. Искать пользовательские уровни?")]
            [GridCategory("2. User level settings")]
            [Gui.Design.DisplayNameAttribute("1. Искать пользовательские уровни?")]
            public bool UseUserLevels
            {
                get { return useUserLevels; }
                set { useUserLevels = value; }
            }
            
            [Description("2. Торговать пользовательские уровни?")]
            [GridCategory("2. User level settings")]
            [Gui.Design.DisplayNameAttribute("2. Торговать пользовательские уровни?")]
            public bool AllowTradeUserLevels
            {
                get { return allowTradeUserLevels; }
                set { allowTradeUserLevels = value; }
            }
            #endregion UserLevelSettings

            #region CommonSettings
            [Description("1. Показывать отладочную информацию?")]
            [GridCategory("3. Common settings")]
            [Gui.Design.DisplayNameAttribute("1. Показывать отладочную информацию?")]
            public bool UseDebug
            {
                get { return debugMode; }
                set { debugMode = value; }
            }
        
            [Description("2. Показывать линии SL и TP?")]
            [GridCategory("3. Common settings")]
            [Gui.Design.DisplayNameAttribute("2. Показывать линии SL и TP?")]
            public bool ShowSLandTP
            {
                get { return showSLandTP; }
                set { showSLandTP = value; }
            } 

            [Description("3. За сколько тиков до уровня выставлять лимит?")]
            [GridCategory("3. Common settings")]
            [Gui.Design.DisplayNameAttribute("3. За сколько тиков до уровня выставлять лимит?")]
            public int TicksToLimit
            {
                get { return ticksToLimit; }
                set { ticksToLimit = Math.Max(0, value); }
            }

            [Description("4. Снимать лимит, если был недотик?")]
            [GridCategory("3. Common settings")]
            [Gui.Design.DisplayNameAttribute("4. Снимать лимит, если был недотик?")]
            public bool RemoveLimitOrderIfNedotick
            {
                get { return removeLimitOrderIfNedotick; }
                set { removeLimitOrderIfNedotick = value; }
            }    
            #endregion CommonSettings
            
            #region TimeSettings
            [Description("1. Время начала торгов (например 03:15:00 будет 031500)")]
            [GridCategory("4. Time settings")]
            [Gui.Design.DisplayNameAttribute("1. Время начала торгов")]
            public int StartTime
            {
                get { return startTime; }
                set { startTime = value; }
            }    
            
            [Description("2. Время окончания торгов")]
            [GridCategory("4. Time settings")]
            [Gui.Design.DisplayNameAttribute("2. Время окончания торгов")]
            public int StopTime
            {
                get { return stopTime; }
                set { stopTime = value; }
            }       
            #endregion TimeSettings

            #region MirrorLevels
            [Description("1. Искать зеркальные уровни?")]
            [GridCategory("6. Mirror settings")]
            [Gui.Design.DisplayNameAttribute("1. Искать зеркальные уровни?")]
            public bool UseMirrorLevels
            {
                get { return useMirrorLevels; }
                set { useMirrorLevels = value; }
            }  
            
            [Description("2. Торговать зеркальные уровни?")]
            [GridCategory("6. Mirror settings")]
            [Gui.Design.DisplayNameAttribute("2. Торговать зеркальные уровни?")]
            public bool AllowTradeMirrorLevels
            {
                get { return allowTradeMirrorLevels; }
                set { allowTradeMirrorLevels = value; }
            }
            
            [Description("3. Количество баров для закрепления цены")]
            [GridCategory("6. Mirror settings")]
            [Gui.Design.DisplayNameAttribute("3. Количество баров для закрепления цены")]
            public int PriceWaitingBars
            {
                get { return priceWaitingBars; }
                set { priceWaitingBars = value; }
            }
            
            #endregion MirrorLevels
            
            
        #endregion
    }
}